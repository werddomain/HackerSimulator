using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Icons;
using HackerOs.Platform.Blazor.Security;
using HackerOs.Platform.Core.Elevation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerOs.Platform.Blazor.Tests.Security;

/// <summary>Render contract of the OS authentication agent's overlay (ADR 0055 section 5).</summary>
public sealed class AdministratorAuthenticationOverlayTests
{
    [Fact]
    public async Task Overlay_shows_the_operation_and_the_app_and_disables_authenticate_without_password()
    {
        AdministratorAuthenticationBroker broker = new();
        using IDisposable host = broker.Attach();
        Task<AdministratorAuthenticationOutcome> outcome = broker.RequestAsync(CreateRequest("admin"));

        string html = await RenderAsync(broker);
        string text = System.Net.WebUtility.HtmlDecode(html);

        Assert.Contains("Authentification administrateur", text, StringComparison.Ordinal);
        Assert.Contains("Uninstall Calculator for all users", text, StringComparison.Ordinal);
        Assert.Contains("org.hackeros.settings", html, StringComparison.Ordinal);
        Assert.Contains("value=\"admin\"", html, StringComparison.Ordinal);
        Assert.Matches("<button type=\"submit\" class=\"aaa-authenticate\" disabled", html);
        Assert.Contains("/var/log/auth.log", html, StringComparison.Ordinal);
        Assert.False(outcome.IsCompleted);
    }

    [Fact]
    public async Task Overlay_renders_nothing_without_a_pending_request()
    {
        string html = await RenderAsync(new AdministratorAuthenticationBroker());

        Assert.DoesNotContain("aaa-veil", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broker_without_an_overlay_reports_the_prompt_unavailable()
    {
        AdministratorAuthenticationOutcome outcome = await new AdministratorAuthenticationBroker().RequestAsync(CreateRequest(null));

        Assert.Equal(AdministratorAuthenticationOutcome.Unavailable, outcome);
    }

    [Fact]
    public async Task Removing_the_overlay_cancels_the_pending_request()
    {
        AdministratorAuthenticationBroker broker = new();
        IDisposable host = broker.Attach();
        Task<AdministratorAuthenticationOutcome> outcome = broker.RequestAsync(CreateRequest(null));

        host.Dispose();

        Assert.Equal(AdministratorAuthenticationOutcome.Cancelled, await outcome);
        Assert.Null(broker.Current);
    }

    private static async Task<string> RenderAsync(AdministratorAuthenticationBroker broker)
    {
        ServiceCollection services = new();
        services.AddSingleton(broker);
        services.AddSingleton<IIconCatalog, IconCatalog>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<AdministratorAuthenticationOverlay>(ParameterView.Empty);
            return root.ToHtmlString();
        });
    }

    private static AdministratorAuthenticationPromptRequest CreateRequest(string? suggestedLogin) =>
        new(
            new AppManifest
            {
                Id = "org.hackeros.settings",
                Name = "Settings",
                Version = "1.0.0",
                PublisherId = "pub.hackeros",
                Description = "Overlay fixture.",
                Kind = AppKind.Window,
                SdkCompatibility = new AppSdkCompatibilityManifest(HackerOsSdk.Version),
                Presentation = new PresentationManifest("system", AppLaunchVisibility.Visible, []),
                Resources = AppResourceProfileManifest.None
            },
            "Uninstall Calculator for all users",
            null,
            suggestedLogin,
            (_, _, _) => Task.FromResult(new AdministratorAuthenticationAttempt(false, 2, "invalid-credentials")));
}
