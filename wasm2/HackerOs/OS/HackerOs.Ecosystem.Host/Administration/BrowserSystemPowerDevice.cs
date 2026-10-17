using HackerOs.Infrastructure.Browser.Schema;
using HackerOs.Platform.Core.Power;
using Microsoft.JSInterop;

namespace HackerOs.Ecosystem.Administration;

/// <summary>
/// The browser as the machine's "hardware" for <see cref="SystemPowerService"/> (ADR 0056,
/// section 11): a restart reloads the page, and erasing the storage deletes the OS IndexedDB
/// database with the existing <c>indexedDb.js</c> module of <c>Infrastructure.Browser</c>.
/// </summary>
internal sealed class BrowserSystemPowerDevice(IJSRuntime runtime) : ISystemPowerDevice
{
    private const string IndexedDbModulePath = "./_content/HackerOs.Infrastructure.Browser/indexedDb.js";

    /// <inheritdoc />
    public async Task RestartAsync(CancellationToken cancellationToken = default) =>
        await runtime.InvokeVoidAsync("location.reload", cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task EraseStorageAsync(CancellationToken cancellationToken = default)
    {
        await using IJSObjectReference module = await runtime
            .InvokeAsync<IJSObjectReference>("import", cancellationToken, IndexedDbModulePath)
            .ConfigureAwait(false);
        await module.InvokeVoidAsync("deleteDatabase", cancellationToken, HackerOsIndexedDbSchema.DatabaseName).ConfigureAwait(false);
    }
}
