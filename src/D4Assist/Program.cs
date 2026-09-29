using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Assist.Input;
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

    private static int Main(string[] args)
    {
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
        if (!File.Exists(definitionsPath))
        {
            Console.Error.WriteLine($"{definitionsPath} not found. Set DefinitionsPath in {AppConfig.FileName}. See README.md.");
            return 1;
        }

        var language = new LanguageSetting();
        using var logWatcher = new WorldLogWatcher(installPath);
        var follower = new LanguageFollower(language, config.ForcedLanguage(), debug ? Console.WriteLine : null);
        if (logWatcher.ReadLastTextLocaleAtStartup() is { } startLocale) follower.OnTextLocale(startLocale);
        logWatcher.TextLocaleChanged += follower.OnTextLocale;

        using ISpeechSink speech = noSpeech || !config.SpeechEnabled ? new ConsoleSpeechSink() : new PrismSpeechSink();
        Console.WriteLine($"Speech backend: {speech.BackendName}");
        speech.Speak(AppStrings.For(language.Current).AssistantActive, interrupt: true);

        using var store = new SnoStore(installPath);
        var schema = TypeSchema.Load(definitionsPath);
        var worldReader = new WorldReader(store, schema);
        var classifier = new WorldClassifier(store, worldReader, config.RadarOnlyWorlds);

        var sceneReader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
        var candidates = new CandidateIndex(CandidateSceneBuilder.Build(store, sceneReader, sceneReader.Group, tilesOnly: false));
        var sceneSnos = store.AllEntries(schema.SnoGroup(SceneDefinitionType)).Select(e => e.Id).ToHashSet();

        // CASC reads happen on several poll threads (walkability, actor classes, DPO quests) and the store isn't thread-safe.
        var cascGate = new object();
        // Static-grid (radar-only) worlds get their tiles offline: the live CScene route only matches DRLG scenes.
        var staticTiles = new StaticWorldTiles(
            worldSno => { lock (cascGate) return worldReader.Read(worldSno).Chunks; },
            sceneSno => { lock (cascGate) return sceneReader.Read(sceneSno).TileSize; },
            store.Name);

        using var session = new GameSession(
            logWatcher, classifier,
            (worldSno, knownSceneSnos) => LiveDungeonSessionReader.TryOpen(installPath, candidates, worldSno, knownSceneSnos),
            sceneSnos, staticTiles: staticTiles.Build);

        var walkability = new LockedTileWalkability(new CascTileWalkability(sceneReader), cascGate);
        var navigation = new NavigationBridge(session, speech, config.AutoAnnouncementsEnabled, diagnostics: debug, walkability, language);

        // One mixer and one output for the radar cues and the beacon loops.
        var mixer = new AudioMixer();
        using var radarPlayer = config.RadarEnabled
            ? new RadarCuePlayer(new WaveOutAudioOut(), mixer) { Frame = config.RadarFrame, Volume = config.RadarVolume }
            : null;
        if (radarPlayer is not null)
        {
            var radarSettings = new RadarSettings { Frame = config.RadarFrame, PollMs = GameSession.DefaultPlayerPollIntervalMs };
            _ = new RadarBridge(session, new DungeonRadarWorld(walkability), radarPlayer, radarSettings, diagnostics: debug);
        }

        bool needsOwnOutput = (config.BeaconSoundsEnabled || config.ObjectiveSoundsEnabled || config.ExplorationGuideEnabled) && radarPlayer is null;
        using IAudioOut? beaconOut = needsOwnOutput ? new WaveOutAudioOut() : null;
        beaconOut?.Start(mixer.Render);
        var loops = config.BeaconSoundsEnabled || config.ObjectiveSoundsEnabled ? StartLoops(session, mixer, config, store, schema, cascGate, debug) : null;

        // Created after the NavigationBridge so each poll marks the tile visited before the guide reads it.
        using var guidePing = config.ExplorationGuideEnabled
            ? new GuidePingPlayer(mixer) { Volume = config.ExplorationGuideVolume }
            : null;
        using var pad = guidePing is not null ? new XInputPad() : null;
        if (guidePing is not null)
            _ = new ExplorationGuideBridge(
                session, navigation.NextGuideStep, guidePing, speech, () => loops?.HasTarget(LoopKind.ExitBeacon) ?? false, debug ? Console.WriteLine : null,
                pad, config.ExplorationGuideInDungeons, language);
        pad?.Start();

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

    private static ActorLoopBridge? StartLoops(
        GameSession session, AudioMixer mixer, AppConfig config, SnoStore store, TypeSchema schema, object cascGate, bool debug)
    {
        Dictionary<string, float[]> sounds;
        try { sounds = SpatialLoopPlayer.LoadSounds(Path.Combine(AppContext.BaseDirectory, "Sounds")); }
        catch (IOException e) { Console.WriteLine($"Loop sounds disabled, sounds not loaded: {e.Message}"); return null; }

        int actorGroup = schema.SnoGroup(ActorDefinitionType);
        var beaconSnos = config.BeaconSoundsEnabled
            ? BeaconTargets.Resolve(
                config.BeaconActorNames, name => store.Resolve(actorGroup, name),
                name => Console.WriteLine($"Beacon actor '{name}' is not in CoreTOC; ignored."))
            : new HashSet<int>();

        var dpo = new DungeonObjectiveReader(store, schema);
        var actorDefinitions = new ActorDefinitionReader(store, schema);
        var objectives = config.ObjectiveSoundsEnabled
            ? new ObjectiveClassifier(sno => { lock (cascGate) return actorDefinitions.Read(sno); }, store.Name)
            : null;
        var selector = new LoopTargetSelector(
            beaconSnos, config.BeaconVolume, objectives, config.ObjectiveVolume, world => { lock (cascGate) return dpo.ActorSnos(world); });
        if (session.CurrentWorld is { } start) selector.SetWorld(WorldSnoOrNone(store, schema, start.Name)); // app started mid-dungeon

        var player = new SpatialLoopPlayer(mixer, sounds);
        return new ActorLoopBridge(session, player, selector, store.Name, log: debug ? Console.WriteLine : null);
    }

    private static int WorldSnoOrNone(SnoStore store, TypeSchema schema, string worldName)
    {
        try { return store.Resolve(schema.SnoGroup(WorldDefinitionType), worldName); }
        catch (ArgumentException) { return GameSession.NoWorldSno; }
    }
}
