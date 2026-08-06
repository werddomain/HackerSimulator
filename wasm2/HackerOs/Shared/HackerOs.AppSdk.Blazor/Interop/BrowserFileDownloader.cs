using Microsoft.JSInterop;

namespace HackerOs.AppSdk.Blazor.Interop;

/// <summary>
/// Helper service for downloading files to the host OS using JavaScript isolation.
/// </summary>
public sealed class BrowserFileDownloader : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public BrowserFileDownloader(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/HackerOs.AppSdk.Blazor/download.js").AsTask());
    }

    /// <summary>
    /// Downloads a file from the provided stream to the client browser.
    /// </summary>
    /// <param name="fileName">The name of the file to download.</param>
    /// <param name="stream">The file stream.</param>
    public async ValueTask DownloadFileFromStreamAsync(string fileName, Stream stream)
    {
        IJSObjectReference module = await _moduleTask.Value;
        using var streamRef = new DotNetStreamReference(stream: stream);
        await module.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            IJSObjectReference module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
