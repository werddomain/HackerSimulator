using System;
using System.Threading.Tasks;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Utility class to handle temporary permission elevation for privileged operations.
    /// This provides sudo-like functionality for file system operations.
    /// </summary>
    public class RootPermissionOverride : IDisposable
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly string _originalUser;
        private readonly string _originalWorkingDirectory;
        private readonly string _operation;
        private readonly bool _logOperations;
        private readonly DateTime _startTime;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new instance of RootPermissionOverride with temporary root privileges.
        /// </summary>
        /// <param name="fileSystem">The file system to operate on</param>
        /// <param name="operation">Description of the operation for logging</param>
        /// <param name="logOperations">Whether to log operations performed with elevated privileges</param>
        public RootPermissionOverride(IVirtualFileSystem fileSystem, string operation, bool logOperations = true)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _operation = operation;
            _logOperations = logOperations;
            _startTime = DateTime.UtcNow;

            // Save original user and working directory
            _originalUser = fileSystem.CurrentUser;
            _originalWorkingDirectory = fileSystem.CurrentWorkingDirectory;

            // Elevate to root
            fileSystem.CurrentUser = "root";

            if (_logOperations)
            {
                LogElevation($"Permission elevation started for: {operation}");
            }
        }

        /// <summary>
        /// Executes an operation with elevated permissions and returns to original permissions.
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="fileSystem">The file system to operate on</param>
        /// <param name="operation">Description of the operation for logging</param>
        /// <param name="action">The action to perform with elevated permissions</param>
        /// <param name="logOperations">Whether to log operations performed with elevated privileges</param>
        /// <returns>The result of the operation</returns>
        public static T ExecuteWithElevatedPermissions<T>(
            IVirtualFileSystem fileSystem, 
            string operation, 
            Func<T> action, 
            bool logOperations = true)
        {
            using var permissionOverride = new RootPermissionOverride(fileSystem, operation, logOperations);
            return action();
        }

        /// <summary>
        /// Executes an asynchronous operation with elevated permissions and returns to original permissions.
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="fileSystem">The file system to operate on</param>
        /// <param name="operation">Description of the operation for logging</param>
        /// <param name="action">The async action to perform with elevated permissions</param>
        /// <param name="logOperations">Whether to log operations performed with elevated privileges</param>
        /// <returns>The result of the operation</returns>
        public static async Task<T> ExecuteWithElevatedPermissionsAsync<T>(
            IVirtualFileSystem fileSystem, 
            string operation, 
            Func<Task<T>> action, 
            bool logOperations = true)
        {
            using var permissionOverride = new RootPermissionOverride(fileSystem, operation, logOperations);
            return await action();
        }

        /// <summary>
        /// Logs an elevation event to the system log.
        /// </summary>
        /// <param name="message">The message to log</param>
        private void LogElevation(string message)
        {
            // In a real implementation, this would log to a secure audit log
            // For now, we'll fire a file system event
            _fileSystem.OnFileSystemEvent?.Invoke(this, new FileSystemEvent
            {
                EventType = FileSystemEventType.PermissionElevation,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Restores original permissions when the object is disposed.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Restore original user and working directory
                _fileSystem.CurrentUser = _originalUser;
                _fileSystem.CurrentWorkingDirectory = _originalWorkingDirectory;

                if (_logOperations)
                {
                    TimeSpan duration = DateTime.UtcNow - _startTime;
                    LogElevation($"Permission elevation ended for: {_operation} (Duration: {duration.TotalMilliseconds:F1}ms)");
                }

                _disposed = true;
            }
        }
    }
}
