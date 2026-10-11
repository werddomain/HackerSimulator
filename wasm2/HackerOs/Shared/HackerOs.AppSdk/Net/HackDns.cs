using System.Net;

namespace HackerOs.AppSdk.Net;

/// <summary>An entry of the host table: a name and its addresses, like <see cref="IPHostEntry"/>.</summary>
/// <param name="HostName">Canonical host name.</param>
/// <param name="AddressList">Addresses of the host, never empty.</param>
/// <param name="Aliases">Other names of the same host.</param>
public sealed record HackHostEntry(string HostName, IReadOnlyList<IPAddress> AddressList, IReadOnlyList<string> Aliases);

/// <summary>
/// Name resolution, modeled on <see cref="Dns"/> (ADR 0056, section 6). Needs
/// <c>network.simulated.read</c>.
/// </summary>
/// <remarks>
/// Resolution reads the OS resolver (the simulated zone); a name it does not know throws a
/// <see cref="System.Net.Sockets.SocketException"/> with <see cref="System.Net.Sockets.SocketError.HostNotFound"/>, as
/// <c>getaddrinfo</c> reports <c>EAI_NONAME</c>.
/// </remarks>
public static class HackDns
{
    /// <summary>Gets the addresses of a host; an address literal is returned as is, without lookup.</summary>
    /// <exception cref="System.Net.Sockets.SocketException">The name is unknown.</exception>
    /// <exception cref="UnauthorizedAccessException">The app lacks <c>network.simulated.read</c>.</exception>
    public static async Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostNameOrAddress);
        if (IPAddress.TryParse(hostNameOrAddress, out IPAddress? literal))
        {
            return [literal];
        }

        HackHostEntry entry = await GetHostEntryAsync(hostNameOrAddress, cancellationToken).ConfigureAwait(false);
        return [.. entry.AddressList];
    }

    /// <summary>Resolves a host name, or reverse-resolves an address literal, to its entry.</summary>
    /// <exception cref="System.Net.Sockets.SocketException">The name or address is unknown.</exception>
    /// <exception cref="UnauthorizedAccessException">The app lacks <c>network.simulated.read</c>.</exception>
    public static async Task<HackHostEntry> GetHostEntryAsync(string hostNameOrAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostNameOrAddress);
        if (IPAddress.TryParse(hostNameOrAddress, out IPAddress? literal))
        {
            return await GetHostEntryAsync(literal, cancellationToken).ConfigureAwait(false);
        }

        HackNetworkResult<HackHostEntry> result = await HackNetworkErrorTranslator
            .CallAsync(gateway => gateway.ResolveAsync(hostNameOrAddress, cancellationToken)).ConfigureAwait(false);
        return HackNetworkErrorTranslator.UnwrapSocket(result);
    }

    /// <summary>Reverse-resolves an address to its entry.</summary>
    /// <exception cref="System.Net.Sockets.SocketException">No host has this address.</exception>
    /// <exception cref="UnauthorizedAccessException">The app lacks <c>network.simulated.read</c>.</exception>
    public static async Task<HackHostEntry> GetHostEntryAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        HackNetworkResult<HackHostEntry> result = await HackNetworkErrorTranslator
            .CallAsync(gateway => gateway.ReverseLookupAsync(address, cancellationToken)).ConfigureAwait(false);
        return HackNetworkErrorTranslator.UnwrapSocket(result);
    }
}
