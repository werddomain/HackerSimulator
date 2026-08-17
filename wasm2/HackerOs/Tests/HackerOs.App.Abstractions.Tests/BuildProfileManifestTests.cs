using HackerOs.App.Abstractions;

namespace HackerOs.App.Abstractions.Tests;

public sealed class BuildProfileManifestTests
{
    [Fact]
    public void SerializeCanonical_emits_deterministic_lower_camel_json()
    {
        BuildProfileManifest manifest = new()
        {
            Id = "core",
            Name = "Core",
            Packages =
            [
                new BuildProfilePackageManifest(
                    "org.hackeros.shell",
                    "HackerOs.Apps.Shell",
                    BuildProfileLoadMode.Lazy,
                    IsBootCritical: false,
                    EnabledByDefault: true)
            ],
            DefaultEnabledAppIds = ["org.hackeros.shell"],
            RequiredGrants = [new BuildProfileGrantManifest("filesystem.private.read", "org.hackeros.shell")],
            Associations = [new BuildProfileAssociationManifest(".txt", "text/plain", "open", "org.hackeros.text-editor")],
            Locales = ["en-US"],
            Themes = ["default"],
            OptionalServerFeatures = ["proxy"]
        };

        string json = BuildProfileJsonSerializer.SerializeCanonical(manifest);

        Assert.Contains("\"defaultEnabledAppIds\"", json);
        Assert.Contains("\"requiredGrants\"", json);
        Assert.Contains("\"loadMode\":\"lazy\"", json);
        Assert.EndsWith("\n", json);
    }

