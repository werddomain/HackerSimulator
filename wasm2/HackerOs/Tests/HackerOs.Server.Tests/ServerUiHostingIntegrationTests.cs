using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HackerOs.Server.Tests;

/// <summary>
/// Proves the server-hosted Blazor UI (ADR 0027) is actually mapped alongside the
/// existing API surface, without asserting on post-circuit rendered component output
/// (Interactive Server with <c>prerender: false</c> serves the render-mode bootstrap
/// shell on first request, not the fully rendered component tree).
/// </summary>
[Collection(ServerEnvironmentCollection.Name)]
public sealed class ServerUiHostingIntegrationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"hackeros-server-ui-integration-{Guid.NewGuid():N}.db");
    private readonly string _backupRoot = Path.Combine(
        Path.GetTempPath(),
        $"hackeros-server-ui-backups-{Guid.NewGuid():N}");
    private string? _previousConnectionString;
    private string? _previousBackupRoot;
    private ServerApplicationFactory? _factory;

    public Task InitializeAsync()
    {
        _previousConnectionString = Environment.GetEnvironmentVariable("HACKEROS_ConnectionStrings__HackerOsDb");
        _previousBackupRoot = Environment.GetEnvironmentVariable("HACKEROS_ServerBackup__Root");
        Environment.SetEnvironmentVariable("HACKEROS_ConnectionStrings__HackerOsDb", $"Data Source={_databasePath}");
        Environment.SetEnvironmentVariable("HACKEROS_ServerBackup__Root", _backupRoot);
        _factory = new ServerApplicationFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("HACKEROS_ConnectionStrings__HackerOsDb", _previousConnectionString);
        Environment.SetEnvironmentVariable("HACKEROS_ServerBackup__Root", _previousBackupRoot);
    }

    [Fact]
    public async Task Root_ServesInteractiveServerBootstrapShell_AndApiRemainsReachable()
    {
        var factory = Assert.IsType<ServerApplicationFactory>(_factory);
        using var client = factory.CreateClient();

        using var uiResponse = await client.GetAsync("/");
        uiResponse.EnsureSuccessStatusCode();
        var uiBody = await uiResponse.Content.ReadAsStringAsync();
        Assert.Contains("blazor.web.js", uiBody, StringComparison.Ordinal);

        using var healthResponse = await client.GetAsync("/health");
        healthResponse.EnsureSuccessStatusCode();
        var healthBody = await healthResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"healthy\"", healthBody, StringComparison.Ordinal);
    }

    private sealed class ServerApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
