using HackerOs.App.Abstractions;

namespace HackerOs.App.Abstractions.Tests;

public sealed class AppManifestValidatorTests
{
    [Fact]
    public void Validate_accepts_a_complete_window_app_manifest()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            FileHandlers =
            [
                new FileHandlerManifest("text/plain", [".txt"], ["open", "edit"])
            ]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_requires_command_metadata_for_terminal_apps()
    {
        AppManifest manifest = CreateValidManifest() with { Kind = AppKind.Terminal };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.terminal.required");
    }

    [Fact]
    public void Validate_rejects_file_handlers_for_non_window_apps()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            Kind = AppKind.Service,
            FileHandlers = [new FileHandlerManifest("text/plain", [".txt"], ["open"])]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.fileHandlers.forbidden");
    }

    [Fact]
    public void Validate_rejects_an_inverted_sdk_version_range()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            SdkCompatibility = new AppSdkCompatibilityManifest("2.0.0", "1.9.0")
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.version.range.invalid");
    }

    [Fact]
    public void Validate_rejects_duplicate_capabilities_case_insensitively()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            Capabilities = ["filesystem.private.read", "FILESYSTEM.PRIVATE.READ"]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.value.duplicate");
    }

    [Fact]
    public void Validate_rejects_unknown_capabilities()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            Capabilities = ["not-a-real-capability"]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.capability.unknown");
    }

    [Fact]
    public void Validate_rejects_dialog_capabilities_for_non_window_apps()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            Kind = AppKind.Service,
            Capabilities = [AppCapabilities.DialogFileOpen]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.capability.incompatible");
    }

    [Fact]
    public void Validate_accepts_dialog_capabilities_for_window_apps()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            Capabilities = [AppCapabilities.DialogFileOpen, AppCapabilities.DialogFileSave, AppCapabilities.DialogFolderSelect]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_accepts_a_well_formed_topic_permission_as_a_requested_capability()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            Capabilities = ["topic-publish:app/org.hackeros.file-explorer/change-directory"]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_accepts_a_topic_permission_the_manifest_declares_and_owns()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            DeclaredTopicPermissions =
            [
                new TopicPermissionDeclarationManifest(
                    "topic-publish:app/org.hackeros.text-editor/change-directory",
                    "Allows another app to change this window's current directory.")
            ]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_a_declared_topic_permission_not_owned_by_this_app()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            DeclaredTopicPermissions =
            [
                new TopicPermissionDeclarationManifest(
                    "topic-publish:app/org.hackeros.some-other-app/change-directory",
                    "Attempts to declare a permission for a different app's namespace.")
            ]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.topicPermission.notOwned");
    }

    [Fact]
    public void Validate_rejects_a_malformed_declared_topic_permission()
    {
        AppManifest manifest = CreateValidManifest() with
        {
            DeclaredTopicPermissions =
            [
                new TopicPermissionDeclarationManifest("not-a-topic-permission", "Malformed identifier.")
            ]
        };

        ManifestValidationResult result = AppManifestValidator.Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "manifest.topicPermission.malformed");
    }

    private static AppManifest CreateValidManifest() => new()
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
        Capabilities = [AppCapabilities.FileSystemUserSelectedRead, AppCapabilities.FileSystemUserSelectedWrite]
    };
}