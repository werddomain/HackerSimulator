# User Management System Analysis Plan

**Project**: HackerOS Simulator - User Management & Linux-like Behavior
**Created**: June 30, 2025
**Purpose**: Design comprehensive user management system matching Linux-like behavior with multi-user session support
**Dependencies**: `analysis-plan-authentication.md`, `analysis-plan-main-entry.md`

---

## 🎯 Executive Summary

This analysis plan designs a comprehensive user management system for HackerOS that mimics Linux user/group behavior while supporting modern session management features. The system will handle user accounts, groups, permissions, home directories, and user-specific configurations in a way that feels authentic to a Linux environment but runs entirely in the browser.

## 📊 Current State Analysis

### Existing User-Related Code
Based on the current codebase examination:
- No dedicated user management system exists
- File system exists but lacks user ownership/permissions
- Shell exists but has no user context
- Settings service exists but isn't user-scoped
- No concept of home directories or user profiles

### What Needs to Be Built
1. **User Account Management**: User creation, authentication, profile management
2. **Group Management**: User groups, group memberships, group permissions
3. **Permission System**: File ownership, access control, privilege escalation
4. **Home Directory Management**: User-specific directories and initialization
5. **User Preferences**: User-specific settings and customizations
6. **Session Management**: Multi-user sessions, session switching

---

## 🏗️ Architecture Design

### User Management Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Interface Layer                     │
│  (Login, User Profile, Admin Panel, Session Switcher)          │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                    User Management Services                     │
│  IUserManager │ IGroupManager │ IPermissionService │ IUserSession │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                      User Data Models                           │
│    User │ Group │ Permission │ UserPreferences │ UserSession    │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                    Storage & Persistence                        │
│  Virtual File System │ LocalStorage │ IndexedDB (future)       │
└─────────────────────────────────────────────────────────────────┘
```

### Linux-Style Directory Structure

```
/                           # Root filesystem
├── etc/                    # System configuration
│   ├── passwd              # User database simulation
│   ├── group               # Group database simulation
│   ├── shadow              # Password hashes (simulated)
│   ├── hackeros.conf       # System configuration
│   └── default/            # Default user templates
├── home/                   # User home directories
│   ├── admin/              # Admin user home
│   │   ├── .config/        # User configuration
│   │   ├── .bash_history   # Command history
│   │   ├── .bashrc         # Shell configuration
│   │   └── Desktop/        # Desktop files
│   └── guest/              # Guest user home (if enabled)
├── root/                   # Root user home
├── tmp/                    # Temporary files
└── var/                    # Variable data
    └── log/                # System logs
        └── auth.log        # Authentication logs
```

### User Permission Model

```
Linux Permission Bits: rwxrwxrwx (owner-group-other)
                        421421421

User Context Hierarchy:
┌─────────────┐
│    Root     │ ← Full system access
│   (uid=0)   │
└─────────────┘
       │
┌─────────────┐
│ Admin Users │ ← Sudo privileges
│ (admin grp) │
└─────────────┘
       │
┌─────────────┐
│ Regular     │ ← Standard user access
│ Users       │
└─────────────┘
       │
┌─────────────┐
│   Guest     │ ← Limited access (optional)
│  (guest)    │
└─────────────┘
```

---

## 💻 Data Models Design

### 1. User Model
```csharp
public class User
{
    // Linux-style user properties
    public string UserId { get; set; }                    // UUID for browser compatibility
    public int Uid { get; set; }                          // Unix user ID (0=root, 1000+=regular)
    public string Username { get; set; }                  // Login name
    public string HashedPassword { get; set; }            // BCrypt hashed password
    public string FullName { get; set; }                  // Display name/GECOS
    public string HomeDirectory { get; set; }             // /home/username
    public string Shell { get; set; }                     // Default shell (/bin/bash)
    public int PrimaryGroupId { get; set; }               // Primary group GID
    
    // Additional properties for enhanced functionality
    public List<int> SecondaryGroups { get; set; }        // Additional group memberships
    public UserStatus Status { get; set; }                // Active, Locked, Disabled
    public DateTime CreatedDate { get; set; }
    public DateTime LastLogin { get; set; }
    public DateTime LastPasswordChange { get; set; }
    public UserPreferences Preferences { get; set; }
    public SecuritySettings Security { get; set; }
    
