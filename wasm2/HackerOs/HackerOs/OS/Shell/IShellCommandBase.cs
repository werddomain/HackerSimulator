using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell
{
    /// <summary>
    /// Base class for commands that use the IShellContext pattern but need to work with CommandBase
    /// </summary>
    public abstract class IShellCommandBase : CommandBase
    {
        /// <summary>
        /// Execute command using the IShellContext pattern
        /// </summary>
        public abstract Task<CommandResult> ExecuteAsync(string[] args, IShellContext context);

        /// <summary>
        /// Get command completions using the IShellContext pattern
        /// </summary>
        public virtual Task<IEnumerable<string>> GetCompletionsAsync(string[] args, int cursorPosition, IShellContext context)
        {
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }

        /// <summary>
        /// Implements the CommandBase ExecuteAsync by bridging to IShellContext pattern
        /// </summary>
        public override async Task<int> ExecuteAsync(
            CommandContext context,
            string[] args,
            Stream stdin,
            Stream stdout,
            Stream stderr,
            CancellationToken cancellationToken = default)
        {
            // Create IShellContext from CommandContext and streams
            var shellContext = new ShellContext(
                context.User,
                context.WorkingDirectory,
                context.FileSystem,
                stdin,
                stdout,
                stderr)
            {
                Environment = context.Environment
            };

            try
            {
                var result = await ExecuteAsync(args, shellContext);
                return result.ExitCode;
            }
            catch (Exception)
            {
                return 1; // Error exit code
            }
        }

        /// <summary>
        /// Implements the CommandBase GetCompletionsAsync by bridging to IShellContext pattern
        /// </summary>
        public override async Task<IEnumerable<string>> GetCompletionsAsync(
            CommandContext context,
            string[] args,
            string currentArg)
        {
            // Create IShellContext from CommandContext
            var shellContext = new ShellContext(
                context.User,
                context.WorkingDirectory,
                context.FileSystem,
                Stream.Null,
                Stream.Null,
                Stream.Null)
            {
                Environment = context.Environment
            };

            try
            {
                return await GetCompletionsAsync(args, args.Length - 1, shellContext);
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
