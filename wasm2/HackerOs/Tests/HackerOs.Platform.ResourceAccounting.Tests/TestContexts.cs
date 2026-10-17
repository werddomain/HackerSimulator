using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Tests.Support;

namespace HackerOs.Platform.ResourceAccounting.Tests;

internal static class TestContexts
{
    public static IAppExecutionContext Process(long pid) => new MinimalAppExecutionContext(
        new AppManifest
        {
            Id = $"org.hackeros.test-{pid}",
            Name = $"Test {pid}",
            Version = "1.0.0",
            PublisherId = "org.hackeros",
            Description = "Resource accounting test process.",
            Kind = AppKind.Terminal,
            EntryPoint = new AppEntryPointManifest("HackerOs.Platform.ResourceAccounting.Tests", "Test"),
            SdkCompatibility = new AppSdkCompatibilityManifest(HackerOsSdk.Version),
            Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
            Resources = AppResourceProfileManifest.None
        },
        processId: ProcessId.FromInt64(pid));
}
