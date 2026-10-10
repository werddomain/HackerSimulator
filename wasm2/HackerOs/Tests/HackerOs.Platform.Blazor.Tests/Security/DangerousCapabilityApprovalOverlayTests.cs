using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Icons;
using HackerOs.Platform.Blazor.Security;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions.Sessions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerOs.Platform.Blazor.Tests.Security;

/// <summary>Render contract of the secure-desktop approval overlay (ADR 0049 sections 6 and 9).</summary>
public sealed class DangerousCapabilityApprovalOverlayTests
{
    [Fact]
    public async Task Overlay_shows_the_app_its_capabilities_with_their_risks_and_disables_approve_without_password()
    {
        DangerousCapabilityApprovalBroker broker = new();
        using IDisposable host = broker.Attach();
        Task<DangerousCapabilityApprovalOutcome> outcome = broker.RequestAsync(CreateRequest(suggestedLogin: "root"));

        string html = await RenderAsync(broker);
        string text = System.Net.WebUtility.HtmlDecode(html);

        Assert.Contains("Autorisation administrateur requise", html, StringComparison.Ordinal);
        Assert.Contains("Scanner", html, StringComparison.Ordinal);
        Assert.Contains("org.example.scanner", html, StringComparison.Ordinal);
        Assert.Contains("App non système", text, StringComparison.Ordinal);
        Assert.Contains("1.2.0", html, StringComparison.Ordinal);
        foreach (string capability in new[] { AppCapabilities.NetworkRealAccess, AppCapabilities.ProcessManage })
        {
            CapabilityDescription description = AppCapabilities.Describe(capability);
            Assert.Contains(capability, html, StringComparison.Ordinal);
            Assert.Contains(description.Title, text, StringComparison.Ordinal);
            Assert.Contains(description.RiskDescription!, text, StringComparison.Ordinal);
        }

        Assert.Contains("value=\"root\"", html, StringComparison.Ordinal);
        Assert.Matches("<button type=\"submit\" class=\"dca-approve\" disabled", html);
        Assert.Contains("/var/log/auth.log", html, StringComparison.Ordinal);
        Assert.False(outcome.IsCompleted);
    }

    [Fact]
    public async Task Overlay_renders_nothing_without_a_pending_request()
    {
        DangerousCapabilityApprovalBroker broker = new();

        string html = await RenderAsync(broker);

        Assert.DoesNotContain("dca-veil", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broker_without_an_overlay_reports_the_prompt_unavailable()
    {
        DangerousCapabilityApprovalBroker broker = new();

        DangerousCapabilityApprovalOutcome outcome = await broker.RequestAsync(CreateRequest(suggestedLogin: null));

        Assert.Equal(DangerousCapabilityApprovalOutcome.Unavailable, outcome);
    }

    [Fact]
    public async Task Removing_the_overlay_dismisses_the_pending_request()
    {
        DangerousCapabilityApprovalBroker broker = new();
        IDisposable host = broker.Attach();
        Task<DangerousCapabilityApprovalOutcome> outcome = broker.RequestAsync(CreateRequest(suggestedLogin: null));

        host.Dispose();

        Assert.Equal(DangerousCapabilityApprovalOutcome.Dismissed, await outcome);
        Assert.Null(broker.Current);
    }

    private static async Task<string> RenderAsync(DangerousCapabilityApprovalBroker broker)
    {
        ServiceCollection services = new();
        services.AddSingleton(broker);
        services.AddSingleton<IIconCatalog, IconCatalog>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<DangerousCapabilityApprovalOverlay>(ParameterView.Empty);
            return root.ToHtmlString();
        });
    }

    private static DangerousCapabilityApprovalRequest CreateRequest(string? suggestedLogin)
    {
        AppManifest manifest = new()
        {
            Id = "org.example.scanner",
            Name = "Scanner",
            Version = "1.2.0",
            PublisherId = "org.example",
            Description = "Overlay fixture.",
            Kind = AppKind.Window,
            EntryPoint = new AppEntryPointManifest("Scanner", "Scanner.ScannerWindow"),
            SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
            Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
            Resources = AppResourceProfileManifest.None,
            Capabilities = [AppCapabilities.NetworkRealAccess, AppCapabilities.ProcessManage]
        };
        LocalGroupId group = LocalGroupId.FromGuid(Guid.NewGuid());
        AuthenticatedPrincipal principal = new(
            SessionId.FromGuid(Guid.NewGuid()),
            LocalUserId.FromGuid(Guid.NewGuid()),
            LocalLoginName.Parse("root"),
            "Root",
            AppAuthority.Administrator,
            group,
            [group],
            InstallationId.FromGuid(Guid.NewGuid()),
            DeviceId.FromGuid(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch);
        return new DangerousCapabilityApprovalRequest(
            manifest,
            [.. manifest.Capabilities.Select(AppCapabilities.Describe)],
            principal,
            suggestedLogin,
            (_, _, _) => Task.FromResult(new DangerousCapabilityApprovalAttempt(false, 2, "invalid-credentials")));
    }
}
