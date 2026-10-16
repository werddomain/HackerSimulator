using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Apps.Settings.Tests;

public sealed class SettingsWindowTests
{
    [Fact]
    public void Manifest_HasWindowKind_AndRequiredCapabilities()
    {
        AppManifest manifest = AppDefinitionMapper.ToManifest(new SettingsApp());
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.True(manifest.SingleInstancePerUser);
        Assert.Contains(AppCapabilities.FileSystemSystemRead, manifest.Capabilities);
        Assert.Contains(AppCapabilities.FileSystemSystemWrite, manifest.Capabilities);
        Assert.DoesNotContain(manifest.Capabilities, capability => capability.StartsWith("settings.", StringComparison.Ordinal));
        Assert.Contains(AppCapabilities.FileAssociationsRead, manifest.Capabilities);
        Assert.Contains(AppCapabilities.FileAssociationsWrite, manifest.Capabilities);
    }
}
