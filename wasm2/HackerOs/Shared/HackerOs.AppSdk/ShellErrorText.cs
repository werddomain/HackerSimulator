using HackerOs.AppSdk.IO;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.AppSdk;

/// <summary>
/// Turns a <c>Hack*</c> file API failure into the short reason a Linux shell prints after
/// <c>hsh: &lt;path&gt;: </c> (the <c>strerror</c> text of the matching <c>errno</c>).
/// </summary>
/// <remarks>
/// Lives in the SDK, next to <see cref="IShellSession"/>, so built-in commands such as <c>cd</c>
/// print the same text as the shell itself without referencing Platform Core (ADR 0056, section 6).
/// </remarks>
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
