using HackerOs.App.Abstractions;

namespace HackerOs.Tools.ManifestGenerator.Tests;

/// <summary>
/// Migration test of ADR 0047 §6: for every app under <c>Apps/</c> that still has its hand-written
/// <c>app.manifest.json</c>, the <c>manifest.json</c> generated from its <c>[AppManifest]</c> class
/// must be semantically equal (same canonical JSON once optional collections are normalized).
/// The legacy files and this test are deleted together once the comparison is green.
/// </summary>
public sealed class LegacyManifestMigrationTests
{
    public static TheoryData<string> LegacyManifests()
    {
        TheoryData<string> data = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(FindSolutionDirectory(), "Apps"), "app.manifest.json", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(FindSolutionDirectory(), Path.GetDirectoryName(path)!));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LegacyManifests))]
    public void Generated_manifest_matches_legacy_app_manifest_json(string projectDirectory)
    {
        string directory = Path.Combine(FindSolutionDirectory(), projectDirectory);
        string generatedPath = Path.Combine(directory, "manifest.json");
        Assert.True(File.Exists(generatedPath), $"{projectDirectory} has no generated manifest.json (missing [AppManifest] class?).");

        Assert.Equal(Canonical(Path.Combine(directory, "app.manifest.json")), Canonical(generatedPath));
    }

    private static string Canonical(string path) =>
        AppManifestJsonSerializer.SerializeCanonical(
            AppManifestJsonSerializer.NormalizeOptionalCollections(
                AppManifestJsonSerializer.DeserializeStrict(File.ReadAllText(path))));

    private static string FindSolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HackerOs.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate HackerOs.sln.");
    }
}
