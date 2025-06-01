using HackerOs.IO.FileSystem;
using HackerOs.OS.Settings;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Service for integrating user management with the file system and settings
    /// </summary>
    public interface IUserSystemIntegration
    {
        /// <summary>
        /// Initializes a user's home directory with default files and folders
        /// </summary>
        Task InitializeUserHomeDirectoryAsync(User user);

        /// <summary>
        /// Loads user preferences from ~/.config directory
        /// </summary>
        Task<Dictionary<string, string>> LoadUserPreferencesAsync(User user);

        /// <summary>
        /// Saves user preferences to ~/.config directory
        /// </summary>
        Task SaveUserPreferencesAsync(User user, Dictionary<string, string> preferences);

        /// <summary>
        /// Creates a user profile configuration file
        /// </summary>
        Task CreateUserProfileAsync(User user);

        /// <summary>
        /// Switches to a different user context (su functionality)
        /// </summary>
        Task<bool> SwitchUserAsync(string targetUsername, string password, User currentUser);

        /// <summary>
        /// Executes a command with elevated privileges (sudo functionality)
        /// </summary>
        Task<bool> ExecuteWithSudoAsync(string command, string password, User currentUser);

        /// <summary>
        /// Checks if a user has permission to perform an action
        /// </summary>
        Task<bool> CheckUserPermissionAsync(User user, string resource, string action);

        /// <summary>
        /// Updates user's current working directory
        /// </summary>
        Task UpdateUserWorkingDirectoryAsync(User user, string newDirectory);

        /// <summary>
        /// Gets the effective user for the current session (for su/sudo)
        /// </summary>
        Task<User?> GetEffectiveUserAsync(UserSession session);
    }

    /// <summary>
    /// Implementation of user system integration
    /// </summary>
    public class UserSystemIntegration : IUserSystemIntegration
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ISettingsService _settingsService;
        private readonly IUserManager _userManager;
        private readonly ILogger<UserSystemIntegration> _logger;

        // Standard directories to create in user home
        private readonly string[] _standardUserDirectories = {
            ".config",
            ".config/hackeros",
            ".config/applications",
            ".local",
            ".local/bin",
            ".local/share",
            ".cache",
            "Desktop",
            "Documents",
            "Downloads",
            "Pictures",
            "Videos",
            "Music",
            "Templates",
            "Public"
        };

        // Standard files to create in user home
        private readonly Dictionary<string, string> _standardUserFiles = new()
        {
            [".bashrc"] = @"# ~/.bashrc: executed by bash(1) for non-login shells.

# If not running interactively, don't do anything
[ -z ""$PS1"" ] && return

# Set a fancy prompt
PS1='${debian_chroot:+($debian_chroot)}\u@\h:\w\$ '

# Enable color support of ls and also add handy aliases
if [ -x /usr/bin/dircolors ]; then
    test -r ~/.dircolors && eval ""$(dircolors -b ~/.dircolors)"" || eval ""$(dircolors -b)""
    alias ls='ls --color=auto'
    alias grep='grep --color=auto'
fi

# Some more ls aliases
alias ll='ls -alF'
alias la='ls -A'
alias l='ls -CF'

# Enable programmable completion features
if ! shopt -oq posix; then
  if [ -f /usr/share/bash-completion/bash_completion ]; then
    . /usr/share/bash-completion/bash_completion
  elif [ -f /etc/bash_completion ]; then
    . /etc/bash_completion
  fi
fi
",
            [".profile"] = @"# ~/.profile: executed by the command interpreter for login shells.

# Set PATH to include user's private bin if it exists
if [ -d ""$HOME/.local/bin"" ] ; then
    PATH=""$HOME/.local/bin:$PATH""
fi

# Set environment variables
export EDITOR=nano
export BROWSER=browser
export TERMINAL=terminal
",
            [".config/hackeros/user.conf"] = @"[Desktop]
theme=gothic-hacker
wallpaper=/usr/share/backgrounds/hackeros-default.jpg
show_icons=true
auto_arrange=false

[Terminal]
transparency=0.8
font_size=14
color_scheme=green-on-black
startup_command=

[Applications]
default_browser=browser
default_editor=editor
default_file_manager=file-explorer

[Preferences]
confirm_delete=true
show_hidden_files=false
auto_save=true
",
            ["Desktop/README.txt"] = @"Welcome to HackerOS!

This is your desktop directory. You can create shortcuts to applications and files here.

Getting Started:
- Open the terminal to access the command line
- Use the file explorer to browse your files
- Check the settings to customize your experience

Default accounts:
- root (password: hackeros) - Administrator account

Have fun exploring the system!
"
        };

        public UserSystemIntegration(
            IVirtualFileSystem fileSystem,
            ISettingsService settingsService,
            IUserManager userManager,
            ILogger<UserSystemIntegration> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeUserHomeDirectoryAsync(User user)
        {
            try
            {
                var homeDir = user.HomeDirectory;
                
                // Ensure home directory exists
                await _fileSystem.CreateDirectoryAsync(homeDir);
                _logger.LogDebug("Created home directory: {HomeDir}", homeDir);

                // Create standard subdirectories
                foreach (var subDir in _standardUserDirectories)
                {
                    var fullPath = $"{homeDir}/{subDir}";
                    await _fileSystem.CreateDirectoryAsync(fullPath);
                    _logger.LogDebug("Created directory: {Directory}", fullPath);
                }

                // Create standard files
                foreach (var file in _standardUserFiles)
                {
                    var filePath = $"{homeDir}/{file.Key}";
                      // Only create if it doesn't exist
                    if (!await _fileSystem.FileExistsAsync(filePath, user))
                    {
                        await _fileSystem.WriteAllTextAsync(filePath, file.Value);
                        _logger.LogDebug("Created file: {FilePath}", filePath);
                    }
                }

                // Set proper permissions for user files
                await SetUserDirectoryPermissionsAsync(homeDir, user);

                _logger.LogInformation("Initialized home directory for user {Username}", user.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize home directory for user {Username}", user.Username);
                throw;
            }
        }

        public async Task<Dictionary<string, string>> LoadUserPreferencesAsync(User user)
        {
            var preferences = new Dictionary<string, string>();

            try
            {
                var configPath = $"{user.HomeDirectory}/.config/hackeros/user.conf";
                
                if (await _fileSystem.FileExistsAsync(configPath, user))
                {
                    // Load preferences using settings service
                    var desktopTheme = await _settingsService.GetSettingAsync<string>("Desktop", "theme", "gothic-hacker", SettingScope.User);
                    var terminalTransparency = await _settingsService.GetSettingAsync<string>("Terminal", "transparency", "0.8", SettingScope.User);
                    var showHiddenFiles = await _settingsService.GetSettingAsync<string>("Preferences", "show_hidden_files", "false", SettingScope.User);

                    if (desktopTheme != null) preferences["desktop.theme"] = desktopTheme;
                    if (terminalTransparency != null) preferences["terminal.transparency"] = terminalTransparency;
                    if (showHiddenFiles != null) preferences["preferences.show_hidden_files"] = showHiddenFiles;
                }

                // Merge with user's stored preferences
                foreach (var pref in user.Preferences)
                {
                    preferences[pref.Key] = pref.Value;
                }

                _logger.LogDebug("Loaded {Count} preferences for user {Username}", preferences.Count, user.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load preferences for user {Username}", user.Username);
            }

            return preferences;
        }

        public async Task SaveUserPreferencesAsync(User user, Dictionary<string, string> preferences)
        {
            try
            {
                // Update user's preferences
                foreach (var pref in preferences)
                {
                    user.Preferences[pref.Key] = pref.Value;
                }

                // Save to configuration files
                foreach (var pref in preferences)
                {
                    var parts = pref.Key.Split('.');
                    if (parts.Length >= 2)
                    {
                        var section = parts[0];
                        var key = string.Join(".", parts.Skip(1));
                        await _settingsService.SetSettingAsync(section, key, pref.Value, SettingScope.User);
                    }
                }

                // Update user in user manager
                await _userManager.UpdateUserAsync(user);

                _logger.LogInformation("Saved {Count} preferences for user {Username}", preferences.Count, user.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save preferences for user {Username}", user.Username);
                throw;
            }
        }

        public async Task CreateUserProfileAsync(User user)
        {
            try
            {
                var profilePath = $"{user.HomeDirectory}/.profile";
                
                // Create personalized .profile
                var profileContent = _standardUserFiles[".profile"];
                profileContent += $"\n# User: {user.FullName} ({user.Username})\n";
                profileContent += $"# Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";
                profileContent += $"export USER=\"{user.Username}\"\n";
                profileContent += $"export HOME=\"{user.HomeDirectory}\"\n";

                await _fileSystem.WriteAllTextAsync(profilePath, profileContent);

                _logger.LogInformation("Created user profile for {Username}", user.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user profile for {Username}", user.Username);
                throw;
            }
        }

        public async Task<bool> SwitchUserAsync(string targetUsername, string password, User currentUser)
        {
            try
            {
                // Authenticate the target user
                var targetUser = await _userManager.AuthenticateAsync(targetUsername, password);
                if (targetUser == null)
                {
                    _logger.LogWarning("Failed su attempt: Invalid credentials for {TargetUsername}", targetUsername);
                    return false;
                }

                // Check if current user has permission to switch
                if (!currentUser.IsAdmin && currentUser.Username != "root")
                {
                    _logger.LogWarning("User {CurrentUser} attempted su without sufficient privileges", currentUser.Username);
                    return false;
                }

                _logger.LogInformation("User {CurrentUser} successfully switched to {TargetUser}", 
                    currentUser.Username, targetUsername);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during su operation from {CurrentUser} to {TargetUser}", 
                    currentUser.Username, targetUsername);
                return false;
            }
        }

        public async Task<bool> ExecuteWithSudoAsync(string command, string password, User currentUser)
        {
            try
            {
                // Verify user's password
                if (!currentUser.VerifyPassword(password))
                {
                    _logger.LogWarning("Failed sudo attempt: Invalid password for user {Username}", currentUser.Username);
                    return false;
                }

                // Check if user is in wheel or admin group
                var wheelGroup = await _userManager.GetGroupAsync("wheel");
                var adminGroup = await _userManager.GetGroupAsync("admin");

                bool canSudo = currentUser.IsAdmin || 
                              (wheelGroup != null && currentUser.BelongsToGroup(wheelGroup.GroupId)) ||
                              (adminGroup != null && currentUser.BelongsToGroup(adminGroup.GroupId));

                if (!canSudo)
                {
                    _logger.LogWarning("User {Username} attempted sudo without sufficient privileges", currentUser.Username);
                    return false;
                }

                _logger.LogInformation("User {Username} executed sudo command: {Command}", currentUser.Username, command);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sudo operation for user {Username}", currentUser.Username);
                return false;
            }
        }

        public async Task<bool> CheckUserPermissionAsync(User user, string resource, string action)
        {
            try
            {
                // Admin users have all permissions
                if (user.IsAdmin || user.Username == "root")
                {
                    return true;
                }

                // Check file system permissions
                if (resource.StartsWith("/"))
                {
                    return await CheckFileSystemPermissionAsync(user, resource, action);
                }

                // Check application permissions
                if (resource.StartsWith("app:"))
                {
                    return await CheckApplicationPermissionAsync(user, resource, action);
                }

                // Default deny for unknown resources
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission for user {Username} on {Resource}:{Action}", 
                    user.Username, resource, action);
                return false;
            }
        }

        public async Task UpdateUserWorkingDirectoryAsync(User user, string newDirectory)
        {
            try
            {                // Validate directory exists
                if (!await _fileSystem.DirectoryExistsAsync(newDirectory, user))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {newDirectory}");
                }

                // Check user has access to the directory
                if (!await CheckFileSystemPermissionAsync(user, newDirectory, "read"))
                {
                    throw new UnauthorizedAccessException($"Access denied to directory: {newDirectory}");
                }

                // Update user's environment
                user.Environment["PWD"] = newDirectory;
                await _userManager.UpdateUserAsync(user);

                _logger.LogDebug("Updated working directory for user {Username} to {Directory}", 
                    user.Username, newDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update working directory for user {Username}", user.Username);
                throw;
            }
        }

        public async Task<User?> GetEffectiveUserAsync(UserSession session)
        {
            // Check if there's a su/sudo context in the session
            var effectiveUserId = session.GetData<int?>("effective_user_id");
            if (effectiveUserId.HasValue)
            {
                return await _userManager.GetUserAsync(effectiveUserId.Value);
            }

            return session.User;
        }

        private async Task SetUserDirectoryPermissionsAsync(string homeDir, User user)
        {
            try
            {
                // In a real implementation, we would set file permissions here
                // For now, we'll just log the operation
                _logger.LogDebug("Set permissions for {HomeDir} - Owner: {Username} (UID: {UserId})", 
                    homeDir, user.Username, user.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set permissions for {HomeDir}", homeDir);
            }
        }

        private async Task<bool> CheckFileSystemPermissionAsync(User user, string path, string action)
        {
            try
            {                // Check if file/directory exists
                if (!await _fileSystem.FileExistsAsync(path, user) && !await _fileSystem.DirectoryExistsAsync(path, user))
                {
                    return false;
                }

                // For simplification, allow access to user's home directory and subdirectories
                if (path.StartsWith(user.HomeDirectory))
                {
                    return true;
                }

                // Allow read access to most system directories
                if (action == "read" && (path.StartsWith("/usr") || path.StartsWith("/bin") || path.StartsWith("/etc")))
                {
                    return true;
                }

                // Deny access to other users' home directories
                if (path.StartsWith("/home/") && !path.StartsWith(user.HomeDirectory))
                {
                    return false;
                }

                // Admin users have broader access
                if (user.IsAdmin)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file system permission for {Path}", path);
                return false;
            }
        }

        private async Task<bool> CheckApplicationPermissionAsync(User user, string resource, string action)
        {
            // Application permissions would be more complex in a real system
            // For now, allow most application access
            return true;
        }
    }
}
