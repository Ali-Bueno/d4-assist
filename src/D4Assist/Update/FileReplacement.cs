namespace D4Assist.Update;

/// <summary>What a replacement did, by path relative to the app folder.</summary>
public sealed record ReplacementReport(IReadOnlyList<string> Copied, IReadOnlyList<string> Kept);

/// <summary>
/// Puts an extracted release over the app folder. The running exe is renamed to ".old" (Windows allows that), the new
/// files are copied in, and any failure puts everything back. The player's d4assist.json is never overwritten.
/// </summary>
public static class FileReplacement
{
    /// <summary>The exe's name inside the release zip.</summary>
    public const string ReleaseExeName = "D4Assist.exe";

    private const string OldSuffix = ".old";
    private const string WriteProbeName = "d4assist-write-test.tmp";
    private const string ConfigFileName = "d4assist.json";

    /// <summary>The running exe's renamed copy a replacement leaves behind.</summary>
    public static string OldPath(string appFolder, string runningExeName) => Path.Combine(appFolder, runningExeName + OldSuffix);

    /// <summary>Deletes the ".old" exe a previous update left (it can only go once that process has ended); best effort.</summary>
    public static void DeleteLeftover(string appFolder, string runningExeName)
    {
        try { File.Delete(OldPath(appFolder, runningExeName)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Replaces the exe named <paramref name="runningExeName"/> and copies the other files of <paramref name="extractedFolder"/>
    /// (d4assist.json only when the app folder has none). <paramref name="backupFolder"/> holds the files overwritten, for the
    /// rollback. Throws <see cref="UnauthorizedAccessException"/> when the folder is read-only; any failure leaves the app unchanged.
    /// </summary>
    public static ReplacementReport Apply(string appFolder, string extractedFolder, string runningExeName, string backupFolder)
    {
        if (!File.Exists(Path.Combine(extractedFolder, ReleaseExeName)))
            throw new InvalidDataException($"The update has no {ReleaseExeName}.");
        CheckWritable(appFolder);

        var runningExe = Path.Combine(appFolder, runningExeName);
        var oldExe = OldPath(appFolder, runningExeName);
        var created = new List<string>();
        var backups = new List<(string Original, string Backup)>();
        var copied = new List<string>();
        var kept = new List<string>();
        File.Delete(oldExe);
        File.Move(runningExe, oldExe);
        try
        {
            foreach (var source in Directory.EnumerateFiles(extractedFolder, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(extractedFolder, source);
                bool isExe = string.Equals(relative, ReleaseExeName, StringComparison.OrdinalIgnoreCase);
                var target = Path.Combine(appFolder, isExe ? runningExeName : relative);
                if (!isExe && string.Equals(relative, ConfigFileName, StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                {
                    kept.Add(relative);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!isExe && File.Exists(target)) backups.Add((target, Backup(backupFolder, target, appFolder)));
                else created.Add(target);
                File.Copy(source, target, overwrite: true);
                copied.Add(relative);
            }
        }
        catch
        {
            RollBack(runningExe, oldExe, created, backups);
            throw;
        }
        return new ReplacementReport(copied, kept);
    }

    private static string Backup(string backupFolder, string target, string appFolder)
    {
        var backup = Path.Combine(backupFolder, Path.GetRelativePath(appFolder, target));
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Copy(target, backup, overwrite: true);
        return backup;
    }

    // Best effort per file, so one stuck file does not stop the rest from being put back.
    private static void RollBack(string runningExe, string oldExe, List<string> created, List<(string Original, string Backup)> backups)
    {
        foreach (var file in created) Try(() => File.Delete(file));
        foreach (var (original, backup) in backups) Try(() => File.Copy(backup, original, overwrite: true));
        Try(() => File.Move(oldExe, runningExe, overwrite: true));
    }

    private static void Try(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Console.WriteLine($"Update rollback step failed: {e.Message}"); }
    }

    // A read-only folder (Program Files) fails here, before anything is renamed.
    private static void CheckWritable(string appFolder)
    {
        var probe = Path.Combine(appFolder, WriteProbeName);
        File.WriteAllText(probe, "");
        File.Delete(probe);
    }
}
