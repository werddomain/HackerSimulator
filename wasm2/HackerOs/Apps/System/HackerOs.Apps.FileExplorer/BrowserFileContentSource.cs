using HackerOs.Simulation.Abstractions.FileSystem;
using Microsoft.AspNetCore.Components.Forms;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// Wraps an IBrowserFile (from InputFile) to provide streaming to the simulated filesystem.
/// </summary>
public sealed class BrowserFileContentSource : IFileSystemContentSource
{
    private readonly IBrowserFile _file;
    private readonly long _maxAllowedSize;

    public BrowserFileContentSource(IBrowserFile file, long maxAllowedSize = 50 * 1024 * 1024)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
        _maxAllowedSize = maxAllowedSize;

        string mediaType = string.IsNullOrWhiteSpace(_file.ContentType) ? "application/octet-stream" : _file.ContentType;
        Descriptor = FileSystemContentDescriptor.Binary(mediaType);
    }

    public FileSystemContentDescriptor Descriptor { get; }

    public long? Length => _file.Size;

    public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        Stream stream = _file.OpenReadStream(_maxAllowedSize, cancellationToken);
        return new ValueTask<Stream>(stream);
    }
}
