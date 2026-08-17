using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;

namespace HackerOs.Platform.Core.Tests;

public sealed class AppCatalogTests
{
    [Fact]
    public void Build_orders_dependencies_deterministically_and_deactivation_in_reverse()
    {
        AppManifest shell = CreateManifest(
            "org.hackeros.shell",
            dependencies: [new AppDependencyManifest("org.hackeros.kernel", "1.0.0")]);
        AppManifest files = CreateManifest("org.hackeros.files");
        AppManifest kernel = CreateManifest("org.hackeros.kernel");

        AppCatalogBuildResult result = AppCatalog.Build([shell, files, kernel]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["org.hackeros.files", "org.hackeros.kernel", "org.hackeros.shell"],
            result.Catalog!.ActivationOrder);
        Assert.Equal(result.Catalog.ActivationOrder.Reverse(), result.Catalog.DeactivationOrder);
    }

    [Fact]
    public void Build_rejects_missing_required_dependency()
    {
        AppManifest app = CreateManifest(
            "org.hackeros.editor",
            dependencies: [new AppDependencyManifest("org.hackeros.files", "1.0.0")]);

        AppCatalogBuildResult result = AppCatalog.Build([app]);

        Assert.Contains(result.Errors, error => error.Code == "catalog.dependency.missing");
    }

    [Fact]
    public void Build_allows_missing_optional_dependency()
    {
        AppManifest app = CreateManifest(
            "org.hackeros.editor",
            dependencies: [new AppDependencyManifest("org.hackeros.preview", "1.0.0", Optional: true)]);

        AppCatalogBuildResult result = AppCatalog.Build([app]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Build_rejects_incompatible_dependency_prerelease()
    {
        AppManifest dependency = CreateManifest("org.hackeros.files", "2.0.0-beta.1");
        AppManifest app = CreateManifest(
            "org.hackeros.editor",
            dependencies: [new AppDependencyManifest("org.hackeros.files", "2.0.0")]);

        AppCatalogBuildResult result = AppCatalog.Build([dependency, app]);

        Assert.Contains(result.Errors, error => error.Code == "catalog.dependency.incompatible");
    }

    [Fact]
    public void Build_rejects_dependency_cycles()
    {
        AppManifest first = CreateManifest(
            "org.hackeros.first",
            dependencies: [new AppDependencyManifest("org.hackeros.second", "1.0.0")]);
        AppManifest second = CreateManifest(
            "org.hackeros.second",
            dependencies: [new AppDependencyManifest("org.hackeros.first", "1.0.0")]);

        AppCatalogBuildResult result = AppCatalog.Build([first, second]);

        Assert.Contains(result.Errors, error => error.Code == "catalog.dependency.cycle");
    }

    [Fact]
    public void Build_rejects_duplicate_app_ids()
    {
        AppManifest first = CreateManifest("org.hackeros.files");
        AppManifest second = CreateManifest("org.hackeros.files");

        AppCatalogBuildResult result = AppCatalog.Build([first, second]);

        Assert.Contains(result.Errors, error => error.Code == "catalog.app-id.duplicate");
    }

    private static AppManifest CreateManifest(
        string appId,
        string version = "1.0.0",
        IReadOnlyList<AppDependencyManifest>? dependencies = null) => new()
    {
        Id = appId,
        Name = appId,
        Version = version,
        PublisherId = "org.hackeros",
        Description = $"Test manifest for {appId}.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.TestApps", $"TestApps.{appId.Replace('.', '_')}"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Dependencies = dependencies ?? []
    };
}