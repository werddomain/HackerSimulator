using System.Text.RegularExpressions;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.IO;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Commands.XdgOpen;

/// <summary>
/// Implements <c>xdg-open &lt;file|folder|URI&gt;</c> (ADR 0096, section 3), modelled on freedesktop's
/// <c>xdg-open</c>: a path opens with its file association (a folder in the default file manager,
/// <c>inode/directory</c>), a URI with the handler of its scheme (<c>x-scheme-handler/&lt;scheme&gt;</c>,
/// the web browser for <c>http</c>/<c>https</c>). It returns as soon as the request is sent.
/// </summary>
/// <remarks>
/// Exit codes follow <c>xdg-open</c>: 0 sent, 1 bad command line, 3 no application handles it,
/// 4 the file does not exist or the action failed (several apps and no default included: a terminal
/// has no chooser and the OS never guesses).
/// </remarks>
public sealed partial class XdgOpenCommand : TerminalAppBase
{
    private const string Usage = "usage: xdg-open { file | URL }";

    /// <summary>Initializes the command with its manifest.</summary>
    /// <param name="manifest">This command's manifest.</param>
    public XdgOpenCommand(AppManifest manifest) : base(manifest) { }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count != 1 || string.IsNullOrWhiteSpace(context.Arguments[0]))
        {
            context.StandardError.WriteLine($"xdg-open: {(context.Arguments.Count > 1 ? "unexpected argument" : "no file or URL given")}");
            context.StandardError.WriteLine(Usage);
            return 1;
        }

        string target = context.Arguments[0];
        if (target is "--help" or "-h")
        {
            context.StandardOutput.WriteLine(Usage);
            return 0;
        }

        try
        {
            return SchemePattern().IsMatch(target) && Uri.TryCreate(target, UriKind.Absolute, out Uri? uri)
                ? await OpenUriAsync(context, uri, cancellationToken)
                : await OpenPathAsync(context, target, cancellationToken);
        }
        catch (AppGatewayAccessDeniedException exception)
        {
            context.StandardError.WriteLine($"xdg-open: {exception.Message}");
            return 4;
        }
    }

    private static async Task<int> OpenPathAsync(TerminalExecutionContext context, string target, CancellationToken cancellationToken)
    {
        VirtualPath path;
        try
        {
            path = VirtualPath.Parse(HackPath.GetFullPath(target));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            context.StandardError.WriteLine($"xdg-open: invalid path '{target}'");
            return 1;
        }

        bool isDirectory = await HackDirectory.ExistsAsync(path.Value, cancellationToken);
        if (!isDirectory && !await HackFile.ExistsAsync(path.Value, cancellationToken))
        {
            context.StandardError.WriteLine($"xdg-open: file '{target}' does not exist");
            return 4;
        }

        // Like Linux, a folder is a file of type inode/directory: its handler is the file manager.
        AppIntentOpenFileResult result = await context.App.Intents.OpenFileAsync(
            path, isDirectory ? "inode/directory" : null, cancellationToken);
        return result.Outcome switch
        {
            AppIntentOpenFileOutcome.Opened => 0,
            AppIntentOpenFileOutcome.NoHandler => Fail(context, 3, $"no application can open '{target}'"),
            AppIntentOpenFileOutcome.ChooserRequired => Fail(
                context, 4,
                $"several applications can open '{target}' ({string.Join(", ", result.CandidateAppIds ?? [])}) and none is the default; " +
                "choose one in Settings > File Associations, or run: launch <app-id> " + path.Value),
            _ => Fail(context, 4, $"'{target}' could not be opened ({result.ErrorCode})")
        };
    }

    private static async Task<int> OpenUriAsync(TerminalExecutionContext context, Uri uri, CancellationToken cancellationToken)
    {
        AppIntentResult result = await context.App.Intents.OpenUriAsync(uri, cancellationToken: cancellationToken);
        return result.Outcome switch
        {
            AppIntentOutcome.Dispatched => 0,
            AppIntentOutcome.NoHandler => Fail(context, 3, $"no application handles '{uri.Scheme}:' URIs"),
            AppIntentOutcome.NotFound => Fail(context, 4, $"file '{uri.LocalPath}' does not exist"),
            AppIntentOutcome.ChooserRequired => Fail(
                context, 4,
                $"several applications handle '{uri.Scheme}:' URIs ({string.Join(", ", result.CandidateAppIds ?? [])}) and none is the default"),
            AppIntentOutcome.Invalid => Fail(context, 1, $"invalid URI '{uri.OriginalString}'"),
            _ => Fail(context, 4, $"'{uri.OriginalString}' could not be opened ({result.ErrorCode})")
        };
    }

    private static int Fail(TerminalExecutionContext context, int exitCode, string message)
    {
        context.StandardError.WriteLine($"xdg-open: {message}");
        return exitCode;
    }

    /// <summary>A URI scheme of two characters or more, so a path never reads as one.</summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]+:", RegexOptions.CultureInvariant)]
    private static partial Regex SchemePattern();
}
