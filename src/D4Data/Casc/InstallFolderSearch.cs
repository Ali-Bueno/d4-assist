namespace D4Data.Casc;

/// <summary>Finds a "Diablo IV" folder near the top of the local drives, for installs no launcher entry points to.</summary>
public static class InstallFolderSearch
{
    /// <summary>The folder name Battle.net and Steam give the install.</summary>
    public const string FolderName = "Diablo IV";

    /// <summary>How many folders deep it looks: D:\Games\Blizzard\Diablo IV is 3.</summary>
    public const int MaxDepth = 3;

    private static readonly EnumerationOptions SubfolderOptions = new()
    {
        IgnoreInaccessible = true,
        // Hidden/system folders ($Recycle.Bin, AppData) never hold the game; reparse points can loop.
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
    };

    /// <summary>The shallowest "Diablo IV" folder on a local drive that <paramref name="isInstall"/> accepts; null when none is.</summary>
    public static string? Locate(Func<string, bool> isInstall) => Candidates(LocalDrives()).FirstOrDefault(isInstall);

    /// <summary><paramref name="folder"/> when it is an install, else the shallowest install under it (a parent picked by the player).</summary>
    public static string? AtOrUnder(string folder, Func<string, bool> isInstall) =>
        isInstall(folder) ? folder : Candidates([folder]).FirstOrDefault(isInstall);

    /// <summary>Every "Diablo IV" folder under <paramref name="roots"/>, shallowest first, at most <see cref="MaxDepth"/> deep, skipping Windows.</summary>
    public static IEnumerable<string> Candidates(IEnumerable<string> roots)
    {
        var level = roots.ToList();
        for (int depth = 0; depth < MaxDepth && level.Count > 0; depth++)
        {
            var next = new List<string>();
            foreach (var folder in level)
                foreach (var child in Subfolders(folder))
                {
                    if (Path.GetFileName(child).Equals(FolderName, StringComparison.OrdinalIgnoreCase)) yield return child;
                    else if (!IsWindowsFolder(child)) next.Add(child);
                }
            level = next;
        }
    }

    private static IEnumerable<string> LocalDrives() =>
        DriveInfo.GetDrives().Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable && d.IsReady).Select(d => d.RootDirectory.FullName);

    private static string[] Subfolders(string folder)
    {
        try { return Directory.GetDirectories(folder, "*", SubfolderOptions); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    private static bool IsWindowsFolder(string folder) =>
        string.Equals(Path.TrimEndingDirectorySeparator(folder), Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
            StringComparison.OrdinalIgnoreCase);
}
