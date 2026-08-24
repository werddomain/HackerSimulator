using System.Collections.Concurrent;

namespace HackerOs.App.Abstractions;

/// <summary>
/// A small, per-launch, thread-safe set of one-shot elevation tickets minted after live
/// Administrator approval. Each ticket unblocks exactly one subsequent filesystem authorization
/// decision matching its (capability, path), then is immediately consumed. Never persisted;
/// scoped to the lifetime of one <see cref="AppOperationContext"/>.
/// </summary>
public sealed class OneShotElevationGrantSet
{
    private readonly ConcurrentDictionary<(string Capability, string? Path), byte> _tickets = new();

    /// <summary>Grants a one-shot ticket for the exact capability/path combination.</summary>
    public void Grant(string capability, VirtualPath? path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        _tickets[(capability, path?.Value)] = 0;
    }

    /// <summary>Attempts to consume a matching ticket; a ticket unblocks exactly one call.</summary>
    public bool TryConsume(string capability, VirtualPath? path) =>
        _tickets.TryRemove((capability, path?.Value), out _);
}
