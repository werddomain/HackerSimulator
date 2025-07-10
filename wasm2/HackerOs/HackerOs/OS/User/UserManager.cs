using HackerOs.OS.IO;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.User
{
   

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

        // System user for internal operations (root equivalent)
        internal static readonly User SystemUser = new User
        {
            UserId = 0,
            Username = "system",
            FullName = "System User",
            PrimaryGroupId = 0,
            HomeDirectory = "/root",
            Shell = "/bin/bash",
            AdditionalGroups = new List<int>()
        };

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
                    _ = Task.Run(() => SaveUsersToFileAsync());
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
                    _usersByUid.Remove(user.UserId);                    _logger.LogInformation("Deleted user {Username}", username);
                    _ = Task.Run(() => SaveUsersToFileAsync());
                    _ = Task.Run(() => SaveGroupsToFileAsync());
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
                // Get the user to set correct ownership
                User? user = await GetUserAsync(username);
                if (user == null)
                {
                    _logger.LogError("Cannot create home directory for non-existent user {Username}", username);
                    return;
                }

                var homePath = $"/home/{username}";
                bool homeCreated = await _fileSystem.CreateDirectoryAsync(homePath);
                
                if (homeCreated)
                {
                    // Set home directory permissions (755 - rwxr-xr-x)
                    await _fileSystem.SetPermissionsAsync(homePath, 0755, user.UserId, user.PrimaryGroupId, SystemUser);
                    
                    // Create standard user directories with appropriate permissions
                    var standardDirs = new Dictionary<string, int>
                    {
                        // Public directories (755 - rwxr-xr-x)
                        { "Desktop", 0755 },
                        { "Documents", 0755 },
                        { "Downloads", 0755 },
                        { "Music", 0755 },
                        { "Pictures", 0755 },
                        { "Public", 0755 },
                        { "Videos", 0755 },
                        
                        // Private directories (700 - rwx------)
                        { ".ssh", 0700 },
                        { ".gnupg", 0700 },
                        
                        // Configuration directories (755 - rwxr-xr-x)
                        { ".config", 0755 },
                        { ".local", 0755 },
                        { ".local/share", 0755 },
                        { ".local/bin", 0755 },
                        { ".cache", 0755 }
                    };
                    
                    foreach (var (dir, permissions) in standardDirs)
                    {
                        string dirPath = $"{homePath}/{dir}";
                        bool dirCreated = await _fileSystem.CreateDirectoryAsync(dirPath);
                        
                        if (dirCreated)
                        {
                            // Set directory ownership and permissions
                            await _fileSystem.SetPermissionsAsync(dirPath, permissions, user.UserId, user.PrimaryGroupId, SystemUser);
                        }
                    }
                    
                    // Create default config directories for applications
                    string configDir = $"{homePath}/.config";
                    var appConfigDirs = new[] { "hackeros", "browser", "terminal", "editor" };
                    
                    foreach (var dir in appConfigDirs)
                    {
                        string appConfigPath = $"{configDir}/{dir}";
                        bool appDirCreated = await _fileSystem.CreateDirectoryAsync(appConfigPath);
                        
                        if (appDirCreated)
                        {
                            // Set directory ownership and permissions (700 - rwx------)
                            await _fileSystem.SetPermissionsAsync(appConfigPath, 0700, user.UserId, user.PrimaryGroupId, SystemUser);
                        }
                    }
                    
                    // Create default configuration files (will be implemented in another task)
                    await CreateDefaultUserConfigFilesAsync(username, homePath);
                    
                    _logger.LogDebug("Created home directory structure for user {Username} at {HomePath}", username, homePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create home directory for user {Username}", username);
            }
        }
        
        /// <summary>
        /// Creates default configuration files for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task CreateDefaultUserConfigFilesAsync(string username, string homePath)
        {
            try
            {
                // Get the user to set correct ownership
                User? user = await GetUserAsync(username);
                if (user == null)
                {
                    _logger.LogError("Cannot create config files for non-existent user {Username}", username);
                    return;
                }

                // Create shell configuration files
                await CreateBashrcAsync(user, homePath);
                await CreateProfileAsync(user, homePath);
                await CreateBashLogoutAsync(user, homePath);
                
                // Create application configuration files
                await CreateUserSettingsAsync(user, homePath);
                await CreateAppConfigsAsync(user, homePath);
                
                _logger.LogInformation("Created default configuration files for user {Username}", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default configuration files for user {Username}", username);
            }
        }

        /// <summary>
        /// Creates a default .bashrc file for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateBashrcAsync(User user, string homePath)
        {
            string filePath = $"{homePath}/.bashrc";
            
            // Skip if file already exists
            if (await _fileSystem.FileExistsAsync(filePath, SystemUser))
            {
                _logger.LogDebug("Skipping .bashrc creation for {Username} as it already exists", user.Username);
                return false;
            }
            
            // Default .bashrc content
            string content = @"# ~/.bashrc: executed by bash for non-login shells

# If not running interactively, don't do anything
[ -z ""$PS1"" ] && return

# Don't put duplicate lines in the history
HISTCONTROL=ignoredups:ignorespace

# Append to the history file, don't overwrite it
shopt -s histappend

# History length
HISTSIZE=1000
HISTFILESIZE=2000

# Check window size after each command
shopt -s checkwinsize

# Make less more friendly for non-text input files
[ -x /usr/bin/lesspipe ] && eval ""$(SHELL=/bin/sh lesspipe)""

# Set prompt
PS1='\[\033[01;32m\]\u@\h\[\033[00m\]:\[\033[01;34m\]\w\[\033[00m\]\$ '

# Enable color support of ls
if [ -x /usr/bin/dircolors ]; then
    test -r ~/.dircolors && eval ""$(dircolors -b ~/.dircolors)"" || eval ""$(dircolors -b)""
    alias ls='ls --color=auto'
    alias grep='grep --color=auto'
    alias fgrep='fgrep --color=auto'
    alias egrep='egrep --color=auto'
fi

# Some useful aliases
alias ll='ls -alF'
alias la='ls -A'
alias l='ls -CF'
alias cls='clear'
alias h='history'
alias j='jobs -l'

# HackerOS specific aliases
alias hackeros-update='sudo apt update && sudo apt upgrade'
alias hackeros-info='neofetch'

# User specific environment and startup programs
if [ -d ""$HOME/.local/bin"" ] ; then
    PATH=""$HOME/.local/bin:$PATH""
fi

# Source global definitions
if [ -f /etc/bashrc ]; then
    . /etc/bashrc
fi
";

            // Write the file
            bool result = await _fileSystem.WriteAllTextAsync(filePath, content, SystemUser);
            
            if (result)
            {
                // Set proper ownership and permissions
                await _fileSystem.SetPermissionsAsync(filePath, 0644, user.UserId, user.PrimaryGroupId, SystemUser);
                _logger.LogDebug("Created .bashrc for {Username}", user.Username);
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a default .profile file for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateProfileAsync(User user, string homePath)
        {
            string filePath = $"{homePath}/.profile";
            
            // Skip if file already exists
            if (await _fileSystem.FileExistsAsync(filePath, SystemUser))
            {
                _logger.LogDebug("Skipping .profile creation for {Username} as it already exists", user.Username);
                return false;
            }
            
            // Default .profile content
            string content = $@"# ~/.profile: executed by the command interpreter for login shells

# Include user's private bin directory in PATH if it exists
if [ -d ""$HOME/.local/bin"" ] ; then
    PATH=""$HOME/.local/bin:$PATH""
fi

# Set environment variables
export EDITOR=nano
export TERM=xterm-256color
export LANG=en_US.UTF-8
export LC_ALL=en_US.UTF-8
export USER={user.Username}
export HOME={homePath}

# Source .bashrc for interactive shells
if [ -n ""$BASH_VERSION"" ]; then
    if [ -f ""$HOME/.bashrc"" ]; then
        . ""$HOME/.bashrc""
    fi
fi

# HackerOS specific environment variables
export HACKEROS_USER_CONFIG=""$HOME/.config/hackeros""

# Display welcome message on login
echo ""Welcome to HackerOS, {user.FullName}!""
echo ""Type 'hackeros-info' for system information.""
";

            // Write the file
            bool result = await _fileSystem.WriteAllTextAsync(filePath, content, SystemUser);
            
            if (result)
            {
                // Set proper ownership and permissions
                await _fileSystem.SetPermissionsAsync(filePath, 0644, user.UserId, user.PrimaryGroupId, SystemUser);
                _logger.LogDebug("Created .profile for {Username}", user.Username);
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a default .bash_logout file for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateBashLogoutAsync(User user, string homePath)
        {
            string filePath = $"{homePath}/.bash_logout";
            
            // Skip if file already exists
            if (await _fileSystem.FileExistsAsync(filePath, SystemUser))
            {
                _logger.LogDebug("Skipping .bash_logout creation for {Username} as it already exists", user.Username);
                return false;
            }
            
            // Default .bash_logout content
            string content = @"# ~/.bash_logout: executed by bash when login shell exits

# Clear the screen when logging out
clear

# When leaving the console, save the history
if [ ""$SHLVL"" = 1 ]; then
    history -a
fi
";

            // Write the file
            bool result = await _fileSystem.WriteAllTextAsync(filePath, content, SystemUser);
            
            if (result)
            {
                // Set proper ownership and permissions
                await _fileSystem.SetPermissionsAsync(filePath, 0644, user.UserId, user.PrimaryGroupId, SystemUser);
                _logger.LogDebug("Created .bash_logout for {Username}", user.Username);
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a default user-settings.json file for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateUserSettingsAsync(User user, string homePath)
        {
            string configDir = $"{homePath}/.config/hackeros";
            string filePath = $"{configDir}/user-settings.json";
            
            // Skip if file already exists
            if (await _fileSystem.FileExistsAsync(filePath, SystemUser))
            {
                _logger.LogDebug("Skipping user-settings.json creation for {Username} as it already exists", user.Username);
                return false;
            }
            
            // Default user settings content
            string content = @"{
  ""ui"": {
    ""theme"": ""default"",
    ""accentColor"": ""blue"",
    ""fontSize"": ""medium"",
    ""darkMode"": true,
    ""animations"": true
  },
  ""desktop"": {
    ""wallpaper"": ""/usr/share/hackeros/wallpapers/default.jpg"",
    ""icons"": {
      ""size"": ""medium"",
      ""arrangement"": ""grid""
    },
    ""taskbar"": {
      ""position"": ""bottom"",
      ""autoHide"": false
    },
    ""startMenu"": {
      ""showRecentApps"": true,
      ""showFavorites"": true
    }
  },
  ""applications"": {
    ""terminal"": {
      ""fontFamily"": ""Monospace"",
      ""fontSize"": 12,
      ""cursorStyle"": ""block"",
      ""cursorBlink"": true,
      ""colorScheme"": ""hackeros-dark""
    },
    ""browser"": {
      ""homepage"": ""about:newtab"",
      ""searchEngine"": ""duckduckgo"",
      ""privacyMode"": ""standard""
    },
    ""fileExplorer"": {
      ""showHiddenFiles"": false,
      ""viewMode"": ""list""
    }
  },
  ""security"": {
    ""lockScreenTimeout"": 300,
    ""requirePasswordOnWake"": true,
    ""notificationsOnLockScreen"": false
  },
  ""network"": {
    ""proxyEnabled"": false,
    ""proxyAddress"": """",
    ""proxyPort"": 0
  },
  ""preferences"": {
    ""language"": ""en-US"",
    ""timeFormat"": ""24h"",
    ""dateFormat"": ""yyyy-MM-dd""
  }
}";

            // Ensure directory exists
            await _fileSystem.CreateDirectoryAsync(configDir);
            
            // Write the file
            bool result = await _fileSystem.WriteAllTextAsync(filePath, content, SystemUser);
            
            if (result)
            {
                // Set proper ownership and permissions
                await _fileSystem.SetPermissionsAsync(filePath, 0600, user.UserId, user.PrimaryGroupId, SystemUser);
                _logger.LogDebug("Created user-settings.json for {Username}", user.Username);
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates default application configuration files for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateAppConfigsAsync(User user, string homePath)
        {
            bool success = true;
            
            // Create terminal configuration
            success = success && await CreateTerminalConfigAsync(user, homePath);
            
            // Create browser configuration
            success = success && await CreateBrowserConfigAsync(user, homePath);
            
            // Create editor configuration
            success = success && await CreateEditorConfigAsync(user, homePath);
            
            return success;
        }
        
        /// <summary>
        /// Creates a default terminal configuration file for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateTerminalConfigAsync(User user, string homePath)
        {
            string configDir = $"{homePath}/.config/terminal";
            string filePath = $"{configDir}/config.json";
            
            // Skip if file already exists
            if (await _fileSystem.FileExistsAsync(filePath, SystemUser))
            {
                _logger.LogDebug("Skipping terminal config creation for {Username} as it already exists", user.Username);
                return false;
            }
            
            // Default terminal configuration
            string content = @"{
  ""appearance"": {
    ""fontFamily"": ""Monospace"",
    ""fontSize"": 12,
    ""lineHeight"": 1.2,
    ""cursorStyle"": ""block"",
    ""cursorBlink"": true,
    ""colorScheme"": ""hackeros-dark"",
    ""opacity"": 0.95,
    ""padding"": 5
  },
  ""behavior"": {
    ""scrollback"": 10000,
    ""scrollSensitivity"": 1.0,
    ""copyOnSelect"": false,
    ""pasteOnMiddleClick"": true,
    ""rightClickBehavior"": ""contextMenu"",
    ""confirmOnExit"": true
  },
  ""keybindings"": {
    ""copyToClipboard"": ""Ctrl+Shift+C"",
    ""pasteFromClipboard"": ""Ctrl+Shift+V"",
    ""newTab"": ""Ctrl+Shift+T"",
    ""closeTab"": ""Ctrl+Shift+W"",
    ""clearScreen"": ""Ctrl+L"",
    ""searchBuffer"": ""Ctrl+Shift+F""
  },
  ""advanced"": {
    ""shell"": ""/bin/bash"",
    ""shellArgs"": [""--login""],
    ""startupDirectory"": ""~"",
    ""terminalType"": ""xterm-256color"",
    ""useConpty"": true,
    ""bellStyle"": ""sound""
  },
  ""colorSchemes"": {
    ""hackeros-dark"": {
      ""background"": ""#1E1E1E"",
      ""foreground"": ""#F8F8F2"",
      ""black"": ""#272822"",
      ""red"": ""#F92672"",
      ""green"": ""#A6E22E"",
      ""yellow"": ""#F4BF75"",
      ""blue"": ""#66D9EF"",
      ""magenta"": ""#AE81FF"",
      ""cyan"": ""#A1EFE4"",
      ""white"": ""#F8F8F2"",
      ""brightBlack"": ""#75715E"",
      ""brightRed"": ""#F92672"",
      ""brightGreen"": ""#A6E22E"",
      ""brightYellow"": ""#F4BF75"",
      ""brightBlue"": ""#66D9EF"",
      ""brightMagenta"": ""#AE81FF"",
      ""brightCyan"": ""#A1EFE4"",
      ""brightWhite"": ""#F9F8F5""
    }
  }
}";

            // Ensure directory exists
            await _fileSystem.CreateDirectoryAsync(configDir);
            
            // Write the file
            bool result = await _fileSystem.WriteAllTextAsync(filePath, content, SystemUser);
            
            if (result)
            {
                // Set proper ownership and permissions
                await _fileSystem.SetPermissionsAsync(filePath, 0644, user.UserId, user.PrimaryGroupId, SystemUser);
                _logger.LogDebug("Created terminal config for {Username}", user.Username);
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a default browser configuration file for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateBrowserConfigAsync(User user, string homePath)
        {
            string configDir = $"{homePath}/.config/browser";
            string filePath = $"{configDir}/config.json";
            
            // Skip if file already exists
            if (await _fileSystem.FileExistsAsync(filePath, SystemUser))
            {
                _logger.LogDebug("Skipping browser config creation for {Username} as it already exists", user.Username);
                return false;
            }
            
            // Default browser configuration
            string content = @"{
  ""general"": {
    ""homepage"": ""about:newtab"",
    ""startPage"": ""homepage"",
    ""newTabPage"": ""about:newtab"",
    ""searchEngine"": ""duckduckgo"",
    ""showBookmarksBar"": true,
    ""showHomeButton"": true,
    ""restoreTabs"": true
  },
  ""privacy"": {
    ""cookiePolicy"": ""accept-first-party"",
    ""doNotTrack"": true,
    ""clearHistoryOnExit"": false,
    ""clearCookiesOnExit"": false,
    ""blockPopups"": true,
    ""blockThirdPartyContent"": false,
    ""useHttps"": true
  },
  ""security"": {
    ""passwordSaving"": ""ask"",
    ""fraudWarning"": true,
    ""malwareProtection"": true,
    ""securityUpdates"": true
  },
  ""appearance"": {
    ""theme"": ""system"",
    ""zoomLevel"": 100,
    ""fontSize"": ""medium"",
    ""showStatusBar"": true,
    ""tabWidth"": ""dynamic""
  },
  ""downloads"": {
    ""defaultLocation"": ""~/Downloads"",
    ""askBeforeDownloading"": true,
    ""openFileAfterDownload"": false
  },
  ""advanced"": {
    ""hardwareAcceleration"": true,
    ""smoothScrolling"": true,
    ""enableJavaScript"": true,
    ""enableWebGL"": true,
    ""cacheSize"": 1024
  },
  ""bookmarks"": [
    {
      ""title"": ""HackerOS Documentation"",
      ""url"": ""https://docs.hackeros.local"",
      ""folder"": ""HackerOS""
    },
    {
      ""title"": ""Security News"",
      ""url"": ""https://news.hackeros.local"",
      ""folder"": ""HackerOS""
    }
  ]
}";

            // Ensure directory exists
            await _fileSystem.CreateDirectoryAsync(configDir);
            
            // Write the file
            bool result = await _fileSystem.WriteAllTextAsync(filePath, content, SystemUser);
            
            if (result)
            {
                // Set proper ownership and permissions
                await _fileSystem.SetPermissionsAsync(filePath, 0644, user.UserId, user.PrimaryGroupId, SystemUser);
                _logger.LogDebug("Created browser config for {Username}", user.Username);
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a default editor configuration file for a user
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="homePath">The user's home directory path</param>
        private async Task<bool> CreateEditorConfigAsync(User user, string homePath)
        {
            string configDir = $"{homePath}/.config/editor";
            string filePath = $"{configDir}/config.json";
            
            // Skip if file already exists
            if (await _fileSystem.FileExistsAsync(filePath, SystemUser))
            {
                _logger.LogDebug("Skipping editor config creation for {Username} as it already exists", user.Username);
                return false;
            }
            
            // Default editor configuration
            string content = @"{
  ""editor"": {
    ""fontFamily"": ""Source Code Pro, monospace"",
    ""fontSize"": 14,
    ""tabSize"": 2,
    ""insertSpaces"": true,
    ""wordWrap"": ""on"",
    ""lineNumbers"": true,
    ""rulers"": [80, 120],
    ""cursorBlinking"": ""blink"",
    ""cursorStyle"": ""line"",
    ""formatOnSave"": true,
    ""formatOnPaste"": false,
    ""minimap"": {
      ""enabled"": true,
      ""showSlider"": ""always"",
      ""renderCharacters"": true
    }
  },
  ""appearance"": {
    ""theme"": ""hackeros-dark"",
    ""colorizeParentheses"": true,
    ""showWhitespace"": ""boundary"",
    ""showIndentGuides"": true,
    ""scrollBeyondLastLine"": true,
    ""smoothScrolling"": true
  },
  ""features"": {
    ""autoSave"": ""afterDelay"",
    ""autoSaveDelay"": 1000,
    ""suggestions"": true,
    ""quickSuggestions"": {
      ""other"": true,
      ""comments"": false,
      ""strings"": false
    },
    ""suggestOnTriggerCharacters"": true,
    ""tabCompletion"": ""on"",
    ""snippetsPreventQuickSuggestions"": false,
    ""parameterHints"": true
  },
  ""files"": {
    ""encoding"": ""utf8"",
    ""eol"": ""\\n"",
    ""insertFinalNewline"": true,
    ""trimTrailingWhitespace"": true,
    ""excludes"": [""**/.git"", ""**/node_modules"", ""**/dist""]
  },
  ""search"": {
    ""quickOpen"": {
      ""includeSymbols"": true,
      ""includeHistory"": true
    },
    ""exclude"": {
      ""**/node_modules"": true,
      ""**/.git"": true
    }
  },
  ""diffEditor"": {
    ""ignoreTrimWhitespace"": true,
    ""renderSideBySide"": true
  },
  ""keyboardShortcuts"": {
    ""save"": ""Ctrl+S"",
    ""find"": ""Ctrl+F"",
    ""replace"": ""Ctrl+H"",
    ""formatDocument"": ""Shift+Alt+F"",
    ""commentLine"": ""Ctrl+/"",
    ""goToLine"": ""Ctrl+G""
  }
}";

            // Ensure directory exists
            await _fileSystem.CreateDirectoryAsync(configDir);
            
            // Write the file
            bool result = await _fileSystem.WriteAllTextAsync(filePath, content, SystemUser);
            
            if (result)
            {
                // Set proper ownership and permissions
                await _fileSystem.SetPermissionsAsync(filePath, 0644, user.UserId, user.PrimaryGroupId, SystemUser);
                _logger.LogDebug("Created editor config for {Username}", user.Username);
            }
            
            return result;
        }

        private async Task LoadUsersFromFileAsync()
        {
            try
            {
                if (await _fileSystem.FileExistsAsync(PasswdFile, SystemUser))
                {
                    var content = await _fileSystem.ReadAllTextAsync(PasswdFile, SystemUser) ?? string.Empty;
                    var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var highestUid = 0;
                    int linesProcessed = 0;
                    int userCount = 0;
                    
                    foreach (var line in lines)
                    {
                        try
                        {
                            // Skip comment lines
                            if (line.Trim().StartsWith("#"))
                                continue;
                                
                            // Parse passwd line format: username:x:uid:gid:comment:home:shell
                            var parts = line.Split(':', 7);
                            if (parts.Length < 7)
                            {
                                _logger.LogWarning("Malformed line in {PasswdFile}: {Line}", PasswdFile, line);
                                continue;
                            }
                            
                            var username = parts[0];
                            if (string.IsNullOrWhiteSpace(username))
                            {
                                _logger.LogWarning("Invalid username in {PasswdFile}: {Line}", PasswdFile, line);
                                continue;
                            }
                            
                            // Parse user ID
                            if (!int.TryParse(parts[2], out var uid))
                            {
                                _logger.LogWarning("Invalid UID in {PasswdFile}: {Line}", PasswdFile, line);
                                continue;
                            }
                            
                            // Parse primary group ID
                            if (!int.TryParse(parts[3], out var gid))
                            {
                                _logger.LogWarning("Invalid GID in {PasswdFile}: {Line}", PasswdFile, line);
                                continue;
                            }
                            
                            // Don't overwrite existing user objects
                            if (_users.ContainsKey(username))
                            {
                                _logger.LogDebug("User {Username} already loaded, skipping passwd entry", username);
                                continue;
                            }
                            
                            // Create the user object
                            var user = new User
                            {
                                Username = username,
                                UserId = uid,
                                PrimaryGroupId = gid,
                                FullName = parts[4],
                                HomeDirectory = parts[5],
                                Shell = parts[6]
                            };
                            
                            // Track highest UID for _nextUserId
                            if (uid > highestUid)
                                highestUid = uid;
                            
                            // Add to dictionaries
                            _users[username] = user;
                            _usersByUid[uid] = user;
                            userCount++;
                            
                            // Initialize environment variables
                            user.UpdateEnvironment();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing line in {PasswdFile}: {Line}", PasswdFile, line);
                        }
                        
                        linesProcessed++;
                    }
                    
                    // Set next user ID to highest + 1, with minimum of 1000
                    if (highestUid >= 999)
                        _nextUserId = highestUid + 1;
                    
                    _logger.LogInformation("Loaded {UserCount} users from {PasswdFile} ({LinesProcessed} lines processed)", 
                        userCount, PasswdFile, linesProcessed);
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
                if (await _fileSystem.FileExistsAsync(GroupFile, SystemUser))
                {
                    var content = await _fileSystem.ReadAllTextAsync(GroupFile, SystemUser) ?? string.Empty;
                    var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var highestGid = 0;
                    int linesProcessed = 0;
                    int groupCount = 0;
                    
                    foreach (var line in lines)
                    {
                        try
                        {
                            // Skip comment lines
                            if (line.Trim().StartsWith("#"))
                                continue;
                                
                            // Parse group line format: groupname:x:gid:member1,member2,...
                            var parts = line.Split(':', 4);
                            if (parts.Length < 4)
                            {
                                _logger.LogWarning("Malformed line in {GroupFile}: {Line}", GroupFile, line);
                                continue;
                            }
                            
                            var groupName = parts[0];
                            if (string.IsNullOrWhiteSpace(groupName))
                            {
                                _logger.LogWarning("Invalid group name in {GroupFile}: {Line}", GroupFile, line);
                                continue;
                            }
                            
                            // Parse group ID
                            if (!int.TryParse(parts[2], out var gid))
                            {
                                _logger.LogWarning("Invalid GID in {GroupFile}: {Line}", GroupFile, line);
                                continue;
                            }
                            
                            // Don't overwrite existing group objects
                            if (_groups.ContainsKey(groupName))
                            {
                                _logger.LogDebug("Group {GroupName} already loaded, skipping group entry", groupName);
                                continue;
                            }
                            
                            // Parse members
                            var memberNames = !string.IsNullOrEmpty(parts[3])
                                ? parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                : Array.Empty<string>();
                            

                            // Create the group object
                            var group = new Group(groupName, "", gid);
                            
                            // Add members by username
                            foreach (var memberName in memberNames)
                            {
                                if (_users.TryGetValue(memberName, out var user))
                                {
                                    group.AddMember(user.UserId);
                                    
                                    // Also update the user's additional groups if needed
                                    if (user.PrimaryGroupId != gid)
                                        user.AddToGroup(gid);
                                }
                                else
                                {
                                    _logger.LogDebug("User {Username} not found for group {GroupName}, will be added later if found", 
                                        memberName, groupName);
                                }
                            }
                            
                            // Track highest GID for _nextGroupId
                            if (gid > highestGid)
                                highestGid = gid;
                            

                            // Add to dictionaries
                            _groups[groupName] = group;
                            _groupsByGid[gid] = group;
                            groupCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing line in {GroupFile}: {Line}", GroupFile, line);
                        }
                        
                        linesProcessed++;
                    }
                    
                    // Set next group ID to highest + 1, with minimum of 1000
                    if (highestGid >= 999)
                        _nextGroupId = highestGid + 1;
                    
                    _logger.LogInformation("Loaded {GroupCount} groups from {GroupFile} ({LinesProcessed} lines processed)", 
                        groupCount, GroupFile, linesProcessed);
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
                
                // Create a temporary file path in the same directory
                string tempFile = $"{PasswdFile}.tmp";
                
                // Write to the temporary file
                var content = string.Join("\n", lines);
                await _fileSystem.WriteAllTextAsync(tempFile, content, SystemUser);
                
                // Atomically replace the original file
                await AtomicReplaceFileAsync(tempFile, PasswdFile);
                
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
                
                // Create a temporary file path in the same directory
                string tempFile = $"{GroupFile}.tmp";
                
                // Write to the temporary file
                var content = string.Join("\n", lines);
                await _fileSystem.WriteAllTextAsync(tempFile, content, SystemUser);
                
                // Atomically replace the original file
                await AtomicReplaceFileAsync(tempFile, GroupFile);
                
                _logger.LogDebug("Saved groups to {GroupFile}", GroupFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save groups to {GroupFile}", GroupFile);
            }
        }
        
        /// <summary>
        /// Atomically replaces a file with a new version to prevent corruption
        /// </summary>
        /// <param name="sourcePath">Path to the new file</param>
        /// <param name="destinationPath">Path to the file to be replaced</param>
        private async Task AtomicReplaceFileAsync(string sourcePath, string destinationPath)
        {
            try
            {
                // Check if the source file exists
                if (!await _fileSystem.FileExistsAsync(sourcePath, SystemUser))
                {
                    throw new InvalidOperationException($"Source file '{sourcePath}' does not exist for atomic replace operation");
                }
                
                // Create a backup of the destination file if it exists
                if (await _fileSystem.FileExistsAsync(destinationPath, SystemUser))
                {
                    string backupPath = $"{destinationPath}.bak";
                    
                    // Use the correct method via extension
                    await FileSystemPermissionExtensions.CopyFileAsync(_fileSystem, destinationPath, backupPath, SystemUser);
                }
                
                // Rename source to destination (atomic operation)
                await FileSystemPermissionExtensions.MoveFileAsync(_fileSystem, sourcePath, destinationPath, SystemUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform atomic file replacement of {DestinationPath}", destinationPath);
                throw;
            }
        }
    }
}
