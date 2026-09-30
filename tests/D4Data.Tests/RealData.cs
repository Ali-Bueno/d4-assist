namespace D4Data.Tests;

/// <summary>Locates the repo's definitions.json for the tests that read the local Diablo IV install.</summary>
internal static class RealData
{
    private const string DefinitionsRelativePath = "data/definitions.json";

    public static string? FindDefinitions([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, DefinitionsRelativePath);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
