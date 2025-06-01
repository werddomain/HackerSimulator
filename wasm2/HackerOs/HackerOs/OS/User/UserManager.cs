using HackerOs.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Interface for user management operations
    /// </summary>
    public interface IUserManager
    {
        /// <summary>
        /// Authenticates a user with username and password
        /// </summary>
        Task<User?> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Creates a new user account
        /// </summary>
        Task<User> CreateUserAsync(string username, string fullName, string password, bool isAdmin = false);

        /// <summary>
        /// Gets a user by username
        /// </summary>
        Task<User?> GetUserAsync(string username);

        /// <summary>
        /// Gets a user by user ID
        /// </summary>
        Task<User?> GetUserAsync(int userId);

        /// <summary>
        /// Gets all users in the system
        /// </summary>
        Task<IEnumerable<User>> GetAllUsersAsync();

        /// <summary>
        /// Updates an existing user
        /// </summary>
        Task<bool> UpdateUserAsync(User user);

        /// <summary>
        /// Deletes a user account
        /// </summary>
        Task<bool> DeleteUserAsync(string username);

        /// <summary>
        /// Changes a user's password
        /// </summary>
        Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword);

        /// <summary>
        /// Creates a new group
        /// </summary>
        Task<Group> CreateGroupAsync(string groupName, string description = "");

        /// <summary>
        /// Gets a group by name
        /// </summary>
        Task<Group?> GetGroupAsync(string groupName);

        /// <summary>
        /// Gets a group by group ID
        /// </summary>
        Task<Group?> GetGroupAsync(int groupId);

        /// <summary>
        /// Gets all groups in the system
        /// </summary>
        Task<IEnumerable<Group>> GetAllGroupsAsync();

        /// <summary>
        /// Updates an existing group
        /// </summary>
        Task<bool> UpdateGroupAsync(Group group);

        /// <summary>
        /// Deletes a group
        /// </summary>
        Task<bool> DeleteGroupAsync(string groupName);

        /// <summary>
        /// Adds a user to a group
        /// </summary>
        Task<bool> AddUserToGroupAsync(string username, string groupName);

        /// <summary>
        /// Removes a user from a group
        /// </summary>
        Task<bool> RemoveUserFromGroupAsync(string username, string groupName);

        /// <summary>
        /// Checks if a user belongs to a specific group
        /// </summary>
        Task<bool> IsUserInGroupAsync(string username, string groupName);

        /// <summary>
        /// Gets all groups a user belongs to
        /// </summary>
        Task<IEnumerable<Group>> GetUserGroupsAsync(string username);

        /// <summary>
        /// Initializes the user management system
        /// </summary>
        Task InitializeAsync();
    }

    /// <summary>
    /// Manages users and groups in the HackerOS system, simulating /etc/passwd and /etc/group
    /// </summary>
    public class UserManager : IUserManager
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<UserManager> _logger;
        private readonly Dictionary<string, User> _users;
        private readonly Dictionary<int, User> _usersByUid;
        private readonly Dictionary<string, Group> _groups;
        private readonly Dictionary<int, Group> _groupsByGid;
        private readonly object _lock = new();
        private int _nextUserId = 1000; // Start UIDs at 1000 for regular users
        private int _nextGroupId = 1000; // Start GIDs at 1000 for regular groups
        private bool _initialized = false;

        // Simulated system files
        private const string PasswdFile = "/etc/passwd";
        private const string GroupFile = "/etc/group";
        private const string ShadowFile = "/etc/shadow";

        public UserManager(IVirtualFileSystem fileSystem, ILogger<UserManager> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _users = new Dictionary<string, User>();
            _usersByUid = new Dictionary<int, User>();
            _groups = new Dictionary<string, Group>();
            _groupsByGid = new Dictionary<int, Group>();
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            _logger.LogInformation("Initializing UserManager system...");

            try
            {
                // Ensure /etc directory exists
                await _fileSystem.CreateDirectoryAsync("/etc");

                // Initialize standard system groups first
                await InitializeSystemGroupsAsync();

                // Initialize system users
                await InitializeSystemUsersAsync();

                // Load existing users and groups from files
                await LoadUsersFromFileAsync();
                await LoadGroupsFromFileAsync();

                _initialized = true;
                _logger.LogInformation("UserManager system initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize UserManager system");
                throw;
            }
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                if (_users.TryGetValue(username, out var user))
                {
                    if (user.IsActive && user.VerifyPassword(password))
                    {
                        user.UpdateLastLogin();
                        _logger.LogInformation("User {Username} authenticated successfully", username);
                        _ = Task.Run(() => SaveUsersToFileAsync()); // Save updated last login
                        return user;
                    }
                    else
                    {
                        _logger.LogWarning("Authentication failed for user {Username} - invalid password or inactive account", username);
                    }
                }
                else
                {
                    _logger.LogWarning("Authentication failed for user {Username} - user not found", username);
                }
            }

            return null;
        }

        public async Task<User> CreateUserAsync(string username, string fullName, string password, bool isAdmin = false)
        {
            await EnsureInitializedAsync();

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            lock (_lock)
            {
                if (_users.ContainsKey(username))
                    throw new InvalidOperationException($"User '{username}' already exists");

                int userId = _nextUserId++;
                var user = new User(username, fullName, userId)
                {
                    PrimaryGroupId = Group.StandardGroups.UsersGroupId,
                    IsAdmin = isAdmin
                };

                user.SetPassword(password);

                _users[username] = user;
                _usersByUid[userId] = user;

                // Add to appropriate groups
                if (isAdmin && _groups.TryGetValue("wheel", out var wheelGroup))
                {
                    user.AddToGroup(wheelGroup.GroupId);
                    wheelGroup.AddMember(userId);
                }

                // Add to users group
                if (_groups.TryGetValue("users", out var usersGroup))
                {
                    usersGroup.AddMember(userId);
                }

                _logger.LogInformation("Created user {Username} with UID {UserId}", username, userId);
            }

            // Create home directory
            await CreateHomeDirectoryAsync(username);

            // Save to files
            await SaveUsersToFileAsync();
            await SaveGroupsToFileAsync();

            return await GetUserAsync(username) ?? throw new InvalidOperationException("Failed to retrieve created user");
        }

        public async Task<User?> GetUserAsync(string username)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                return _users.TryGetValue(username, out var user) ? user : null;
            }
        }

        public async Task<User?> GetUserAsync(int userId)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                return _usersByUid.TryGetValue(userId, out var user) ? user : null;
            }
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                return _users.Values.ToList();
            }
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            await EnsureInitializedAsync();

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            lock (_lock)
            {
                if (_users.ContainsKey(user.Username))
                {
                    _users[user.Username] = user;
                    _usersByUid[user.UserId] = user;
                    user.UpdateEnvironment();
                    
                    _logger.LogInformation("Updated user {Username}", user.Username);
                    _ = Task.Run(SaveUsersToFileAsync);
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> DeleteUserAsync(string username)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                if (_users.TryGetValue(username, out var user))
                {
                    // Remove from all groups
                    foreach (var group in _groups.Values)
                    {
                        group.RemoveMember(user.UserId);
                    }

                    _users.Remove(username);
                    _usersByUid.Remove(user.UserId);

                    _logger.LogInformation("Deleted user {Username}", username);
                    _ = Task.Run(SaveUsersToFileAsync);
                    _ = Task.Run(SaveGroupsToFileAsync);
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword)
        {
            await EnsureInitializedAsync();

            var user = await AuthenticateAsync(username, oldPassword);
            if (user != null)
            {
                user.SetPassword(newPassword);
                await UpdateUserAsync(user);
                _logger.LogInformation("Password changed for user {Username}", username);
                return true;
            }

            return false;
        }

        public async Task<Group> CreateGroupAsync(string groupName, string description = "")
        {
            await EnsureInitializedAsync();

            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("Group name cannot be empty", nameof(groupName));

            lock (_lock)
            {
                if (_groups.ContainsKey(groupName))
                    throw new InvalidOperationException($"Group '{groupName}' already exists");

                int groupId = _nextGroupId++;
                var group = new Group(groupName, description, groupId);

                _groups[groupName] = group;
                _groupsByGid[groupId] = group;

                _logger.LogInformation("Created group {GroupName} with GID {GroupId}", groupName, groupId);
            }

            await SaveGroupsToFileAsync();
            return await GetGroupAsync(groupName) ?? throw new InvalidOperationException("Failed to retrieve created group");
        }

        public async Task<Group?> GetGroupAsync(string groupName)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                return _groups.TryGetValue(groupName, out var group) ? group : null;
            }
        }

        public async Task<Group?> GetGroupAsync(int groupId)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                return _groupsByGid.TryGetValue(groupId, out var group) ? group : null;
            }
        }

        public async Task<IEnumerable<Group>> GetAllGroupsAsync()
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                return _groups.Values.ToList();
            }
        }

        public async Task<bool> UpdateGroupAsync(Group group)
        {
            await EnsureInitializedAsync();

            if (group == null)
                throw new ArgumentNullException(nameof(group));

            lock (_lock)
            {
                if (_groups.ContainsKey(group.GroupName))
                {
                    _groups[group.GroupName] = group;
                    _groupsByGid[group.GroupId] = group;
                    
                    _logger.LogInformation("Updated group {GroupName}", group.GroupName);
                    _ = Task.Run(SaveGroupsToFileAsync);
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> DeleteGroupAsync(string groupName)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                if (_groups.TryGetValue(groupName, out var group))
                {
                    // Remove group from all users
                    foreach (var user in _users.Values)
                    {
                        user.RemoveFromGroup(group.GroupId);
                        if (user.PrimaryGroupId == group.GroupId)
                        {
                            user.PrimaryGroupId = Group.StandardGroups.UsersGroupId; // Reset to users group
                        }
                    }

                    _groups.Remove(groupName);
                    _groupsByGid.Remove(group.GroupId);

                    _logger.LogInformation("Deleted group {GroupName}", groupName);
                    _ = Task.Run(SaveUsersToFileAsync);
                    _ = Task.Run(SaveGroupsToFileAsync);
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> AddUserToGroupAsync(string username, string groupName)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                if (_users.TryGetValue(username, out var user) && _groups.TryGetValue(groupName, out var group))
                {
                    bool userAdded = user.AddToGroup(group.GroupId);
                    bool groupAdded = group.AddMember(user.UserId);

                    if (userAdded || groupAdded)
                    {
                        _logger.LogInformation("Added user {Username} to group {GroupName}", username, groupName);
                        _ = Task.Run(SaveUsersToFileAsync);
                        _ = Task.Run(SaveGroupsToFileAsync);
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<bool> RemoveUserFromGroupAsync(string username, string groupName)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                if (_users.TryGetValue(username, out var user) && _groups.TryGetValue(groupName, out var group))
                {
                    user.RemoveFromGroup(group.GroupId);
                    group.RemoveMember(user.UserId);

                    _logger.LogInformation("Removed user {Username} from group {GroupName}", username, groupName);
                    _ = Task.Run(SaveUsersToFileAsync);
                    _ = Task.Run(SaveGroupsToFileAsync);
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> IsUserInGroupAsync(string username, string groupName)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                if (_users.TryGetValue(username, out var user) && _groups.TryGetValue(groupName, out var group))
                {
                    return user.BelongsToGroup(group.GroupId);
                }
            }

            return false;
        }

        public async Task<IEnumerable<Group>> GetUserGroupsAsync(string username)
        {
            await EnsureInitializedAsync();

            lock (_lock)
            {
                if (_users.TryGetValue(username, out var user))
                {
                    var userGroups = new List<Group>();

                    // Add primary group
                    if (_groupsByGid.TryGetValue(user.PrimaryGroupId, out var primaryGroup))
                    {
                        userGroups.Add(primaryGroup);
                    }

                    // Add additional groups
                    foreach (var groupId in user.AdditionalGroups)
                    {
                        if (_groupsByGid.TryGetValue(groupId, out var group))
                        {
                            userGroups.Add(group);
                        }
                    }

                    return userGroups;
                }
            }

            return Enumerable.Empty<Group>();
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }
        }

        private async Task InitializeSystemGroupsAsync()
        {
            var standardGroups = Group.StandardGroups.GetStandardGroups();
            
            foreach (var group in standardGroups)
            {
                _groups[group.GroupName] = group;
                _groupsByGid[group.GroupId] = group;
                _logger.LogDebug("Initialized system group: {GroupName} (GID: {GroupId})", group.GroupName, group.GroupId);
            }

            await SaveGroupsToFileAsync();
        }

        private async Task InitializeSystemUsersAsync()
        {
            // Create root user
            var root = new User("root", "Administrator", 0)
            {
                PrimaryGroupId = Group.StandardGroups.RootGroupId,
                HomeDirectory = "/root",
                Shell = "/bin/bash",
                IsAdmin = true
            };
            root.SetPassword("hackeros"); // Default password for demo

            _users["root"] = root;
            _usersByUid[0] = root;

            // Add root to wheel group
            if (_groups.TryGetValue("wheel", out var wheelGroup))
            {
                root.AddToGroup(wheelGroup.GroupId);
                wheelGroup.AddMember(0);
            }

            _logger.LogDebug("Initialized system user: root");

            await SaveUsersToFileAsync();
        }

        private async Task CreateHomeDirectoryAsync(string username)
        {
            try
            {
                var homePath = $"/home/{username}";
                await _fileSystem.CreateDirectoryAsync(homePath);

                // Create standard user directories
                var standardDirs = new[] { ".config", "Desktop", "Documents", "Downloads", "Pictures" };
                foreach (var dir in standardDirs)
                {
                    await _fileSystem.CreateDirectoryAsync($"{homePath}/{dir}");
                }

                _logger.LogDebug("Created home directory for user {Username} at {HomePath}", username, homePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create home directory for user {Username}", username);
            }
        }

        private async Task LoadUsersFromFileAsync()
        {
            try
            {
                if (await _fileSystem.FileExistsAsync(PasswdFile))
                {
                    var content = await _fileSystem.ReadAllTextAsync(PasswdFile);
                    // Parse passwd file format and load users
                    // For now, we'll skip this as we're managing users in memory
                    _logger.LogDebug("Loaded users from {PasswdFile}", PasswdFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load users from {PasswdFile}", PasswdFile);
            }
        }

        private async Task LoadGroupsFromFileAsync()
        {
            try
            {
                if (await _fileSystem.FileExistsAsync(GroupFile))
                {
                    var content = await _fileSystem.ReadAllTextAsync(GroupFile);
                    // Parse group file format and load groups
                    // For now, we'll skip this as we're managing groups in memory
                    _logger.LogDebug("Loaded groups from {GroupFile}", GroupFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load groups from {GroupFile}", GroupFile);
            }
        }

        private async Task SaveUsersToFileAsync()
        {
            try
            {
                var lines = new List<string>();
                
                lock (_lock)
                {
                    foreach (var user in _users.Values.OrderBy(u => u.UserId))
                    {
                        // Format: username:x:uid:gid:comment:home:shell
                        var line = $"{user.Username}:x:{user.UserId}:{user.PrimaryGroupId}:{user.FullName}:{user.HomeDirectory}:{user.Shell}";
                        lines.Add(line);
                    }
                }

                var content = string.Join("\n", lines);
                await _fileSystem.WriteAllTextAsync(PasswdFile, content);
                _logger.LogDebug("Saved users to {PasswdFile}", PasswdFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save users to {PasswdFile}", PasswdFile);
            }
        }

        private async Task SaveGroupsToFileAsync()
        {
            try
            {
                var lines = new List<string>();
                
                lock (_lock)
                {
                    foreach (var group in _groups.Values.OrderBy(g => g.GroupId))
                    {
                        // Format: groupname:x:gid:members
                        var members = string.Join(",", group.Members.Select(uid => 
                            _usersByUid.TryGetValue(uid, out var user) ? user.Username : uid.ToString()));
                        var line = $"{group.GroupName}:x:{group.GroupId}:{members}";
                        lines.Add(line);
                    }
                }

                var content = string.Join("\n", lines);
                await _fileSystem.WriteAllTextAsync(GroupFile, content);
                _logger.LogDebug("Saved groups to {GroupFile}", GroupFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save groups to {GroupFile}", GroupFile);
            }
        }
    }
}
