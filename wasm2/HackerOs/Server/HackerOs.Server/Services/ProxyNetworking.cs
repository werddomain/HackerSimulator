using System.Net;

namespace HackerOs.Server.Services;

/// <summary>Resolves proxy destinations through an injectable, testable boundary.</summary>
public interface IProxyAddressResolver
{
    /// <summary>Returns every address currently published for <paramref name="host"/>.</summary>
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}

/// <summary>Uses the operating-system DNS resolver for production proxy requests.</summary>
public sealed class SystemProxyAddressResolver : IProxyAddressResolver
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken);
}

/// <summary>
/// Carries the server-validated destination address to the socket connection callback.
/// Async-local state keeps concurrent requests isolated while allowing pooled handlers.
/// </summary>
public interface IProxyConnectionPinAccessor
{
    /// <summary>Gets the address authorized for the current asynchronous request flow.</summary>
    IPAddress? Address { get; }

    /// <summary>Temporarily pins the current flow to an already validated address.</summary>
    IDisposable Push(IPAddress address);
}

/// <inheritdoc />
public sealed class ProxyConnectionPinAccessor : IProxyConnectionPinAccessor
{
    private readonly AsyncLocal<IPAddress?> _current = new();

    /// <inheritdoc />
    public IPAddress? Address => _current.Value;

    /// <inheritdoc />
    public IDisposable Push(IPAddress address)
    {
        var previous = _current.Value;
        _current.Value = address;
        return new RestoreScope(() => _current.Value = previous);
    }

    private sealed class RestoreScope(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}
