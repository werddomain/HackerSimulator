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
        FileHandlers = [new FileHandlerManifest("text/plain", [".txt"], ["open", "edit"], 3)]
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
