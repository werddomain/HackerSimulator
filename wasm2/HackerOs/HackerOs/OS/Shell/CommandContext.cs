using System;
using System.Collections.Generic;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell
{
    /// <summary>
    /// Execution context for shell commands
    /// </summary>
    public class CommandContext
    {
        /// <summary>
        /// The user executing the command
        /// </summary>
        public User.User CurrentUser { get; init; } = null!;

        /// <summary>
        /// Current working directory
        /// </summary>
        public string WorkingDirectory { get; init; } = "/";

        /// <summary>
        /// Environment variables
        /// </summary>
        public Dictionary<string, string> Environment { get; init; } = new();

        /// <summary>
        /// Command execution session ID
        /// </summary>
        public string SessionId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the current user's home directory
        /// </summary>
        public string HomeDirectory => $"/home/{CurrentUser.Username}";

        /// <summary>
        /// Creates a new command context
        /// </summary>
        public CommandContext()
        {
            // Set default environment variables
            Environment["PATH"] = "/bin:/usr/bin:/usr/local/bin";
            Environment["HOME"] = HomeDirectory;
            Environment["USER"] = CurrentUser?.Username ?? "anonymous";
            Environment["PWD"] = WorkingDirectory;
            Environment["SHELL"] = "/bin/bash";
        }

        /// <summary>
        /// Creates a new command context with specific user and working directory
        /// </summary>
        public CommandContext(User.User user, string workingDirectory) : this()
        {
            CurrentUser = user;
            WorkingDirectory = workingDirectory;
            Environment["HOME"] = HomeDirectory;
            Environment["USER"] = user.Username;
            Environment["PWD"] = workingDirectory;
        }

        /// <summary>
        /// Creates a copy of the context with a new working directory
        /// </summary>
        public CommandContext WithWorkingDirectory(string workingDirectory)
        {
            var newEnv = new Dictionary<string, string>(Environment)
            {
                ["PWD"] = workingDirectory
            };

            return new CommandContext
            {
                CurrentUser = CurrentUser,
                WorkingDirectory = workingDirectory,
                Environment = newEnv,
                SessionId = SessionId
            };
        }

        /// <summary>
        /// Creates a copy of the context with additional environment variables
        /// </summary>
        public CommandContext WithEnvironment(Dictionary<string, string> additionalEnv)
        {
            var newEnv = new Dictionary<string, string>(Environment);
            foreach (var kvp in additionalEnv)
            {
                newEnv[kvp.Key] = kvp.Value;
            }

            return new CommandContext
            {
                CurrentUser = CurrentUser,
                WorkingDirectory = WorkingDirectory,
                Environment = newEnv,
                SessionId = SessionId
            };
        }
    }
}
