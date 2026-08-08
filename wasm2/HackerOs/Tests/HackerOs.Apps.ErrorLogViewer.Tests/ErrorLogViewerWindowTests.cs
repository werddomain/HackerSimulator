using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Apps.ErrorLogViewer.Tests;

public sealed class ErrorLogViewerWindowTests
{
    [Fact]
    public void Manifest_HasWindowKind_AndRequiredCapabilities()
    {
        AppManifest manifest = ErrorLogViewerWindow.StaticManifest;
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.True(manifest.SingleInstancePerUser);
        Assert.Contains(AppCapabilities.DiagnosticsRead, manifest.Capabilities);
    }
}
