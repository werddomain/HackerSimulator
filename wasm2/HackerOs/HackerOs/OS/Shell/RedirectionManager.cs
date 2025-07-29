using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.Shell;

/// <summary>
/// Manages I/O redirection for shell commands
/// </summary>
public class RedirectionManager : IRedirectionManager
{
    /// <inheritdoc />
    public async Task<RedirectionContext> SetupRedirectionAsync(IEnumerable<RedirectionNode> redirections, IVirtualFileSystem fileSystem)
    {
        var context = new RedirectionContext();
        
        try
        {
            foreach (var redirection in redirections)
            {
                await ProcessRedirectionNodeAsync(redirection, context, fileSystem);
            }
            
            return context;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task CleanupAsync(RedirectionContext context)
    {
        if (context != null)
        {
            // Flush any output streams before disposal
            if (context.OutputStream != null)
            {
                await context.OutputStream.FlushAsync();
            }
            
            if (context.ErrorStream != null)
            {
                await context.ErrorStream.FlushAsync();
            }
            
            context.Dispose();
        }
    }
      private async Task ProcessRedirectionNodeAsync(RedirectionNode redirection, RedirectionContext context, IVirtualFileSystem fileSystem)
    {
        switch (redirection.Type)
        {
            case RedirectionType.Input: // <
                await SetupInputRedirectionAsync(redirection, context, fileSystem);
                break;
                
            case RedirectionType.Output: // >
                await SetupOutputRedirectionAsync(redirection, context, fileSystem, append: false);
                break;
                
            case RedirectionType.Append: // >>
                await SetupOutputRedirectionAsync(redirection, context, fileSystem, append: true);
                break;
                
            case RedirectionType.ErrorOutput: // 2>
                await SetupErrorRedirectionAsync(redirection, context, fileSystem, append: false);
                break;
                
            case RedirectionType.ErrorAppend: // 2>>
                await SetupErrorRedirectionAsync(redirection, context, fileSystem, append: true);
                break;
                
            case RedirectionType.ErrorToOutput: // 2>&1
                SetupErrorToOutputRedirection(context);
                break;
                
            default:
                throw new NotSupportedException($"Redirection type {redirection.Type} is not supported");
        }
    }
    
    private async Task SetupInputRedirectionAsync(RedirectionNode redirection, RedirectionContext context, IVirtualFileSystem fileSystem)
    {
        if (string.IsNullOrEmpty(redirection.Target))
        {
            throw new ArgumentException("Input redirection target cannot be empty");
        }
        
        var file = fileSystem.GetFile(redirection.Target);
        if (file == null)
        {
            throw new FileNotFoundException($"Input file not found: {redirection.Target}");
        }
        
        var stream = await file.OpenReadAsync();
        context.InputStream = stream;
        context.StreamsToDispose.Add(stream);
    }
    
    private async Task SetupOutputRedirectionAsync(RedirectionNode redirection, RedirectionContext context, IVirtualFileSystem fileSystem, bool append)
    {
        if (string.IsNullOrEmpty(redirection.Target))
        {
            throw new ArgumentException("Output redirection target cannot be empty");
        }
        
        Stream stream;
        var existingFile = fileSystem.GetFile(redirection.Target);
        
        if (existingFile != null)
        {
            if (append)
            {
                stream = await existingFile.OpenAppendAsync();
            }
            else
            {
                stream = await existingFile.OpenWriteAsync();
            }
        }
        else
        {
            var directory = fileSystem.GetDirectory(Path.GetDirectoryName(redirection.Target) ?? "/");
            if (directory == null)
            {
                throw new DirectoryNotFoundException($"Directory not found for: {redirection.Target}");
            }
            
            var newFile = await directory.CreateFileAsync(Path.GetFileName(redirection.Target));
            stream = await newFile.OpenWriteAsync();
        }
        
        context.OutputStream = stream;
        context.StreamsToDispose.Add(stream);
    }
    
    private async Task SetupErrorRedirectionAsync(RedirectionNode redirection, RedirectionContext context, IVirtualFileSystem fileSystem, bool append)
    {
        if (string.IsNullOrEmpty(redirection.Target))
        {
            throw new ArgumentException("Error redirection target cannot be empty");
        }
        
        Stream stream;
        var existingFile = fileSystem.GetFile(redirection.Target);
        
        if (existingFile != null)
        {
            if (append)
            {
                stream = await existingFile.OpenAppendAsync();
            }
            else
            {
                stream = await existingFile.OpenWriteAsync();
            }
        }
        else
        {
            var directory = fileSystem.GetDirectory(Path.GetDirectoryName(redirection.Target) ?? "/");
            if (directory == null)
            {
                throw new DirectoryNotFoundException($"Directory not found for: {redirection.Target}");
            }
            
            var newFile = await directory.CreateFileAsync(Path.GetFileName(redirection.Target));
            stream = await newFile.OpenWriteAsync();
        }
        
        context.ErrorStream = stream;
        context.StreamsToDispose.Add(stream);
    }
    
    private void SetupErrorToOutputRedirection(RedirectionContext context)
    {
        // Redirect stderr to stdout - use the same stream
        context.ErrorStream = context.OutputStream;
        // Don't add to StreamsToDispose since it's the same reference as OutputStream
    }
}
