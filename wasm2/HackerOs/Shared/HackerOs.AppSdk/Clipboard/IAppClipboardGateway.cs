namespace HackerOs.AppSdk.Clipboard;

/// <summary>
/// Gateway for typed clipboard operations within HackerOS applications.
/// </summary>
public interface IAppClipboardGateway
{
    /// <summary>
    /// Gets text content from the clipboard.
    /// </summary>
    Task<string?> GetTextAsync();

    /// <summary>
    /// Sets text content on the clipboard.
    /// </summary>
    Task SetTextAsync(string text);

    /// <summary>
    /// Gets virtual file path references from the clipboard.
    /// </summary>
    Task<IReadOnlyList<string>> GetFileReferencesAsync();

    /// <summary>
    /// Sets virtual file path references on the clipboard.
    /// </summary>
    Task SetFileReferencesAsync(IReadOnlyList<string> filePaths);
}
