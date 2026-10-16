namespace HackerOs.Platform.WorkerHost.Tests;

/// <summary>
/// ADR 0076 end to end: the MVC site of <see cref="MvcSite"/> started on demand by <c>curl</c>, its
/// controllers, conventional route, session cookie and redirects, on the main thread and in the web worker.
/// </summary>
public sealed class MvcSiteTests
{
    public static TheoryData<bool> Modes => new() { false, true };

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Curl_reaches_the_conventional_route_and_an_api_controller(bool workers)
    {
        await using OsFixture os = await StartAsync(workers);

        ShellRun home = await os.RunAsync("curl http://mvc.hack/").WaitAsync(TimeSpan.FromSeconds(20));
        ShellRun item = await os.RunAsync("curl http://mvc.hack/api/items/4").WaitAsync(TimeSpan.FromSeconds(20));
        ShellRun again = await os.RunAsync("curl http://mvc.hack/api/items/5").WaitAsync(TimeSpan.FromSeconds(20));
        ShellRun missing = await os.RunAsync("curl -i http://mvc.hack/api/items/0").WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(home.ExitCode == 0, home.Stderr);
        Assert.Equal($"home in={(workers ? "worker" : "main")}", home.Stdout.TrimEnd());
        Assert.Equal("item 4 #1", item.Stdout.TrimEnd());
        // The singleton lives as long as the site process.
        Assert.Equal("item 5 #2", again.Stdout.TrimEnd());
        Assert.StartsWith("HTTP/1.1 404", missing.Stdout, StringComparison.Ordinal);
        Assert.Contains("application/problem+json", missing.Stdout, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Forms_redirects_and_the_session_cookie_cross_the_socket(bool workers)
    {
        await using OsFixture os = await StartAsync(workers);

        ShellRun created = await os.RunAsync("curl -i -d name=neo http://mvc.hack/api/items").WaitAsync(TimeSpan.FromSeconds(20));
        ShellRun redirected = await os.RunAsync("curl http://mvc.hack/home/go").WaitAsync(TimeSpan.FromSeconds(20));
        // The 302 of Enter set .HackSession; curl's cookie jar sent it back on the second hop.
        ShellRun session = await os.RunAsync("curl http://mvc.hack/home/enter").WaitAsync(TimeSpan.FromSeconds(20));
        // Each curl run has its own jar, as with the real curl: a new run is a new session.
        ShellRun stranger = await os.RunAsync("curl http://mvc.hack/home/visits").WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(created.ExitCode == 0, created.Stderr);
        Assert.StartsWith("HTTP/1.1 201", created.Stdout, StringComparison.Ordinal);
        // Created(uri) keeps the URI as given, as in ASP.NET Core.
        Assert.Contains("Location: /api/items/3", created.Stdout, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("home in=", redirected.Stdout, StringComparison.Ordinal);
        Assert.Equal("visits=2", session.Stdout.TrimEnd());
        Assert.Equal("visits=1", stranger.Stdout.TrimEnd());
    }

    private static async Task<OsFixture> StartAsync(bool workers)
    {
        OsFixture os = await OsFixture.CreateAsync(workers);
        if (workers)
        {
            os.Startup.MarkStarted();
        }

        return os;
    }
}
