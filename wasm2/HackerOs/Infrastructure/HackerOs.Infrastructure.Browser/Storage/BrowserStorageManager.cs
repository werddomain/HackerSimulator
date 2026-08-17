using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.Storage;

/// <summary>Reports browser storage capacity and requests durable storage retention.</summary>
public sealed class BrowserStorageManager : IAsyncDisposable
{
    /// <summary>Absolute free-space threshold used alongside the proportional threshold.</summary>
    public const long LowSpaceByteThreshold = 64L * 1024 * 1024;

    /// <summary>Proportional free-space threshold used alongside the absolute threshold.</summary>
    public const double LowSpaceRatioThreshold = 0.10;

    private const string ModulePath = "./_content/HackerOs.Infrastructure.Browser/storageManager.js";
    private readonly IJSRuntime _runtime;
    private readonly SemaphoreSlim _moduleGate = new(1, 1);
    private IJSObjectReference? _module;
    private bool _disposed;

    /// <summary>Creates a browser storage manager backed by the native StorageManager API.</summary>
    public BrowserStorageManager(IJSRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    /// <summary>Reads the current storage estimate and durable-retention state.</summary>
    public async ValueTask<BrowserStorageStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        BrowserStorageEstimate estimate = await (await GetModuleAsync(cancellationToken).ConfigureAwait(false))
            .InvokeAsync<BrowserStorageEstimate>("getStorageEstimate", cancellationToken)
            .ConfigureAwait(false);
        long availableBytes = Math.Max(0, estimate.QuotaBytes - estimate.UsageBytes);
        bool isLowSpace = estimate.QuotaBytes > 0
            && (availableBytes < LowSpaceByteThreshold
                || (double)availableBytes / estimate.QuotaBytes < LowSpaceRatioThreshold);

        return new BrowserStorageStatus(
            estimate.UsageBytes,
            estimate.QuotaBytes,
            availableBytes,
            estimate.IsPersisted,
            isLowSpace);
    }

    /// <summary>Requests durable retention and returns whether the browser granted it.</summary>
    public async ValueTask<bool> RequestPersistenceAsync(
        CancellationToken cancellationToken = default) =>
        await (await GetModuleAsync(cancellationToken).ConfigureAwait(false))
            .InvokeAsync<bool>("requestPersistence", cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Browser shutdown can disconnect JS before the DI scope is disposed.
            }
        }

        _moduleGate.Dispose();
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_module is not null)
        {
            return _module;
        }

        await _moduleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _module ??= await _runtime.InvokeAsync<IJSObjectReference>(
                "import", cancellationToken, ModulePath).ConfigureAwait(false);
            return _module;
        }
        finally
        {
            _moduleGate.Release();
        }
    }

    private sealed record BrowserStorageEstimate(long UsageBytes, long QuotaBytes, bool IsPersisted);
}

/// <summary>Describes the browser's current origin-storage state.</summary>
/// <param name="UsageBytes">Bytes currently used by the origin.</param>
/// <param name="QuotaBytes">Bytes currently granted to the origin.</param>
/// <param name="AvailableBytes">Non-negative difference between quota and usage.</param>
/// <param name="IsPersisted">Whether durable retention is currently granted.</param>
/// <param name="IsLowSpace">Whether free space is below 10 percent or 64 MiB.</param>
public sealed record BrowserStorageStatus(
    long UsageBytes,
    long QuotaBytes,
    long AvailableBytes,
    bool IsPersisted,
    bool IsLowSpace);

/// <summary>Represents a recoverable browser quota exhaustion failure.</summary>
public sealed class BrowserStorageQuotaException : Exception
{
    /// <summary>Creates a quota failure while preserving the browser exception.</summary>
    public BrowserStorageQuotaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}