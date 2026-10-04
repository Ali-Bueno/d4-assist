using System.Reflection;

namespace D4Assist.Update;

/// <summary>Version numbers as the updater compares them: a release tag ("v0.1.7") against this build's own version.</summary>
public static class UpdateVersion
{
    /// <summary>The number in a tag or informational version ("v0.1.7", "0.1.6+abc123"); false for anything else.</summary>
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(text)) return false;
        var number = text.Trim().TrimStart('v', 'V');
        int suffix = number.IndexOf('+');
        if (suffix >= 0) number = number[..suffix];
        if (!Version.TryParse(number, out var parsed)) return false;
        // Version("0.1.6") and Version("0.1.6.0") compare unequal otherwise.
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }

    /// <summary>This build's version: the numeric part of its informational version (the SDK appends +commit).</summary>
    public static Version Current { get; } = TryParse(
        typeof(UpdateVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out var version)
        ? version : new Version();
}
