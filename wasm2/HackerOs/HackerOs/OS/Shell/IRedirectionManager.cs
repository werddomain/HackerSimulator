using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.Shell;

/// <summary>
/// Interface for managing I/O redirection in shell commands
/// </summary>
public interface IRedirectionManager
{
    /// <summary>
    /// Processes redirection nodes and sets up appropriate streams
    /// </summary>
    Task<RedirectionContext> SetupRedirectionAsync(IEnumerable<RedirectionNode> redirections, IVirtualFileSystem fileSystem);
    
    /// <summary>
    /// Cleans up redirection resources
    /// </summary>
    Task CleanupAsync(RedirectionContext context);
}

/// <summary>
/// Context containing redirection stream information
/// </summary>
public class RedirectionContext : IDisposable
{
    public Stream? InputStream { get; set; }
    public Stream? OutputStream { get; set; }
    public Stream? ErrorStream { get; set; }
    public List<Stream> StreamsToDispose { get; } = new();
    
    public void Dispose()
    {
        foreach (var stream in StreamsToDispose)
        {
            stream?.Dispose();
        }
        StreamsToDispose.Clear();
    }
}
