using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Cat;

/// <summary>
/// Implements the <c>cat</c> terminal command (`P2-CMD-004`).
/// </summary>
public sealed class CatCommand : TerminalAppBase
{
    /// <summary>Initializes the command with its manifest.</summary>
    public CatCommand(AppManifest manifest) : base(manifest)
    {
    }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine("cat: missing file argument");
            return 1;
        }

        int exitCode = 0;
        foreach (string fileArg in context.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string resolvedPath = ResolvePath(context.WorkingDirectory, fileArg);
            FileSystemResult<FileSystemContentReadHandle> readResult = await context.App.FileSystem.ReadAsync(
                new FileSystemReadRequest(VirtualPath.Parse(resolvedPath)), cancellationToken);

            if (!readResult.Succeeded || readResult.Value is null)
            {
                if (readResult.Error?.Code == FileSystemErrorCode.PermissionDenied || readResult.Error?.Code == FileSystemErrorCode.CapabilityDenied)
                {
                    context.StandardError.WriteLine($"cat: {fileArg}: Permission denied");
                }
                else
                {
                    context.StandardError.WriteLine($"cat: {fileArg}: No such file or directory");
                }
                exitCode = 1;
            }
            else
            {
                await using FileSystemContentReadHandle handle = readResult.Value;
                using StreamReader reader = new(handle.Content, System.Text.Encoding.UTF8, leaveOpen: true);
                string text = await reader.ReadToEndAsync(cancellationToken);
                context.StandardOutput.Write(text);
                if (!text.EndsWith('\n'))
                {
                    context.StandardOutput.WriteLine();
                }
            }
        }

        return exitCode;
    }

    private static string ResolvePath(string cwd, string target)
    {
        if (target.StartsWith('/'))
        {
            return NormalizePath(target);
        }
        return NormalizePath($"{cwd.TrimEnd('/')}/{target}");
    }

    private static string NormalizePath(string rawPath)
    {
        string[] segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Stack<string> stack = new();

        foreach (string segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
            else
            {
                stack.Push(segment);
            }
        }

        return "/" + string.Join('/', stack.ToArray().Reverse());
    }
}
