using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Extension methods for converting between different User model implementations
    /// </summary>
    public static class UserModelExtensions
    {
        /// <summary>
        /// Standard group mapping from group IDs to names
        /// </summary>
        private static readonly Dictionary<int, string> StandardGroupNames = new Dictionary<int, string>
        {
            { 0, "root" },
            { 1, "bin" },
            { 2, "daemon" },
            { 3, "sys" },
            { 4, "adm" },
            { 5, "tty" },
            { 6, "disk" },
            { 10, "wheel" },
            { 12, "mail" },
            { 27, "sudo" },
            { 33, "www-data" },
            { 100, "users" },
            { 998, "nogroup" },
            { 999, "nobody" }
        };

        /// <summary>
        /// Converts a User from OS.User namespace to User.Models namespace
        /// </summary>
        /// <param name="user">The core User object to convert</param>
        /// <returns>A Models.User representation of the user</returns>
        public static Models.User ToUserModel(this User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            return new Models.User
            {
                UserId = user.UserId.ToString(),
                Username = user.Username,
                FullName = user.FullName,
                HomeDirectory = user.HomeDirectory,
                IsActive = user.IsActive,
                LastLogin = user.LastLogin ?? DateTime.UtcNow,
                Uid = user.UserId,
                Gid = user.PrimaryGroupId,
                SecondaryGroups = user.AdditionalGroups.ToList(),
                Status = user.IsActive ? Models.UserStatus.Active : Models.UserStatus.Disabled,
                DefaultShell = user.Shell,
                CreatedDate = user.CreatedAt
            };
        }

        /// <summary>
        /// Converts a User from User.Models namespace to OS.User namespace
        /// </summary>
        /// <param name="userModel">The Models.User object to convert</param>
        /// <returns>A core User representation of the user model</returns>
        public static User ToUserCore(this Models.User userModel)
        {
            if (userModel == null)
                throw new ArgumentNullException(nameof(userModel));

            int userId = int.TryParse(userModel.UserId, out int parsedId) ? parsedId : 0;
            
            var user = new User(userModel.Username, userModel.FullName, userId)
            {
                PrimaryGroupId = userModel.Gid,
                HomeDirectory = userModel.HomeDirectory,
                Shell = userModel.DefaultShell,
                IsActive = userModel.IsActive,
                IsAdmin = userModel.IsAdmin
            };

            // Convert secondary groups
            foreach (var groupId in userModel.SecondaryGroups)
            {
                user.AddToGroup(groupId);
            }

            // Set last login if available
            if (userModel.LastLogin != default)
            {
                user.LastLogin = userModel.LastLogin;
            }

            return user;
        }

        /// <summary>
        /// Gets the group name for a given group ID
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <returns>The group name if known, otherwise the group ID as a string</returns>
        public static string GetGroupName(int groupId)
        {
            return StandardGroupNames.TryGetValue(groupId, out var name) 
                ? name 
                : $"group{groupId}";
        }

        /// <summary>
        /// Gets the group ID for a given group name
        /// </summary>
        /// <param name="groupName">The group name</param>
        /// <returns>The group ID if known, otherwise -1</returns>
        public static int GetGroupId(string groupName)
        {
            foreach (var group in StandardGroupNames)
            {
                if (string.Equals(group.Value, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    return group.Key;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gets a list of group names for a user
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns>A list of group names the user belongs to</returns>
        public static List<string> GetGroupNames(this User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var groups = new List<string>();
            
            // Add primary group
            groups.Add(GetGroupName(user.PrimaryGroupId));
            
            // Add secondary groups
            foreach (var groupId in user.AdditionalGroups)
            {
                var groupName = GetGroupName(groupId);
                if (!groups.Contains(groupName))
                {
                    groups.Add(groupName);
                }
            }
            
            return groups;
        }

        /// <summary>
        /// Gets a list of group names for a user model
        /// </summary>
        /// <param name="userModel">The user model</param>
        /// <returns>A list of group names the user belongs to</returns>
        public static List<string> GetGroupNames(this Models.User userModel)
        {
            if (userModel == null)
                throw new ArgumentNullException(nameof(userModel));

            var groups = new List<string>();
            
            // Add primary group
            groups.Add(GetGroupName(userModel.Gid));
            
            // Add secondary groups
            foreach (var groupId in userModel.SecondaryGroups)
            {
                var groupName = GetGroupName(groupId);
                if (!groups.Contains(groupName))
                {
                    groups.Add(groupName);
                }
            }
            
            return groups;
        }

        /// <summary>
        /// Gets the username for a given user ID
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>The username if known, otherwise the user ID as a string</returns>
        public static string GetUserName(int userId)
        {
            // We'll return some standard usernames for system UIDs
            switch (userId)
            {
                case 0: return "root";
                case 1: return "bin";
                case 2: return "daemon";
                case 65534: return "nobody";
                default:
                    // For non-system users, try to look up by ID
                    // In a real implementation, this would query from the user database
                    // For now, we'll just return a formatted string
                    return $"user{userId}";
            }
        }

        /// <summary>
        /// Gets the user ID for a given username
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>The user ID if known, otherwise null</returns>
        public static int? GetUserId(string username)
        {
            // We'll return some standard UIDs for system usernames
            switch (username.ToLowerInvariant())
            {
                case "root": return 0;
                case "bin": return 1;
                case "daemon": return 2;
                case "nobody": return 65534;
                default:
                    // For non-system users, try to parse if the username is in the format "userXXX"
                    if (username.StartsWith("user", StringComparison.OrdinalIgnoreCase) && 
                        int.TryParse(username.Substring(4), out int userId))
                    {
                        return userId;
                    }
                    // In a real implementation, this would query from the user database
                    return null;
            }
        }
    }
}