    // Computed properties
    public bool IsRoot => Uid == 0;
    public bool IsAdmin => SecondaryGroups.Contains(0) || PrimaryGroupId == 0;
    public bool IsSystemUser => Uid < 1000;
    public string HomePath => $"/home/{Username}";
}

public enum UserStatus
{
    Active,
    Locked,
    Disabled,
    PendingActivation
}
```

### 2. Group Model
```csharp
public class Group
{
    public int Gid { get; set; }                          // Group ID (0=root/wheel)
    public string GroupName { get; set; }                 // Group name
    public string Description { get; set; }               // Group description
    public List<string> Members { get; set; }             // Group member usernames
    public GroupType Type { get; set; }                   // System, User, Special
    public DateTime CreatedDate { get; set; }
    public Dictionary<string, object> Properties { get; set; } // Group-specific properties
    
    // Standard Linux groups
    public bool IsAdminGroup => Gid == 0 || GroupName == "admin" || GroupName == "wheel";
    public bool IsSystemGroup => Gid < 1000;
}

public enum GroupType
{
    System,    // System groups (root, admin, etc.)
    User,      // Regular user groups
    Special    // Application-specific groups
}
```

### 3. Permission Model
```csharp
public class FilePermissions
{
    public int Mode { get; set; }                         // Unix permission bits (octal)
    public string Owner { get; set; }                     // Owner username
    public string Group { get; set; }                     // Group name
    
    // Permission checking methods
    public bool CanRead(User user) => CheckPermission(user, PermissionType.Read);
    public bool CanWrite(User user) => CheckPermission(user, PermissionType.Write);
    public bool CanExecute(User user) => CheckPermission(user, PermissionType.Execute);
    
    // Permission modification methods
    public void SetOwnerPermissions(bool read, bool write, bool execute);
    public void SetGroupPermissions(bool read, bool write, bool execute);
    public void SetOtherPermissions(bool read, bool write, bool execute);
    
    // Unix-style permission string (e.g., "rwxr-xr--")
    public string ToUnixString();
    public static FilePermissions FromUnixString(string permissions);
    public static FilePermissions FromOctal(int octal);
}

public enum PermissionType
{
    Read = 4,
    Write = 2,
    Execute = 1
}
```

### 4. User Preferences Model
```csharp
public class UserPreferences
{
    // Desktop preferences
    public string Theme { get; set; } = "gothic-hacker";
    public bool ShowDesktopIcons { get; set; } = true;
    public string Wallpaper { get; set; }
    public double TerminalTransparency { get; set; } = 0.8;
    
    // Shell preferences
    public string DefaultShell { get; set; } = "/bin/bash";
    public string Prompt { get; set; } = "\\u@\\h:\\w\\$ ";
    public List<string> StartupCommands { get; set; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public Dictionary<string, string> Aliases { get; set; } = new();
    
    // Application preferences
    public List<string> PinnedApplications { get; set; } = new();
    public Dictionary<string, object> ApplicationSettings { get; set; } = new();
    
    // Privacy and security
    public bool EnableCommandHistory { get; set; } = true;
    public bool EnableActivityLogging { get; set; } = true;
    public TimeSpan AutoLockTimeout { get; set; } = TimeSpan.FromMinutes(15);
}

public class SecuritySettings
{
    public int MaxFailedLogins { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);
    public bool RequirePasswordChange { get; set; } = false;
    public DateTime PasswordExpiry { get; set; }
    public List<string> TrustedDevices { get; set; } = new();
    public bool EnableTwoFactor { get; set; } = false;
}
```

---

## 🔧 Service Interface Design

### 1. IUserManager Interface
```csharp
public interface IUserManager
{
    // User lifecycle management
    Task<User> CreateUserAsync(User user);
    Task<User> GetUserAsync(string username);
    Task<User> GetUserByIdAsync(string userId);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<bool> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(string username);
    Task<bool> UserExistsAsync(string username);
    Task<bool> HasUsersAsync();
    
