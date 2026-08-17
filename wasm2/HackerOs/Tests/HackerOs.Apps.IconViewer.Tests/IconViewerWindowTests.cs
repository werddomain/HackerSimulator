using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Apps.IconViewer.Tests;

public sealed class IconViewerWindowTests
{
    [Fact]
    public void Manifest_HasWindowKind_AndNoCapabilities()
    {
        AppManifest manifest = IconViewerWindow.StaticManifest;
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.Equal("org.hackeros.icon-viewer", manifest.Id);
        Assert.Empty(manifest.Capabilities);
    }

    [Fact]
    public void Manifest_PassesValidation()
    {
        ManifestValidationResult result = AppManifestValidator.Validate(IconViewerWindow.StaticManifest);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }
}
