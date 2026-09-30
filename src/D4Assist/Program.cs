using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Logging;
using D4Assist.Navigation;
using D4Assist.Radar;
using D4Assist.Speech;
using D4Assist.Strings;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;
using D4Memory.Scan;

namespace D4Assist;

/// <summary>
/// Companion app entry point: wires the config, PRISM speech sink, offline D4Data lookups and the
/// GameSession state machine together, then runs until Ctrl+C. See README for setup/requirements.
/// </summary>
internal static class Program
{
    // Matches D4MemScan's PlayerCommand.SceneRootType — the SNO group PlayerRecordMatcher validates
    // the player's scene field against.
    private const string SceneDefinitionType = "SceneDefinition";
    // The Actor SNO group (CoreTOC group 1) is keyed by its definition type.
    private const string ActorDefinitionType = "ActorDefinition";
    private const string WorldDefinitionType = "WorldDefinition";
    private const string MarkerSetType = "MarkerSetDefinition";
    private const string QuestDefinitionType = "QuestDefinition";
    // data/definitions.json, embedded by D4Assist.csproj when it existed at build time; the default DefinitionsPath.
    private const string EmbeddedDefinitions = "definitions.json";

    private static int Main(string[] args)
    {
        using var log = LogFile.Start(AppContext.BaseDirectory);
        if (args.Contains("--sound-test"))
            return SoundTestMode.Run(args.Contains("--no-speech"));

        bool noSpeech = args.Contains("--no-speech");
        bool debug = args.Contains("--debug");

        var config = AppConfig.Load(Path.Combine(AppContext.BaseDirectory, AppConfig.FileName));
        var installPath = config.ResolveInstallPath(Directory.Exists, () => SnoStore.TryLocateInstall(out var found) ? found : null);
        if (installPath is null)
        {
            Console.Error.WriteLine($"Diablo IV install not found. Set InstallPath in {AppConfig.FileName}. See README.md.");
            return 1;
        }
        var definitionsPath = config.ResolveDefinitionsPath(AppContext.BaseDirectory);
        if (definitionsPath is null ? !HasEmbeddedDefinitions() : !File.Exists(definitionsPath))
        {
            Console.Error.WriteLine(definitionsPath is null
                ? $"This build has no embedded definitions.json (data/definitions.json was missing). Set DefinitionsPath in {AppConfig.FileName}. See README.md."
                : $"{definitionsPath} not found. Set DefinitionsPath in {AppConfig.FileName}. See README.md.");
            return 1;
        }

        var language = new LanguageSetting();
        using var logWatcher = new WorldLogWatcher(installPath);
        var follower = new LanguageFollower(language, config.ForcedLanguage(), debug ? Console.WriteLine : null);
        if ((GameLocale.ReadLauncherLocale(installPath) ?? logWatcher.ReadLastTextLocaleAtStartup()) is { } startLocale) follower.OnTextLocale(startLocale);
        logWatcher.TextLocaleChanged += follower.OnTextLocale;

        using ISpeechSink speech = noSpeech || !config.SpeechEnabled ? new ConsoleSpeechSink() : new PrismSpeechSink();
        Console.WriteLine($"Speech backend: {speech.BackendName}");
        speech.Speak(AppStrings.For(language.Current).AssistantActive, interrupt: true);

        using var store = new SnoStore(installPath);
        var schema = definitionsPath is null ? LoadEmbeddedDefinitions() : TypeSchema.Load(definitionsPath);
        var worldReader = new WorldReader(store, schema);
        var classifier = new WorldClassifier(store, worldReader, config.RadarOnlyWorlds, config.OutsideDungeonsEnabled);

        var sceneReader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
        var candidates = new CandidateIndex(CandidateSceneBuilder.Build(store, sceneReader, sceneReader.Group, tilesOnly: false));
        var sceneSnos = store.AllEntries(schema.SnoGroup(SceneDefinitionType)).Select(e => e.Id).ToHashSet();
        var actorDefinitions = new ActorDefinitionReader(store, schema);
        var playerActorSnos = actorDefinitions.PlayerActorSnos();
        if (debug) Console.WriteLine($"Player actors: {playerActorSnos.Count}.");

        // CASC reads happen on several poll threads (walkability, actor classes, DPO quests) and the store isn't thread-safe.
        var cascGate = new object();
        // Static-grid (radar-only) worlds get their tiles offline: the live CScene route only matches DRLG scenes.
        var staticTiles = new StaticWorldTiles(
            worldSno => { lock (cascGate) return worldReader.Read(worldSno).Chunks; },
            sceneSno => { lock (cascGate) return sceneReader.Read(sceneSno).TileSize; },
            store.Name);

        bool routesEnabled = config.QuestRoutesEnabled && config.OutsideDungeonsEnabled;
        // Open menus also gate D-pad left (the guide toggle), so they are watched whenever the guide or the routes run.
        bool watchMenus = config.PauseCuesInMenus || config.ExplorationGuideEnabled || routesEnabled;
        var menuScreens = watchMenus ? MenuScreenTracker.ForLiveProcess(debug ? Console.WriteLine : null) : null;
        using var session = new GameSession(
            logWatcher, classifier,
            (worldSno, knownSceneSnos) => LiveDungeonSessionReader.TryOpen(installPath, candidates, playerActorSnos, worldSno, knownSceneSnos, menuScreens),
            sceneSnos, staticTiles: staticTiles.Build);

        var walkability = new LockedTileWalkability(new CascTileWalkability(sceneReader), cascGate);
        var exitScenes = LoadExitCandidateScenes(store, schema, cascGate);
        if (debug) Console.WriteLine($"Exit candidate scenes: {exitScenes.Count}.");
        var navigation = new NavigationBridge(
            session, speech, config.AutoAnnouncementsEnabled, diagnostics: debug, walkability, language, exitScenes.Contains);

        // One mixer and one output for the radar cues and the beacon loops.
        Func<int, ActorDefinitionInfo?> definitionOf = sno => { lock (cascGate) return actorDefinitions.Read(sno); };
        var interactables = new InteractableClassifier(definitionOf, store.Name);

        var mixer = new AudioMixer();
        using var radarPlayer = config.RadarEnabled
            ? new RadarCuePlayer(new WaveOutAudioOut(), mixer) { Frame = config.RadarFrame, Volume = config.RadarVolume }
            : null;
        if (radarPlayer is not null)
        {
            var radarSettings = new RadarSettings { Frame = config.RadarFrame, PollMs = GameSession.DefaultPlayerPollIntervalMs };
            var doors = config.RadarClosedDoorsEnabled ? new DoorObstacles(interactables, definitionOf) : null;
            _ = new RadarBridge(session, new DungeonRadarWorld(walkability), radarPlayer, radarSettings, diagnostics: debug, doors);
        }

        bool actorLoops = config.BeaconSoundsEnabled || config.ObjectiveSoundsEnabled || config.LoopedInteractables().Count > 0;
        bool needsOwnOutput = (actorLoops || config.ExplorationGuideEnabled || routesEnabled) && radarPlayer is null;
        using IAudioOut? beaconOut = needsOwnOutput ? new WaveOutAudioOut() : null;
        beaconOut?.Start(mixer.Render);
        var portals = actorLoops ? StartLoops(session, mixer, config, store, schema, cascGate, debug, definitionOf, interactables) : null;
        var cueGate = new CueGate(paused => mixer.Paused = paused, Console.WriteLine); // mute reasons always logged: testers' "no sound" reports
        session.TransitChanged += loading => cueGate.Set(CueMute.Loading, loading); // the loops would hang on their last positions
        if (config.PauseCuesInCutscenes) WatchCutscenes(logWatcher, session, cueGate, debug);
        using var focus = config.PauseCuesInBackground ? new GameWindowFocus(cueGate) : null;
        if (config.PauseCuesInConversations)
        {
            var conversations = new ConversationDefinitionReader(store, schema);
            var talk = new ConversationMute(muted => cueGate.Set(CueMute.Conversation, muted), store.Name,
                sno => { lock (cascGate) return conversations.ReadType(sno); }, sno => { lock (cascGate) return conversations.IsNpcTalk(sno); },
                debug ? Console.WriteLine : null);
            session.ConversationRead += talk.OnSnapshot;
            session.WorldChanged += (_, _, _) => talk.Reset();
        }
        MenuMute? menus = null;
        if (watchMenus)
        {
            var watcher = new MenuMute(open => { if (config.PauseCuesInMenus) cueGate.Set(CueMute.Menu, open); }, debug ? Console.WriteLine : null);
            session.MenusRead += watcher.OnScreens;
            session.WorldChanged += (_, _, _) => watcher.Reset();
            menus = watcher;
        }

        // Created after the NavigationBridge so each poll marks the tile visited before the guide reads it.
        // The exploration guide (dungeons) and the quest routes (overworld) share one ping and D-pad left.
        using var guidePing = config.ExplorationGuideEnabled || routesEnabled
            ? new GuidePingPlayer(mixer) { Volume = config.ExplorationGuideVolume }
            : null;
        // Xbox pads through XInput, PlayStation pads (which XInput never sees) through HID.
        using var xboxPad = guidePing is not null ? new XInputPad() : null;
        using var sonyPad = guidePing is not null ? new SonyHidPad(Console.WriteLine) : null;
        // In a game menu (inventory, map...) the D-pad navigates the menu, so presses there never toggle the guide.
        var pad = xboxPad is not null && sonyPad is not null
            ? new GatedPad(new CombinedPad([xboxPad, sonyPad]), () => menus?.AnyOpen ?? false, debug ? () => Console.WriteLine("GUIDE toggle ignored: menu open") : null)
            : null;
        if (guidePing is not null && config.ExplorationGuideEnabled)
            _ = new ExplorationGuideBridge(
                session, navigation.NextGuideStep, guidePing, speech, () => portals?.FreshInSight ?? false, debug ? Console.WriteLine : null,
                pad, config.ExplorationGuideInDungeons, language, portals is null ? null : portals.Destinations);
        var routes = routesEnabled ? LoadQuestRoutes(store, schema, cascGate, debug) : [];
        if (guidePing is not null && routes.Count > 0)
            _ = new QuestRouteBridge(session, routes, guidePing, speech, language, pad, store.Name, debug ? Console.WriteLine : null);
        xboxPad?.Start();
        sonyPad?.Start();

        // Sound examples on Ctrl+Alt+Shift+1..8, to show the cues without a fight's noise.
        var demo = new SoundDemo(
            speech, digit => AppStrings.For(language.Current).SoundExamples[digit - 1], radarPlayer, mixer,
            LoadLoopSounds()?.GetValueOrDefault(LoopTargetSelector.QuestItemSound), LoopProfiles.Quest.NearGain * config.ObjectiveVolume, guidePing,
            cueGate.Example);
        using var hotkeys = new DemoHotkeys(SoundDemo.ExampleCount);
        hotkeys.DigitPressed += demo.Play;
        hotkeys.Start();

        session.WorldChanged += (sno, name, isDungeon) =>
        {
            Console.WriteLine($"World: {name} (SNO {sno}), dungeon={isDungeon}, state={session.State}.");
            if (debug) Console.WriteLine($"World kind: {session.CurrentWorldKind}.");
            if (config.AutoAnnouncementsEnabled && sno != GameSession.NoWorldSno && !session.CurrentWorldRadarOnly)
            {
                var strings = AppStrings.For(language.Current);
                speech.Speak(isDungeon ? strings.EnteringDungeon : strings.LeavingDungeon, config.InterruptOnContextChange);
            }
        };

        if (session.State == SessionState.WaitingForGame)
        {
            Console.WriteLine("No world seen yet in FenrisDebug.txt.");
            speech.Speak(AppStrings.For(language.Current).WaitingForGame, interrupt: false);
        }

        Console.WriteLine("D4 Dungeon Assist running. Press Ctrl+C to exit.");
        using var exit = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
        exit.Wait();
        return 0;
    }

