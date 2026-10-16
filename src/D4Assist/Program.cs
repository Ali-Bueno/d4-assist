using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Logging;
using D4Assist.Navigation;
using D4Assist.Radar;
using D4Assist.Speech;
using D4Assist.Strings;
using D4Assist.Ui;
using D4Assist.Update;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Readers;
using D4Data.Schema;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Assist;

/// <summary>
/// Companion app entry point: wires the config, PRISM speech sink, offline D4Data lookups and the
/// GameSession state machine together, then runs from the notification area until Exit. See README for setup/requirements.
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
    // Passed to the copy a restart starts, which then waits for this one to exit.
    private const string RestartedFlag = "--restarted";
    private static readonly TimeSpan RestartWait = TimeSpan.FromSeconds(30);

    private static int Main(string[] args)
    {
        if (args.Contains("--sound-test"))
        {
            using var testLog = LogFile.Start(AppContext.BaseDirectory);
            return SoundTestMode.Run(args.Contains("--no-speech"));
        }
        using var instance = SingleInstance.TryAcquire(args.Contains(RestartedFlag) ? RestartWait : TimeSpan.Zero);
        if (instance is null)
        {
            var text = SettingsStrings.For(WindowsLanguage());
            System.Windows.Forms.MessageBox.Show(text.AlreadyRunning, text.AppName,
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            return 1;
        }
        using var log = LogFile.Start(AppContext.BaseDirectory);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.Error.WriteLine($"Unhandled error: {e.ExceptionObject}");
        Console.WriteLine($"Diablo 4 Assist {AppVersion}");
        var exeName = Path.GetFileName(Environment.ProcessPath!);
        bool justUpdated = FileReplacement.HasLeftover(AppContext.BaseDirectory, exeName);
        if (justUpdated) _ = FileReplacement.DeleteLeftoverSoon(AppContext.BaseDirectory, exeName); // the exe the update renamed

        bool noSpeech = args.Contains("--no-speech");
        bool debug = args.Contains("--debug");

        var configPath = Path.Combine(AppContext.BaseDirectory, AppConfig.FileName);
        AppConfig config;
        try { config = AppConfig.Load(configPath); }
        catch (Exception e) when (e is InvalidDataException or JsonException or IOException) { return Fail($"{AppConfig.FileName}: {e.Message}"); }
        var installPath = config.ResolveInstallPath(Directory.Exists, LocateInstall) ?? SearchOrAskForInstall(config, configPath);
        if (installPath is null)
            return Fail($"Diablo IV install not found. Start Diablo IV and open Diablo 4 Assist again, or set InstallPath in {AppConfig.FileName}, "
                + "for example \"InstallPath\": \"E:/Games/Diablo IV\". See README.txt.");
        Console.WriteLine($"Game install: {installPath} ({Presence(installPath, LiveDungeonSessionReader.ExecutableFileName)}, "
            + $"{Presence(installPath, WorldLogWatcher.LogFileName)}).");
        var definitionsPath = config.ResolveDefinitionsPath(AppContext.BaseDirectory);
        if (definitionsPath is null ? !HasEmbeddedDefinitions() : !File.Exists(definitionsPath))
            return Fail(definitionsPath is null
                ? $"This build has no embedded definitions.json (data/definitions.json was missing). Set DefinitionsPath in {AppConfig.FileName}. See README.md."
                : $"{definitionsPath} not found. Set DefinitionsPath in {AppConfig.FileName}. See README.md.");

        var language = new LanguageSetting();
        using var logWatcher = new WorldLogWatcher(installPath);
        var follower = new LanguageFollower(language, config.ForcedLanguage(), debug ? Console.WriteLine : null);
        if ((GameLocale.ReadLauncherLocale(installPath) ?? logWatcher.ReadLastTextLocaleAtStartup()) is { } startLocale) follower.OnTextLocale(startLocale);
        logWatcher.TextLocaleChanged += follower.OnTextLocale;

        using ISpeechSink speech = noSpeech || !config.SpeechEnabled ? new ConsoleSpeechSink() : new PrismSpeechSink();
        Console.WriteLine($"Speech backend: {speech.BackendName}");
        speech.Speak(AppStrings.For(language.Current).AssistantActive, interrupt: true);
        if (justUpdated) speech.Speak(string.Format(AppStrings.For(language.Current).UpdatedToFormat, UpdateVersion.Current.ToString(3)), interrupt: false);
        // A crash would otherwise end in silence.
        AppDomain.CurrentDomain.UnhandledException += (_, _) => speech.Speak(AppStrings.For(language.Current).Stopped, interrupt: true);
        // A read that keeps failing after a game update is said once, so the player knows what stopped working.
        var health = ReadHealth.Live(Console.WriteLine, check => speech.Speak(AppStrings.For(language.Current).ReadWarning(check), interrupt: false));
        // Checked before the slow loading: just started from the desktop, the app may still bring its question to the front.
        using var exit = new ManualResetEventSlim();
        bool restart = false, updated = false;
        var updates = UpdateService.ForThisApp(speech, language, Console.WriteLine, () => { restart = updated = true; exit.Set(); });
        if (config.CheckForUpdates) { updates.CheckAtStart(); updates.CheckPeriodically(); }

        SnoStore store;
        try { store = new SnoStore(installPath, Console.WriteLine); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Game files: {e}");
            return Fail($"Can't read the Diablo IV files in {installPath}: {e.Message} Please send d4assist-log.txt.");
        }
        using var storeLifetime = store;
        var schema = definitionsPath is null ? LoadEmbeddedDefinitions() : TypeSchema.Load(definitionsPath);
        var worldReader = new WorldReader(store, schema);
        var classifier = new WorldClassifier(store, worldReader, config.RadarOnlyWorlds, config.OutsideDungeonsEnabled);

        var sceneReader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
        var candidates = new CandidateIndex(CandidateSceneBuilder.Build(store, sceneReader, sceneReader.Group, tilesOnly: false));
        var sceneSnos = store.AllEntries(schema.SnoGroup(SceneDefinitionType)).Select(e => e.Id).ToHashSet();
        var actorDefinitions = new ActorDefinitionReader(store, schema);
        var playerActorSnos = actorDefinitions.PlayerActorSnos();
        // Every actor SNO the game files hold: the live layout check (LayoutDiscovery) tells a real SNO field by it.
        var actorSnos = store.AllEntries(schema.SnoGroup(ActorDefinitionType)).Select(e => e.Id).ToHashSet();
        if (debug) Console.WriteLine($"Player actors: {playerActorSnos.Count}.");

        // CASC reads happen on several poll threads (walkability, actor classes, DPO quests) and the store isn't thread-safe.
        var cascGate = new object();
        // Static (radar-only) worlds get their tiles offline: the live CScene route only matches DRLG scenes.
        var staticTiles = new StaticWorldTiles(
            worldSno => { lock (cascGate) { var (info, chunks) = worldReader.Read(worldSno); return (info.GridSize, chunks); } },
            sceneSno => { lock (cascGate) return sceneReader.Read(sceneSno).Walkability?.Map; },
            store.Name);

        bool routesEnabled = config.QuestRoutesEnabled && config.OutsideDungeonsEnabled;
        // Open menus also gate D-pad left (the guide toggle), so they are watched whenever the guide or the routes run.
        bool watchMenus = config.PauseCuesInMenus || config.ExplorationGuideEnabled || routesEnabled;
        var menuScreens = watchMenus ? MenuScreenTracker.ForLiveProcess(debug ? Console.WriteLine : null) : null;
        // Where far quest objectives stand: the map's markers (research/dungeon-objectives.md §19); one line in every log.
        var mapMarkers = MapMarkerTracker.ForLiveProcess(Console.WriteLine);
        // ACD flags are read for gizmos (used), NPCs (quest followers) and bosses and uniques (invulnerable) only: other monsters and
        // the player carry hundreds of attributes.
        Func<int, bool> readsFlags = sno =>
        {
            lock (cascGate)
                return actorDefinitions.Read(sno) is { } d
                    && (d.GizmoType >= 0 || d.IsNpc || d.MonsterRarity is Conventions.MonsterRarityUnique or Conventions.MonsterRarityBoss);
        };
        Func<int, bool> isMultiUseSwitch = sno => { lock (cascGate) return actorDefinitions.Read(sno) is { MultiUseSwitch: true }; };
        var questScripts = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));
        Func<int, IReadOnlyList<QuestPhaseObjects>> questMarkers =
            quest => { lock (cascGate) return questScripts.PhaseObjects(quest); };
        var dpo = new DungeonObjectiveReader(store, schema);
        Func<int, ActorDefinitionInfo?> definitionOf = sno => { lock (cascGate) return actorDefinitions.Read(sno); };
        var portalRule = new PortalClassifier(definitionOf, store.Name);
        // What tiles place in their gameplay marker layers, known before it streams in (research/dungeon-objectives.md §18): the
        // portals (PortalMemory) and the other gizmos a quest phase may name by SNO alone (the Labyrinth's levers).
        var markerScenes = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: false);
        var tilePortals = new TileActorReader(markerScenes, portalRule.IsPortal);
        // Not triggers: a generic trigger SNO is placed all over a tileset; the map marks the copy a phase means (same doc, §19).
        var tileGizmos = new TileActorReader(
            markerScenes, sno => definitionOf(sno) is { GizmoType: >= 0 and not Conventions.GizmoTrigger } && !portalRule.IsPortal(sno));
        // The world's own objective quest too: a seasonal hub's zone quest names its steps by actor, with no marker to find it by,
        // and the game shows its phase once the main quest hands over to it (research/dungeon-objectives.md §18).
        Func<QuestSearchArea, IReadOnlyList<int>> questsHere = area =>
        {
            lock (cascGate)
                return [.. questScripts.QuestsWithMarkersIn(new[] { area.WorldSno }.Concat(area.Scenes).Select(store.Name).OfType<string>())
                    .Union(questScripts.QuestsNaming(area.Actors, ActiveQuests.MarkerReach))
                    .Union(dpo.QuestSnos(area.WorldSno))];
        };
        // Found once per game build while someone talks, then kept in a file next to the exe (research/client-memory.md §5).
        var conversationSnos = store.AllEntries(schema.SnoGroup("ConversationDefinition")).Select(e => e.Id).ToHashSet();
        var conversationAddresses = new ConversationAddresses(
            Path.Combine(AppContext.BaseDirectory, "d4assist-addresses.json"), conversationSnos.Contains, debug ? Console.WriteLine : null);
        using var session = new GameSession(
            logWatcher, classifier,
            (worldSno, knownSceneSnos) => LiveDungeonSessionReader.TryOpen(
                installPath, candidates, playerActorSnos, worldSno, knownSceneSnos, menuScreens, readsFlags, questMarkers, conversationAddresses,
                questsHere, health, actorSnos.Contains, debug ? new SwitchStateLog(isMultiUseSwitch, store.Name, Console.WriteLine) : null,
                scene => { lock (cascGate) return tileGizmos.In(scene); }, mapMarkers),
            sceneSnos, staticTiles: staticTiles.Build, health: health);
        if (menuScreens is not null) session.WorldChanged += (_, _, _) => menuScreens.Relocate();

        var walkability = new LockedTileWalkability(new CascTileWalkability(sceneReader), cascGate);
        var exitScenes = LoadExitCandidateScenes(store, schema, cascGate);
        if (debug) Console.WriteLine($"Exit candidate scenes: {exitScenes.Count}.");
        var navigation = new NavigationBridge(
            session, speech, config.AutoAnnouncementsEnabled, diagnostics: debug, walkability, language, exitScenes.Contains,
            config.CompassDirections ? DirectionStyle.Compass : DirectionStyle.Screen);

        // One mixer and one output for the radar cues and the beacon loops.
        var interactables = new InteractableClassifier(definitionOf, store.Name);

        // Every cue runs whatever the sound settings say: a switched-off cue is only silent, so the settings window turns it back on live.
        var liveSound = new LiveSoundMix(SoundMix.From(config.Sound));
        var mixer = new AudioMixer();
        var loopSounds = LoadLoopSounds();
        using var radarPlayer = new RadarCuePlayer(new WaveOutAudioOut(), mixer)
        {
            Frame = config.RadarFrame, GainOf = (kind, ray) => liveSound.Current.RadarGain(kind, ray),
        };
        var radarSettings = new RadarSettings { Frame = config.RadarFrame, PollMs = GameSession.DefaultPlayerPollIntervalMs };
        var doors = config.RadarClosedDoorsEnabled ? new DoorObstacles(interactables, definitionOf) : null;
        var radarWorld = new DungeonRadarWorld(walkability);
        _ = new RadarBridge(session, radarWorld, radarPlayer, radarSettings, diagnostics: debug, doors);
        // Barrier walls a dungeon raises until its objective is done: walls for the radar and the guide while they stand. Doors the
        // player can't open yet: walls for the guide only (the radar sounds every closed door).
        var lockedDoors = new LockedDoors(doors ?? new DoorObstacles(interactables, definitionOf), store.Name, Console.WriteLine);
        _ = new BarrierBridge(
            session, config.LiveBarriersEnabled ? new BarrierObstacles(store.Name, definitionOf, Console.WriteLine) : null, lockedDoors,
            radarWorld.SetBarriers, navigation.SetBlockedCells);

        var loopMemory = loopSounds is null ? null : StartLoops(
            session, mixer, loopSounds, config, store, schema, cascGate, debug, definitionOf, interactables, dpo, portalRule,
            scene => { lock (cascGate) return tilePortals.In(scene); }, cue => liveSound.Current.Level(cue), lockedDoors.IsLocked);
        var portals = loopMemory?.Portals;
        var objectives = loopMemory?.Objectives;
        using var npcTap = new NpcCuePlayer(mixer);
        StartNpcTap(session, npcTap, definitionOf, store.Name, debug);
        _ = new StolenSkillsAnnouncer(session, new ObjectiveClassifier(definitionOf, store.Name).IsStolenSkill, speech, language);
        // A restart mid-dungeon (a rebuild, a crash) keeps the map, the explored tiles, the portals and the objectives heard.
        _ = new ExplorationPersistence(
            session, new ExplorationStore(AppContext.BaseDirectory, Console.WriteLine), navigation, portals, ProcessLocator.StartedUtc, Console.WriteLine,
            objectives);
        var cueGate = new CueGate(paused => mixer.Paused = paused, Console.WriteLine); // mute reasons always logged: testers' "no sound" reports
        session.TransitChanged += loading => cueGate.Set(CueMute.Loading, loading); // the loops would hang on their last positions
        if (config.PauseCuesInCutscenes)
        {
            WatchCutscenes(logWatcher, session, cueGate, debug);
            // In-engine cinematics log nothing; the player's RTC Playing flag covers them (research/runtime-signals.md §2).
            session.CinematicRead += playing => cueGate.Set(CueMute.Cinematic, playing);
            session.WorldChanged += (_, _, _) => cueGate.Set(CueMute.Cinematic, false);
        }
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
        using var guidePing = config.ExplorationGuideEnabled || routesEnabled ? new GuidePingPlayer(mixer) : null;
        void ApplySound(SoundMix mix)
        {
            mixer.MasterVolume = mix.Master;
            (npcTap.Volume, npcTap.Rate) = mix.Level(SoundCue.Npc);
            npcTap.Voices = mix.NpcVoices;
            if (guidePing is null) return;
            (guidePing.Volume, guidePing.Rate) = mix.Level(SoundCue.GuidePing);
            guidePing.ArrivalVolume = mix.Level(SoundCue.GuideArrival).Gain;
        }
        ApplySound(liveSound.Current);
        liveSound.Changed += ApplySound;
        // Xbox pads through XInput, PlayStation pads (which XInput never sees) through HID.
        using var xboxPad = guidePing is not null ? new XInputPad() : null;
        using var sonyPad = guidePing is not null ? new SonyHidPad(Console.WriteLine) : null;
        // Ctrl+Alt+Shift+G toggles the guide for keyboard players; the digits play the sound examples (below).
        using var hotkeys = new GlobalHotkeys(SoundDemo.ExampleCount);
        // In a game menu (inventory, map...) the D-pad navigates the menu, so presses there never toggle the guide; the key still does.
        var pad = xboxPad is not null && sonyPad is not null
            ? new CombinedPad([
                new GatedPad(new CombinedPad([xboxPad, sonyPad]), () => menus?.AnyOpen ?? false, debug ? () => Console.WriteLine("GUIDE toggle ignored: menu open") : null),
                hotkeys])
            : null;
        if (guidePing is not null && config.ExplorationGuideEnabled)
            _ = new ExplorationGuideBridge(
                session, navigation.NextGuideStep, guidePing, speech, () => portals?.FreshInSight ?? false, debug ? Console.WriteLine : null,
                pad, config.ExplorationGuideInDungeons, language, portals is null ? null : portals.Destinations,
                objectives is null ? null : () => [.. objectives.Destinations(), .. portals!.ObjectiveDestinations()],
                objectives is null ? null : () => { objectives.Rearm(); portals!.Rearm(); lockedDoors.Rearm(); });
        var routes = routesEnabled ? LoadQuestRoutes(store, schema, cascGate, debug) : [];
        if (guidePing is not null && routes.Count > 0)
            _ = new QuestRouteBridge(session, routes, guidePing, speech, language, pad, store.Name, debug ? Console.WriteLine : null);
        xboxPad?.Start();
        sonyPad?.Start();

        // Sound examples on Ctrl+Alt+Shift+1..8, to show the cues without a fight's noise.
        var demo = new SoundDemo(
            speech, digit => AppStrings.For(language.Current).SoundExamples[digit - 1], radarPlayer, mixer,
            loopSounds?.GetValueOrDefault(LoopTargetSelector.QuestItemSound), LoopProfiles.Quest.NearGain, guidePing,
            cueGate.Example);
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

        // The settings window edits a copy of the last saved config; the sound part plays live, the rest applies on restart.
        var savedConfig = config.Clone();
        var preview = new CuePreview(mixer, loopSounds, config.RadarFrame);
        using var tray = new TrayApp(SettingsStrings.For(language.Current), () =>
        {
            var visit = new SettingsSession(savedConfig, configPath, liveSound);
            var window = new SettingsForm(visit, SettingsStrings.For(language.Current), preview);
            window.FormClosed += (_, _) => savedConfig = visit.Saved;
            return window;
        });
        tray.ExitRequested += exit.Set;
        tray.RestartRequested += () => { restart = true; exit.Set(); };
        tray.CheckUpdatesRequested += updates.CheckNow;
        tray.Start();

        Console.WriteLine("Diablo 4 Assist running in the notification area; Ctrl+Alt+Shift+C opens the settings.");
        exit.Wait();
        var farewell = AppStrings.For(language.Current);
        if (!updated) speech.Speak(restart ? farewell.Restarting : farewell.AssistantClosed, interrupt: true); // the updater said it already
        if (restart) StartAnotherCopy(args);
        return 0;
    }

    // Battle.net or Steam; else the running game's folder, for any other store or a folder moved by hand.
    private static string? LocateInstall() =>
        SnoStore.TryLocateInstall(out var found) ? found
        : ProcessLocator.ExecutableDirectory() is { } running && SnoStore.IsInstall(running) ? running : null;

    // Slower than LocateInstall, so what it finds is kept in d4assist.json for the next starts.
    private static string? SearchOrAskForInstall(AppConfig config, string configPath)
    {
        Console.WriteLine("No launcher entry or running game names the install: searching the drives.");
        // The game's language comes from the install, so the picker uses the forced language, else Windows' one.
        var text = SettingsStrings.For(config.ForcedLanguage() ?? WindowsLanguage());
        var found = InstallFolderSearch.Locate(SnoStore.IsInstall) ?? InstallFolderPicker.AskAtStartup(text);
        if (found is null) return null;
        config.InstallPath = found;
        try { config.Save(configPath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Console.WriteLine($"Couldn't save InstallPath in {AppConfig.FileName}: {e.Message}"); }
        return found;
    }

    // For dialogs shown before the game's language is known: "es-MX" → "esMX", a game locale.
    private static SpeechLanguage WindowsLanguage() => SpeechLanguages.FromLocale(CultureInfo.CurrentUICulture.Name.Replace("-", ""));

    // For the log of a store we haven't tested (Steam): is the game's own file where the app expects it?
    private static string Presence(string folder, string file) => $"{file} {(File.Exists(Path.Combine(folder, file)) ? "found" : "missing")}";

    /// <summary>Logs a startup error and shows it: with no console window it would otherwise go unseen.</summary>
    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        System.Windows.Forms.MessageBox.Show(message, SettingsStrings.Spanish.AppName,
            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        return 1;
    }

    // The new copy waits for this one's single-instance slot, so it starts once this one has exited.
    private static void StartAnotherCopy(string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
        foreach (var arg in args.Where(a => a != RestartedFlag)) start.ArgumentList.Add(arg);
        start.ArgumentList.Add(RestartedFlag);
        Process.Start(start);
    }

    // <Version> in D4Assist.csproj plus the commit the SDK appends (+hash): which build a tester's log came from.
    private static string AppVersion =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "(version unknown)";

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

    /// <summary>The portals and objectives the actor loops found in the current dungeon, for the exploration guide.</summary>
    private sealed record LoopMemory(PortalMemory Portals, ObjectiveMemory Objectives);

    /// <summary>Starts the actor loops, levelled live by <paramref name="levelOf"/>; returns the memory of the portals and objectives they found.</summary>
    private static LoopMemory StartLoops(
        GameSession session, AudioMixer mixer, Dictionary<string, float[]> sounds, AppConfig config, SnoStore store, TypeSchema schema,
        object cascGate, bool debug, Func<int, ActorDefinitionInfo?> definitionOf, InteractableClassifier interactables,
        DungeonObjectiveReader dpo, PortalClassifier portalRule, Func<int, IReadOnlyList<TileActor>> tilePortals, Func<SoundCue, CueLevel> levelOf,
        Func<int, (float X, float Y), bool> lockedDoor)
    {
        int actorGroup = schema.SnoGroup(ActorDefinitionType);
        var beaconSnos = BeaconTargets.Resolve(
            config.BeaconActorNames, name => store.Resolve(actorGroup, name),
            name => Console.WriteLine($"Beacon actor '{name}' is not in CoreTOC; ignored."));

        var objectives = new ObjectiveClassifier(
            definitionOf, store.Name, sno => { lock (cascGate) return dpo.ActorsNamedByAnyQuest().Contains(sno); });
        var safeZones = new SafeZoneClassifier(definitionOf, store.Name);
        var selector = new LoopTargetSelector(
            beaconSnos, objectives, world => { lock (cascGate) return dpo.ActorSnos(world); }, interactables, portalRule.IsPortal,
            safeZones.InsideRadius);
        if (session.CurrentWorld is { } start) selector.SetWorld(WorldSnoOrNone(store, schema, start.Name), start.IsDungeon); // app started mid-dungeon

        var player = new SpatialLoopPlayer(mixer, sounds);
        var sightings = debug
            ? new ActorSightingLog(definitionOf, store.Name, Console.WriteLine)
            : null;
        var chests = GlowingChestStore.Load(AppContext.BaseDirectory, Console.WriteLine);
        var used = new GizmoUsedSignal(store.Name, chests.Kinds, chests.Add, debug ? Console.WriteLine : null);
        var loops = new ActorLoopBridge(
            session, player, selector, store.Name, log: debug ? Console.WriteLine : null, sightings: sightings, usedSignal: used, levelOf: levelOf);
        var objectiveMemory = new ObjectiveMemory(
            session, store.Name, debug ? Console.WriteLine : null, sno => definitionOf(sno)?.TriggerRadius, selector.ReceptacleTakesHeld,
            sno => definitionOf(sno) is { } info ? MathF.Max(info.BoundsHalfExtents.X, info.BoundsHalfExtents.Y) : null, lockedDoor);
        loops.ObjectivesSeen += objectiveMemory.Observe;
        session.QuestPlacesRead += objectiveMemory.ObservePlaces;
        var portals = new PortalMemory(
            session, actor => selector.Select(actor)?.Kind == LoopKind.ExitBeacon, store.Name, debug ? Console.WriteLine : null,
            tilePortals, () => selector.WorldObjectiveActors);
        return new LoopMemory(portals, objectiveMemory);
    }

    // Prisoners and light carriers keep their own loop, so they never tap as well.
    private static void StartNpcTap(
        GameSession session, NpcCuePlayer tap, Func<int, ActorDefinitionInfo?> definitionOf, Func<int, string?> nameOf, bool debug)
    {
        var npcs = new NpcClassifier(definitionOf, nameOf);
        var objectives = new ObjectiveClassifier(definitionOf, nameOf);
        var safeZones = new SafeZoneClassifier(definitionOf, nameOf);
        var none = new HashSet<int>();
        _ = new NpcCueBridge(
            session, tap,
            a => npcs.IsNpc(a.ActorSno) && objectives.Classify(a.ActorSno, none) is null && safeZones.InsideRadius(a.ActorSno) is null,
            nameOf, debug ? Console.WriteLine : null);
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
