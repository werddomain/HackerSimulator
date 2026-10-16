using System.Text;
using System.Text.Json;
using HackerOs.App.Abstractions;

namespace HackerOs.App.Abstractions.Tests.Serialization;

public sealed class AppManifestJsonSerializerTests
{
    [Fact]
    public void SerializeCanonical_writes_expected_canonical_fixture()
    {
        AppManifest manifest = CreateManifest();

        string json = AppManifestJsonSerializer.SerializeCanonical(manifest);
        string expected = File.ReadAllText(Path.Combine(FixturesDirectory.Value, "app-manifest.canonical.json"), Encoding.UTF8);

        Assert.Equal(expected.Replace("\r\n", "\n"), json.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Execution_round_trips_and_is_omitted_for_a_manifest_written_before_ADR_0070()
    {
        AppManifest manifest = CreateManifest() with { Execution = AppExecutionHost.Dedicated };

        AppManifest roundTripped = AppManifestJsonSerializer.DeserializeStrict(AppManifestJsonSerializer.SerializeCanonical(manifest));
        string legacy = AppManifestJsonSerializer.SerializeCanonical(manifest with { Execution = null });

        Assert.Equal(AppExecutionHost.Dedicated, roundTripped.Execution);
        Assert.Contains("\"execution\":\"dedicated\"", AppManifestJsonSerializer.SerializeCanonical(manifest), StringComparison.Ordinal);
        Assert.DoesNotContain("execution", legacy, StringComparison.Ordinal);
        Assert.Null(AppManifestJsonSerializer.DeserializeStrict(legacy).Execution);
    }

    [Fact]
    public void Components_round_trip_and_are_omitted_when_none()
    {
        AppManifest manifest = CreateManifest();

        AppManifest roundTripped = AppManifestJsonSerializer.DeserializeStrict(AppManifestJsonSerializer.SerializeCanonical(manifest));
        string withoutComponents = AppManifestJsonSerializer.SerializeCanonical(manifest with { Components = null });

        Assert.Equal(manifest.Components, roundTripped.Components);
        Assert.DoesNotContain("components", withoutComponents, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_urls_round_trip_in_declared_order_and_are_omitted_for_other_kinds()
    {
        AppManifest manifest = CreateManifest() with
        {
            Kind = AppKind.Web,
            Execution = AppExecutionHost.Web,
            FileHandlers = [],
            Components = null,
            Web = new WebAppManifest(["http://intranet.hack:8080/admin/", "http://intranet.hack/"])
        };

        string json = AppManifestJsonSerializer.SerializeCanonical(manifest);
        AppManifest roundTripped = AppManifestJsonSerializer.DeserializeStrict(json);

        Assert.Contains("\"execution\":\"web\"", json, StringComparison.Ordinal);
        Assert.Equal(AppKind.Web, roundTripped.Kind);
        Assert.Equal(["http://intranet.hack:8080/admin/", "http://intranet.hack/"], roundTripped.Web!.Urls);
        Assert.DoesNotContain("\"web\":", AppManifestJsonSerializer.SerializeCanonical(CreateManifest()), StringComparison.Ordinal);
    }

    [Fact]
    public void Web_blazor_round_trips_and_rejects_unknown_properties()
    {
        AppManifest manifest = CreateManifest() with
        {
            Kind = AppKind.Web,
            Execution = AppExecutionHost.Web,
            FileHandlers = [],
            Components = null,
            Web = new WebAppManifest(["http://intranet.hack/"], new WebBlazorManifest("Intranet.dll", "Intranet.App"))
        };

        string json = AppManifestJsonSerializer.SerializeCanonical(manifest);
        AppManifest roundTripped = AppManifestJsonSerializer.DeserializeStrict(json);
        string extra = json.Replace("\"blazor\":{", "\"blazor\":{\"extra\":1,", StringComparison.Ordinal);

        Assert.Contains("\"blazor\":{\"assembly\":\"Intranet.dll\",\"type\":\"Intranet.App\"}", json, StringComparison.Ordinal);
        Assert.Equal(manifest.Web.Blazor, roundTripped.Web!.Blazor);
        Assert.Throws<JsonException>(() => AppManifestJsonSerializer.DeserializeStrict(extra));
    }

    [Fact]
    public void DeserializeStrict_rejects_unknown_component_properties()
    {
        string json = AppManifestJsonSerializer.SerializeCanonical(CreateManifest())
            .Replace("\"contract\":", "\"extra\":1,\"contract\":", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => AppManifestJsonSerializer.DeserializeStrict(json));
    }

    [Fact]
    public void DeserializeStrict_rejects_unknown_properties()
    {
        const string payload = """
        {
          "schemaVersion": 1,
          "id": "org.hackeros.text-editor",
          "name": "Text Editor",
          "version": "1.0.0",
          "publisherId": "org.hackeros",
          "description": "Edits text files.",
          "kind": "window",
          "entryPoint": {
            "assembly": "HackerOs.Apps.TextEditor",
            "type": "HackerOs.Apps.TextEditor.TextEditorApp"
          },
          "sdkCompatibility": {
            "minimumVersion": "1.0.0"
          },
          "presentation": {
            "category": "editors",
            "launchVisibility": "visible",
            "iconAssetPaths": []
          },
          "resources": {
            "baselineCpuWeight": 0,
            "burstCpuWeight": 0,
            "baselineMemoryWeight": 0,
            "burstMemoryWeight": 0,
            "baselineStorageIoWeight": 0,
            "burstStorageIoWeight": 0,
            "baselineNetworkIoWeight": 0,
            "burstNetworkIoWeight": 0
          },
          "unexpected": true
        }
        """;

        Assert.Throws<JsonException>(() => AppManifestJsonSerializer.DeserializeStrict(payload));
    }

    private static AppManifest CreateManifest() => new()
    {
        Id = "org.hackeros.text-editor",
        Name = "Text Editor",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Edits text files in the HackerOS virtual filesystem.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Apps.TextEditor", "HackerOs.Apps.TextEditor.TextEditorApp"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("editors", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Capabilities = ["filesystem.private.read"],
        Dependencies = [new AppDependencyManifest("org.hackeros.shared", "1.0.0", Optional: true)],
        Assets = [
            new AssetManifest("assets/editor.css", AssetKind.Css, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "stylesheet")
        ],
        Localizations = [new LocalizationManifest("fr-FR", "locales/fr-FR.json")],
        Intents = [new AppIntentDeclarationManifest("hackeros.open", "schemas/open.json")],
        Settings = new AppSettingsSchemaManifest(1, [new AppSettingFieldManifest("theme", AppSettingValueType.Enum, "dark", AppSettingScope.AppUser, AppSettingSensitivity.Public, ["dark", "light"])]),
        FileHandlers = [new FileHandlerManifest("text/plain", [".txt"], ["open", "edit"], 3)],
        Components = [new AppComponentManifest("hackeros.text-view", "HackerOs.Apps.TextEditor", "HackerOs.Apps.TextEditor.TextViewEmbed")],
        Execution = AppExecutionHost.MainThread
    };

    private static readonly Lazy<string> FixturesDirectory = new(() =>
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string solution = Path.Combine(directory, "HackerOs.sln");
            if (File.Exists(solution))
            {
                return Path.Combine(directory, "Shared", "HackerOs.App.Abstractions", "Schema", "Fixtures");
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Could not locate HackerOs.sln by walking up from test output directory.");
    });
}
