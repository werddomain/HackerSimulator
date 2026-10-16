using HackerOs.AppSdk.Web.Mvc;
using HackerOs.AppSdk.Web.Routing;
using HackerOs.AppSdk.Web.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HackerOs.AppSdk.Web.Hosting;

/// <summary>
/// Builds a site (ASP.NET Core's <c>WebApplicationBuilder</c>, ADR 0076 §2): register services in
/// <see cref="Services"/>, set <see cref="Json"/> and <see cref="Mvc"/>, then <see cref="Build"/> the
/// <see cref="HackWebApplication"/> and describe its pipeline.
/// </summary>
public sealed class HackWebApplicationBuilder
{
    private HackWebApplication? _application;

    /// <summary>Initializes a builder.</summary>
    /// <param name="applicationId">The app id (<c>com.example.intranet</c>).</param>
    /// <param name="applicationName">The display name.</param>
    /// <param name="urls">The URLs the site serves.</param>
    /// <param name="routes">The controller actions, as generated at build (ADR 0081).</param>
    public HackWebApplicationBuilder(string applicationId, string applicationName, IReadOnlyList<string> urls, HackControllerRouteTable routes)
    {
        ArgumentException.ThrowIfNullOrEmpty(applicationId);
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentNullException.ThrowIfNull(routes);
        ApplicationId = applicationId;
        ApplicationName = string.IsNullOrEmpty(applicationName) ? applicationId : applicationName;
        Urls = urls;
        Routes = routes;
    }

    /// <summary>Gets the app id.</summary>
    public string ApplicationId { get; }

    /// <summary>Gets the display name.</summary>
    public string ApplicationName { get; }

    /// <summary>Gets the URLs the site serves.</summary>
    public IReadOnlyList<string> Urls { get; }

    /// <summary>Gets the controller actions of the site.</summary>
    public HackControllerRouteTable Routes { get; }

    /// <summary>Gets the site's services (<c>Microsoft.Extensions.DependencyInjection</c>); a scope per request.</summary>
    public IServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>Gets the JSON settings.</summary>
    public HackWebJsonOptions Json { get; } = new();

    /// <summary>Gets the MVC settings (global filters).</summary>
    public HackMvcOptions Mvc { get; } = new();

    /// <summary>Gets the application built by <see cref="Build"/>, or <see langword="null"/>.</summary>
    internal HackWebApplication? Application => _application;

    /// <summary>
    /// Builds the application: adds the framework's services (JSON and MVC options, links, sessions,
    /// <see cref="TimeProvider"/>) and creates the service provider. Call it once.
    /// </summary>
    /// <returns>The application, whose pipeline is still to describe.</returns>
    /// <exception cref="InvalidOperationException">Already built.</exception>
    public HackWebApplication Build()
    {
        if (_application is not null)
        {
            throw new InvalidOperationException("The web application is already built.");
        }

        HackEndpointDataSource endpoints = new();
        Services.AddSingleton(Json);
        Services.AddSingleton(Mvc);
        Services.AddSingleton(endpoints);
        Services.AddSingleton(new HackLinkGenerator(endpoints));
        Services.TryAddSingleton(TimeProvider.System);
        Services.TryAddSingleton<HackSessionStore>();
        ServiceProvider provider = Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _application = new HackWebApplication(this, provider, endpoints);
        return _application;
    }
}
