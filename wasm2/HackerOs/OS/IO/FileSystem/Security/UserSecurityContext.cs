using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem.Security
{
    /// <summary>
    /// Represents the security context of a user during operations
    /// </summary>
    public class UserSecurityContext
    {
        private readonly User _originalUser;
        private User _effectiveUser;
        private readonly Stack<User> _userStack = new Stack<User>();
        private readonly Stack<int> _groupStack = new Stack<int>();
        private bool _isElevated = false;
        private readonly FileSystemAuditLogger _auditLogger;
        
        /// <summary>
        /// Gets the original user that initiated the operation
        /// </summary>
        public User OriginalUser => _originalUser;
        
        /// <summary>
        /// Gets the effective user for the current operation
        /// </summary>
        public User EffectiveUser => _effectiveUser;
        
        /// <summary>
        /// Gets whether the user context is currently elevated
        /// </summary>
        public bool IsElevated => _isElevated;
        
        /// <summary>
        /// Gets the current effective group ID
        /// </summary>
        public int EffectiveGroupId { get; private set; }
        
        /// <summary>
        /// Creates a new instance of the UserSecurityContext
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="auditLogger">The audit logger</param>
        public UserSecurityContext(User user, FileSystemAuditLogger auditLogger = null)
        {
            _originalUser = user ?? throw new ArgumentNullException(nameof(user));
            _effectiveUser = user;
            EffectiveGroupId = user.PrimaryGroupId;
            _auditLogger = auditLogger;
        }
        
        /// <summary>
        /// Elevates the user context to a different user
        /// </summary>
        /// <param name="targetUserId">The target user ID</param>
        /// <param name="reason">The reason for elevation</param>
        /// <param name="path">The path associated with the elevation</param>
        /// <returns>True if elevation was successful</returns>
        public async Task<bool> ElevateToUserAsync(int targetUserId, string reason, string path)
        {
            // Don't elevate if already the same user
            if (_effectiveUser.Id == targetUserId)
            {
                return true;
            }
            
            // Get the target user
            var targetUser = UserManager.Instance.GetUserById(targetUserId);
            if (targetUser == null)
            {
                await LogElevationAttemptAsync(targetUserId, -1, false, "Target user not found", path);
                return false;
            }
            
            // Security checks
            if (!IsElevationAllowed(_effectiveUser, targetUser))
            {
                await LogElevationAttemptAsync(targetUserId, -1, false, "Elevation not allowed", path);
                return false;
            }
            
            // Push the current user and group onto the stack
            _userStack.Push(_effectiveUser);
            _groupStack.Push(EffectiveGroupId);
            
            // Set the new effective user and group
            _effectiveUser = targetUser;
            EffectiveGroupId = targetUser.PrimaryGroupId;
            _isElevated = true;
            
            await LogElevationAttemptAsync(targetUserId, EffectiveGroupId, true, reason, path);
            
            return true;
        }
        
        /// <summary>
        /// Elevates the effective group ID
        /// </summary>
        /// <param name="targetGroupId">The target group ID</param>
        /// <param name="reason">The reason for elevation</param>
        /// <param name="path">The path associated with the elevation</param>
        /// <returns>True if elevation was successful</returns>
        public async Task<bool> ElevateToGroupAsync(int targetGroupId, string reason, string path)
        {
            // Don't elevate if already the same group
            if (EffectiveGroupId == targetGroupId)
            {
                return true;
            }
            
            // Verify the group exists
            var groupName = GroupManager.Instance.GetGroupNameById(targetGroupId);
            if (string.IsNullOrEmpty(groupName))
            {
                await LogElevationAttemptAsync(-1, targetGroupId, false, "Target group not found", path);
                return false;
            }
            
            // Security checks
            if (!IsGroupElevationAllowed(_effectiveUser, targetGroupId))
            {
                await LogElevationAttemptAsync(-1, targetGroupId, false, "Group elevation not allowed", path);
                return false;
            }
            
            // Push the current group onto the stack
            _groupStack.Push(EffectiveGroupId);
            
            // Set the new effective group
            EffectiveGroupId = targetGroupId;
            _isElevated = true;
            
            await LogElevationAttemptAsync(-1, targetGroupId, true, reason, path);
            
            return true;
        }
        
        /// <summary>
        /// Restores the previous user context
        /// </summary>
        /// <returns>True if restoration was successful</returns>
        public async Task<bool> RestoreUserAsync(string path)
        {
            if (_userStack.Count == 0)
            {
                return false;
            }
            
            var previousUser = _effectiveUser;
            
            // Restore the previous user
            _effectiveUser = _userStack.Pop();
            
            // Restore the previous group if available, otherwise use the user's primary group
            if (_groupStack.Count > 0)
            {
                EffectiveGroupId = _groupStack.Pop();
            }
            else
            {
                EffectiveGroupId = _effectiveUser.PrimaryGroupId;
            }
            
            // Update elevation status
            _isElevated = _userStack.Count > 0 || _groupStack.Count > 0;
            
            await LogRestoreAsync(previousUser.Id, _effectiveUser.Id, path);
            
            return true;
        }
        
        /// <summary>
        /// Restores the previous group ID
        /// </summary>
        /// <returns>True if restoration was successful</returns>
        public async Task<bool> RestoreGroupAsync(string path)
        {
            if (_groupStack.Count == 0)
            {
                return false;
            }
            
            var previousGroupId = EffectiveGroupId;
            
            // Restore the previous group
            EffectiveGroupId = _groupStack.Pop();
            
            // Update elevation status
            _isElevated = _userStack.Count > 0 || _groupStack.Count > 0;
            
            await LogRestoreAsync(-1, -1, path, previousGroupId, EffectiveGroupId);
            
            return true;
        }
        
        /// <summary>
        /// Determines whether elevation is allowed
        /// </summary>
        /// <param name="currentUser">The current user</param>
        /// <param name="targetUser">The target user</param>
        /// <returns>True if elevation is allowed</returns>
        private bool IsElevationAllowed(User currentUser, User targetUser)
        {
            // Root can elevate to anyone
            if (currentUser.Id == 0)
            {
                return true;
            }
            
            // Original root user can elevate to anyone
            if (_originalUser.Id == 0)
            {
                return true;
            }
            
            // Users can only elevate to themselves unless they're in sudo group
            if (currentUser.Id != targetUser.Id && !IsInSudoGroup(currentUser))
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Determines whether group elevation is allowed
        /// </summary>
        /// <param name="currentUser">The current user</param>
        /// <param name="targetGroupId">The target group ID</param>
        /// <returns>True if group elevation is allowed</returns>
        private bool IsGroupElevationAllowed(User currentUser, int targetGroupId)
        {
            // Root can elevate to any group
            if (currentUser.Id == 0)
            {
                return true;
            }
            
            // Original root user can elevate to any group
            if (_originalUser.Id == 0)
            {
                return true;
            }
            
            // Users can only elevate to groups they're a member of
            if (currentUser.PrimaryGroupId != targetGroupId && 
                (currentUser.SecondaryGroups == null || 
                !currentUser.SecondaryGroups.Contains(targetGroupId)))
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks if a user is in the sudo group
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <returns>True if the user is in the sudo group</returns>
        private bool IsInSudoGroup(User user)
        {
            if (user == null)
            {
                return false;
            }
            
            // Get the sudo group ID
            int sudoGroupId = GroupManager.Instance.GetGroupIdByName("sudo");
            if (sudoGroupId < 0)
            {
                return false;
            }
            
            // Check if the user is in the sudo group
            return user.PrimaryGroupId == sudoGroupId || 
                  (user.SecondaryGroups != null && user.SecondaryGroups.Contains(sudoGroupId));
        }
        
        /// <summary>
        /// Logs an elevation attempt
        /// </summary>
        /// <param name="targetUserId">The target user ID</param>
        /// <param name="targetGroupId">The target group ID</param>
        /// <param name="success">Whether the elevation was successful</param>
        /// <param name="reason">The reason for elevation</param>
        /// <param name="path">The path associated with the elevation</param>
        private async Task LogElevationAttemptAsync(int targetUserId, int targetGroupId, bool success, string reason, string path)
        {
            if (_auditLogger == null)
            {
                return;
            }
            
            string operation;
            if (targetUserId >= 0 && targetGroupId < 0)
            {
                operation = $"ElevateToUser:{targetUserId}";
            }
            else if (targetUserId < 0 && targetGroupId >= 0)
            {
                operation = $"ElevateToGroup:{targetGroupId}";
            }
            else
            {
                operation = $"Elevate:User{targetUserId}:Group{targetGroupId}";
            }
            
            await _auditLogger.LogAsync(
                _originalUser,
                AuditEventType.Authentication,
                success ? AuditSeverity.Information : AuditSeverity.Warning,
                path,
                operation,
                success,
                success ? null : reason);
        }
        
        /// <summary>
        /// Logs a context restoration
        /// </summary>
        /// <param name="previousUserId">The previous user ID</param>
        /// <param name="newUserId">The new user ID</param>
        /// <param name="path">The path associated with the restoration</param>
        /// <param name="previousGroupId">The previous group ID</param>
        /// <param name="newGroupId">The new group ID</param>
        private async Task LogRestoreAsync(int previousUserId, int newUserId, string path, int previousGroupId = -1, int newGroupId = -1)
        {
            if (_auditLogger == null)
            {
                return;
            }
            
            string operation;
            if (previousUserId >= 0 && previousGroupId < 0)
            {
                operation = $"RestoreUser:{previousUserId}->{newUserId}";
            }
            else if (previousUserId < 0 && previousGroupId >= 0)
            {
                operation = $"RestoreGroup:{previousGroupId}->{newGroupId}";
            }
            else
            {
                operation = $"Restore:User{previousUserId}->User{newUserId}:Group{previousGroupId}->Group{newGroupId}";
            }
            
            await _auditLogger.LogAsync(
                _originalUser,
                AuditEventType.Authentication,
                AuditSeverity.Information,
                path,
                operation,
                true);
        }
    }
}