    private static bool HasEmbeddedDefinitions() => typeof(Program).Assembly.GetManifestResourceInfo(EmbeddedDefinitions) is not null;

    private static TypeSchema LoadEmbeddedDefinitions()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream(EmbeddedDefinitions)!;
        return TypeSchema.Load(stream);
    }

    private static Dictionary<string, float[]>? LoadLoopSounds()
    {
        try { return SpatialLoopPlayer.LoadSounds(); }
        catch (InvalidDataException e) { Console.WriteLine($"Loop sounds disabled, sounds not loaded: {e.Message}"); return null; }
    }

    /// <summary>Starts the actor loops; returns the memory of the portals they found, or null when the loop sounds are missing.</summary>
    private static PortalMemory? StartLoops(
        GameSession session, AudioMixer mixer, AppConfig config, SnoStore store, TypeSchema schema, object cascGate, bool debug,
        Func<int, ActorDefinitionInfo?> definitionOf, InteractableClassifier interactables)
    {
        if (LoadLoopSounds() is not { } sounds) return null;

        int actorGroup = schema.SnoGroup(ActorDefinitionType);
        var beaconSnos = config.BeaconSoundsEnabled
            ? BeaconTargets.Resolve(
                config.BeaconActorNames, name => store.Resolve(actorGroup, name),
                name => Console.WriteLine($"Beacon actor '{name}' is not in CoreTOC; ignored."))
            : new HashSet<int>();

        var dpo = new DungeonObjectiveReader(store, schema);
        var objectives = config.ObjectiveSoundsEnabled ? new ObjectiveClassifier(definitionOf, store.Name) : null;
        var portalRule = config.BeaconSoundsEnabled ? new PortalClassifier(definitionOf, store.Name) : null;
        var selector = new LoopTargetSelector(
            beaconSnos, config.BeaconVolume, objectives, config.ObjectiveVolume, world => { lock (cascGate) return dpo.ActorSnos(world); },
            interactables, config.LoopedInteractables(), config.InteractableVolume, portalRule is null ? null : portalRule.IsPortal);
        if (session.CurrentWorld is { } start) selector.SetWorld(WorldSnoOrNone(store, schema, start.Name), start.IsDungeon); // app started mid-dungeon

        var player = new SpatialLoopPlayer(mixer, sounds);
        var sightings = debug
            ? new ActorSightingLog(definitionOf, store.Name, Console.WriteLine)
            : null;
        var chests = GlowingChestStore.Load(AppContext.BaseDirectory, Console.WriteLine);
        var used = new GizmoUsedSignal(store.Name, chests.Kinds, chests.Add);
        _ = new ActorLoopBridge(session, player, selector, store.Name, log: debug ? Console.WriteLine : null, sightings: sightings, usedSignal: used);
        return new PortalMemory(session, actor => selector.Select(actor)?.Kind == LoopKind.ExitBeacon, store.Name, debug ? Console.WriteLine : null);
    }

    private static void WatchCutscenes(WorldLogWatcher log, GameSession session, CueGate gate, bool debug)
    {
        var pause = new CutscenePause(paused => gate.Set(CueMute.Cutscene, paused), debug ? Console.WriteLine : null);
        log.CutsceneStateChanged += pause.OnCutsceneState;
        session.WorldChanged += (_, _, _) => pause.OnWorldChanged();
        session.PlayerMoved += pause.OnPlayer;
    }

    private static IReadOnlySet<int> LoadExitCandidateScenes(SnoStore store, TypeSchema schema, object cascGate)
    {
        int sceneGroup = schema.SnoGroup(SceneDefinitionType);
        lock (cascGate)
            return ExitCandidateScenes.Build(store.AllEntries(schema.SnoGroup(MarkerSetType)).Select(e => e.Name), name => store.Resolve(sceneGroup, name));
    }

    private static IReadOnlyList<LoadedQuestRoute> LoadQuestRoutes(SnoStore store, TypeSchema schema, object cascGate, bool debug)
    {
        var markers = new MarkerReader(store, schema);
        var groups = new QuestRouteGroups(schema.SnoGroup(MarkerSetType), schema.SnoGroup(QuestDefinitionType), schema.SnoGroup(ActorDefinitionType));
        List<LoadedQuestRoute> routes;
        lock (cascGate)
            routes = [.. QuestRouteCatalog.All
                .Select(route => QuestRouteLoader.Load(route, groups, (group, name) => SnoOrNull(store, group, name), markers.ReadAll, Console.WriteLine))
                .OfType<LoadedQuestRoute>()];
        if (debug)
            foreach (var r in routes)
                Console.WriteLine($"Quest route {r.Route.Name}: start ({r.Start.X:F1},{r.Start.Y:F1}), points " +
                    string.Join(" ", r.Points.Select(p => $"({p.X:F1},{p.Y:F1})")) + $", {r.ActivationSnos.Count} activation actors.");
        return routes;
    }

    private static int? SnoOrNull(SnoStore store, int group, string name)
    {
        try { return store.Resolve(group, name); }
        catch (ArgumentException) { return null; }
    }

    private static int WorldSnoOrNone(SnoStore store, TypeSchema schema, string worldName)
    {
        try { return store.Resolve(schema.SnoGroup(WorldDefinitionType), worldName); }
        catch (ArgumentException) { return GameSession.NoWorldSno; }
    }
}
