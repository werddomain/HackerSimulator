using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Commands.Common;

namespace HackerOs.Commands.Head;

/// <summary>
/// Implements the <c>head</c> terminal command (`P4-W5-CMD-003`, options extended by ADR 0064):
/// the first 10 lines of each file or of standard input; <c>-n N</c> (or <c>-N</c>) lines,
/// <c>-n -N</c> all but the last N, <c>-c N</c> characters, <c>-q</c>/<c>-v</c> for the
/// <c>==&gt; file &lt;==</c> headers. Reading standard input stops after N lines, so
/// <c>yes | head</c> ends.
/// </summary>
public sealed class HeadCommand : TerminalAppBase
{
    public HeadCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        long count = 10;
        bool bytes = false, allButLast = false, quiet = false, verbose = false;
        List<string> files = [];
        IReadOnlyList<string> args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            string? value = null;
            if (a is "-n" or "-c" or "--lines" or "--bytes" && i + 1 < args.Count)
            {
                value = args[++i];
                bytes = a is "-c" or "--bytes";
            }
            else if ((a.StartsWith("-n", StringComparison.Ordinal) || a.StartsWith("-c", StringComparison.Ordinal)) && a.Length > 2)
            {
                value = a[2..];
                bytes = a[1] == 'c';
            }
            else if (a.Length > 1 && a[0] == '-' && a[1..].All(char.IsAsciiDigit))
            {
                value = a[1..];
            }
            else if (a is "-q" or "--quiet" or "--silent")
            {
                quiet = true;
                continue;
            }
            else if (a is "-v" or "--verbose")
            {
                verbose = true;
                continue;
            }
            else
            {
                files.Add(a);
                continue;
            }

            allButLast = value.StartsWith('-');
            if (!long.TryParse(value.TrimStart('-', '+'), out count))
            {
                context.StandardError.WriteLine($"head: invalid number of {(bytes ? "bytes" : "lines")}: '{value}'");
                return 1;
            }
        }

        if (files.Count == 0)
        {
            files.Add("-");
        }

        int status = 0;
        bool headers = verbose || (files.Count > 1 && !quiet);
        for (int index = 0; index < files.Count; index++)
        {
            string file = files[index];
            if (file == "-" && !bytes && !allButLast)
            {
                // Stream standard input and stop early: the writer upstream then gets "Broken pipe".
                if (headers)
                {
                    await context.StandardOutput.WriteLineAsync($"{(index > 0 ? "\n" : string.Empty)}==> standard input <==");
                }

                long written = 0;
                while (written < count && await context.StandardInput.ReadLineAsync(cancellationToken) is { } line)
                {
                    await context.StandardOutput.WriteLineAsync(line);
                    written++;
                }

                continue;
            }

            string? text = await CommandIO.ReadTextAsync(context, file, "head", cancellationToken);
            if (text is null)
            {
                status = 1;
                continue;
            }

            if (headers)
            {
                await context.StandardOutput.WriteLineAsync($"{(index > 0 ? "\n" : string.Empty)}==> {(file == "-" ? "standard input" : file)} <==");
            }

            if (bytes)
            {
                int length = (int)Math.Clamp(allButLast ? text.Length - count : count, 0, text.Length);
                await context.StandardOutput.WriteAsync(text[..length]);
                continue;
            }

            List<string> lines = CommandIO.SplitLines(text);
            int take = (int)Math.Clamp(allButLast ? lines.Count - count : count, 0, lines.Count);
            foreach (string line in lines.Take(take))
            {
                await context.StandardOutput.WriteLineAsync(line);
            }
        }

        return status;
    }
}
