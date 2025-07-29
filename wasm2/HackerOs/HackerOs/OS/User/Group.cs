namespace HackerOs.OS.User
{
    /// <summary>
    /// Represents a group in the HackerOS system, similar to Unix groups
    /// </summary>
    public class Group
    {
        /// <summary>
        /// Group ID (GID) - unique identifier for the group
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Group name - unique name for the group
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Description of the group's purpose
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// List of user IDs that belong to this group
        /// </summary>
        public List<int> Members { get; set; } = new();

        /// <summary>
        /// List of user IDs that are administrators of this group
        /// </summary>
        public List<int> Administrators { get; set; } = new();

        /// <summary>
        /// Group creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this is a system group (GID < 1000)
        /// </summary>
        public bool IsSystemGroup => GroupId < 1000;

        /// <summary>
        /// Group permissions and settings
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new();

        public Group()
        {
        }

        public Group(string groupName, int groupId = 0)
        {
            GroupName = groupName;
            GroupId = groupId;
        }

        public Group(string groupName, string description, int groupId = 0) : this(groupName, groupId)
        {
            Description = description;
        }

        /// <summary>
        /// Adds a user to the group
        /// </summary>
        /// <param name="userId">User ID to add</param>
        /// <returns>True if user was added, false if already a member</returns>
        public bool AddMember(int userId)
        {
            if (!Members.Contains(userId))
            {
                Members.Add(userId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes a user from the group
        /// </summary>
        /// <param name="userId">User ID to remove</param>
        /// <returns>True if user was removed, false if not a member</returns>
        public bool RemoveMember(int userId)
        {
            bool removed = Members.Remove(userId);
            // Also remove from administrators if they were one
            Administrators.Remove(userId);
            return removed;
        }

        /// <summary>
        /// Checks if a user is a member of this group
        /// </summary>
        /// <param name="userId">User ID to check</param>
        /// <returns>True if user is a member</returns>
        public bool IsMember(int userId)
        {
            return Members.Contains(userId);
        }

        /// <summary>
        /// Adds a user as an administrator of this group
        /// </summary>
        /// <param name="userId">User ID to make administrator</param>
        /// <returns>True if user was added as admin, false if already an admin</returns>
        public bool AddAdministrator(int userId)
        {
            // Ensure user is a member first
            AddMember(userId);
            
            if (!Administrators.Contains(userId))
            {
                Administrators.Add(userId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes a user from group administrators
        /// </summary>
        /// <param name="userId">User ID to remove from administrators</param>
        /// <returns>True if user was removed from administrators</returns>
        public bool RemoveAdministrator(int userId)
        {
            return Administrators.Remove(userId);
        }

        /// <summary>
        /// Checks if a user is an administrator of this group
        /// </summary>
        /// <param name="userId">User ID to check</param>
        /// <returns>True if user is an administrator</returns>
        public bool IsAdministrator(int userId)
        {
            return Administrators.Contains(userId);
        }

        /// <summary>
        /// Gets the number of members in the group
        /// </summary>
        public int MemberCount => Members.Count;

        /// <summary>
        /// Creates a serializable representation of the group for storage
        /// </summary>
        /// <returns>Dictionary representation of the group</returns>
        public Dictionary<string, object> ToSerializable()
        {
            return new Dictionary<string, object>
            {
                ["groupId"] = GroupId,
                ["groupName"] = GroupName,
                ["description"] = Description,
                ["members"] = Members,
                ["administrators"] = Administrators,
                ["createdAt"] = CreatedAt.ToString("O"),
                ["properties"] = Properties
            };
        }

        /// <summary>
        /// Creates a Group instance from a serializable representation
        /// </summary>
        /// <param name="data">Dictionary representation of the group</param>
        /// <returns>Group instance</returns>
        public static Group FromSerializable(Dictionary<string, object> data)
        {
            var group = new Group
            {
                GroupId = Convert.ToInt32(data["groupId"]),
                GroupName = data["groupName"].ToString() ?? "",
                Description = data["description"].ToString() ?? ""
            };

            if (data.ContainsKey("members") && data["members"] is IEnumerable<object> members)
            {
                group.Members = members.Select(m => Convert.ToInt32(m)).ToList();
            }

            if (data.ContainsKey("administrators") && data["administrators"] is IEnumerable<object> admins)
            {
                group.Administrators = admins.Select(a => Convert.ToInt32(a)).ToList();
            }

            if (data.ContainsKey("createdAt") && DateTime.TryParse(data["createdAt"].ToString(), out var createdAt))
            {
                group.CreatedAt = createdAt;
            }

            if (data.ContainsKey("properties") && data["properties"] is Dictionary<string, object> props)
            {
                group.Properties = props.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? "");
            }

            return group;
        }

        public override string ToString()
        {
            return $"{GroupName} (GID: {GroupId}, Members: {MemberCount})";
        }

        /// <summary>
        /// Standard system groups that should exist in HackerOS
        /// </summary>
        public static class StandardGroups
        {
            public const int RootGroupId = 0;
            public const int WheelGroupId = 1;
            public const int UsersGroupId = 100;
            public const int AdminGroupId = 27;
            public const int AudioGroupId = 29;
            public const int VideoGroupId = 44;
            public const int NetworkGroupId = 90;
            public const int StorageGroupId = 91;

            public static Group Root => new("root", "Root group", RootGroupId);
            public static Group Wheel => new("wheel", "System administrators", WheelGroupId);
            public static Group Users => new("users", "Regular users", UsersGroupId);
            public static Group Admin => new("admin", "System administrators", AdminGroupId);
            public static Group Audio => new("audio", "Audio device access", AudioGroupId);
            public static Group Video => new("video", "Video device access", VideoGroupId);
            public static Group Network => new("network", "Network configuration", NetworkGroupId);
            public static Group Storage => new("storage", "Storage device access", StorageGroupId);

            /// <summary>
            /// Gets all standard system groups
            /// </summary>
            /// <returns>List of standard groups</returns>
            public static List<Group> GetStandardGroups()
            {
                return new List<Group>
                {
                    Root, Wheel, Users, Admin, Audio, Video, Network, Storage
                };
            }
        }
    }
}
