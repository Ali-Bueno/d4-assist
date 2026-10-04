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
        var work = WorkPath(release.Version);
        if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
        Directory.CreateDirectory(work);
        var zip = Path.Combine(work, ZipFileName);
        await client.DownloadAsync(release.ZipUrl, zip, percentDone);
        if (release.Sha256 is not null && !Sha256Matches(zip, release.Sha256))
            throw new InvalidDataException("The downloaded update does not match its published SHA-256.");
        var files = Path.Combine(work, ExtractedFolder);
        ZipFile.ExtractToDirectory(zip, files);
        return files;
    }

    /// <summary>True when the file's SHA-256 equals <paramref name="expectedHex"/> (any case).</summary>
    public static bool Sha256Matches(string path, string expectedHex)
    {
        using var stream = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
