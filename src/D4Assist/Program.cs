using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
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

    private static int Main(string[] args)
    {
        if (args.Contains("--sound-test"))
            return SoundTestMode.Run(args.Contains("--no-speech"));

        bool noSpeech = args.Contains("--no-speech");
        bool debug = args.Contains("--debug");

        var config = AppConfig.Load(Path.Combine(AppContext.BaseDirectory, AppConfig.FileName));
        if (string.IsNullOrWhiteSpace(config.DefinitionsPath))
        {
            Console.Error.WriteLine($"{AppConfig.FileName}: DefinitionsPath is not set. See README.md.");
            return 1;
        }

        using ISpeechSink speech = noSpeech || !config.SpeechEnabled ? new ConsoleSpeechSink() : new PrismSpeechSink();
        Console.WriteLine($"Speech backend: {speech.BackendName}");
        speech.Speak(SpanishStrings.AssistantActive, interrupt: true);

        using var store = new SnoStore(config.InstallPath);
        var schema = TypeSchema.Load(config.DefinitionsPath);
        var classifier = new WorldClassifier(store, new WorldReader(store, schema));

        var sceneReader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
        var candidates = new CandidateIndex(CandidateSceneBuilder.Build(store, sceneReader, sceneReader.Group, tilesOnly: false));
        var sceneSnos = store.AllEntries(schema.SnoGroup(SceneDefinitionType)).Select(e => e.Id).ToHashSet();

        using var logWatcher = new WorldLogWatcher(config.InstallPath);
        using var session = new GameSession(
            logWatcher, classifier,
            (worldSno, knownSceneSnos) => LiveDungeonSessionReader.TryOpen(config.InstallPath, candidates, worldSno, knownSceneSnos),
            sceneSnos);

        // One lookup for both bridges; they run on different poll threads.
        var walkability = new LockedTileWalkability(new CascTileWalkability(sceneReader));
        _ = new NavigationBridge(session, speech, config.AutoAnnouncementsEnabled, diagnostics: debug, walkability);

        using var radarPlayer = config.RadarEnabled
            ? new RadarCuePlayer(new WaveOutAudioOut()) { Frame = config.RadarFrame, Volume = config.RadarVolume }
            : null;
        if (radarPlayer is not null)
        {
            var radarSettings = new RadarSettings { Frame = config.RadarFrame, PollMs = GameSession.DefaultPlayerPollIntervalMs };
            _ = new RadarBridge(session, new DungeonRadarWorld(walkability), radarPlayer, radarSettings, diagnostics: debug);
        }

        session.WorldChanged += (sno, name, isDungeon) =>
        {
            Console.WriteLine($"World: {name} (SNO {sno}), dungeon={isDungeon}, state={session.State}.");
            if (config.AutoAnnouncementsEnabled)
                speech.Speak(isDungeon ? SpanishStrings.EnteringDungeon : SpanishStrings.LeavingDungeon, config.InterruptOnContextChange);
        };

        if (session.State == SessionState.WaitingForGame)
        {
            Console.WriteLine("No world seen yet in FenrisDebug.txt.");
            speech.Speak(SpanishStrings.WaitingForGame, interrupt: false);
        }

        Console.WriteLine("D4 Dungeon Assist running. Press Ctrl+C to exit.");
        using var exit = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
        exit.Wait();
        return 0;
    }
}
