using System.Text.Json.Nodes;
using Json.Schema;

namespace HackerOs.App.Abstractions.Tests.Schema;

/// <summary>
/// Verifies the published manifest JSON Schema (Draft 2020-12) accepts every valid fixture
/// (one per <see cref="AppKind"/>) and rejects every invalid fixture with a schema-level violation.
/// These tests exercise the schema document itself, independent of <see cref="AppManifestValidator"/>,
/// per ADR 0010's validation order: JSON structure/schema comes before semantic validation.
/// </summary>
public sealed class ManifestSchemaConformanceTests
{
    private static readonly Json.Schema.JsonSchema Schema = Json.Schema.JsonSchema.FromText(
        ManifestSchemaResource.LoadCurrentSchemaJson());

    public static TheoryData<string> ValidFixtureNames => new()
    {
        "window.valid.json",
        "terminal.valid.json",
        "service.valid.json",
        "window-multi-platform.valid.json"
    };

    public static TheoryData<string> InvalidFixtureNames => new()
    {
        "invalid-schema-version.json",
        "invalid-app-id.json",
        "invalid-missing-resources.json",
        "invalid-unknown-property.json",
        "invalid-terminal-missing-terminal-block.json",
        "invalid-filehandlers-on-service.json",
        "invalid-asset-hash.json",
        "invalid-resource-weight-out-of-range.json",
        "invalid-settings-enum-missing-allowed-values.json",
        "invalid-platform-ambiguous.json"
    };

    [Theory]
    [MemberData(nameof(ValidFixtureNames))]
    public void Schema_accepts_every_valid_fixture(string fixtureName)
    {
        JsonNode? instance = LoadFixture(fixtureName);

        EvaluationResults result = Schema.Evaluate(instance, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        Assert.True(result.IsValid, DescribeFailure(fixtureName, result));
    }

    [Theory]
    [MemberData(nameof(InvalidFixtureNames))]
    public void Schema_rejects_every_invalid_fixture(string fixtureName)
    {
        JsonNode? instance = LoadFixture(fixtureName);

        EvaluationResults result = Schema.Evaluate(instance, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        Assert.False(result.IsValid, $"Expected fixture '{fixtureName}' to be rejected by the schema.");
    }

    private static JsonNode? LoadFixture(string fixtureName)
    {
        string path = Path.Combine(FixturesDirectory.Value, fixtureName);
        string json = File.ReadAllText(path);
        return JsonNode.Parse(json);
    }

    private static string DescribeFailure(string fixtureName, EvaluationResults result)
    {
        IEnumerable<string> errors = result.Details
            .Where(detail => detail.HasErrors)
            .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation}: {error.Value}"));
        return $"Fixture '{fixtureName}' failed schema validation: {string.Join("; ", errors)}";
    }

    private static readonly Lazy<string> FixturesDirectory = new(() =>
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, "HackerOs.sln");
            if (File.Exists(candidate))
            {
                return Path.Combine(directory, "Shared", "HackerOs.App.Abstractions", "Schema", "Fixtures");
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Could not locate HackerOs.sln by walking up from the test output directory.");
    });
}
