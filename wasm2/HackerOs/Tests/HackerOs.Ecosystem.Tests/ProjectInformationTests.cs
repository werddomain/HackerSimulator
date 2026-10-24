using HackerOs.App.Abstractions;
using HackerOs.Ecosystem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HackerOs.Ecosystem.Tests;

/// <summary>Checks the actual rendered product information, including exact public-host matching.</summary>
public sealed class ProjectInformationTests
{
    [Theory]
    [InlineData("https://hackos.kazo.ca/", true, true)]
    [InlineData("https://HACKOS.KAZO.CA/login", true, true)]
    [InlineData("https://hackos.kazo.ca/", false, false)]
    [InlineData("https://hackos.kazo.ca.example.com/", true, false)]
    [InlineData("http://localhost:7240/?host=hackos.kazo.ca", true, false)]
    [InlineData("https://kazo-ca.github.io/HackerOs/", true, false)]
    public async Task Migration_notice_is_only_shown_on_the_public_authentication_pages(string url, bool enabled, bool visible)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<NavigationManager>(new TestNavigation(url));
        services.AddLocalization();
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<ProjectInformation>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(ProjectInformation.ShowLegacyNotice)] = enabled }));
            return result.ToHtmlString();
        });
        Assert.Equal(visible, html.Contains("https://hackos.kazo.ca/legacy/", StringComparison.Ordinal));
        Assert.Contains("https://github.com/Kazo-ca/HackerOs", html);
        Assert.Contains($"HackerOS {HackerOsSdk.Version}", html);
    }

    private sealed class TestNavigation : NavigationManager
    {
        internal TestNavigation(string url) => Initialize(new Uri(url).GetLeftPart(UriPartial.Authority) + "/", new Uri(url).AbsoluteUri);
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

}
