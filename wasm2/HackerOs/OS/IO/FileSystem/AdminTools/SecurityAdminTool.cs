using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem.AdminTools
{
    /// <summary>
    /// Administrative tool for file system security management
    /// </summary>
    public class SecurityAdminTool
    {
        private readonly VirtualFileSystem _fileSystem;
        private readonly FileSystemAuditLogger _auditLogger;
        
        /// <summary>
        /// Creates a new instance of the SecurityAdminTool
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="auditLogger">The audit logger</param>
        public SecurityAdminTool(VirtualFileSystem fileSystem, FileSystemAuditLogger auditLogger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }
        
        /// <summary>
        /// Gets a security report for a path
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="user">The user performing the check</param>
        /// <param name="recursive">Whether to check recursively</param>
        /// <returns>A formatted security report</returns>
        public async Task<string> GetSecurityReportAsync(string path, User user, bool recursive = false)
        {
            try
            {
                // Check if path exists
                if (!await _fileSystem.ExistsAsync(path))
                    return $"Path not found: {path}";
                    
                var sb = new StringBuilder();
                sb.AppendLine($"Security Report for: {path}");
                sb.AppendLine("===============================");
                sb.AppendLine();
                
                // Get the node
                var node = await _fileSystem.GetNodeAsync(path);
                if (node == null)
                    return $"Failed to get node information for: {path}";
                    
                // Get owner and group information
                string ownerName = UserManager.Instance.GetUserNameById(node.OwnerId);
                string groupName = GroupManager.Instance.GetGroupNameById(node.GroupId);
                
                sb.AppendLine($"Type: {(node.IsDirectory ? "Directory" : "File")}");
                sb.AppendLine($"Owner: {ownerName} (ID: {node.OwnerId})");
                sb.AppendLine($"Group: {groupName} (ID: {node.GroupId})");
                sb.AppendLine($"Permissions: {node.Permissions} ({node.Permissions.ToOctalString()})");
                
                // Check special bits
                bool hasSetUID = node.Permissions.HasSetUID();
                bool hasSetGID = node.Permissions.HasSetGID();
                bool hasSticky = node.Permissions.HasStickyBit();
                
                if (hasSetUID || hasSetGID || hasSticky)
                {
                    sb.AppendLine("Special Bits:");
                    if (hasSetUID) sb.AppendLine("- SetUID: Enabled");
                    if (hasSetGID) sb.AppendLine("- SetGID: Enabled");
                    if (hasSticky) sb.AppendLine("- Sticky: Enabled");
                }
                
                sb.AppendLine();
                
                // Check permissions for the current user
                sb.AppendLine("Access Rights for Current User:");
                sb.AppendLine("------------------------------");
                
                bool canRead = await _fileSystem.CheckAccessAsync(path, user, FileAccessMode.Read);
                bool canWrite = await _fileSystem.CheckAccessAsync(path, user, FileAccessMode.Write);
                bool canExecute = await _fileSystem.CheckAccessAsync(path, user, FileAccessMode.Execute);
                
                sb.AppendLine($"Read: {(canRead ? "Allowed" : "Denied")}");
                sb.AppendLine($"Write: {(canWrite ? "Allowed" : "Denied")}");
                sb.AppendLine($"Execute/Traverse: {(canExecute ? "Allowed" : "Denied")}");
                
                sb.AppendLine();
                
                // Get recent audit log entries for this path
                sb.AppendLine("Recent Security Events:");
                sb.AppendLine("----------------------");
                
                var auditEntries = _auditLogger.GetFilteredMemoryLog(e => e.Path == path)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(10)
                    .ToList();
                    
                if (auditEntries.Count == 0)
                {
                    sb.AppendLine("No recent security events for this path.");
                }
                else
                {
                    foreach (var entry in auditEntries)
                    {
                        sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.Username} - {entry.Operation} - {(entry.Success ? "Success" : "Denied: " + entry.FailureReason)}");
                    }
                }
                
                // If directory and recursive, process children
                if (node.IsDirectory && recursive)
                {
                    sb.AppendLine();
                    sb.AppendLine("Child Items Security Summary:");
                    sb.AppendLine("----------------------------");
                    
                    var children = await _fileSystem.ListDirectoryAsync(path);
                    foreach (var child in children)
                    {
                        string childPath = System.IO.Path.Combine(path, child.Name).Replace("\\", "/");
                        string childOwnerName = UserManager.Instance.GetUserNameById(child.OwnerId);
                        string childGroupName = GroupManager.Instance.GetGroupNameById(child.GroupId);
                        
                        sb.AppendLine($"{child.Name} ({(child.IsDirectory ? "Dir" : "File")}):");
                        sb.AppendLine($"  Owner: {childOwnerName}, Group: {childGroupName}, Permissions: {child.Permissions}");
                        
                        bool childCanRead = await _fileSystem.CheckAccessAsync(childPath, user, FileAccessMode.Read);
                        bool childCanWrite = await _fileSystem.CheckAccessAsync(childPath, user, FileAccessMode.Write);
                        bool childCanExecute = await _fileSystem.CheckAccessAsync(childPath, user, FileAccessMode.Execute);
                        
                        sb.AppendLine($"  Access: {(childCanRead ? "R" : "-")}{(childCanWrite ? "W" : "-")}{(childCanExecute ? "X" : "-")}");
                    }
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generating security report: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Fixes permissions for a path
        /// </summary>
        /// <param name="path">The path to fix</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="recursive">Whether to fix recursively</param>
        /// <param name="template">Optional permission template to apply</param>
        /// <returns>Results of the permission fixing operation</returns>
        public async Task<string> FixPermissionsAsync(
            string path, 
            User user, 
            bool recursive = false,
            PermissionTemplate? template = null)
        {
            try
            {
                // Check if path exists
                if (!await _fileSystem.ExistsAsync(path))
                    return $"Path not found: {path}";
                    
                // Get the node
                var node = await _fileSystem.GetNodeAsync(path);
                if (node == null)
                    return $"Failed to get node information for: {path}";
                    
                // Check if user has permission to change permissions
                if (user.Id != 0 && user.Id != node.OwnerId)
                {
                    return $"Permission denied: Only root or the owner can change permissions for {path}";
                }
                
                var sb = new StringBuilder();
                sb.AppendLine($"Permission Fix Report for: {path}");
                sb.AppendLine("==================================");
                sb.AppendLine();
                
                // Apply permission template if specified
                if (template.HasValue)
                {
                    FilePermissions permissions = template.Value.ToPermissions();
                    
                    // Apply permissions
                    if (node.IsDirectory)
                    {
                        var result = await _fileSystem.SetDirectoryPermissionsSecurelyAsync(
                            path, permissions, user, recursive);
                            
                        sb.AppendLine($"Applied {template.Value} template ({permissions}) to directory: {(result ? "Success" : "Failed")}");
                    }
                    else
                    {
                        var result = await _fileSystem.SetFilePermissionsSecurelyAsync(
                            path, permissions, user);
                            
                        sb.AppendLine($"Applied {template.Value} template ({permissions}) to file: {(result ? "Success" : "Failed")}");
                    }
                }
                else
                {
                    // Check and fix common permission issues
                    
                    // For directories
                    if (node.IsDirectory)
                    {
                        // Ensure directories have execute bit for owner
                        if (!node.Permissions.OwnerCanExecute)
                        {
                            var updatedPermissions = node.Permissions.Clone();
                            updatedPermissions.OwnerCanExecute = true;
                            
                            var result = await _fileSystem.SetDirectoryPermissionsSecurelyAsync(
                                path, updatedPermissions, user, false);
                                
                            sb.AppendLine($"Fixed owner execute permission for directory: {(result ? "Success" : "Failed")}");
                        }
                        
                        // If recursive, process children with appropriate templates
                        if (recursive)
                        {
                            var children = await _fileSystem.ListDirectoryAsync(path);
                            int fixedFiles = 0;
                            int fixedDirs = 0;
                            
                            foreach (var child in children)
                            {
                                string childPath = System.IO.Path.Combine(path, child.Name).Replace("\\", "/");
                                
                                if (child.IsDirectory)
                                {
                                    // Recursively process subdirectories
                                    await FixPermissionsAsync(childPath, user, true, null);
                                    fixedDirs++;
                                }
                                else
                                {
                                    // For files, apply appropriate permissions based on extension
                                    FilePermissions childPermissions = child.Permissions.Clone();
                                    
                                    // Make executable files executable
                                    if (child.Name.EndsWith(".sh") || child.Name.EndsWith(".exe") || 
                                        child.Name.EndsWith(".dll") || child.Name.EndsWith(".com"))
                                    {
                                        childPermissions.OwnerCanExecute = true;
                                        childPermissions.GroupCanExecute = true;
                                        
                                        var result = await _fileSystem.SetFilePermissionsSecurelyAsync(
                                            childPath, childPermissions, user);
                                            
                                        if (result)
                                            fixedFiles++;
                                    }
                                }
                            }
                            
                            sb.AppendLine($"Recursively processed: {fixedDirs} directories and {fixedFiles} files");
                        }
                    }
                    // For files
                    else
                    {
                        // Make executable files executable if they have the right extension
                        if (path.EndsWith(".sh") || path.EndsWith(".exe") || 
                            path.EndsWith(".dll") || path.EndsWith(".com"))
                        {
                            var updatedPermissions = node.Permissions.Clone();
                            updatedPermissions.OwnerCanExecute = true;
                            
                            var result = await _fileSystem.SetFilePermissionsSecurelyAsync(
                                path, updatedPermissions, user);
                                
                            sb.AppendLine($"Fixed execute permission for executable file: {(result ? "Success" : "Failed")}");
                        }
                    }
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error fixing permissions: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Sets secure ownership for a path
        /// </summary>
        /// <param name="path">The path to update</param>
        /// <param name="ownerId">The new owner ID</param>
        /// <param name="groupId">The new group ID</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="recursive">Whether to apply recursively</param>
        /// <returns>Results of the ownership operation</returns>
        public async Task<string> SetSecureOwnershipAsync(
            string path,
            int ownerId,
            int groupId,
            User user,
            bool recursive = false)
        {
            try
            {
                // Check if path exists
                if (!await _fileSystem.ExistsAsync(path))
                    return $"Path not found: {path}";
                    
                // Verify the user and group exist
                string ownerName = UserManager.Instance.GetUserNameById(ownerId);
                if (string.IsNullOrEmpty(ownerName))
                    return $"User with ID {ownerId} does not exist";
                    
                string groupName = GroupManager.Instance.GetGroupNameById(groupId);
                if (string.IsNullOrEmpty(groupName))
                    return $"Group with ID {groupId} does not exist";
                    
                var sb = new StringBuilder();
                sb.AppendLine($"Ownership Change Report for: {path}");
                sb.AppendLine("==================================");
                sb.AppendLine();
                sb.AppendLine($"New Owner: {ownerName} (ID: {ownerId})");
                sb.AppendLine($"New Group: {groupName} (ID: {groupId})");
                sb.AppendLine();
                
                // Get the node
                var node = await _fileSystem.GetNodeAsync(path);
                bool isDirectory = node?.IsDirectory ?? false;
                
                // Set ownership
                bool result;
                if (isDirectory)
                {
                    result = await _fileSystem.SetDirectoryOwnershipSecurelyAsync(
                        path, ownerId, groupId, user, recursive);
                }
                else
                {
                    result = await _fileSystem.SetFileOwnershipSecurelyAsync(
                        path, ownerId, groupId, user);
                }
                
                sb.AppendLine($"Changed ownership: {(result ? "Success" : "Failed")}");
                
                if (recursive && isDirectory)
                {
                    sb.AppendLine("Recursively applied to all contents.");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error changing ownership: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Gets audit log entries for a path
        /// </summary>
        /// <param name="path">The path to get audit logs for</param>
        /// <param name="maxEntries">The maximum number of entries to return</param>
        /// <returns>Formatted audit log entries</returns>
        public string GetAuditLog(string path, int maxEntries = 50)
        {
            try
            {
                var entries = _auditLogger.GetFilteredMemoryLog(e => 
                    string.IsNullOrEmpty(path) || e.Path.StartsWith(path))
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxEntries)
                    .ToList();
                    
                if (entries.Count == 0)
                {
                    return $"No audit log entries found for path: {path}";
                }
                
                var sb = new StringBuilder();
                sb.AppendLine($"Audit Log for: {(string.IsNullOrEmpty(path) ? "All Paths" : path)}");
                sb.AppendLine("=========================================");
                sb.AppendLine();
                
                foreach (var entry in entries)
                {
                    sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Severity}] User: {entry.Username}");
                    sb.AppendLine($"  Path: {entry.Path}");
                    sb.AppendLine($"  Operation: {entry.Operation}");
                    sb.AppendLine($"  Result: {(entry.Success ? "Success" : "Failed - " + entry.FailureReason)}");
                    sb.AppendLine();
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error retrieving audit log: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Gets a summary of security issues in the file system
        /// </summary>
        /// <param name="user">The user performing the operation</param>
        /// <returns>Formatted security issues summary</returns>
        public string GetSecurityIssuesSummary(User user)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("File System Security Issues Summary");
                sb.AppendLine("==================================");
                sb.AppendLine();
                
                // Get audit log entries for security issues
                var securityIssues = _auditLogger.GetFilteredMemoryLog(e => 
                    !e.Success && 
                    (e.EventType == AuditEventType.Access || 
                     e.EventType == AuditEventType.Modification || 
                     e.EventType == AuditEventType.PermissionChange))
                    .OrderByDescending(e => e.Timestamp)
                    .ToList();
                    
                // Group by path
                var issuesByPath = securityIssues
                    .GroupBy(e => e.Path)
                    .Select(g => new 
                    { 
                        Path = g.Key, 
                        Count = g.Count(),
                        Latest = g.OrderByDescending(e => e.Timestamp).First().Timestamp,
                        Users = g.Select(e => e.Username).Distinct().ToList()
                    })
                    .OrderByDescending(g => g.Count)
                    .ToList();
                    
                sb.AppendLine($"Total Security Issues Found: {securityIssues.Count}");
                sb.AppendLine($"Unique Paths with Issues: {issuesByPath.Count}");
                sb.AppendLine();
                
                // Show top issues by path
                sb.AppendLine("Top Problematic Paths:");
                sb.AppendLine("---------------------");
                
                foreach (var issue in issuesByPath.Take(10))
                {
                    sb.AppendLine($"{issue.Path} - {issue.Count} issues, Last: {issue.Latest:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"  Affected Users: {string.Join(", ", issue.Users)}");
                }
                
                // Group by user
                var issuesByUser = securityIssues
                    .GroupBy(e => e.Username)
                    .Select(g => new 
                    { 
                        Username = g.Key, 
                        Count = g.Count(),
                        Latest = g.OrderByDescending(e => e.Timestamp).First().Timestamp
                    })
                    .OrderByDescending(g => g.Count)
                    .ToList();
                    
                sb.AppendLine();
                sb.AppendLine("Users with Most Security Issues:");
                sb.AppendLine("-------------------------------");
                
                foreach (var issue in issuesByUser.Take(5))
                {
                    sb.AppendLine($"{issue.Username} - {issue.Count} issues, Last: {issue.Latest:yyyy-MM-dd HH:mm:ss}");
                }
                
                // Group by issue type
                var issuesByType = securityIssues
                    .GroupBy(e => e.FailureReason)
                    .Select(g => new 
                    { 
                        Reason = g.Key, 
                        Count = g.Count()
                    })
                    .OrderByDescending(g => g.Count)
                    .ToList();
                    
                sb.AppendLine();
                sb.AppendLine("Issues by Type:");
                sb.AppendLine("--------------");
                
                foreach (var issue in issuesByType)
                {
                    sb.AppendLine($"{issue.Reason} - {issue.Count} occurrences");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generating security issues summary: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Enables or disables audit logging
        /// </summary>
        /// <param name="enabled">Whether to enable or disable logging</param>
        /// <returns>Status message</returns>
        public string SetAuditLogging(bool enabled)
        {
            _auditLogger.LoggingEnabled = enabled;
            return $"Audit logging {(enabled ? "enabled" : "disabled")}";
        }
        
        /// <summary>
        /// Sets the minimum severity level for audit logging
        /// </summary>
        /// <param name="severity">The minimum severity level</param>
        /// <returns>Status message</returns>
        public string SetAuditLogSeverity(AuditSeverity severity)
        {
            _auditLogger.MinimumSeverity = severity;
            return $"Audit log minimum severity set to {severity}";
        }
    }
}
