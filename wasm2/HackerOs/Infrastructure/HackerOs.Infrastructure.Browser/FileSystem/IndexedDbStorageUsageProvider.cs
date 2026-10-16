using HackerOs.Infrastructure.Browser.Storage;
using HackerOs.Simulation.Abstractions.Metrics;
using Microsoft.JSInterop;

namespace HackerOs.Infrastructure.Browser.FileSystem;

/// <summary>
/// Browser <see cref="IStorageUsageProvider"/> behind <c>HackDriveInfo</c> (ADR 0057 section 7):
/// the used space is what the deduplicated IndexedDB content store keeps for live files, and the
/// quota comes from <c>navigator.storage.estimate()</c> through <see cref="BrowserStorageManager"/>.
/// Only infrastructure code calls the browser here, never an app (ADR 0055).
/// </summary>
public sealed class IndexedDbStorageUsageProvider : IStorageUsageProvider, IAsyncDisposable
{
    private readonly IndexedDbFileSystemProvider _fileSystem;
    private readonly BrowserStorageManager _storage;

    /// <summary>Initializes the provider.</summary>
    /// <param name="fileSystem">Persistent filesystem measured for the used space.</param>
    /// <param name="runtime">JavaScript runtime used to ask the browser for its quota.</param>
    public IndexedDbStorageUsageProvider(IndexedDbFileSystemProvider fileSystem, IJSRuntime runtime)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _storage = new BrowserStorageManager(runtime ?? throw new ArgumentNullException(nameof(runtime)));
    }

    /// <inheritdoc />
    public async ValueTask<StorageUsage> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        long used = await _fileSystem.MeasureStoredContentBytesAsync(cancellationToken).ConfigureAwait(false);
        long? quota;
        try
        {
            BrowserStorageStatus status = await _storage.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            quota = status.QuotaBytes > 0 ? status.QuotaBytes : null;
        }
        catch (JSException)
        {
            // A browser without the StorageManager API: the disk keeps its configured size.
            quota = null;
        }

        return new StorageUsage(used, quota);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _storage.DisposeAsync();
}
