using Microsoft.JSInterop;

namespace HackerOs.AppSdk.Blazor.Interop;

/// <summary>
/// Copies text to the clipboard of the real browser that hosts HackerOS, so the user can paste it
/// outside the simulation (an exported report, a diagnostic dump). Like
/// <see cref="BrowserFileDownloader"/>, it is the host-side exit of the SDK: the simulated clipboard
/// shared by HackerOS apps is <c>IAppClipboardGateway</c>.
/// </summary>
/// <remarks>
/// The script ships with this library (<c>_content/HackerOs.AppSdk.Blazor/clipboard.js</c>) and is
/// imported on first use; an app passes its own <see cref="IJSRuntime"/> (ADR 0055, section 3).
/// The browser may refuse the write (no focus, no permission): the
/// <see cref="JSException"/> is then thrown to the caller.
/// </remarks>
public sealed class BrowserClipboard : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    /// <summary>Creates the helper over the app's JavaScript runtime.</summary>
    /// <param name="jsRuntime">The runtime the module is imported with.</param>
    public BrowserClipboard(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/HackerOs.AppSdk.Blazor/clipboard.js").AsTask());
    }

    /// <summary>Replaces the host clipboard's text.</summary>
    /// <param name="text">Text to copy.</param>
    public async ValueTask WriteTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        IJSObjectReference module = await _moduleTask.Value.ConfigureAwait(false);
        await module.InvokeVoidAsync("writeText", text).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            IJSObjectReference module = await _moduleTask.Value.ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
    }
}
