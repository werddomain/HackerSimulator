using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Dirname;

/// <summary><c>dirname NAME...</c> (ADR 0064): each name without its last component, <c>.</c> when there is no slash.</summary>
public sealed class DirnameCommand(AppManifest manifest) : TerminalAppBase(manifest)
{
    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Arguments.Count == 0)
        {
            await context.StandardError.WriteLineAsync("dirname: missing operand");
            return 1;
        }

        foreach (string name in context.Arguments)
        {
            await context.StandardOutput.WriteLineAsync(Parent(name));
        }

        return 0;
    }

    /// <summary>POSIX <c>dirname</c> as text: <c>/a/b/</c> → <c>/a</c>, <c>a</c> → <c>.</c>, <c>/</c> → <c>/</c>.</summary>
    internal static string Parent(string name)
    {
        string trimmed = name.TrimEnd('/');
        if (trimmed.Length == 0)
        {
            return name.StartsWith('/') ? "/" : ".";
        }

        int slash = trimmed.LastIndexOf('/');
        if (slash < 0)
        {
            return ".";
        }

        string parent = trimmed[..slash].TrimEnd('/');
        return parent.Length == 0 ? "/" : parent;
    }
}
