using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Provides centralized utilities for managing group-based access control in the file system.
    /// Handles group membership verification, SetGID behavior, and group permission inheritance.
    /// </summary>
    public static class GroupAccessManager
    {
        // Cache for group membership to improve performance
        private static readonly Dictionary<string, HashSet<int>> _userGroupCache = new Dictionary<string, HashSet<int>>();
        
        /// <summary>
        /// Determines if a user has access to a file or directory via group membership.
        /// </summary>
        /// <param name="user">The user attempting access</param>
        /// <param name="node">The file system node being accessed</param>
        /// <param name="mode">The access mode (Read, Write, Execute)</param>
        /// <returns>True if the user has access through group membership; otherwise, false</returns>
        public static bool HasGroupAccess(UserEntity user, VirtualFileSystemNode node, FileAccessMode mode)
        {
            // Root always has access
            if (user.UserId == 0)
            {
                return true;
            }
            
            // Get the node's group ID
            int groupId = GetNodeGroupId(node);
            
            // Check if user is a member of the node's group
            if (!IsUserInGroup(user, groupId))
            {
                return false;
            }
            
            // Check specific group permissions based on mode
            var permissions = node.Permissions;
            
            switch (mode)
            {
                case FileAccessMode.Read:
                    return permissions.GroupRead;
                case FileAccessMode.Write:
                    return permissions.GroupWrite;
                case FileAccessMode.Execute:
                    return permissions.GroupExecute;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Checks if a user is a member of a specific group.
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <param name="groupId">The group ID</param>
        /// <returns>True if the user is a member of the group; otherwise, false</returns>
        public static bool IsUserInGroup(UserEntity user, int groupId)
        {
            // User's primary group
            if (user.PrimaryGroupId == groupId)
            {
                return true;
            }
            
            // Check secondary groups
            if (user.SecondaryGroups != null && user.SecondaryGroups.Contains(groupId))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets all groups a user belongs to (primary and secondary).
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns>A collection of group IDs the user belongs to</returns>
        public static IEnumerable<int> GetUserGroups(UserEntity user)
        {
            // Cache key based on user ID and a timestamp that might change if groups change
            string cacheKey = $"{user.UserId}_{user.LastModified?.Ticks ?? 0}";
            
            // Try to get from cache
            if (_userGroupCache.TryGetValue(cacheKey, out var cachedGroups))
            {
                return cachedGroups;
            }
            
            // Create a new set starting with primary group
            var groups = new HashSet<int> { user.PrimaryGroupId };
            
            // Add secondary groups if any
            if (user.SecondaryGroups != null)
            {
                foreach (var groupId in user.SecondaryGroups)
                {
                    groups.Add(groupId);
                }
            }
            
            // Cache the result
            _userGroupCache[cacheKey] = groups;
            
            return groups;
        }
        
        /// <summary>
        /// Clears the group membership cache for a specific user or all users.
        /// </summary>
        /// <param name="userId">The user ID to clear, or null for all users</param>
        public static void ClearGroupCache(int? userId = null)
        {
            if (userId.HasValue)
            {
                // Clear cache for specific user
                var keysToRemove = _userGroupCache.Keys
                    .Where(k => k.StartsWith($"{userId.Value}_"))
                    .ToList();
                
                foreach (var key in keysToRemove)
                {
                    _userGroupCache.Remove(key);
                }
            }
            else
            {
                // Clear all cache
                _userGroupCache.Clear();
            }
        }
        
        /// <summary>
        /// Checks if a directory has the SetGID bit set.
        /// </summary>
        /// <param name="directory">The directory to check</param>
        /// <returns>True if the directory has SetGID set; otherwise, false</returns>
        public static bool HasSetGID(VirtualDirectory directory)
        {
            return directory.Permissions.SetGID;
        }
        
        /// <summary>
        /// Determines the appropriate group for a new file based on parent directory.
        /// </summary>
        /// <param name="parentDir">The parent directory</param>
        /// <param name="user">The user creating the file</param>
        /// <returns>The group ID string for the new file</returns>
        public static string DetermineGroupForNewFile(VirtualDirectory parentDir, UserEntity user)
        {
            // If parent directory has SetGID, inherit its group
            if (HasSetGID(parentDir))
            {
                return parentDir.Group;
            }
            
            // Otherwise, use the user's primary group
            return user.PrimaryGroupId.ToString();
        }
        
        /// <summary>
        /// Determines if SetGID should be inherited by a new directory.
        /// </summary>
        /// <param name="parentDir">The parent directory</param>
        /// <returns>True if SetGID should be inherited; otherwise, false</returns>
        public static bool ShouldInheritSetGID(VirtualDirectory parentDir)
        {
            return HasSetGID(parentDir);
        }
        
        /// <summary>
        /// Applies appropriate group permissions to a new file or directory.
        /// </summary>
        /// <param name="node">The file system node to modify</param>
        /// <param name="parentDir">The parent directory</param>
        /// <param name="user">The user creating the node</param>
        /// <param name="isDirectory">Whether the node is a directory</param>
        public static void ApplyGroupPermissions(
            VirtualFileSystemNode node, 
            VirtualDirectory parentDir, 
            UserEntity user, 
            bool isDirectory)
        {
            // Set the group based on SetGID rules
            node.Group = DetermineGroupForNewFile(parentDir, user);
            
            // If this is a directory and parent has SetGID, inherit it
            if (isDirectory && node is VirtualDirectory && ShouldInheritSetGID(parentDir))
            {
                var permissions = node.Permissions;
                permissions.SetGID = true;
                node.Permissions = permissions;
            }
        }
        
        /// <summary>
        /// Logs a group access event for auditing purposes.
        /// </summary>
        /// <param name="user">The user attempting access</param>
        /// <param name="node">The node being accessed</param>
        /// <param name="mode">The access mode</param>
        /// <param name="granted">Whether access was granted</param>
        /// <param name="fileSystem">The file system instance</param>
        public static void LogGroupAccess(
            UserEntity user, 
            VirtualFileSystemNode node, 
            FileAccessMode mode, 
            bool granted, 
            VirtualFileSystem fileSystem)
        {
            if (fileSystem != null)
            {
                int groupId = GetNodeGroupId(node);
                
                fileSystem.LogFileSystemEvent(
                    granted ? FileSystemEventType.FileRead : FileSystemEventType.PermissionDenied,
                    node.FullPath,
                    $"Group access {(granted ? "granted" : "denied")}: User {user.Username} (uid={user.UserId}) " +
                    $"attempting {mode} access to {node.FullPath} owned by group {node.Group} (gid={groupId})");
            }
        }
        
        /// <summary>
        /// Gets a node's group ID as an integer.
        /// </summary>
        /// <param name="node">The file system node</param>
        /// <returns>The group ID as an integer</returns>
        private static int GetNodeGroupId(VirtualFileSystemNode node)
        {
            return int.TryParse(node.Group, out int groupId) ? groupId : 0;
        }
    }
}
