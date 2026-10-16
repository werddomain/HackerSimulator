using HackerOs.App.Abstractions;

namespace HackerOs.Tools.ManifestGenerator.Tests;

/// <summary>Aggregation rules of ADR 0047 §3, on manifests written to a temporary directory.</summary>
public sealed class BuiltInApplicationsAggregatorTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("hackeros-aggregate-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string WriteManifest(Type definition, string fileName)
    {
        GeneratedManifest generated = Assert.Single(
            AppDefinitionInspector.Inspect(typeof(ValidCommandApp).Assembly, [definition], "1.0.0").Manifests);
        string path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, generated.CanonicalJson);
        return path;
    }

    [Fact]
    public void IsSystemApp_is_carried_per_reference()
    {
        AggregationResult result = BuiltInApplicationsAggregator.Aggregate(
        [
            new(WriteManifest(typeof(ValidCommandApp), "valid.json"), IsSystemApp: true),
            new(WriteManifest(typeof(SecondCommandApp), "second.json"), IsSystemApp: false)
        ]);

        Assert.Empty(result.Diagnostics);
        IReadOnlyList<BuiltInApplication> applications = BuiltInApplicationsJsonSerializer.DeserializeStrict(result.Json!);
        Assert.Equal(
            [("org.hackeros.tests.second", false), ("org.hackeros.tests.valid", true)],
            applications.Select(item => (item.Manifest.Id, item.IsSystemApp)));
    }

    [Fact]
    public void Duplicate_id_across_projects_fails_with_HOSAPP0021()
    {
        AggregationResult result = BuiltInApplicationsAggregator.Aggregate(
        [
            new(WriteManifest(typeof(ValidCommandApp), "one.json"), false),
            new(WriteManifest(typeof(DuplicateIdApp), "two.json"), false)
        ]);

        Assert.Null(result.Json);
        GeneratorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorCodes.InvalidCatalog, diagnostic.Code);
        Assert.Contains("catalog.app-id.duplicate", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_or_malformed_manifest_fails_with_HOSAPP0020()
    {
        string malformed = Path.Combine(_directory, "bad.json");
        File.WriteAllText(malformed, """{"schemaVersion":1,"unknown":true}""");

        AggregationResult result = BuiltInApplicationsAggregator.Aggregate(
        [
            new(Path.Combine(_directory, "missing.json"), false),
            new(malformed, false)
        ]);

        Assert.Null(result.Json);
        Assert.Equal([GeneratorCodes.UnreadableManifest, GeneratorCodes.UnreadableManifest], result.Diagnostics.Select(item => item.Code));
    }

    [Fact]
    public void No_app_gives_an_empty_document()
    {
        AggregationResult result = BuiltInApplicationsAggregator.Aggregate([]);

        Assert.Equal("{\"schemaVersion\":1,\"applications\":[]}\n", result.Json);
    }

    [Fact]
    public void List_parsing_defaults_to_non_system_and_merges_duplicates()
    {
        string a = Path.Combine(_directory, "a.json");
        string b = Path.Combine(_directory, "b.json");

        IReadOnlyList<AggregationInput> inputs = BuiltInApplicationsAggregator.ParseList(
            [$"|{b}", $"true|{a}", $"false|{a}", "", $"False|{b}"]);

        Assert.Equal([new AggregationInput(a, true), new AggregationInput(b, false)], inputs);
    }
}
