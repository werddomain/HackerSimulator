using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Tests;

public sealed class SettingsAuthorizationTests
{
    private static readonly VirtualPath AssociationsPath =
        VirtualPath.Parse("/etc/hackeros/file-associations.json");

    [Fact]
    public async Task Normal_user_cannot_write_protected_associations_even_with_capability()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext context = CreateContext(
            AppAuthority.User,
            AppCapabilities.FileAssociationsWrite);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, ValidSettings(".txt"), 1),
            context);

        Assert.Equal(SettingsWriteStatus.Denied, result.Status);
    }

    [Fact]
    public async Task Administrator_without_write_capability_cannot_write_associations()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext context = CreateContext(AppAuthority.Administrator);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, ValidSettings(".txt"), 1),
            context);

        Assert.Equal(SettingsWriteStatus.Denied, result.Status);
    }

    [Fact]
    public async Task Administrator_with_capability_commits_valid_document_and_audit_event()
    {
        InMemorySettingsDocumentService service = CreateService();
        SettingsDocumentChangedEventArgs? changed = null;
        service.DocumentChanged += (_, args) => changed = args;
        AppOperationContext context = CreateContext(
            AppAuthority.Administrator,
            AppCapabilities.FileAssociationsRead,
            AppCapabilities.FileAssociationsWrite);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, ValidSettings(".txt"), 1),
            context);

        Assert.Equal(SettingsWriteStatus.Success, result.Status);
        Assert.Equal(2, result.Document?.Revision);
        Assert.Equal(AppAuthority.Administrator, changed?.Authority);
        Assert.Equal("org.hackeros.settings", changed?.AppId);
    }

    [Fact]
    public async Task Invalid_document_is_rejected_without_changing_canonical_revision()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext admin = CreateContext(
            AppAuthority.Administrator,
            AppCapabilities.FileAssociationsRead,
            AppCapabilities.FileAssociationsWrite);

        SettingsWriteResult write = await service.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, "{ invalid", 1),
            admin);
        SettingsReadResult read = await service.ReadAsync(AssociationsPath, admin);

        Assert.Equal(SettingsWriteStatus.Invalid, write.Status);
        Assert.Equal(1, read.Document?.Revision);
        Assert.Equal(EmptySettings, read.Document?.Content);
    }

    [Fact]
    public async Task Stale_revision_is_rejected_as_a_conflict()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext admin = CreateContext(
            AppAuthority.Administrator,
            AppCapabilities.FileAssociationsWrite);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, ValidSettings(".txt"), 0),
            admin);

        Assert.Equal(SettingsWriteStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Filesystem_projection_reads_the_same_canonical_document()
    {
        InMemorySettingsDocumentService service = CreateService();
        SettingsFileProjection projection = new(service);
        AppOperationContext admin = CreateContext(
            AppAuthority.Administrator,
            AppCapabilities.FileAssociationsRead,
            AppCapabilities.FileAssociationsWrite);
        SettingsWriteResult write = await projection.WriteFileAsync(
            new SettingsWriteRequest(AssociationsPath, ValidSettings(".log"), 1),
            admin);

        SettingsReadResult directRead = await service.ReadAsync(AssociationsPath, admin);

        Assert.Equal(SettingsWriteStatus.Success, write.Status);
        Assert.Equal(write.Document, directRead.Document);
    }

    [Fact]
    public async Task Explicit_system_operation_uses_system_authority_but_still_needs_capability()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext system = CreateContext(
            AppAuthority.User,
            AppCapabilities.FileAssociationsWrite) with
        {
            IsSystemOperation = true
        };

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, ValidSettings(".sys"), 1),
            system);

        Assert.Equal(SettingsWriteStatus.Success, result.Status);
    }

    [Fact]
    public async Task Explicit_system_operation_without_capability_is_denied()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext system = CreateContext(AppAuthority.User) with
        {
            IsSystemOperation = true
        };

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, ValidSettings(".sys"), 1),
            system);

        Assert.Equal(SettingsWriteStatus.Denied, result.Status);
    }

    private const string EmptySettings = """
        {"schemaVersion":1,"associations":[]}
        """;

    private static InMemorySettingsDocumentService CreateService() => new(
    [
        new SettingsDocumentDefinition(
            AssociationsPath,
            HackerOs.Simulation.Abstractions.Settings.SettingsDocumentKey.ForOsAdmin("file-associations"),
            EmptySettings,
            "application/json",
            AppCapabilities.FileAssociationsRead,
            AppCapabilities.FileAssociationsWrite,
            AppAuthority.User,
            AppAuthority.Administrator,
            new FileAssociationSettingsValidator())
    ]);

    private static AppOperationContext CreateContext(
        AppAuthority authority,
        params string[] capabilities) => new()
    {
        AppId = "org.hackeros.settings",
        UserId = "user",
        UserAuthority = authority,
        GrantedCapabilities = new HashSet<string>(capabilities, StringComparer.Ordinal)
    };

    private static string ValidSettings(string extension) => $$"""
        {
          "schemaVersion": 1,
          "associations": [
            {
              "extension": "{{extension}}",
              "appId": "org.hackeros.text-editor",
              "actions": ["open", "edit"]
            }
          ]
        }
        """;
}