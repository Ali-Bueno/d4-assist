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
        catch (Exception e) when (e is InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
        { return Fail($"{AppConfig.FileName}: {e.Message}"); }
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
        if (debug) speech.Speak(AppStrings.For(language.Current).DebugMode, interrupt: false);
        if (justUpdated) speech.Speak(string.Format(AppStrings.For(language.Current).UpdatedToFormat, UpdateVersion.Current.ToString(3)), interrupt: false);
        // A crash would otherwise end in silence.
        AppDomain.CurrentDomain.UnhandledException += (_, _) => speech.Speak(AppStrings.For(language.Current).Stopped, interrupt: true);
        // A read that keeps failing after a game update is said once, so the player knows what stopped working.
        var health = ReadHealth.Live(Console.WriteLine, check => speech.Speak(AppStrings.For(language.Current).ReadWarning(check), interrupt: false));
        // Checked before the slow loading: just started from the desktop, the app may still bring its question to the front.
        using var exit = new ManualResetEventSlim();
        bool restart = false, updated = false;
        if (UpdatePackage.DeleteWorkFolders(Path.GetTempPath()) is { } cleaned) Console.WriteLine(cleaned); // the last update's download
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
        LogSchemaCheck(store, schema, definitionsPath);
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
        bool watchMenus = config.PauseCuesInMenus || config.ExplorationGuideEnabled || routesEnabled || config.WaypointListEnabled;
        // Scan results always logged: a tester's "can't find the game menus" after a character switch came with no other clue.
        var menuScreens = watchMenus ? MenuScreenTracker.ForLiveProcess(Console.WriteLine) : null;
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
        // A quest chest belongs to the quest its name starts with (QuestOwnerIndex): silent while that quest isn't followed.
        var questOwners = new QuestOwnerIndex(store.AllEntries(schema.SnoGroup("QuestDefinition")));
        var chestOwners = new System.Collections.Concurrent.ConcurrentDictionary<int, int?>();
        Func<int, int?> questOwner = sno => chestOwners.GetOrAdd(sno, s =>
            definitionOf(s) is { GizmoType: Conventions.GizmoQuestChest } && store.Name(s) is { } name ? questOwners.OwnerOf(name) : null);
        // The world's own objective quest too: a seasonal hub's zone quest names its steps by actor, with no marker to find it by,
        // and the game shows its phase once the main quest hands over to it (research/dungeon-objectives.md §18). And the quest of a
        // quest chest nearby, so the chest sounds once its quest runs. Outside the open world, every quest whose step is shown in
        // this world (the open world shows 2041 quests' steps, a story instance a handful).
        Func<QuestSearchArea, IReadOnlyList<int>> questsHere = area =>
        {
            var owners = area.Actors.Select(a => questOwner(a.ActorSno)).OfType<int>().ToList();
            lock (cascGate)
                return [.. questScripts.QuestsWithMarkersIn(new[] { area.WorldSno }.Concat(area.Scenes).Select(store.Name).OfType<string>())
                    .Union(questScripts.QuestsNaming(area.Actors, ActiveQuests.MarkerReach))
                    .Union(dpo.QuestSnos(area.WorldSno))
                    .Union(store.Name(area.WorldSno) == WorldClassifier.OpenWorldName ? [] : questScripts.QuestsShownIn(area.WorldSno))
                    .Union(owners)];
        };
        // Found once per game build while someone talks, then kept in a file next to the exe (research/client-memory.md §5).
        var conversationSnos = store.AllEntries(schema.SnoGroup("ConversationDefinition")).Select(e => e.Id).ToHashSet();
        var conversationAddresses = new ConversationAddresses(
            Path.Combine(AppContext.BaseDirectory, "d4assist-addresses.json"), conversationSnos.Contains, debug ? Console.WriteLine : null);
        var questHistory = QuestPhaseHistory.Load(AppContext.BaseDirectory, log: Console.WriteLine);
        var sceneLayers = new SceneLayerPlacement(Console.WriteLine);
        using var session = new GameSession(
            logWatcher, classifier,
            (worldSno, knownSceneSnos) => LiveDungeonSessionReader.TryOpen(
                installPath, candidates, playerActorSnos, worldSno, knownSceneSnos, menuScreens, readsFlags, questMarkers, conversationAddresses,
                questsHere, health, actorSnos.Contains, debug ? new SwitchStateLog(isMultiUseSwitch, store.Name, Console.WriteLine) : null,
                scene => { lock (cascGate) return tileGizmos.In(scene); }, mapMarkers, questOwner, questHistory,
                sno => definitionOf(sno) is { } definition && ObjectiveClassifier.IsStructure(store.Name(sno), definition),
                sceneLayers),
            sceneSnos, staticTiles: staticTiles.Build, health: health);
        if (menuScreens is not null) session.WorldChanged += (_, _, _) => menuScreens.Relocate();
        // The Horadric guardian's barriers: only the vessel a barrier names sounds, the guide leads to it, and its name is spoken
        // as the game shows it ("Gefäß des Cain").
        string VesselShown(string vessel)
        {
            lock (cascGate) return store.Text(SpeechLanguages.GameLocale(language.Current), "Actor_" + vessel, "Name") ?? vessel;
        }
        var ward = new HoradricWard(store.Name, Console.WriteLine,
            vessel => speech.Speak(VesselShown(vessel), interrupt: true),
            vessel => speech.Speak(string.Format(AppStrings.For(language.Current).VesselUsedFormat, VesselShown(vessel)), interrupt: true));
        session.ActorRewrite = ward.Apply;
        session.WorldChanged += (_, _, _) => ward.Reset();

        var walkability = new LockedTileWalkability(new CascTileWalkability(sceneReader), cascGate);
        var exitScenes = LoadExitCandidateScenes(store, schema, cascGate);
        if (debug) Console.WriteLine($"Exit candidate scenes: {exitScenes.Count}.");
        var navigation = new NavigationBridge(
            session, speech, config.AutoAnnouncementsEnabled, diagnostics: debug, walkability, language, exitScenes.Contains,
            config.CompassDirections ? DirectionStyle.Compass : DirectionStyle.Screen);
        // Traversal points join floor the walk map splits (a gap to jump, a ladder, a gap to duck under): the guide routes over them
        // and leads to the traversal point; a slide one way to its exit, a climb from its top exit to its bottom one (jumps: live
        // 2026-10-18, Philios mindscape; ladders and a duck-under: three of the Necropolis's five tumors stand on ground reached only
        // by them, research/dungeon-objectives.md §12; slide and climb: live 2026-10-22, "Die Leere").
        var jumpScenes = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: true, includeMap: false);
        var tileJumps = new TileActorReader(jumpScenes, sno => TraversalKinds.Of(store.Name(sno)) != TraversalKind.None);
        _ = new JumpLinkCollector(session, scene => { lock (cascGate) return tileJumps.In(scene); }, navigation.SetJumpLinks, debug ? Console.WriteLine : null,
            sno => TraversalKinds.Of(store.Name(sno)));

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
        var barrierBridge = new BarrierBridge(
            session, config.LiveBarriersEnabled ? new BarrierObstacles(store.Name, definitionOf, Console.WriteLine) : null, lockedDoors,
            radarWorld.SetBarriers, navigation.SetBlockedCells, navigation.SetGates);
        // A boss room's door opens when the objective is done, often while the player is too far for the client to list it.
        session.QuestMovedOn += barrierBridge.QuestMovedOn;

        var waypointCatalog = new WaypointCatalog(store, schema);
        IReadOnlyList<Waypoint> allWaypoints;
        lock (cascGate) allWaypoints = waypointCatalog.All;
        Func<IReadOnlySet<int>, IReadOnlyList<int>?> readActivatedWaypoints =
            levelAreas => { IReadOnlyList<int>? read = null; session.Query(r => read = r.ReadActivatedWaypoints(levelAreas)); return read; };
        var waypointBeacons = new WaypointBeacons(allWaypoints, readActivatedWaypoints, () => session.CurrentWorldSno, debug ? Console.WriteLine : null);

        var loopMemory = loopSounds is null ? null : StartLoops(
            session, mixer, loopSounds, config, store, schema, cascGate, debug, definitionOf, interactables, dpo, portalRule,
            scene => { lock (cascGate) return tilePortals.In(scene); }, cue => liveSound.Current.Level(cue), lockedDoors.IsLocked,
            waypointBeacons.Spots);
        var portals = loopMemory?.Portals;
        var objectives = loopMemory?.Objectives;
        session.QuestActorsNamed += snos =>
        {
            lockedDoors.ObserveNamed(snos);
            objectives?.ObserveNamed(snos);
        };
        using var npcTap = new NpcCuePlayer(mixer);
        StartNpcTap(session, npcTap, definitionOf, store.Name, debug);
        var markerLog = new MapMarkerLog(store.Name, Console.WriteLine, everyKind: debug);
        session.MapMarkersRead += markerLog.OnMarkers;
        session.WorldChanged += (_, _, _) => markerLog.Reset();
        using var enemyCue = new EnemyCuePlayer(mixer);
        var carried = new CarryGoals(new ObjectiveClassifier(definitionOf, store.Name));
        var enemyClasses = new EnemyClassifier(definitionOf);
        double telegraphGain = 1;
        _ = new EnemyCueBridge(session, enemyCue, enemyClasses.Of, store.Name, debug ? Console.WriteLine : null,
            actors => actors.Any(carried.IsHeld) ? [.. actors.Select(a => carried.IsBossToBind(a) ? a with { Invulnerable = false } : a)] : actors);
        _ = new StolenSkillsAnnouncer(session, new ObjectiveClassifier(definitionOf, store.Name).IsStolenSkill, speech, language);
        // A boss or elite attack about to land where the player stands: a sharp cue toward it and the way to step out.
        var telegraphCue = TelegraphCueSynth.Synthesize();
        _ = new TelegraphWarner(session, new TelegraphClassifier(store.Name), enemyClasses.Of, speech, language,
            pan => { if (telegraphGain > 0) mixer.Play(telegraphCue, telegraphGain * RadarCuePlayer.LoudnessBoost, pan, priority: TelegraphPriority); },
            (x, y) => radarWorld.Cell((int)MathF.Floor(x), (int)MathF.Floor(y)) is RadarCell.Blocked or RadarCell.Door,
            debug ? Console.WriteLine : null, isProjectile: sno => definitionOf(sno)?.EType == ProjectileActorType);
        // Every special or area attack, from its telegraph on: a loop where it is, moving with it.
        if (loopSounds is not null && loopSounds.ContainsKey(HazardLoopBridge.HazardSound))
        {
            var hazardLoops = new SpatialLoopPlayer(mixer, loopSounds) { MaxVoices = HazardLoopBridge.MaxLoops };
            _ = new HazardLoopBridge(session, new TelegraphClassifier(store.Name), enemyClasses.Of,
                sno => definitionOf(sno)?.EType == ProjectileActorType, hazardLoops.Update, () => liveSound.Current.Level(SoundCue.Telegraph),
                debug ? Console.WriteLine : null);
        }
        // A restart mid-dungeon (a rebuild, a crash) keeps the map, the explored tiles, the portals and the objectives heard.
        var persistence = new ExplorationPersistence(
            session, new ExplorationStore(AppContext.BaseDirectory, Console.WriteLine), navigation, portals, ProcessLocator.StartedUtc, Console.WriteLine,
            objectives, sceneLayers);
        session.WorldChanged += (_, _, _) => sceneLayers.Reset();
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
            foreach (var (kind, cue) in new[] { (EnemyClass.Boss, SoundCue.Boss), (EnemyClass.Elite, SoundCue.Elite) })
            {
                var (volume, rate) = mix.Level(cue);
                enemyCue.SetLevel(kind, volume, rate);
            }
            telegraphGain = mix.Level(SoundCue.Telegraph).Gain;
            if (guidePing is null) return;
            (guidePing.Volume, guidePing.Rate) = mix.Level(SoundCue.GuidePing);
            guidePing.ArrivalVolume = mix.Level(SoundCue.GuideArrival).Gain;
        }
        ApplySound(liveSound.Current);
        liveSound.Changed += ApplySound;
        // Xbox pads through XInput, PlayStation pads (which XInput never sees) through HID.
        using var xboxPad = guidePing is not null ? new XInputPad(Console.WriteLine) : null;
        using var sonyPad = guidePing is not null ? new SonyHidPad(Console.WriteLine) : null;
        // Ctrl+Alt+Shift+G toggles the guide for keyboard players; the digits play the sound examples (below). Only while the
        // game is in front: the settings and exit keys (TrayApp) are the only ones that work everywhere.
        using var hotkeys = new GlobalHotkeys(SoundDemo.ExampleCount, GameWindowFocus.IsGameForeground, config.WalletKeyEnabled);
        // F2: the helltide's cinders and Baneful Hearts, named as the game names them.
        var wallet = new WalletAnnouncer(
            () => { IReadOnlyList<long>? amounts = null; session.Query(r => amounts = r.ReadCurrencies()); return amounts; },
            type => WalletCurrencyItems.TryGetValue(type, out var item)
                ? StripGrammarTag(LockedText(cascGate, () => store.Text(SpeechLanguages.GameLocale(language.Current), "Item_" + item, "Name")))
                : null,
            speech, language, debug ? Console.WriteLine : null);
        hotkeys.WalletKeyPressed += wallet.Announce;
        // In a game menu (inventory, map...) the D-pad navigates the menu, so presses there never toggle the guide; the key still does.
        // The pad is read whatever window has the focus: in another game, or with Diablo IV closed, its D-pad isn't ours.
        var pad = xboxPad is not null && sonyPad is not null
            ? new CombinedPad([
                new GatedPad(
                    new GatedPad(new CombinedPad([xboxPad, sonyPad]), () => !GameWindowFocus.IsGameForeground(),
                        debug ? () => Console.WriteLine("GUIDE toggle ignored: game not in the foreground") : null),
                    () => menus?.AnyOpen ?? false, debug ? () => Console.WriteLine("GUIDE toggle ignored: menu open") : null),
                hotkeys])
            : null;
        if (pad is not null)
        {
            pad.DpadLeftPressed += () => Console.WriteLine("Guide toggle pressed."); // rare; tells a silent pad from a silent guide
            pad.DpadLeftHeld += () => Console.WriteLine("Guide mode pressed.");
        }
        QuestRouteBridge? routeBridge = null;
        QuestPathBridge? pathBridge = null;
        var bossEntrances = new TileActorReader(markerScenes, sno => store.Name(sno) == BossRoomDestination.EntranceTriggerName);
        var bossRoom = new BossRoomDestination(session, scene => { lock (cascGate) return bossEntrances.In(scene); }, debug ? Console.WriteLine : null);
        persistence.Restored += bossRoom.OnRestored;
        // A dungeon whose objective quest names no actor asks for a kill count: every monster (the seasonal Den of Evil).
        var monsters = new RemainingMonstersDestination(session, name =>
        {
            int world = WorldSnoOrNone(store, schema, name);
            lock (cascGate) return dpo.QuestSnos(world).Count > 0 && dpo.ActorSnos(world).Count == 0;
        }, debug ? Console.WriteLine : null);
        session.MapMarkersRead += monsters.OnMarkers;
        if (guidePing is not null && config.ExplorationGuideEnabled)
            _ = new ExplorationGuideBridge(
                session, navigation.NextGuideStep, guidePing, speech, () => portals?.FreshInSight ?? false, debug ? Console.WriteLine : null,
                pad, config.ExplorationGuideInDungeons, language,
                portals is null ? () => [.. monsters.Destinations(), .. bossRoom.Destinations()]
                    : () => [.. monsters.Destinations(), .. bossRoom.Destinations(), .. portals.Destinations()],
                objectives is null ? null : () =>
                {
                    bool elsewhere = false;
                    session.Query(r => elsewhere = r.QuestStepElsewhere);
                    return [.. ward.Destinations(), .. objectives.Destinations(), .. portals!.ObjectiveDestinations(),
                        .. elsewhere ? portals.WayOutDestinations() : []];
                },
                objectives is null ? null : walkedThrough => { objectives.Rearm(walkedThrough); portals!.Rearm(); lockedDoors.Rearm(); navigation.RearmGates(); },
                () => routeBridge?.IsActive == true || pathBridge?.TakesPad == true, () => objectives?.BossFight == true);
        var routes = routesEnabled ? LoadQuestRoutes(store, schema, cascGate, debug) : [];
        if (guidePing is not null && routes.Count > 0)
            routeBridge = new QuestRouteBridge(session, routes, guidePing, speech, language, pad, store.Name, debug ? Console.WriteLine : null);
        if (routeBridge is not null) session.QuestMovedOn += routeBridge.OnQuestMovedOn;
        if (guidePing is not null && routesEnabled)
        {
            var paths = NewOverworldPaths(store, schema, cascGate);
            // The session sets the world's kind before it raises the world change.
            pathBridge = new QuestPathBridge(
                session, _ => session.CurrentWorldKind is WorldKind.Overworld or WorldKind.RadarOnly,
                markers =>
                {
                    IReadOnlyList<int>? owned = null;
                    session.Query(r => owned = r.ReadOwnedQuests());
                    IReadOnlyDictionary<int, uint>? FindPhases(IReadOnlyDictionary<int, IReadOnlySet<uint>> quests)
                    {
                        IReadOnlyDictionary<int, uint>? found = null;
                        session.Query(r => found = r.FindQuestPhases(quests));
                        return found;
                    }
                    return QuestMapPlaces(markers, owned, FindPhases, questHistory, store, questScripts, language, cascGate, debug ? Console.WriteLine : null);
                },
                paths.Find,
                guidePing, speech, language, pad, log: debug ? Console.WriteLine : null, routeActive: () => routeBridge?.IsActive == true);
            session.MapMarkersRead += pathBridge.OnMapMarkers;
        }
        xboxPad?.Start();
        sonyPad?.Start();

        // Sound examples on Ctrl+Alt+Shift+1..8, to show the cues without a fight's noise.
        var demo = new SoundDemo(
            speech, digit => AppStrings.For(language.Current).SoundExamples[digit - 1], radarPlayer, mixer,
            loopSounds?.GetValueOrDefault(LoopTargetSelector.QuestItemSound), LoopProfiles.Quest.NearGain, guidePing,
            cueGate.Example);
        hotkeys.DigitPressed += demo.Play;
        hotkeys.Start();

        // Shift+F1 on the world map: the activated waypoints, nearest first; Enter puts the mouse cursor on one.
        using var mapKeys = config.WaypointListEnabled ? new MapListKeys(Console.WriteLine) : null;
        if (mapKeys is not null)
        {
            var zones = new ZoneCatalog(store, schema);
            var bounties = new HelltideBountyCatalog(store, schema);
            // The game's text bundle (60,000 tables) loads on the first name, the regions on the first one asked (every subzone read):
            // both now, not on the first Shift+F1.
            if (allWaypoints.Count > 0)
                _ = Task.Run(() => { lock (cascGate) waypointCatalog.Zone(allWaypoints[0], SpeechLanguages.GameLocale(language.Current)); });
            var waypointList = new WaypointListBridge(
                allWaypoints, w => { lock (cascGate) return waypointCatalog.Name(w, SpeechLanguages.GameLocale(language.Current)); },
                readActivatedWaypoints,
                () => { D4Memory.Ui.MapView? view = null; session.Query(r => view = r.ReadMapView()); return view; },
                () => session.CurrentWorldSno, speech, language, config.CompassDirections ? DirectionStyle.Compass : DirectionStyle.Screen, mapKeys, new GameMouse(),
                GameWindowFocus.IsGameForeground, debug ? Console.WriteLine : null,
                w => { lock (cascGate) return waypointCatalog.Zone(w, SpeechLanguages.GameLocale(language.Current)); },
                m => { lock (cascGate) return HelltidePlaceOf(m, store, zones, SpeechLanguages.GameLocale(language.Current)); },
                () =>
                {
                    IReadOnlyDictionary<int, int>? states = null;
                    session.Query(r => states = r.ReadWorldStates());
                    return states is null ? null : ActiveHelltides.From(states, ActiveHelltides.ExpansionsHaveHelltides(store.BuildVersion));
                },
                region => HelltideRegionName(region, allWaypoints, w => { lock (cascGate) return waypointCatalog.Zone(w, SpeechLanguages.GameLocale(language.Current)); }),
                helltideEndsEarly: ActiveHelltides.BaseEndsEarly(store.BuildVersion),
                bountiesOf: region =>
                {
                    IReadOnlyDictionary<int, int>? states = null;
                    session.Query(r => states = r.ReadWorldStates());
                    if (states is null) return [];
                    lock (cascGate)
                        return bounties.All.Where(b => HelltideRegionOf(b.Territory) == region && states.GetValueOrDefault(b.WorldStateSno) == 1 &&
                                b.HasTurn(states.GetValueOrDefault(bounties.BountySubzoneSno)))
                            .Select(b => (bounties.Name(b, SpeechLanguages.GameLocale(language.Current)), (double)b.Position.X, (double)b.Position.Y))
                            .ToList();
                });
            session.WorldChanged += (_, _, _) => waypointList.OnWorldChanged();
            session.PlayerMoved += waypointList.OnPlayerMoved;
            session.MapMarkersRead += waypointList.OnMapMarkers;
            session.MenusRead += waypointList.OnMenus;
            waypointList.Start();
            mapKeys.Start();
            Console.WriteLine($"Waypoint list: {allWaypoints.Count} waypoints in the game data; Shift+F1 on the world map, Shift+F2 the helltide.");
        }

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
        updates.AppStarted();

        Console.WriteLine("Diablo 4 Assist running in the notification area; Ctrl+Alt+Shift+C opens the settings, Ctrl+Alt+Shift+Q exits.");
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

    // After a game patch the schema can go stale without a word: every offset would read garbage. The check reads ~100
    // records (tens of ms) and logs which ones disagree, so a tester's log says whether definitions.json still fits.
    private static void LogSchemaCheck(SnoStore store, TypeSchema schema, string? definitionsPath)
    {
        string source = definitionsPath ?? $"embedded, d4data {EmbeddedDefinitionsBuild() ?? "build unknown"}";
        var result = SchemaCheck.Run(store, schema);
        if (result.Passed)
        {
            Console.WriteLine($"Schema: definitions.json ({source}) fits game build {store.BuildVersion} ({result.Records} records checked).");
            return;
        }
        Console.WriteLine($"Schema: definitions.json ({source}) does NOT fit game build {store.BuildVersion}, rooms, quests and objects "
            + $"may read wrong: {string.Join("; ", result.Failures)} ({result.Records} records checked).");
    }

    private static string? EmbeddedDefinitionsBuild()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("definitions-build.txt");
        return stream is null ? null : new StreamReader(stream).ReadToEnd().Trim();
    }

    private static Dictionary<string, float[]>? LoadLoopSounds()
    {
        try { return SpatialLoopPlayer.LoadSounds(Path.Combine(AppContext.BaseDirectory, "Sounds"), Console.WriteLine); }
        catch (InvalidDataException e) { Console.WriteLine($"Loop sounds disabled, sounds not loaded: {e.Message}"); return null; }
    }

    /// <summary>The portals and objectives the actor loops found in the current dungeon, for the exploration guide.</summary>
    private sealed record LoopMemory(PortalMemory Portals, ObjectiveMemory Objectives);

    /// <summary>Starts the actor loops, levelled live by <paramref name="levelOf"/>; returns the memory of the portals and objectives they found.</summary>
    private static LoopMemory StartLoops(
        GameSession session, AudioMixer mixer, Dictionary<string, float[]> sounds, AppConfig config, SnoStore store, TypeSchema schema,
        object cascGate, bool debug, Func<int, ActorDefinitionInfo?> definitionOf, InteractableClassifier interactables,
        DungeonObjectiveReader dpo, PortalClassifier portalRule, Func<int, IReadOnlyList<TileActor>> tilePortals, Func<SoundCue, CueLevel> levelOf,
        Func<int, (float X, float Y), bool> lockedDoor, Func<PlayerState, IReadOnlyList<LoopSpot>> spots)
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

        var player = new SpatialLoopPlayer(mixer, sounds) { MaxVoices = AudioMixer.MaxLoopVoices - HazardLoopBridge.MaxLoops };
        var sightings = debug
            ? new ActorSightingLog(definitionOf, store.Name, Console.WriteLine)
            : null;
        if (debug) _ = new SoulSplitLog(session, store.Name, definitionOf, Console.WriteLine);
        if (debug)
        {
            var life = new PlayerLifeLog(Console.WriteLine);
            session.PlayerMoved += life.OnPlayer;
            session.LifeRead += life.OnLife;
        }
        var chests = GlowingChestStore.Load(AppContext.BaseDirectory, Console.WriteLine);
        var used = new GizmoUsedSignal(store.Name, chests.Kinds, chests.Add, debug ? Console.WriteLine : null);
        var loops = new ActorLoopBridge(
            session, player, selector, store.Name, log: debug ? Console.WriteLine : null, sightings: sightings, usedSignal: used, levelOf: levelOf,
            spots: spots);
        var objectiveMemory = new ObjectiveMemory(
            session, store.Name, debug ? Console.WriteLine : null, sno => definitionOf(sno)?.TriggerRadius, selector.ReceptacleTakesHeld,
            sno => definitionOf(sno) is { } info ? MathF.Max(info.BoundsHalfExtents.X, info.BoundsHalfExtents.Y) : null, lockedDoor,
            selector.CarryableWaits, safeZones.EscortRadius, objectives.IsBossOrUnique);
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
        var questScripts = new QuestScriptMarkerReader(store, schema, markers);
        var groups = new QuestRouteGroups(schema.SnoGroup(MarkerSetType), schema.SnoGroup(QuestDefinitionType), schema.SnoGroup(ActorDefinitionType));
        List<LoadedQuestRoute> routes;
        lock (cascGate)
            routes = [.. QuestRouteCatalog.All
                .Select(route => QuestRouteLoader.Load(
                    route, groups, (group, name) => SnoOrNull(store, group, name), markers.ReadAll, questScripts.Markers, Console.WriteLine))
                .OfType<LoadedQuestRoute>()];
        if (debug)
            foreach (var r in routes)
                Console.WriteLine($"Quest route {r.Route.Name}: start ({r.Start.X:F1},{r.Start.Y:F1}), points " +
                    string.Join(" ", r.Points.Select(p => $"({p.X:F1},{p.Y:F1})")) +
                    (r.End is { } end ? $", end ({end.X:F1},{end.Y:F1})" : "") + $", {r.ActivationSnos.Count} activation actors.");
        return routes;
    }

    // A map marker as an object of the helltide list (HelltideObjects): its UI name ("Gequältes Geschenk der Geheimnisse"; the
    // Name label adds the cinder cost on a second line), its level area's name and the helltide's area, the territory and region
    // ("Auen von Fethis, Hawezar").
    // The currencies F2 says, by eCurrencyType: the ItemDefinitions that name them (ItemDefinition.eCurrencyType).
    private static readonly Dictionary<int, string> WalletCurrencyItems = new()
    {
        [D4Memory.Actors.CurrencyReader.Cinders] = "Helltide_Currency", [D4Memory.Actors.CurrencyReader.BanefulHearts] = "Helltide_BossSummon_Currency",
    };

    private static string? LockedText(object gate, Func<string?> read)
    {
        lock (gate) return read();
    }

    private static string? StripGrammarTag(string? text) =>
        text is ['[', ..] && text.IndexOf(']') is > 0 and var end ? text[(end + 1)..] : text;

    // A helltide region's name in the game's language: the region of a town waypoint in it ("Hawezar" for Zarbinzet), as the
    // helltide objects' areas name it.
    private static readonly Dictionary<HelltideRegion, string> HelltideRegionTowns = new()
    {
        [HelltideRegion.Scosglen] = "Scos_Cerrigar", [HelltideRegion.FracturedPeaks] = "Frac_Kyovashad", [HelltideRegion.Hawezar] = "Hawe_Zarbinzet",
        [HelltideRegion.Kehjistan] = "Kehj_Gea_Kul", [HelltideRegion.DrySteppes] = "Step_KedBardu", [HelltideRegion.Nahantu] = "Naha_Kurast_Bazaar",
        [HelltideRegion.Skovos] = "Skov_Temis",
    };

    // A helltide bounty's region by its territory key's prefix ("Scos_DeepForest" → Scosglen).
    private static HelltideRegion? HelltideRegionOf(string territory) => territory.Split('_')[0] switch
    {
        "Scos" => HelltideRegion.Scosglen, "Frac" => HelltideRegion.FracturedPeaks, "Hawe" => HelltideRegion.Hawezar,
        "Kehj" => HelltideRegion.Kehjistan, "Step" => HelltideRegion.DrySteppes, "Naha" => HelltideRegion.Nahantu, "Skov" => HelltideRegion.Skovos,
        _ => null,
    };

    private static string HelltideRegionName(HelltideRegion region, IReadOnlyList<Waypoint> waypoints, Func<Waypoint, string?> zoneOf) =>
        waypoints.FirstOrDefault(w => w.LevelAreaName == HelltideRegionTowns[region]) is { } town && zoneOf(town) is { } name ? name : region.ToString();

    private static HelltidePlace? HelltidePlaceOf(MarkedActor marker, SnoStore store, ZoneCatalog zones, string locale)
    {
        string? actor = store.Name(marker.ActorSno);
        if (!HelltideObjects.Is(marker.Kind, actor)) return null;
        // A monster's Name carries a grammar tag ("[ms]Gefallener Altar").
        string name = StripGrammarTag(store.Text(locale, "Actor_" + actor, "UIName")
            ?? store.Text(locale, "Actor_" + actor, "Name")?.Split(['\r', '\n'])[0]) ?? actor!;
        string? region = store.Name(marker.LevelAreaSno) is { } area ? store.Text(locale, "LevelArea_" + area, "Name") : null;
        string[] parts = [.. new[] { zones.TerritoryName(marker.LevelAreaSno, locale), zones.Name(marker.LevelAreaSno, locale) }.OfType<string>()];
        return new HelltidePlace(name, region, parts.Length > 0 ? string.Join(", ", parts) : null);
    }

    // The game's own quest markers (map kind 12: Symbol_Quest_Proxy for an area step such as "kill the fiend roaming here", live
    // 2026-10-19; MarkerLocation by the siege towers of "Verwundete Seelen", where D-pad left found no third tower, live 2026-10-21),
    // named after a quest read as in progress with an object near, else "quest marker"; and the map's markers that a
    // quest read as in progress names at their spot (ActiveQuests.MarkerReach). The map also shows objects of quests the player
    // hasn't taken ("Kuriose Kuriositäten", "Last der Sünde" near the Volcano, live 2026-10-19): those are left out, and so are a
    // quest's objects of other steps than the one last read (QuestStep.Of). A quest the map names that isn't owned yet is looked
    // for in progress at once (its {quest, phase} pair): owned quests are found only once their objects load around the player,
    // and right after the app started D-pad left said "no quest place" beside "In Rathmas Fußstapfen" (live 2026-10-21). One not
    // found that way counts as in progress at its phase remembered from earlier (QuestPhaseHistory, kept across runs): the module
    // data holds one quest's pair at a time, and after a restart "Verwundete Seelen" was never found again (live 2026-10-21).
    private static IReadOnlyList<QuestMapPlace> QuestMapPlaces(
        IReadOnlyList<MarkedActor> markers, IReadOnlyList<int>? owned,
        Func<IReadOnlyDictionary<int, IReadOnlySet<uint>>, IReadOnlyDictionary<int, uint>?> findPhases, QuestPhaseHistory questHistory,
        SnoStore store, QuestScriptMarkerReader questScripts, LanguageSetting language, object cascGate, Action<string>? log)
    {
        string locale = SpeechLanguages.GameLocale(language.Current);
        string NameOf(int quest) => store.Name(quest) is { } coreName ? store.Text(locale, "Quest_" + coreName, "Name") ?? coreName : quest.ToString();
        var named = new List<(MarkedActor Marker, int Quest)>();
        lock (cascGate)
            foreach (var marker in markers.Where(m => !IsQuestSymbol(m)))
                foreach (int quest in questScripts.QuestsNaming([(marker.ActorSno, new System.Numerics.Vector3(marker.X, marker.Y, marker.Z))], ActiveQuests.MarkerReach))
                    named.Add((marker, quest));

        var inProgress = new HashSet<int>(owned ?? []);
        var unknown = named.Select(n => n.Quest).Where(q => !inProgress.Contains(q)).Distinct().ToList();
        if (unknown.Count > 0)
        {
            Dictionary<int, IReadOnlySet<uint>> phaseUids;
            lock (cascGate) phaseUids = unknown.ToDictionary(q => q, q => (IReadOnlySet<uint>)questScripts.PhaseObjects(q).Select(p => p.PhaseUid).ToHashSet());
            var found = findPhases(phaseUids) ?? new Dictionary<int, uint>();
            foreach (var (quest, phase) in found)
            {
                questHistory.Record(quest, phase);
                inProgress.Add(quest);
            }
            var remembered = unknown.Where(q => !found.ContainsKey(q) && questHistory.Last(q) is not null).ToList();
            inProgress.UnionWith(remembered);
            log?.Invoke($"PATH quests the map names, not owned: {string.Join(", ", unknown)}; in progress: " +
                (found.Count > 0 ? string.Join(", ", found.Select(f => $"{f.Key} at {f.Value}")) : "none") +
                (remembered.Count > 0 ? "; remembered: " + string.Join(", ", remembered.Select(q => $"{q} at {questHistory.Last(q)}")) : ""));
        }

        var places = new List<QuestMapPlace>();
        lock (cascGate)
        {
            foreach (var marker in markers.Where(IsQuestSymbol))
            {
                var at = new System.Numerics.Vector2(marker.X, marker.Y);
                int? quest = owned is null ? null : questScripts.QuestNearest(at, QuestSymbolNamingUnits, inProgress);
                places.Add(new QuestMapPlace(quest ?? 0, quest is { } q ? NameOf(q) : AppStrings.For(language.Current).QuestMarker, marker.ActorSno, at,
                    GameMarker: true));
            }
            foreach (var (marker, quest) in named.Where(n => inProgress.Contains(n.Quest)))
            {
                var at = new System.Numerics.Vector2(marker.X, marker.Y);
                var step = questHistory.Last(quest) is { } phase ? questScripts.PhaseObjects(quest).FirstOrDefault(p => p.PhaseUid == phase) : null;
                var role = step is null ? StepPlace.Shown : QuestStep.Of(step, marker.ActorSno, at);
                if (role != StepPlace.NotInStep) places.Add(new QuestMapPlace(quest, NameOf(quest), marker.ActorSno, at, Hidden: role == StepPlace.Hidden));
            }
        }
        return places;

        bool IsQuestSymbol(MarkedActor marker) => marker.Kind == QuestSymbolMarkerKind && store.Name(marker.ActorSno) is { } name && QuestSymbolActors.Contains(name);
    }

    // The game's generic quest map markers and their marker kind; quest objects this near name their quest.
    private static readonly string[] QuestSymbolActors = ["Symbol_Quest_Proxy", "MarkerLocation"];
    private const int QuestSymbolMarkerKind = 12;
    private const float QuestSymbolNamingUnits = 60f;

    // A traversal's gizmos (Traversal_Gizmo_Jump, _DuckUnder, _Slide...) and the spots it lands at (research/dungeon-data.md §4.1c).
    private const string TraversalGizmoPrefix = "Traversal_Gizmo_";
    private const string TraversalSlideName = "Traversal_Gizmo_Slide";
    private const string TraversalExitName = "MarkerLocation_TraversalExit";

    // Paths over the offline scene surfaces of the open world and the story areas; every game-data read holds the CASC gate.
    // Above every radar cue (RadarCueKindExtensions.Priority): a warning is never the voice stolen.
    private const int TelegraphPriority = 100;

    // ActorDefinition.eType of a projectile (the Horadric guardian's wave wall, tornadoes, chakram).
    private const int ProjectileActorType = 10;

    private static OverworldPaths NewOverworldPaths(SnoStore store, TypeSchema schema, object cascGate)
    {
        var worlds = new WorldReader(store, schema);
        var scenes = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: false);
        // Traversals sit in any marker layer ("Game", "Traversals"), as the jumps of JumpLinkCollector.
        var traversals = new TileActorReader(
            new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: true, includeMap: false),
            sno => store.Name(sno) is { } name && (name.StartsWith(TraversalGizmoPrefix, StringComparison.Ordinal) || name == TraversalExitName));
        var chunks = new Dictionary<string, IReadOnlyList<SceneChunkInfo>>();
        // A world's own marker layers ("Frac_Underworld (QST_Frac_Underworld_02_JumpTraversal)") place traversals in world space.
        var markers = new MarkerReader(store, schema);
        int markerSets = schema.SnoGroup(MarkerSetType), actors = schema.SnoGroup(ActorDefinitionType);
        var worldTraversals = new Dictionary<string, IReadOnlyList<TraversalPoint>>();
        return new OverworldPaths(
            worldName =>
            {
                lock (cascGate)
                {
                    if (chunks.TryGetValue(worldName, out var known)) return known;
                    var world = SnoOrNull(store, worlds.Group, worldName);
                    return chunks[worldName] = world is null ? [] : worlds.Read(world.Value).Chunks;
                }
            },
            scene => { lock (cascGate) return scenes.SurfaceVoxels(scene); },
            scene =>
            {
                lock (cascGate)
                    return [.. traversals.In(scene).Select(t => new TraversalPoint(t.Local, store.Name(t.ActorSno) == TraversalSlideName))];
            },
            worldName =>
            {
                lock (cascGate)
                {
                    if (worldTraversals.TryGetValue(worldName, out var known)) return known;
                    return worldTraversals[worldName] = [.. store.EntriesWithPrefix(markerSets, worldName + " (")
                        .SelectMany(set => markers.ReadAll(set.Id))
                        .Where(m => m.Group == actors && store.Name(m.Sno) is { } name
                            && (name.StartsWith(TraversalGizmoPrefix, StringComparison.Ordinal) || name == TraversalExitName))
                        .Select(m => new TraversalPoint(m.Position, store.Name(m.Sno) == TraversalSlideName))];
                }
            });
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
