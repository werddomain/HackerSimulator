using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Tests.Policy;


public sealed class PolicySettingsDocumentsTests
{
    [Fact]
    public async Task Normal_user_cannot_write_protected_policy_document()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext user = CreateContext(AppAuthority.User, AppCapabilities.SettingsSystemWrite);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(PolicySettingsDocuments.Path, "capabilityGrantsLocked=true\n", 1),
            user);

        Assert.Equal(SettingsWriteStatus.Denied, result.Status);
    }

    [Fact]
    public async Task System_kind_app_launched_by_user_without_audited_system_context_is_still_denied()
    {
        // Even a manifest-declared system app cannot write protected policy while operated by a
        // normal user unless the platform explicitly marks the operation as an audited system context.
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext userLaunchedSystemApp = CreateContext(AppAuthority.User, AppCapabilities.SettingsSystemWrite) with
        {
            IsSystemOperation = false
        };

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(PolicySettingsDocuments.Path, "capabilityGrantsLocked=true\n", 1),
            userLaunchedSystemApp);

        Assert.Equal(SettingsWriteStatus.Denied, result.Status);
    }

    [Fact]
    public async Task Administrator_with_capability_commits_and_shares_revision_with_direct_read()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext admin = CreateContext(
            AppAuthority.Administrator,
            AppCapabilities.SettingsSystemRead,
            AppCapabilities.SettingsSystemWrite);

        SettingsWriteResult write = await service.WriteAsync(
            new SettingsWriteRequest(
                PolicySettingsDocuments.Path,
                "schemaVersion=1\ncapabilityGrantsLocked=true\nallowRuntimePackageInstall=false\n",
                1),
            admin);
        SettingsReadResult read = await service.ReadAsync(PolicySettingsDocuments.Path, admin);

        Assert.Equal(SettingsWriteStatus.Success, write.Status);
        Assert.Equal(2, read.Document?.Revision);
    }

    [Fact]
    public async Task Invalid_policy_document_content_is_rejected()
    {
        InMemorySettingsDocumentService service = CreateService();
        AppOperationContext admin = CreateContext(
            AppAuthority.Administrator,
            AppCapabilities.SettingsSystemWrite);

        SettingsWriteResult result = await service.WriteAsync(
            new SettingsWriteRequest(PolicySettingsDocuments.Path, "capabilityGrantsLocked=not-a-bool\n", 1),
            admin);

        Assert.Equal(SettingsWriteStatus.Invalid, result.Status);
    }

    private static InMemorySettingsDocumentService CreateService() => new([PolicySettingsDocuments.CreateDefinition()]);

    private static AppOperationContext CreateContext(AppAuthority authority, params string[] capabilities) => new()
    {
        AppId = "org.hackeros.settings",
        UserId = "user",
        UserAuthority = authority,
        GrantedCapabilities = new HashSet<string>(capabilities, StringComparer.Ordinal)
    };
}