    [Fact]
    public void Validate_rejects_duplicate_packages_and_unknown_load_modes()
    {
        BuildProfileManifest manifest = new()
        {
            Id = "core",
            Name = "Core",
            Packages =
            [
                new BuildProfilePackageManifest("org.hackeros.shell", "HackerOs.Apps.Shell", BuildProfileLoadMode.Eager),
                new BuildProfilePackageManifest("org.hackeros.shell", "HackerOs.Apps.Shell", BuildProfileLoadMode.Eager)
            ]
        };

        BuildProfileValidationResult result = BuildProfileValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "buildProfile.package.duplicate");
    }

    [Fact]
    public void Validate_with_catalog_resolves_references_and_limits_publish_assets_to_included_apps()
    {
        BuildProfileManifest manifest = new()
        {
            Id = "core",
            Name = "Core",
            Packages = [new BuildProfilePackageManifest("org.hackeros.shell", "HackerOs.Apps.Shell", BuildProfileLoadMode.Eager)],
            DefaultEnabledAppIds = ["org.hackeros.text-editor"],
            RequiredGrants = [new BuildProfileGrantManifest("filesystem.private.read", "org.hackeros.ghost")],
            Associations = [new BuildProfileAssociationManifest(".txt", "text/plain", "open", "org.hackeros.text-editor")]
        };

        AppManifest shell = CreateAppManifest("org.hackeros.shell", "assets/shell.png");
        AppManifest editor = CreateAppManifest("org.hackeros.text-editor", "assets/editor.png");

        BuildProfileValidationResult result = BuildProfileValidator.Validate(manifest, [shell, editor]);

        Assert.Contains(result.Errors, error => error.Code == "buildProfile.reference.unresolved" && error.Path == "buildProfile.requiredGrants.appId");
        Assert.Equal(["org.hackeros.shell", "org.hackeros.text-editor"], result.IncludedAppIds);
        Assert.Equal(["assets/editor.png", "assets/shell.png"], result.PublishAssetPaths);
    }

    [Fact]
    public void Validate_rejects_unresolved_package_references()
    {
        BuildProfileManifest manifest = new()
        {
            Id = "core",
            Name = "Core",
            Packages =
            [
                new BuildProfilePackageManifest("org.hackeros.ghost", "HackerOs.Apps.Ghost", BuildProfileLoadMode.Eager),
                new BuildProfilePackageManifest("org.hackeros.shell", "HackerOs.Apps.Shell", BuildProfileLoadMode.Eager)
            ],
            DefaultEnabledAppIds = ["org.hackeros.text-editor"]
        };

        AppManifest shell = CreateAppManifest("org.hackeros.shell", "assets/shell.png");
        AppManifest editor = CreateAppManifest("org.hackeros.text-editor", "assets/editor.png");

        BuildProfileValidationResult result = BuildProfileValidator.Validate(manifest, [shell, editor]);

        Assert.Contains(result.Errors, error => error.Code == "buildProfile.reference.unresolved" && error.Path == "buildProfile.packages.appId");
        Assert.Equal(["org.hackeros.shell", "org.hackeros.text-editor"], result.IncludedAppIds);
        Assert.Equal(["assets/editor.png", "assets/shell.png"], result.PublishAssetPaths);
    }

    [Fact]
    public void Validate_rejects_dependency_cycles_and_requires_boot_recovery()
    {
        BuildProfileManifest manifest = new()
        {
            Id = "core",
            Name = "Core",
            Packages =
            [
                new BuildProfilePackageManifest("org.hackeros.shell", "HackerOs.Apps.Shell", BuildProfileLoadMode.Eager),
                new BuildProfilePackageManifest("org.hackeros.files", "HackerOs.Apps.Files", BuildProfileLoadMode.Lazy)
            ]
        };

        AppManifest shell = CreateAppManifest("org.hackeros.shell", "assets/shell.png")
            with { Dependencies = [new AppDependencyManifest("org.hackeros.files", "1.0.0")] };
        AppManifest files = CreateAppManifest("org.hackeros.files", "assets/files.png")
            with { Dependencies = [new AppDependencyManifest("org.hackeros.shell", "1.0.0")] };

        BuildProfileValidationResult result = BuildProfileValidator.Validate(manifest, [shell, files]);

        Assert.Contains(result.Errors, error => error.Code == "buildProfile.dependency.cycle");
        Assert.Contains(result.Errors, error => error.Code == "buildProfile.bootRecovery.required");
    }

    [Fact]
    public void Validate_rejects_unknown_load_modes_and_duplicate_values()
    {
        BuildProfileManifest manifest = new()
        {
            Id = "core",
            Name = "Core",
            Packages =
            [
                new BuildProfilePackageManifest("org.hackeros.shell", "HackerOs.Apps.Shell", (BuildProfileLoadMode)999)
            ],
            DefaultEnabledAppIds = ["org.hackeros.shell", "org.hackeros.shell"],
            Locales = ["en-US", "en-US"]
        };

        AppManifest shell = CreateAppManifest("org.hackeros.shell", "assets/shell.png");

        BuildProfileValidationResult result = BuildProfileValidator.Validate(manifest, [shell]);

        Assert.Contains(result.Errors, error => error.Code == "buildProfile.package.loadMode.invalid");
        Assert.Contains(result.Errors, error => error.Code == "buildProfile.value.duplicate");
    }

    [Fact]
    public void BuildDiscoveryAppIds_assembles_a_deterministic_explicit_discovery_list()
    {
        BuildProfileManifest manifest = new()
        {
            Id = "core",
            Name = "Core",
            Packages =
            [
                new BuildProfilePackageManifest("org.hackeros.shell", "HackerOs.Apps.Shell", BuildProfileLoadMode.Eager),
                new BuildProfilePackageManifest("org.hackeros.files", "HackerOs.Apps.Files", BuildProfileLoadMode.Lazy)
            ],
            DefaultEnabledAppIds = ["org.hackeros.text-editor", "org.hackeros.shell"]
        };

        AppManifest shell = CreateAppManifest("org.hackeros.shell", "assets/shell.png");
        AppManifest files = CreateAppManifest("org.hackeros.files", "assets/files.png");
        AppManifest editor = CreateAppManifest("org.hackeros.text-editor", "assets/editor.png");

        IReadOnlyList<string> discoveryAppIds = BuildProfileValidator.BuildDiscoveryAppIds(manifest, [shell, files, editor]);

        Assert.Equal(["org.hackeros.files", "org.hackeros.shell", "org.hackeros.text-editor"], discoveryAppIds);
    }

    private static AppManifest CreateAppManifest(string id, string assetPath) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = id,
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Apps.Sample", "HackerOs.Apps.Sample.SampleApp"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("apps", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Assets = [new AssetManifest(assetPath, AssetKind.Image, new string('0', 64))]
    };
}
