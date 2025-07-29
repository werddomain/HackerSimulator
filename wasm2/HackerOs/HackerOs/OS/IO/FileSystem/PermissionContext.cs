using System;
using System.Threading;
using System.Threading.Tasks;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// A context class for managing temporary permission elevations.
    /// Implements IDisposable to ensure permissions are properly restored.
    /// </summary>
    public class PermissionContext : IDisposable
    {
        // Thread-local storage for the current permission context
        private static readonly AsyncLocal<PermissionContext> _currentContext = new AsyncLocal<PermissionContext>();
        
        /// <summary>
        /// Gets the current permission context for this thread.
        /// </summary>
        public static PermissionContext Current => _currentContext.Value;
        
        /// <summary>
        /// The original user before elevation.
        /// </summary>
        public UserEntity OriginalUser { get; }
        
        /// <summary>
        /// The elevated user during the context.
        /// </summary>
        public UserEntity ElevatedUser { get; }
        
        /// <summary>
        /// The path of the file/directory that caused the elevation.
        /// </summary>
        public string OperationPath { get; }
        
        /// <summary>
        /// The reason for the permission elevation.
        /// </summary>
        public string ElevationReason { get; }
        
        /// <summary>
        /// When the elevation occurred.
        /// </summary>
        public DateTime ElevationTime { get; }
        
        /// <summary>
        /// Whether this context is active.
        /// </summary>
        public bool IsActive { get; private set; }
        
        /// <summary>
        /// Private constructor to force use of factory methods.
        /// </summary>
        private PermissionContext(
            UserEntity originalUser, 
            UserEntity elevatedUser, 
            string path, 
            string reason)
        {
            OriginalUser = originalUser;
            ElevatedUser = elevatedUser;
            OperationPath = path;
            ElevationReason = reason;
            ElevationTime = DateTime.UtcNow;
            IsActive = true;
            
            // Set this as the current context
            var previousContext = _currentContext.Value;
            _currentContext.Value = this;
            
            // Track the previous context for nested elevations
            PreviousContext = previousContext;
        }
        
        /// <summary>
        /// The previous context, if this is a nested elevation.
        /// </summary>
        private PermissionContext PreviousContext { get; }
        
        /// <summary>
        /// Creates a new permission context with elevated permissions.
        /// </summary>
        /// <param name="targetUser">The user to elevate to</param>
        /// <param name="currentUser">The current user</param>
        /// <param name="path">The path causing elevation</param>
        /// <param name="reason">The reason for elevation</param>
        /// <returns>A new permission context</returns>
        public static PermissionContext ElevateToUser(
            UserEntity targetUser,
            UserEntity currentUser,
            string path,
            string reason)
        {
            if (targetUser == null)
                throw new ArgumentNullException(nameof(targetUser));
                
            if (currentUser == null)
                throw new ArgumentNullException(nameof(currentUser));
                
            // Create a new context
            return new PermissionContext(currentUser, targetUser, path, reason);
        }
        
        /// <summary>
        /// Gets the effective user for the current context.
        /// </summary>
        /// <param name="defaultUser">The default user if no context is active</param>
        /// <returns>The effective user for permission checks</returns>
        public static UserEntity GetEffectiveUser(UserEntity defaultUser)
        {
            var context = Current;
            return context != null && context.IsActive ? context.ElevatedUser : defaultUser;
        }
        
        /// <summary>
        /// Creates an audit log entry for permission elevation.
        /// </summary>
        private void LogElevationEvent(string action)
        {
            // We need a more direct way to get the file system instance
            // This would normally be injected or accessed through a service locator
            
            // For now, we'll just log to the console as a fallback
            Console.WriteLine($"Permission context {action}: {ElevationReason}, " +
                             $"User {OriginalUser.Username} (uid={OriginalUser.UserId}) " +
                             $"to {ElevatedUser.Username} (uid={ElevatedUser.UserId})");
            
            // In a real implementation, this would be properly logged to the file system
        }
        
        
        /// <summary>
        /// Disposes the context and restores original permissions.
        /// </summary>
        public void Dispose()
        {
            if (!IsActive)
                return;
                
            // Mark as inactive
            IsActive = false;
            
            // Restore the previous context
            _currentContext.Value = PreviousContext;
            
            // Log the de-elevation
            LogElevationEvent("ended");
        }
        
        /// <summary>
        /// Validates that the user has appropriate permission to perform an elevated operation.
        /// </summary>
        /// <param name="operation">The operation being performed</param>
        /// <param name="path">The path being accessed</param>
        /// <returns>True if the operation is permitted; otherwise, false</returns>
        public bool ValidateOperation(string operation, string path)
        {
            // Implement security validation here
            // For example, check if the path is within allowed scope
            
            // Basic validation: ensure the operation is on the same path or a subpath
            if (!path.StartsWith(OperationPath) && !OperationPath.StartsWith(path))
            {
                // Log attempted scope escape
                LogSecurityViolation(operation, path, "path scope violation");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Logs a security violation attempt.
        /// </summary>
        private void LogSecurityViolation(string operation, string path, string reason)
        {
            // Similar to LogElevationEvent, we'll use console logging as a fallback
            Console.WriteLine($"Security violation: {operation} on {path}, " +
                             $"Reason: {reason}, " +
                             $"Context: {ElevationReason}");
            
            // In a real implementation, this would be properly logged to the file system
        }
    }
}
