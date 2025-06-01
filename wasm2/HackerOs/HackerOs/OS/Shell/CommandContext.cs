using System;
using System.Collections.Generic;
using HackerOs.OS.User;
using HackerOs.IO.FileSystem;

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
        /// The user executing the command (alias for CurrentUser for compatibility)
        /// </summary>
        public User.User User => CurrentUser;

        /// <summary>
        /// Current working directory
        /// </summary>
        public string WorkingDirectory { get; init; } = "/";

        /// <summary>
        /// Current working directory (alias for WorkingDirectory for compatibility)
        /// </summary>
        public string CurrentWorkingDirectory => WorkingDirectory;

        /// <summary>
        /// Environment variables
        /// </summary>
        public Dictionary<string, string> Environment { get; init; } = new();

        /// <summary>
        /// Environment variables (alias for Environment for compatibility)
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables => Environment;        /// <summary>
        /// File system reference
        /// </summary>
        public IVirtualFileSystem FileSystem => Shell?.FileSystem ?? null!;

        /// <summary>
        /// Shell reference
        /// </summary>
        public Shell Shell { get; init; } = null!;

        /// <summary>
        /// User session reference
        /// </summary>
        public HackerOs.OS.User.UserSession UserSession { get; init; } = null!;

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
        }        /// <summary>
        /// Creates a new command context with specific user and working directory
        /// </summary>
        public CommandContext(User.User user, string workingDirectory) : this()
        {
            CurrentUser = user;
            WorkingDirectory = workingDirectory;
            Environment["HOME"] = HomeDirectory;
            Environment["USER"] = user.Username;
            Environment["PWD"] = workingDirectory;
        }        /// <summary>
        /// Creates a new command context with shell, working directory, environment, and user session
        /// </summary>
        public CommandContext(Shell shell, string workingDirectory, Dictionary<string, string> environmentVariables, HackerOs.OS.User.UserSession userSession)
        {
            Shell = shell;
            WorkingDirectory = workingDirectory;
            Environment = new Dictionary<string, string>(environmentVariables);
            UserSession = userSession;
            CurrentUser = userSession.User;
            // FileSystem will be set via the Shell property when needed
            
            // Update environment with current context
            Environment["HOME"] = HomeDirectory;
            Environment["USER"] = CurrentUser.Username;
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
