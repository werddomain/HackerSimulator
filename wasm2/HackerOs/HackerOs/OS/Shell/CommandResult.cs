using System;
using System.Collections.Generic;

namespace HackerOs.OS.Shell
{
    /// <summary>
    /// Result of command execution
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// Exit code (0 = success, non-zero = error)
        /// </summary>
        public int ExitCode { get; init; }

        /// <summary>
        /// Output data from the command
        /// </summary>
        public string Output { get; init; } = string.Empty;

        /// <summary>
        /// Error output from the command
        /// </summary>
        public string ErrorOutput { get; init; } = string.Empty;

        /// <summary>
        /// Execution time duration
        /// </summary>
        public TimeSpan ExecutionTime { get; init; }

        /// <summary>
        /// Whether the command executed successfully
        /// </summary>
        public bool IsSuccess => ExitCode == 0;

        /// <summary>
        /// Additional metadata from command execution
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = new();

        /// <summary>
        /// Creates a successful command result
        /// </summary>
        public static CommandResult Success(string output = "", TimeSpan? executionTime = null)
        {
            return new CommandResult
            {
                ExitCode = 0,
                Output = output,
                ExecutionTime = executionTime ?? TimeSpan.Zero
            };
        }

        /// <summary>
        /// Creates a failed command result
        /// </summary>
        public static CommandResult Error(int exitCode, string errorOutput = "", string output = "", TimeSpan? executionTime = null)
        {
            return new CommandResult
            {
                ExitCode = exitCode,
                Output = output,
                ErrorOutput = errorOutput,
                ExecutionTime = executionTime ?? TimeSpan.Zero
            };
        }

        /// <summary>
        /// Creates a failed command result with a generic error code
        /// </summary>
        public static CommandResult Error(string errorOutput, string output = "", TimeSpan? executionTime = null)
        {
            return Error(1, errorOutput, output, executionTime);
        }

        /// <summary>
        /// Creates a command result from exit code
        /// </summary>
        public static CommandResult FromExitCode(int exitCode, string output = "", string errorOutput = "", TimeSpan? executionTime = null)
        {
            return new CommandResult
            {
                ExitCode = exitCode,
                Output = output,
                ErrorOutput = errorOutput,
                ExecutionTime = executionTime ?? TimeSpan.Zero
            };
        }

        /// <summary>
        /// Returns a string representation of the result
        /// </summary>
        public override string ToString()
        {
            var status = IsSuccess ? "Success" : $"Error (Exit Code: {ExitCode})";
            return $"CommandResult: {status}, Output: {Output.Length} chars, ErrorOutput: {ErrorOutput.Length} chars";
        }
    }
}
