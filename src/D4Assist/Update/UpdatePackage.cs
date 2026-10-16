using System.IO.Compression;
using System.Security.Cryptography;

namespace D4Assist.Update;

/// <summary>Downloads a release zip to a temp folder, checks its SHA-256 and unpacks it.</summary>
public static class UpdatePackage
{
    private const string WorkFolder = "D4AssistUpdate";
    private const string ZipFileName = "D4Assist.zip";
    private const string ExtractedFolder = "files";
    private const string BackupFolder = "backup";

    /// <summary>The temp folder an update of <paramref name="version"/> works in.</summary>
    public static string WorkPath(Version version) => Path.Combine(Path.GetTempPath(), WorkFolder, version.ToString(3));

    /// <summary>Where <see cref="FileReplacement"/> keeps the files it overwrites, inside <paramref name="version"/>'s work folder.</summary>
    public static string BackupPath(Version version) => Path.Combine(WorkPath(version), BackupFolder);

    /// <summary>Downloads, verifies and extracts <paramref name="release"/>; returns the folder holding the new files.</summary>
    public static async Task<string> PrepareAsync(ReleaseInfo release, ReleaseClient client, Action<int>? percentDone = null)
    {
        // GitHub publishes a SHA-256 for every asset (every release from 0.1.0 on has one): a zip without it is not installed
        // unchecked, nor even downloaded.
        if (release.Sha256 is not { } sha256)
            throw new InvalidDataException($"The release {release.Version.ToString(3)} publishes no SHA-256 for {ReleaseInfo.ZipAssetName}.");
        var work = WorkPath(release.Version);
        if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
        Directory.CreateDirectory(work);
        var zip = Path.Combine(work, ZipFileName);
        await client.DownloadAsync(release.ZipUrl, zip, percentDone);
        if (!Sha256Matches(zip, sha256))
            throw new InvalidDataException("The downloaded update does not match its published SHA-256.");
        var files = Path.Combine(work, ExtractedFolder);
        ZipFile.ExtractToDirectory(zip, files);
        return files;
    }

    /// <summary>Removes every update's work folder (zip, unpacked files, backups: ~100 MB each) under <paramref name="tempPath"/>.
    /// Called at start, before any check: an update finishes or rolls back before the app restarts, so nothing there is
    /// still needed. Never throws; returns what it did for the log, or null when there was nothing.</summary>
    public static string? DeleteWorkFolders(string tempPath)
    {
        var root = Path.Combine(tempPath, WorkFolder);
        if (!Directory.Exists(root)) return null;
        try
        {
            Directory.Delete(root, recursive: true);
            return $"Update: removed the temporary files in {root}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Update: could not remove {root}: {e.Message}";
        }
    }

    /// <summary>True when the file's SHA-256 equals <paramref name="expectedHex"/> (any case).</summary>
    public static bool Sha256Matches(string path, string expectedHex)
    {
        using var stream = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
