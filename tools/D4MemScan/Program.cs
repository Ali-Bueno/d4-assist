using D4MemScan.Commands;

namespace D4MemScan;

/// <summary>Read-only diagnostic memory scanner for locating a revealed dungeon layout and the player's pose.</summary>
/// <remarks>Never opens the game process for anything but bounded, read-only memory queries — see
/// D4Memory.Safety.ProcessReader for the exact safety policy. This tool is build/test only; it is not run
/// against the game as part of building it.</remarks>
internal static class Program
{
    private const string Usage =
        "usage: D4MemScan world [--install <dir>] --defs <definitions.json>\n" +
        "       D4MemScan scenes --defs <definitions.json> [--install <dir>] [--tiles-only] [--focus <name-regex>] [--replay <capture.focus.json>] [--dump-module <file.focus.json>] [--out <file.json>]\n" +
        "       D4MemScan scene-route --defs <definitions.json> [--install <dir>] [--compare]\n" +
        "       D4MemScan player --defs <definitions.json> [--install <dir>] [--samples N] [--interval-ms M] [--dump-module <file.focus.json>] [--replay-module <file.focus.json>]\n" +
        "       D4MemScan pose-snapshot --scenes <report.json> --defs <definitions.json> [--install <dir>] [--out <file.json>]\n" +
        "       D4MemScan pose-filter --in <file.json> --mode changed|unchanged [--out <file.json>]\n" +
        "       D4MemScan pose-context --in <file.json> [--out <file.focus.json>]";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) throw new ArgumentException("Missing command.");
            var rest = args[1..];
            int code = args[0] switch
            {
                "world" => WorldCommand.Run(rest),
                "scenes" => ScenesCommand.Run(rest),
                "scene-route" => SceneRouteCommand.Run(rest),
                "player" => PlayerCommand.Run(rest),
                "pose-snapshot" => PoseSnapshotCommand.Run(rest),
                "pose-filter" => PoseFilterCommand.Run(rest),
                "pose-context" => PoseContextCommand.Run(rest),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'."),
            };
            return code;
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(Usage);
            return 1;
        }
    }
}
