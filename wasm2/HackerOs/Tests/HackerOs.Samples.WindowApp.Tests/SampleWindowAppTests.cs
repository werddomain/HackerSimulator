using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Samples.WindowApp.Tests;

public sealed class SampleWindowAppTests
{
    [Fact]
    public void Manifest_HasWindowKindAndSingleInstance()
    {
        AppManifest manifest = SampleWindow.StaticManifest;
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.True(manifest.SingleInstancePerUser);
        Assert.Contains(AppCapabilities.DialogFileOpen, manifest.Capabilities);
        Assert.Contains(AppCapabilities.DialogFileSave, manifest.Capabilities);
    }
}