    // Authentication
    Task<bool> ValidateCredentialsAsync(string username, string password);
    Task<bool> ChangePasswordAsync(string username, string currentPassword, string newPassword);
    Task<bool> ResetPasswordAsync(string username, string newPassword); // Admin only
    
    // User state management
    Task<bool> LockUserAsync(string username);
    Task<bool> UnlockUserAsync(string username);
    Task<bool> DisableUserAsync(string username);
    Task<bool> EnableUserAsync(string username);
    
    // Group management
    Task<bool> AddUserToGroupAsync(string username, string groupName);
    Task<bool> RemoveUserFromGroupAsync(string username, string groupName);
    Task<IEnumerable<Group>> GetUserGroupsAsync(string username);
    
    // Home directory management
    Task InitializeUserHomeDirectoryAsync(User user);
    Task<string> GetUserHomeDirectoryAsync(string username);
    
    // Events
    event EventHandler<UserCreatedEventArgs> UserCreated;
    event EventHandler<UserDeletedEventArgs> UserDeleted;
    event EventHandler<UserModifiedEventArgs> UserModified;
}
```

### 2. IGroupManager Interface
```csharp
public interface IGroupManager
{
    // Group lifecycle management
    Task<Group> CreateGroupAsync(Group group);
    Task<Group> GetGroupAsync(string groupName);
    Task<Group> GetGroupByIdAsync(int gid);
    Task<IEnumerable<Group>> GetAllGroupsAsync();
    Task<bool> UpdateGroupAsync(Group group);
    Task<bool> DeleteGroupAsync(string groupName);
    Task<bool> GroupExistsAsync(string groupName);
    
    // Group membership
    Task<bool> AddMemberAsync(string groupName, string username);
    Task<bool> RemoveMemberAsync(string groupName, string username);
    Task<IEnumerable<string>> GetGroupMembersAsync(string groupName);
    Task<bool> IsUserInGroupAsync(string username, string groupName);
    
    // Permission management
    Task<bool> CanUserPerformActionAsync(string username, string action, string resource);
    
    // System group initialization
    Task InitializeSystemGroupsAsync();
}
```

### 3. IPermissionService Interface
```csharp
public interface IPermissionService
{
    // Permission checking
    Task<bool> CanAccessFileAsync(User user, string path, PermissionType permission);
    Task<bool> CanExecuteCommandAsync(User user, string command);
    Task<bool> CanAccessApplicationAsync(User user, string applicationId);
    
    // Permission management
    Task<bool> SetFilePermissionsAsync(string path, FilePermissions permissions, User requestingUser);
    Task<bool> ChangeFileOwnerAsync(string path, string newOwner, User requestingUser);
    Task<bool> ChangeFileGroupAsync(string path, string newGroup, User requestingUser);
    
    // Privilege escalation
    Task<bool> CanEscalatePrivilegesAsync(User user);
    Task<User> EscalatePrivilegesAsync(User user, string password);
    Task<bool> ValidateSudoAsync(User user, string password);
    
    // Access control lists (future enhancement)
    Task<AccessControlList> GetACLAsync(string path);
    Task SetACLAsync(string path, AccessControlList acl, User requestingUser);
}
```

---

## 🏠 Home Directory Management

### Home Directory Structure
```
/home/{username}/
├── .config/                    # User configuration files
│   ├── hackeros/              # HackerOS specific config
│   │   ├── user.conf          # User preferences
│   │   ├── theme.conf         # Theme settings
│   │   └── keybindings.conf   # Keyboard shortcuts
│   ├── applications/          # Application configurations
│   └── autostart/             # Startup applications
├── .cache/                    # Cached data
├── .local/                    # Local user data
│   ├── share/                 # Shared data
│   └── bin/                   # User executables
├── Desktop/                   # Desktop files
├── Documents/                 # User documents
├── Downloads/                 # Downloaded files
├── .bash_history             # Command history
├── .bashrc                   # Shell configuration
├── .profile                  # Profile settings
└── .ssh/                     # SSH configuration (future)
```

### Home Directory Initialization
```csharp
public class HomeDirectoryInitializer
{
    private readonly IVirtualFileSystem _fileSystem;
    
