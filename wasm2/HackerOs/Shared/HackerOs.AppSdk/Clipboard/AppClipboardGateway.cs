namespace HackerOs.AppSdk.Clipboard;

/// <summary>
/// In-memory implementation of the typed clipboard gateway.
/// </summary>
public sealed class AppClipboardGateway : IAppClipboardGateway
{
    private string? _textContent;
    private readonly List<string> _fileReferences = new();

    public Task<string?> GetTextAsync() => Task.FromResult(_textContent);

    public Task SetTextAsync(string text)
    {
        _textContent = text;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetFileReferencesAsync()
    {
        IReadOnlyList<string> refs = _fileReferences.AsReadOnly();
        return Task.FromResult(refs);
    }

    public Task SetFileReferencesAsync(IReadOnlyList<string> filePaths)
    {
        _fileReferences.Clear();
        if (filePaths != null)
        {
            _fileReferences.AddRange(filePaths);
        }
        return Task.CompletedTask;
    }
}
