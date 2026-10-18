using HackerOs.Platform.Core.Isolation;

namespace HackerOs.Platform.Core.Tests.Isolation;

/// <summary>The setting, the headers and the page's answer of ADR 0131 (adjusted by ADR 0143).</summary>
public sealed class CrossOriginIsolationTests
{
    [Theory]
    [InlineData(null, CrossOriginIsolationMode.Auto)]
    [InlineData("", CrossOriginIsolationMode.Auto)]
    [InlineData("maybe", CrossOriginIsolationMode.Auto)]
    [InlineData(" AUTO ", CrossOriginIsolationMode.Auto)]
    [InlineData("on", CrossOriginIsolationMode.On)]
    [InlineData("True", CrossOriginIsolationMode.On)]
    [InlineData("off", CrossOriginIsolationMode.Off)]
    [InlineData("false", CrossOriginIsolationMode.Off)]
    public void The_setting_reads_auto_on_or_off(string? text, CrossOriginIsolationMode expected) =>
        Assert.Equal(expected, CrossOriginIsolationPolicy.Parse(text));

    [Theory]
    [InlineData(CrossOriginIsolationMode.Auto, true, true)]
    [InlineData(CrossOriginIsolationMode.Auto, false, false)]
    [InlineData(CrossOriginIsolationMode.On, false, true)]
    [InlineData(CrossOriginIsolationMode.Off, true, false)]
    public void Auto_follows_the_audit_and_on_or_off_override_it(CrossOriginIsolationMode mode, bool audit, bool sends) =>
        Assert.Equal(sends, CrossOriginIsolationPolicy.SendsHeaders(mode, audit));

    [Fact]
    public void A_server_host_sends_coop_and_credentialless_coep_unless_off()
    {
        Assert.Equal(
            [new("Cross-Origin-Opener-Policy", "same-origin"), new("Cross-Origin-Embedder-Policy", "credentialless")],
            CrossOriginIsolationPolicy.ServerHeaders(CrossOriginIsolationMode.On));
        Assert.Empty(CrossOriginIsolationPolicy.ServerHeaders(CrossOriginIsolationMode.Off));
        Assert.Equal(CrossOriginIsolationPolicy.AuditPassed, CrossOriginIsolationPolicy.ServerHeaders(CrossOriginIsolationMode.Auto).Count == 2);
    }

    [Fact]
    public void An_isolated_page_turns_on_rings_synchronous_calls_and_memory_measurement()
    {
        CrossOriginIsolationStatus status = CrossOriginIsolationProbe.Read(CrossOriginIsolationMode.Auto,
            """{"isolated":true,"secureContext":true,"controlled":true,"workerSends":true,"workerEmbedderPolicy":"credentialless","sharedArrayBuffer":true,"measureMemory":true,"timerResolutionUs":5,"reloading":false}""");

        Assert.True(status.IsIsolated);
        Assert.Equal(CrossOriginIsolationReason.None, status.Reason);
        Assert.Equal("credentialless", status.EmbedderPolicy);
        Assert.True(status.SharedPipes);
        Assert.True(status.SynchronousCalls);
        Assert.True(status.MemoryMeasurement);
        Assert.Equal(TimeSpan.FromMicroseconds(5), status.TimerResolution);
        Assert.StartsWith("isolation: cross-origin isolated (setting auto, COEP credentialless)", CrossOriginIsolationProbe.Describe(status), StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_isolated_by_a_server_without_a_service_worker_has_no_known_policy()
    {
        CrossOriginIsolationStatus status = CrossOriginIsolationProbe.Read(CrossOriginIsolationMode.On,
            """{"isolated":true,"secureContext":true,"workerSends":null,"sharedArrayBuffer":true,"measureMemory":false,"timerResolutionUs":5}""");

        Assert.True(status.IsIsolated);
        Assert.Null(status.EmbedderPolicy);
        Assert.False(status.MemoryMeasurement);
        Assert.Contains("COEP set by the server", CrossOriginIsolationProbe.Describe(status), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CrossOriginIsolationMode.Auto, """{"isolated":false,"secureContext":false,"workerSends":true}""", CrossOriginIsolationReason.InsecureContext)]
    [InlineData(CrossOriginIsolationMode.Off, """{"isolated":false,"secureContext":true,"workerSends":false}""", CrossOriginIsolationReason.Setting)]
    [InlineData(CrossOriginIsolationMode.Auto, """{"isolated":false,"secureContext":true,"workerSends":true}""", CrossOriginIsolationReason.Browser)]
    [InlineData(CrossOriginIsolationMode.Auto, """{"isolated":false,"secureContext":true,"workerSends":null}""", CrossOriginIsolationReason.MissingHeaders)]
    public void A_page_that_is_not_isolated_says_why(CrossOriginIsolationMode mode, string json, CrossOriginIsolationReason reason)
    {
        CrossOriginIsolationStatus status = CrossOriginIsolationProbe.Read(mode, json);

        Assert.False(status.IsIsolated);
        Assert.Equal(reason, status.Reason);
        Assert.False(status.SharedPipes);
        Assert.False(status.SynchronousCalls);
        Assert.Contains($"({CrossOriginIsolationPolicy.Format(reason)}, setting {CrossOriginIsolationPolicy.Format(mode)})", CrossOriginIsolationProbe.Describe(status), StringComparison.Ordinal);
    }

    [Fact]
    public void A_probe_that_could_not_run_reads_as_a_browser_that_does_not_isolate()
    {
        CrossOriginIsolationStatus status = CrossOriginIsolationProbe.Read(CrossOriginIsolationMode.On, null);

        Assert.False(status.IsIsolated);
        Assert.Equal(CrossOriginIsolationReason.Browser, status.Reason);
        Assert.Equal(CrossOriginIsolationMode.On, status.Mode);
    }

    [Fact]
    public async Task The_probe_records_its_answer_in_the_state()
    {
        CrossOriginIsolationState state = new();
        Assert.Same(CrossOriginIsolationStatus.NotBrowser, state.Status);
        CrossOriginIsolationStatus found = CrossOriginIsolationStatus.NotBrowser with { IsIsolated = true, Reason = CrossOriginIsolationReason.None };

        CrossOriginIsolationStatus returned = await new CrossOriginIsolationProbe(state, () => Task.FromResult(found)).RunAsync();

        Assert.Same(found, returned);
        Assert.Same(found, state.Status);
    }

    /// <summary>The PWA's service worker mirrors the audit constant: both must say the same.</summary>
    [Fact]
    public void The_service_worker_mirrors_the_audit_result()
    {
        string script = File.ReadAllText(Path.Combine(SolutionDirectory(), "OS/HackerOs.Ecosystem/wwwroot/service-worker.published.js"));

        Assert.Contains(
            $"const crossOriginIsolationAuditPassed = {(CrossOriginIsolationPolicy.AuditPassed ? "true" : "false")};",
            script,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OS/HackerOs.Ecosystem/wwwroot/appsettings.json")]
    [InlineData("test/test/appsettings.json")]
    [InlineData("Server/HackerOs.Server/appsettings.json")]
    public void Each_host_ships_the_setting_at_auto(string relativePath) =>
        Assert.Contains("\"CrossOriginIsolation\": \"auto\"", File.ReadAllText(Path.Combine(SolutionDirectory(), relativePath)), StringComparison.Ordinal);

    private static string SolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HackerOs.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("HackerOs.sln not found above the test output.");
    }
}
