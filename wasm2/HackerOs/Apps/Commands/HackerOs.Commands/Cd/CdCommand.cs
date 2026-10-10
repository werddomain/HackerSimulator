using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Cd;

/// <summary>
/// Implements the <c>cd</c> terminal command (`P2-CMD-003`).
/// </summary>
public sealed class CdCommand : TerminalAppBase
{
    /// <summary>Initializes the command with its manifest.</summary>
    public CdCommand(AppManifest manifest) : base(manifest)
    {
    }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string targetDirectory = context.Arguments.Count > 0 ? context.Arguments[0] : "~";
        if (targetDirectory == "~")
        {
            targetDirectory = context.Environment.TryGetValue("HOME", out string? home) ? home : "/home/user";
        }

        string resolvedPath = ResolvePath(context.WorkingDirectory, targetDirectory);
        FileSystemResult<FileSystemEntrySnapshot> stat = await context.App.FileSystem.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse(resolvedPath)), cancellationToken);

        if (!stat.Succeeded || stat.Value is null)
        {
            if (stat.Error?.Code == FileSystemErrorCode.PermissionDenied || stat.Error?.Code == FileSystemErrorCode.CapabilityDenied)
            {
                context.StandardError.WriteLine($"cd: {targetDirectory}: Permission denied");
            }
            else
            {
                context.StandardError.WriteLine($"cd: {targetDirectory}: No such file or directory");
            }
            return 1;
        }

        if (stat.Value.Metadata.Kind != FileSystemEntryKind.Directory)
        {
            context.StandardError.WriteLine($"cd: {targetDirectory}: Not a directory");
            return 1;
        }

        context.StandardOutput.WriteLine(resolvedPath);
        return 0;
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
