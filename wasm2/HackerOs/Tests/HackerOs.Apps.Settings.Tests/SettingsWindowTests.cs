using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Apps.Settings.Tests;

public sealed class SettingsWindowTests
{
    [Fact]
    public void Manifest_HasWindowKind_AndRequiredCapabilities()
    {
        AppManifest manifest = SettingsWindow.StaticManifest;
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.True(manifest.SingleInstancePerUser);
        Assert.Contains(AppCapabilities.SettingsRead, manifest.Capabilities);
        Assert.Contains(AppCapabilities.SettingsWrite, manifest.Capabilities);
        Assert.Contains(AppCapabilities.FileAssociationsRead, manifest.Capabilities);
        Assert.Contains(AppCapabilities.FileAssociationsWrite, manifest.Capabilities);
    }
}
