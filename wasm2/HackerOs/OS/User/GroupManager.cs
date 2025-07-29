using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Manages user groups in the HackerOS system, simulating Linux group management.
    /// Handles group creation, deletion, membership, and persistence.
    /// </summary>
    public class GroupManager
    {
        private const string GROUP_FILE_PATH = "/etc/group";
        private const string GROUP_BACKUP_PATH = "/etc/group.bak";
        private const int MIN_SYSTEM_GID = 0;
        private const int MAX_SYSTEM_GID = 999;
        private const int MIN_USER_GID = 1000;
        private const int DEFAULT_GROUP_GID = 100; // 'users' group

        private readonly IO.FileSystem.VirtualFileSystem _fileSystem;
        private readonly Dictionary<int, Group> _groupsById;
        private readonly Dictionary<string, Group> _groupsByName;
        private bool _initialized;

        /// <summary>
        /// Gets all groups in the system
        /// </summary>
        public IEnumerable<Group> AllGroups => _groupsById.Values;

        /// <summary>
        /// Gets all system groups (GID < 1000)
        /// </summary>
        public IEnumerable<Group> SystemGroups => _groupsById.Values.Where(g => g.IsSystemGroup);

        /// <summary>
        /// Gets all user groups (GID >= 1000)
        /// </summary>
        public IEnumerable<Group> UserGroups => _groupsById.Values.Where(g => !g.IsSystemGroup);

        /// <summary>
        /// Event fired when a group is created
        /// </summary>
        public event EventHandler<GroupEventArgs> GroupCreated;

        /// <summary>
        /// Event fired when a group is deleted
        /// </summary>
        public event EventHandler<GroupEventArgs> GroupDeleted;

        /// <summary>
        /// Event fired when a group is modified
        /// </summary>
        public event EventHandler<GroupEventArgs> GroupModified;

        /// <summary>
        /// Constructs a new GroupManager instance
        /// </summary>
        /// <param name="fileSystem">The virtual file system to use</param>
        public GroupManager(IO.FileSystem.VirtualFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _groupsById = new Dictionary<int, Group>();
            _groupsByName = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
            _initialized = false;
        }

        /// <summary>
        /// Initializes the group manager, loading groups from the file system
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            // Ensure the /etc directory exists
            if (!await _fileSystem.DirectoryExistsAsync("/etc"))
            {
                await _fileSystem.CreateDirectoryAsync("/etc", new IO.FileSystem.FilePermissions(0755));
            }

            // Load groups from /etc/group if it exists
            if (await _fileSystem.FileExistsAsync(GROUP_FILE_PATH))
            {
                await LoadGroupsAsync();
            }
            else
            {
                // Create default groups
                await CreateDefaultGroupsAsync();
                await SaveGroupsAsync();
            }

            _initialized = true;
        }

        /// <summary>
        /// Creates a new group
        /// </summary>
        /// <param name="groupName">The name of the group</param>
        /// <param name="description">Optional description</param>
        /// <param name="gid">Optional specific GID (0 for auto-assign)</param>
        /// <returns>The created group</returns>
        public async Task<Group> CreateGroupAsync(string groupName, string description = "", int gid = 0)
        {
            if (!_initialized)
                await InitializeAsync();

            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("Group name cannot be empty", nameof(groupName));

            if (_groupsByName.ContainsKey(groupName))
                throw new InvalidOperationException($"Group with name '{groupName}' already exists");

            if (gid != 0 && _groupsById.ContainsKey(gid))
                throw new InvalidOperationException($"Group with GID {gid} already exists");

            // Auto-assign GID if needed
            if (gid == 0)
            {
                gid = GetNextAvailableGid();
            }

            var group = new Group(groupName, description, gid);
            _groupsById[gid] = group;
            _groupsByName[groupName] = group;

            // Save changes
            await SaveGroupsAsync();

            // Raise event
            GroupCreated?.Invoke(this, new GroupEventArgs(group));

            return group;
        }

        /// <summary>
        /// Gets a group by its GID
        /// </summary>
        /// <param name="gid">The group ID</param>
        /// <returns>The group, or null if not found</returns>
        public async Task<Group> GetGroupByIdAsync(int gid)
        {
            if (!_initialized)
                await InitializeAsync();

            return _groupsById.TryGetValue(gid, out var group) ? group : null;
        }

        /// <summary>
        /// Gets a group by its name
        /// </summary>
        /// <param name="name">The group name</param>
        /// <returns>The group, or null if not found</returns>
        public async Task<Group> GetGroupByNameAsync(string name)
        {
            if (!_initialized)
                await InitializeAsync();

            return _groupsByName.TryGetValue(name, out var group) ? group : null;
        }

        /// <summary>
        /// Deletes a group
        /// </summary>
        /// <param name="gid">The group ID to delete</param>
        /// <returns>True if successful, false if not found</returns>
        public async Task<bool> DeleteGroupAsync(int gid)
        {
            if (!_initialized)
                await InitializeAsync();

            if (!_groupsById.TryGetValue(gid, out var group))
                return false;

            // Don't allow deleting system groups
            if (group.IsSystemGroup)
                throw new InvalidOperationException("Cannot delete system groups");

            // Make sure the group has no members
            if (group.Members.Count > 0)
                throw new InvalidOperationException("Cannot delete group with members");

            _groupsById.Remove(gid);
            _groupsByName.Remove(group.GroupName);

            // Save changes
            await SaveGroupsAsync();

            // Raise event
            GroupDeleted?.Invoke(this, new GroupEventArgs(group));

            return true;
        }

        /// <summary>
        /// Adds a user to a group
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="gid">The group ID</param>
        /// <returns>True if added, false if already a member</returns>
        public async Task<bool> AddUserToGroupAsync(int userId, int gid)
        {
            if (!_initialized)
                await InitializeAsync();

            if (!_groupsById.TryGetValue(gid, out var group))
                throw new KeyNotFoundException($"Group with GID {gid} not found");

            bool added = group.AddMember(userId);
            if (added)
            {
                // Save changes
                await SaveGroupsAsync();

                // Raise event
                GroupModified?.Invoke(this, new GroupEventArgs(group));
            }

            return added;
        }

        /// <summary>
        /// Removes a user from a group
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="gid">The group ID</param>
        /// <returns>True if removed, false if not a member</returns>
        public async Task<bool> RemoveUserFromGroupAsync(int userId, int gid)
        {
            if (!_initialized)
                await InitializeAsync();

            if (!_groupsById.TryGetValue(gid, out var group))
                throw new KeyNotFoundException($"Group with GID {gid} not found");

            bool removed = group.RemoveMember(userId);
            if (removed)
            {
                // Save changes
                await SaveGroupsAsync();

                // Raise event
                GroupModified?.Invoke(this, new GroupEventArgs(group));
            }

            return removed;
        }

        /// <summary>
        /// Checks if a user is a member of a group
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="gid">The group ID</param>
        /// <returns>True if the user is a member</returns>
        public async Task<bool> IsUserInGroupAsync(int userId, int gid)
        {
            if (!_initialized)
                await InitializeAsync();

            if (!_groupsById.TryGetValue(gid, out var group))
                return false;

            return group.IsMember(userId);
        }

        /// <summary>
        /// Gets all groups a user is a member of
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>List of groups the user belongs to</returns>
        public async Task<List<Group>> GetUserGroupsAsync(int userId)
        {
            if (!_initialized)
                await InitializeAsync();

            return _groupsById.Values
                .Where(g => g.IsMember(userId))
                .ToList();
        }

        /// <summary>
        /// Creates standard system groups
        /// </summary>
        private async Task CreateDefaultGroupsAsync()
        {
            // Clear existing groups
            _groupsById.Clear();
            _groupsByName.Clear();

            // Create standard system groups
            AddGroup(new Group("root", "Superuser group", 0));
            AddGroup(new Group("bin", "System binaries", 1));
            AddGroup(new Group("daemon", "System daemons", 2));
            AddGroup(new Group("sys", "System processes", 3));
            AddGroup(new Group("adm", "Administration group", 4));
            AddGroup(new Group("tty", "Terminal users", 5));
            AddGroup(new Group("disk", "Disk operators", 6));
            AddGroup(new Group("wheel", "Privileged users", 10));
            AddGroup(new Group("mail", "Mail subsystem", 12));
            AddGroup(new Group("sudo", "Sudo users", 27));
            AddGroup(new Group("www-data", "Web server", 33));
            AddGroup(new Group("users", "Standard users", 100));
            AddGroup(new Group("nogroup", "No group", 998));
            AddGroup(new Group("nobody", "Unprivileged group", 999));

            // Add root user to admin groups
            var wheelGroup = await GetGroupByNameAsync("wheel");
            var sudoGroup = await GetGroupByNameAsync("sudo");
            
            if (wheelGroup != null)
                wheelGroup.AddMember(0); // Root user
            
            if (sudoGroup != null)
                sudoGroup.AddMember(0); // Root user
        }

        /// <summary>
        /// Helper to add a group to internal collections
        /// </summary>
        private void AddGroup(Group group)
        {
            _groupsById[group.GroupId] = group;
            _groupsByName[group.GroupName] = group;
        }

        /// <summary>
        /// Finds the next available GID for a new group
        /// </summary>
        private int GetNextAvailableGid()
        {
            // Start at MIN_USER_GID and find the first available GID
            int gid = MIN_USER_GID;
            while (_groupsById.ContainsKey(gid))
            {
                gid++;
            }
            return gid;
        }

        /// <summary>
        /// Loads groups from the /etc/group file
        /// </summary>
        private async Task LoadGroupsAsync()
        {
            try
            {
                // Clear existing groups
                _groupsById.Clear();
                _groupsByName.Clear();

                // Read the group file
                string content = await _fileSystem.ReadFileAsync(GROUP_FILE_PATH);
                using var reader = new StringReader(content);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                        continue; // Skip empty lines and comments

                    // Parse the line (format: name:password:gid:members)
                    string[] parts = line.Split(':');
                    if (parts.Length >= 4)
                    {
                        string name = parts[0];
                        string passwd = parts[1]; // Usually 'x' or empty
                        int gid = int.Parse(parts[2]);
                        string[] memberNames = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries);

                        // Create the group
                        var group = new Group(name, "", gid);

                        // Add members (we need to convert usernames to UIDs)
                        foreach (var memberName in memberNames)
                        {
                            var userId = UserModelExtensions.GetUserId(memberName);
                            if (userId.HasValue)
                            {
                                group.AddMember(userId.Value);
                            }
                        }

                        // Add to collections
                        AddGroup(group);
                    }
                }

                // Create default groups if none were loaded
                if (_groupsById.Count == 0)
                {
                    await CreateDefaultGroupsAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading groups: {ex.Message}");
                // Create default groups on error
                await CreateDefaultGroupsAsync();
            }
        }

        /// <summary>
        /// Saves groups to the /etc/group file
        /// </summary>
        private async Task SaveGroupsAsync()
        {
            try
            {
                // Create a backup if the file exists
                if (await _fileSystem.FileExistsAsync(GROUP_FILE_PATH))
                {
                    string content = await _fileSystem.ReadFileAsync(GROUP_FILE_PATH);
                    await _fileSystem.WriteFileAsync(GROUP_BACKUP_PATH, content);
                }

                // Format the groups
                var sb = new StringBuilder();
                sb.AppendLine("# /etc/group: Group file");
                sb.AppendLine("# format: group_name:password:gid:user_list");
                sb.AppendLine();

                // Sort groups by GID
                foreach (var group in _groupsById.Values.OrderBy(g => g.GroupId))
                {
                    // Convert member UIDs to usernames
                    var memberNames = group.Members
                        .Select(uid => UserModelExtensions.GetUserName(uid))
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList();

                    // Format: name:x:gid:member1,member2,...
                    sb.AppendLine($"{group.GroupName}:x:{group.GroupId}:{string.Join(",", memberNames)}");
                }

                // Write the file
                await _fileSystem.WriteFileAsync(GROUP_FILE_PATH, sb.ToString());

                // Set proper permissions (644, root:root)
                var permissions = new IO.FileSystem.FilePermissions(0644);
                await _fileSystem.SetFilePermissionsAsync(GROUP_FILE_PATH, permissions);
                await _fileSystem.SetFileOwnerAsync(GROUP_FILE_PATH, "0", "0");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving groups: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Event arguments for group events
    /// </summary>
    public class GroupEventArgs : EventArgs
    {
        /// <summary>
        /// The group associated with the event
        /// </summary>
        public Group Group { get; }

        public GroupEventArgs(Group group)
        {
            Group = group;
        }
    }
}