    public async Task InitializeAsync(User user)
    {
        var homePath = user.HomeDirectory;
        
        // Create directory structure
        await CreateDirectoryStructureAsync(homePath);
        
        // Set proper permissions
        await SetHomePermissionsAsync(homePath, user);
        
        // Create default configuration files
        await CreateDefaultConfigurationAsync(homePath, user);
        
        // Initialize application directories
        await InitializeApplicationDirectoriesAsync(homePath, user);
    }
    
    private async Task CreateDirectoryStructureAsync(string homePath)
    {
        var directories = new[]
        {
            ".config", ".config/hackeros", ".config/applications", ".config/autostart",
            ".cache", ".local", ".local/share", ".local/bin",
            "Desktop", "Documents", "Downloads", ".ssh"
        };
        
        foreach (var dir in directories)
        {
            await _fileSystem.CreateDirectoryAsync($"{homePath}/{dir}");
        }
    }
    
    private async Task CreateDefaultConfigurationAsync(string homePath, User user)
    {
        // Create .bashrc
        var bashrcContent = GenerateBashrcContent(user);
        await _fileSystem.WriteFileAsync($"{homePath}/.bashrc", bashrcContent);
        
        // Create user configuration
        var userConfig = GenerateUserConfigContent(user);
        await _fileSystem.WriteFileAsync($"{homePath}/.config/hackeros/user.conf", userConfig);
        
        // Create theme configuration
        var themeConfig = GenerateThemeConfigContent(user);
        await _fileSystem.WriteFileAsync($"{homePath}/.config/hackeros/theme.conf", themeConfig);
    }
}
```

---

## 🔐 Security Implementation

### Password Security
```csharp
public class PasswordManager
{
    private const int BcryptRounds = 12;
    
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BcryptRounds);
    }
    
    public bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
    
    public bool IsStrongPassword(string password)
    {
        // Implement password strength checking
        return password.Length >= 8 &&
               password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit) &&
               password.Any(ch => !char.IsLetterOrDigit(ch));
    }
}
```

### Privilege Escalation (Sudo)
```csharp
public class PrivilegeEscalationService
{
    public async Task<bool> CanUseSudoAsync(User user)
    {
        // Check if user is in admin/wheel group
        var groups = await _groupManager.GetUserGroupsAsync(user.Username);
        return groups.Any(g => g.IsAdminGroup);
    }
    
    public async Task<User> CreateSudoContextAsync(User user, string password)
    {
        // Validate password
        if (!await _userManager.ValidateCredentialsAsync(user.Username, password))
        {
            throw new UnauthorizedAccessException("Invalid password for sudo");
        }
        
        // Create temporary elevated context
        var sudoUser = user.Clone();
        sudoUser.IsSudoElevated = true;
        sudoUser.SudoElevationTime = DateTime.UtcNow;
        
        return sudoUser;
    }
}
```

---

## 💾 Data Persistence Strategy

### File System Storage
```csharp
public class UserDataPersistence
{
    private readonly IVirtualFileSystem _fileSystem;
    
    // Store user database in /etc/passwd format
    public async Task SaveUserDatabaseAsync(IEnumerable<User> users)
    {
        var passwdContent = GeneratePasswdContent(users);
        await _fileSystem.WriteFileAsync("/etc/passwd", passwdContent);
        
        var shadowContent = GenerateShadowContent(users);
        await _fileSystem.WriteFileAsync("/etc/shadow", shadowContent);
    }
    
    // Store group database in /etc/group format
    public async Task SaveGroupDatabaseAsync(IEnumerable<Group> groups)
    {
        var groupContent = GenerateGroupContent(groups);
        await _fileSystem.WriteFileAsync("/etc/group", groupContent);
    }
    
    private string GeneratePasswdContent(IEnumerable<User> users)
    {
        // Format: username:x:uid:gid:gecos:home:shell
        return string.Join("\n", users.Select(u => 
            $"{u.Username}:x:{u.Uid}:{u.PrimaryGroupId}:{u.FullName}:{u.HomeDirectory}:{u.Shell}"));
    }
}
```

### LocalStorage Caching
```csharp
public class UserCacheService
{
    private const string USERS_CACHE_KEY = "hackeros_users";
    private const string CURRENT_USER_KEY = "hackeros_current_user";
    
