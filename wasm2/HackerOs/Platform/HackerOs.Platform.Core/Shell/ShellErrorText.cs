using HackerOs.AppSdk.IO;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// Turns a <c>Hack*</c> file API failure into the short reason a Linux shell prints after
/// <c>hsh: &lt;path&gt;: </c> (the <c>strerror</c> text of the matching <c>errno</c>).
/// </summary>
public static class ShellErrorText
{
    /// <summary>Gets the reason for <paramref name="exception"/>, e.g. "No such file or directory".</summary>
    public static string Describe(Exception exception) => exception switch
    {
        HackUnauthorizedAccessException { ErrorCode: FileSystemErrorCode.NotFile } => "Is a directory",
        UnauthorizedAccessException => "Permission denied",
        HackFileNotFoundException or HackDirectoryNotFoundException => "No such file or directory",
        HackIOException { ErrorCode: FileSystemErrorCode.NotDirectory } => "Not a directory",
        ArgumentException => "Invalid path",
        _ => exception.Message
    };
}
