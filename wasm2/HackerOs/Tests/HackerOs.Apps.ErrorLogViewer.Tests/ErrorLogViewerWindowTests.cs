using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Apps.ErrorLogViewer.Tests;

public sealed class ErrorLogViewerWindowTests
{
    [Fact]
    public void Manifest_HasWindowKind_AndReadsLogsThroughFileRights()
    {
        AppManifest manifest = ErrorLogViewerWindow.StaticManifest;
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.True(manifest.SingleInstancePerUser);
        Assert.Contains(AppCapabilities.FileSystemSystemRead, manifest.Capabilities);
        Assert.Contains(AppCapabilities.FileSystemSystemWrite, manifest.Capabilities);
        Assert.DoesNotContain(AppCapabilities.DiagnosticsRead, manifest.Capabilities);
    }

    [Fact]
    public void StaticManifest_MatchesAppManifestJson()
    {
        string json = File.ReadAllText(FindManifestPath());
        AppManifest fromJson = AppManifestJsonSerializer.DeserializeStrict(json);
        Assert.Equal(ErrorLogViewerWindow.StaticManifest.Capabilities, fromJson.Capabilities);
        Assert.Equal(ErrorLogViewerWindow.StaticManifest.Id, fromJson.Id);
    }

    private static string FindManifestPath()
    {
        for (DirectoryInfo? directory = new(System.AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "Apps", "System", "HackerOs.Apps.ErrorLogViewer", "app.manifest.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("app.manifest.json of Error Log Viewer not found above the test output.");
    }
}
