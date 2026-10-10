using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Samples.TerminalApp;

/// <summary>
/// Sample terminal application deriving <see cref="TerminalAppBase"/>.
/// </summary>
/// <remarks>
/// Demonstrates:
/// <list type="bullet">
///   <item>Reading command-line arguments and flags.</item>
///   <item>Writing to standard output and standard error streams.</item>
///   <item>Inspecting current working directory and executing VFS operations.</item>
///   <item>Observing cancellation tokens.</item>
///   <item>Returning standard exit codes (0 for success, non-zero for error).</item>
/// </list>
/// </remarks>
public class SampleTerminalApp : TerminalAppBase
{
    public SampleTerminalApp()
        : this(AppDefinitionMapper.ToManifest(new TerminalSampleApp()))
    {
    }

    public SampleTerminalApp(AppManifest manifest)
        : base(manifest)
    {
    }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Arguments.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
            context.Arguments.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            await context.StandardOutput.WriteLineAsync(
                "Usage: sample-cmd [--verbose] [message]".AsMemory(), cancellationToken);
            return 0;
        }

        bool verbose = context.Arguments.Contains("--verbose", StringComparer.OrdinalIgnoreCase);
        string message = context.Arguments.FirstOrDefault(arg => !arg.StartsWith('-')) ?? "HackerOS SDK 1.0 Sample Command Executed.";

        if (verbose)
        {
            await context.StandardOutput.WriteLineAsync(
                $"Working Directory: {context.WorkingDirectory}".AsMemory(), cancellationToken);
            await context.StandardOutput.WriteLineAsync(
                $"User ID: {context.App.UserId}".AsMemory(), cancellationToken);
        }

        await context.StandardOutput.WriteLineAsync(message.AsMemory(), cancellationToken);

        context.App.Logging.Log(
            DiagnosticSeverity.Information,
            "SampleTerminalApp executed successfully.",
            new Dictionary<string, string> { ["Message"] = message });

        return 0;
    }
}
