using System.Net;

namespace HackerOs.AppSdk.Net;

/// <summary>
/// The network stack of the OS as one app instance sees it (ADR 0056, section 6): name
/// resolution, echo requests, HTTP exchanges and TCP port probes. Reached through
/// <see cref="IAppExecutionContext.Network"/>; apps normally use the <see cref="HackDns"/>,
/// <see cref="HackPing"/>, <see cref="HackHttpClient"/> and <see cref="HackTcpClient"/> facades.
/// </summary>
/// <remarks>
/// <para>
/// The OS tries the simulated network first. When the target is not part of it, the app holds
/// <c>network.real.access</c> and the optional server is connected, the OS relays the call through
/// the server proxy, managing the access token itself. The app sees neither the token nor the
/// state of the server: without a server the outcome is <see cref="HackNetworkError.HostNotFound"/>.
/// </para>
/// <para>
/// Capabilities: <c>network.simulated.read</c> for resolution, ping, GET, HEAD and probes;
/// <c>network.simulated.write</c> for any other HTTP method. A missing capability throws
/// <c>AppGatewayAccessDeniedException</c>; network failures are returned as a result, never thrown.
/// </para>
/// </remarks>
public interface IAppNetworkGateway
{
    /// <summary>Resolves a host name to its entry (forward lookup).</summary>
    ValueTask<HackNetworkResult<HackHostEntry>> ResolveAsync(string hostName, CancellationToken cancellationToken = default);

    /// <summary>Finds the host name of an address (reverse lookup).</summary>
    ValueTask<HackNetworkResult<HackHostEntry>> ReverseLookupAsync(IPAddress address, CancellationToken cancellationToken = default);

    /// <summary>Sends one echo request to a host name or address.</summary>
    ValueTask<HackNetworkResult<HackPingReply>> PingAsync(
        string hostNameOrAddress, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Performs one HTTP exchange, following redirects (at most 10, like a browser).</summary>
    ValueTask<HackNetworkResult<HackHttpResponse>> SendAsync(
        HackHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>Probes the TCP ports <paramref name="firstPort"/> to <paramref name="lastPort"/> of a host.</summary>
    ValueTask<HackNetworkResult<IReadOnlyList<HackPortProbeResult>>> ProbeTcpAsync(
        string hostNameOrAddress, int firstPort, int lastPort, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guesses the operating system of a host from its TCP/IP stack, as <c>nmap -O</c> does;
    /// a <see langword="null"/> value means the stack matched nothing known.
    /// </summary>
    ValueTask<HackNetworkResult<HackOsFingerprint?>> DetectOperatingSystemAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default);
}

/// <summary>Why a network call failed, independent of the transport that carried it.</summary>
public enum HackNetworkError
{
    /// <summary>The call succeeded.</summary>
    None,

    /// <summary>No network knows this name (<c>ERR_NAME_NOT_RESOLVED</c>).</summary>
    HostNotFound,

    /// <summary>The host exists but does not answer (<c>ERR_HOST_UNREACHABLE</c>).</summary>
    HostUnreachable,

    /// <summary>The host answered with a refusal (<c>ERR_CONNECTION_REFUSED</c>).</summary>
    ConnectionRefused,

    /// <summary>More than 10 redirects (<c>ERR_TOO_MANY_REDIRECTS</c>).</summary>
    TooManyRedirects,

    /// <summary>The connection broke or timed out on the way (<c>ERR_CONNECTION_FAILED</c>).</summary>
    ConnectionFailed
}

/// <summary>Outcome of one <see cref="IAppNetworkGateway"/> call: a value or a <see cref="HackNetworkError"/>.</summary>
/// <typeparam name="T">Type of the value on success.</typeparam>
public sealed record HackNetworkResult<T>
{
    private HackNetworkResult(T? value, HackNetworkError error, string? message)
    {
        Value = value;
        Error = error;
        Message = message;
    }

    /// <summary>Gets the value; meaningful only when <see cref="Succeeded"/> is <see langword="true"/>.</summary>
    public T? Value { get; }

    /// <summary>Gets the failure, <see cref="HackNetworkError.None"/> on success.</summary>
    public HackNetworkError Error { get; }

    /// <summary>Gets an optional human-readable detail of the failure.</summary>
    public string? Message { get; }

    /// <summary>Gets whether the call succeeded.</summary>
    public bool Succeeded => Error == HackNetworkError.None;

    /// <summary>Creates a successful result.</summary>
    public static HackNetworkResult<T> Success(T value) => new(value, HackNetworkError.None, null);

    /// <summary>Creates a failed result.</summary>
    /// <exception cref="ArgumentException"><paramref name="error"/> is <see cref="HackNetworkError.None"/>.</exception>
    public static HackNetworkResult<T> Failure(HackNetworkError error, string? message = null) =>
        error == HackNetworkError.None
            ? throw new ArgumentException("A failure needs an error.", nameof(error))
            : new(default, error, message);
}

/// <summary>Stable codes of <see cref="HackNetworkError"/>, as a browser prints them.</summary>
public static class HackNetworkErrorCodes
{
    /// <summary>Gets the <c>ERR_*</c> code of <paramref name="error"/>, or an empty string for <see cref="HackNetworkError.None"/>.</summary>
    public static string ToCode(HackNetworkError error) => error switch
    {
        HackNetworkError.HostNotFound => "ERR_NAME_NOT_RESOLVED",
        HackNetworkError.HostUnreachable => "ERR_HOST_UNREACHABLE",
        HackNetworkError.ConnectionRefused => "ERR_CONNECTION_REFUSED",
        HackNetworkError.TooManyRedirects => "ERR_TOO_MANY_REDIRECTS",
        HackNetworkError.ConnectionFailed => "ERR_CONNECTION_FAILED",
        _ => string.Empty
    };
}

/// <summary>Gateway used by contexts that have no network (tests, detached contexts).</summary>
internal sealed class UnsupportedAppNetworkGateway : IAppNetworkGateway
{
    private const string Reason = "This IAppExecutionContext implementation does not provide a Network gateway.";

    public ValueTask<HackNetworkResult<HackHostEntry>> ResolveAsync(string hostName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Reason);

    public ValueTask<HackNetworkResult<HackHostEntry>> ReverseLookupAsync(IPAddress address, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Reason);

    public ValueTask<HackNetworkResult<HackPingReply>> PingAsync(
        string hostNameOrAddress, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Reason);

    public ValueTask<HackNetworkResult<HackHttpResponse>> SendAsync(
        HackHttpRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Reason);

    public ValueTask<HackNetworkResult<IReadOnlyList<HackPortProbeResult>>> ProbeTcpAsync(
        string hostNameOrAddress, int firstPort, int lastPort, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Reason);

    public ValueTask<HackNetworkResult<HackOsFingerprint?>> DetectOperatingSystemAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Reason);
}
