using System;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem.Security;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Class responsible for integrating the VirtualFileSystem with the security framework
    /// </summary>
    public static class VirtualFileSystemIntegration
    {
        private static FileSystemAuditLogger _auditLogger;
        private static FileSystemExecutionService _executionService;
        private static bool _isInitialized = false;
        
        /// <summary>
        /// Initializes the security integration for the virtual file system
        /// </summary>
        /// <param name="fileSystem">The virtual file system to integrate</param>
        /// <param name="quotaManager">The quota manager to use</param>
        /// <param name="policyManager">The policy manager to use</param>
        /// <returns>True if initialization was successful</returns>
        public static async Task<bool> InitializeSecurityFrameworkAsync(
            VirtualFileSystem fileSystem,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
                
            if (quotaManager == null)
                throw new ArgumentNullException(nameof(quotaManager));
                
            if (policyManager == null)
                throw new ArgumentNullException(nameof(policyManager));
                
            // Initialize the security context
            fileSystem.InitializeSecurity(quotaManager, policyManager);
            
            // Create and configure the audit logger
            _auditLogger = new FileSystemAuditLogger(fileSystem);
            
            // Initialize the execution service
            _executionService = new FileSystemExecutionService(fileSystem, _auditLogger);
            
            // Initialize special permission handling
            SpecialPermissionHandler.Initialize(fileSystem, _auditLogger);
            
            // Create the audit log directory if it doesn't exist
            string auditLogDir = "/var/log";
            if (!await fileSystem.ExistsAsync(auditLogDir))
            {
                await fileSystem.CreateDirectoryAsync(auditLogDir);
                
                // Set permissions for audit log directory
                var node = await fileSystem.GetNodeAsync(auditLogDir);
                if (node != null)
                {
                    node.OwnerId = 0; // root
                    node.GroupId = 0; // root
                    node.Permissions = new FilePermissions(7, 5, 0); // rwxr-x---
                }
            }
            
            // Subscribe to file system events
            SubscribeToFileSystemEvents(fileSystem);
            
            _isInitialized = true;
            
            return true;
        }
        
        /// <summary>
        /// Subscribes to file system events for audit logging
        /// </summary>
        /// <param name="fileSystem">The file system to subscribe to</param>
        private static void SubscribeToFileSystemEvents(VirtualFileSystem fileSystem)
        {
            if (fileSystem == null || _auditLogger == null)
                return;
                
            // Subscribe to file system events
            fileSystem.FileSystemChanged += async (sender, e) =>
            {
                // Get the user from the event if available
                User user = null;
                if (e.Context != null && e.Context.ContainsKey("User"))
                {
                    user = e.Context["User"] as User;
                }
                
                // Determine event type and severity
                AuditEventType eventType = AuditEventType.Access;
                AuditSeverity severity = AuditSeverity.Information;
                
                switch (e.EventType)
                {
                    case FileSystemEventType.FileCreated:
                    case FileSystemEventType.FileDeleted:
                    case FileSystemEventType.FileModified:
                    case FileSystemEventType.DirectoryCreated:
                    case FileSystemEventType.DirectoryDeleted:
                        eventType = AuditEventType.Modification;
                        break;
                        
                    case FileSystemEventType.PermissionChanged:
                    case FileSystemEventType.OwnershipChanged:
                        eventType = AuditEventType.PermissionChange;
                        break;
                        
                    case FileSystemEventType.PermissionDenied:
                        eventType = AuditEventType.Access;
                        severity = AuditSeverity.Warning;
                        break;
                        
                    case FileSystemEventType.QuotaExceeded:
                        eventType = AuditEventType.Quota;
                        severity = AuditSeverity.Warning;
                        break;
                        
                    case FileSystemEventType.PolicyDenied:
                        eventType = AuditEventType.Policy;
                        severity = AuditSeverity.Error;
                        break;
                        
                    case FileSystemEventType.Error:
                        severity = AuditSeverity.Error;
                        break;
                }
                
                // Log the event
                await _auditLogger.LogAsync(
                    user,
                    eventType,
                    severity,
                    e.Path,
                    e.EventType.ToString(),
                    e.EventType != FileSystemEventType.Error &&
                    e.EventType != FileSystemEventType.PermissionDenied &&
                    e.EventType != FileSystemEventType.QuotaExceeded &&
                    e.EventType != FileSystemEventType.PolicyDenied,
                    e.Message);
            };
        }
        
        /// <summary>
        /// Gets the audit logger instance
        /// </summary>
        /// <returns>The audit logger</returns>
        public static FileSystemAuditLogger GetAuditLogger()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Security framework is not initialized");
                
            return _auditLogger;
        }
        
        /// <summary>
        /// Gets the file system execution service
        /// </summary>
        /// <returns>The file system execution service</returns>
        public static FileSystemExecutionService GetExecutionService()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Security framework is not initialized");
                
            return _executionService;
        }
        
        /// <summary>
        /// Updates the VirtualFileSystem class to use secure operations by default
        /// </summary>
        /// <param name="fileSystem">The file system to update</param>
        public static void UpdateVirtualFileSystemToUseSecureOperations(VirtualFileSystem fileSystem)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
                
            if (!_isInitialized)
                throw new InvalidOperationException("Security framework is not initialized");
                
            // This method would ideally patch the VirtualFileSystem instance to use secure operations
            // In a real implementation, we would use dependency injection or method replacement
            // Since we can't modify the class at runtime, this method serves as documentation
            
            // The secure operations should be used in application code instead of the raw VirtualFileSystem methods
            Console.WriteLine("To use secure operations, call the extension methods defined in VirtualFileSystemSecurityExtensions");
        }
    }
}
