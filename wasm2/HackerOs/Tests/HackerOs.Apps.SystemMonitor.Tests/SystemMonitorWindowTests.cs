using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Apps.SystemMonitor.Tests;

public sealed class SystemMonitorWindowTests
{
    [Fact]
    public void Manifest_HasWindowKind_AndProcessCapabilities()
    {
        AppManifest manifest = SystemMonitorWindow.StaticManifest;
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.True(manifest.SingleInstancePerUser);
        Assert.Contains(AppCapabilities.ProcessList, manifest.Capabilities);
        Assert.Contains(AppCapabilities.ProcessManage, manifest.Capabilities);
        Assert.Contains(AppCapabilities.ServicesManage, manifest.Capabilities);
    }
}
