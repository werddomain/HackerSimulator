namespace HackerOs.HostSmoke.Tests;

/// <summary>
/// What <c>build/verify-hosts.sh</c> tells the suite about the host under test, through
/// environment variables (the only channel <c>dotnet test</c> passes through unchanged).
/// </summary>
/// <param name="Url">Base address of the running host, e.g. <c>http://127.0.0.1:5123</c>.</param>
/// <param name="HostName">Label used in the output (<c>ecosystem</c>, <c>test-host</c>, <c>server</c>…).</param>
/// <param name="ExpectedCpuSource">
/// When set, the value <c>/proc/self/status</c> must show on its <c>CpuSource:</c> line
/// (ADR 0066), e.g. <c>simulated</c> for a run with the measurement switched off.
/// </param>
/// <param name="ExpectedMemSource">Same as <paramref name="ExpectedCpuSource"/> for <c>MemSource:</c>.</param>
/// <param name="ArtifactsDirectory">Where a failing run leaves its screenshot, or <see langword="null"/>.</param>
public sealed record HostSmokeSettings(
    string Url,
    string HostName,
    string? ExpectedCpuSource,
    string? ExpectedMemSource,
    string? ArtifactsDirectory)
{
    /// <summary>Base address of the host under test. Required.</summary>
    public const string UrlVariable = "HACKEROS_SMOKE_URL";

    /// <summary>Label of the host under test.</summary>
    public const string HostVariable = "HACKEROS_SMOKE_HOST";

    /// <summary>Expected <c>CpuSource:</c> value.</summary>
    public const string CpuSourceVariable = "HACKEROS_SMOKE_EXPECT_CPU_SOURCE";

    /// <summary>Expected <c>MemSource:</c> value.</summary>
    public const string MemSourceVariable = "HACKEROS_SMOKE_EXPECT_MEM_SOURCE";

    /// <summary>Directory for failure screenshots.</summary>
    public const string ArtifactsVariable = "HACKEROS_SMOKE_ARTIFACTS";

    /// <summary>Reads the settings, or returns <see langword="null"/> when no URL is set.</summary>
    /// <summary>
    /// Gets whether the host runs commands in Web Workers (ADR 0070): every WebAssembly host, not the
    /// server, which renders in Interactive Server mode (section 6).
    /// </summary>
    public bool ExpectsWorkers => !string.Equals(HostName, "server", StringComparison.Ordinal);

    public static HostSmokeSettings? FromEnvironment()
    {
        string? url = Read(UrlVariable);
        if (url is null)
        {
            return null;
        }

        return new HostSmokeSettings(
            url.TrimEnd('/'),
            Read(HostVariable) ?? "host",
            Read(CpuSourceVariable),
            Read(MemSourceVariable),
            Read(ArtifactsVariable));
    }

    private static string? Read(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