    public async Task CacheUserAsync(User user)
    {
        var json = JsonSerializer.Serialize(user);
        await _localStorage.SetItemAsync(CURRENT_USER_KEY, json);
    }
    
    public async Task<User> GetCachedUserAsync()
    {
        var json = await _localStorage.GetItemAsync<string>(CURRENT_USER_KEY);
        return json != null ? JsonSerializer.Deserialize<User>(json) : null;
    }
}
```

---

## 🧪 Testing Strategy

### Unit Testing
```csharp
[TestClass]
public class UserManagerTests
{
    [TestMethod]
    public async Task CreateUser_ValidUser_ShouldSucceed()
    {
        // Test user creation with valid data
    }
    
    [TestMethod]
    public async Task ValidateCredentials_CorrectPassword_ShouldReturnTrue()
    {
        // Test password validation
    }
    
    [TestMethod]
    public async Task AddUserToGroup_ValidGroup_ShouldSucceed()
    {
        // Test group membership
    }
}

[TestClass]
public class PermissionServiceTests
{
    [TestMethod]
    public async Task CanAccessFile_OwnerWithReadPermission_ShouldReturnTrue()
    {
        // Test file permission checking
    }
    
    [TestMethod]
    public async Task EscalatePrivileges_AdminUser_ShouldSucceed()
    {
        // Test sudo functionality
    }
}
```

### Integration Testing
```csharp
[TestClass]
public class UserManagementIntegrationTests
{
    [TestMethod]
    public async Task CompleteUserWorkflow_CreateLoginAndAccess_ShouldWork()
    {
        // Test complete user lifecycle
        // 1. Create user
        // 2. Initialize home directory
        // 3. Login
        // 4. Access files
        // 5. Use sudo
    }
}
```

---

## 📈 Performance Considerations

### User Data Loading
- **Lazy Loading**: Load user data only when needed
- **Caching**: Cache frequently accessed user information
- **Pagination**: For user lists in admin interfaces

### Permission Checking
- **Caching**: Cache permission results for frequently accessed files
- **Batch Operations**: Group multiple permission checks
- **Optimization**: Pre-calculate permissions for common scenarios

### Home Directory Operations
- **Virtual Directories**: Don't create physical directories until needed
- **Template System**: Use templates for faster home directory creation
- **Bulk Operations**: Optimize bulk file operations

---

## 🎯 Implementation Phases

### Phase 1: Core User Models (1-2 days)
1. Create User, Group, and Permission models
2. Implement UserPreferences and SecuritySettings
3. Create basic validation logic
4. Add unit tests for models

### Phase 2: User Management Services (2-3 days)
1. Implement IUserManager interface
2. Implement IGroupManager interface
3. Create PasswordManager service
4. Add user persistence to file system

### Phase 3: Permission System (2-3 days)
1. Implement IPermissionService interface
2. Add file permission checking
3. Implement sudo/privilege escalation
4. Integrate with file system

### Phase 4: Home Directory Management (1-2 days)
1. Create HomeDirectoryInitializer
2. Implement default configuration generation
3. Add directory structure creation
4. Test home directory workflows

### Phase 5: Integration & Testing (1-2 days)
1. Integration testing
2. Performance optimization
3. Security validation
4. Documentation

---

## 📋 Success Criteria

### Functional Requirements
- [x] Create and manage user accounts
- [x] Linux-like user/group system
- [x] File ownership and permissions
- [x] Home directory initialization
- [x] Password authentication
- [x] Privilege escalation (sudo)

### Non-Functional Requirements
- [x] Secure password storage (BCrypt)
- [x] Performance: < 100ms for user operations
- [x] Memory efficient user data management
- [x] Proper session isolation

### Integration Requirements
- [x] Seamless integration with authentication system
- [x] File system permission integration
- [x] Shell user context integration
- [x] Application user awareness

---

**Next Steps**: Use this analysis plan to guide the implementation of user management models and services as outlined in the authentication task list.
