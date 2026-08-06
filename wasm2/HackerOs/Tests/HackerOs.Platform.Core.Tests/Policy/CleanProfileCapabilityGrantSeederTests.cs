using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Platform.Core.Policy;

namespace HackerOs.Platform.Core.Tests.Policy;

public sealed class CleanProfileCapabilityGrantSeederTests
{
    [Fact]
    public void Seeding_grants_exactly_the_declared_capabilities_and_nothing_more()
    {
        CapabilityGrantRepository repository = new();
        AppManifest manifest = CreateManifest([
            AppCapabilities.FileSystemUserSelectedRead,
            AppCapabilities.FileSystemUserSelectedWrite
        ]);

        IReadOnlyList<CapabilityGrantMutationResult> results =
            CleanProfileCapabilityGrantSeeder.SeedDeclaredCapabilities(repository, manifest, "user-1");

        Assert.All(results, result => Assert.Equal(CapabilityGrantMutationStatus.Granted, result.Status));

        CapabilityPolicyEvaluation declared = repository.Evaluate(
            manifest.Id,
            "user-1",
            AppCapabilities.FileSystemUserSelectedRead,
            AppAuthority.User,
            AppAuthority.User);
        CapabilityPolicyEvaluation undeclared = repository.Evaluate(
            manifest.Id,
            "user-1",
            AppCapabilities.FileSystemSystemWrite,
            AppAuthority.User,
            AppAuthority.User);

        Assert.True(declared.Granted);
        Assert.False(undeclared.Granted);
        Assert.Equal(CapabilityPolicyEvaluationReason.Missing, undeclared.Reason);
    }

    [Fact]
    public void System_authority_alone_does_not_imply_an_undeclared_capability()
    {
        CapabilityGrantRepository repository = new();
        AppManifest manifest = CreateManifest([AppCapabilities.FileSystemPrivateRead]);

        CleanProfileCapabilityGrantSeeder.SeedDeclaredCapabilities(repository, manifest, "user-1");

        CapabilityPolicyEvaluation evaluation = repository.Evaluate(
            manifest.Id,
            "user-1",
            AppCapabilities.FileSystemSystemWrite,
            AppAuthority.System,
            AppAuthority.User);

        Assert.False(evaluation.Granted);
        Assert.Equal(CapabilityPolicyEvaluationReason.Missing, evaluation.Reason);
    }

    [Fact]
    public void Seeding_is_idempotent_when_called_multiple_times()
    {
        CapabilityGrantRepository repository = new();
        AppManifest manifest = CreateManifest([
            AppCapabilities.FileSystemUserSelectedRead,
            AppCapabilities.FileSystemUserSelectedWrite
        ]);

        IReadOnlyList<CapabilityGrantMutationResult> firstPass =
            CleanProfileCapabilityGrantSeeder.SeedDeclaredCapabilities(repository, manifest, "user-1");
        IReadOnlyList<CapabilityGrantMutationResult> secondPass =
            CleanProfileCapabilityGrantSeeder.SeedDeclaredCapabilities(repository, manifest, "user-1");

        Assert.Equal(2, firstPass.Count);
        Assert.Empty(secondPass);
    }

    private static AppManifest CreateManifest(IReadOnlyList<string> capabilities) => new()
    {
        Id = "org.hackeros.sample-app",
        Name = "Sample App",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "A manifest fixture for clean-profile default grant tests.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Apps.Sample", "HackerOs.Apps.Sample.SampleApp"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Capabilities = capabilities
    };
}
