using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using HackerOs.OS.Security;

namespace HackerOs.OS.User.Models
{
    /// <summary>
    /// Represents a user in the HackerOS system with Linux-like user properties.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Gets or sets the unique identifier for this user.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UNIX-style numeric user identifier.
        /// 0 is reserved for root, 1-999 for system users, 1000+ for regular users.
        /// </summary>
        public int Uid { get; set; }

        /// <summary>
        /// Gets or sets the primary group identifier for this user.
        /// </summary>
        public int Gid { get; set; }

        /// <summary>
        /// Gets or sets the username for this user.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name or display name for this user.
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hashed password for this user.
        /// This should never be exposed to client-side code directly.
        /// </summary>
        [JsonIgnore]
        public string HashedPassword { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the salt used for password hashing.
        /// </summary>
        [JsonIgnore]
        public string PasswordSalt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the home directory path for this user.
        /// </summary>
        public string HomeDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default shell for this user.
        /// </summary>
        public string DefaultShell { get; set; } = "/bin/bash";

        /// <summary>
        /// Gets or sets the list of secondary groups this user belongs to.
        /// </summary>
        public List<int> SecondaryGroups { get; set; } = new List<int>();

        /// <summary>
        /// Gets or sets the date when this user was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the date of the last login for this user.
        /// </summary>
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// Gets or sets the date of the last password change for this user.
        /// </summary>
        public DateTime LastPasswordChange { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets a value indicating whether this user is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the current status of the user account.
        /// </summary>
        public UserStatus Status { get; set; } = UserStatus.Active;

        /// <summary>
        /// Gets or sets the preferences for this user.
        /// </summary>
        public UserPreferences Preferences { get; set; } = new UserPreferences();

        /// <summary>
        /// Gets or sets the security settings for this user.
        /// </summary>
        public SecuritySettings Security { get; set; } = new SecuritySettings();

        /// <summary>
        /// Gets a value indicating whether this user is the root user.
        /// </summary>
        [JsonIgnore]
        public bool IsRoot => Uid == 0;

        /// <summary>
        /// Gets a value indicating whether this user has administrative privileges.
        /// </summary>
        [JsonIgnore]
        public bool IsAdmin => IsRoot || SecondaryGroups.Contains(0) || Gid == 0;

        /// <summary>
        /// Gets a value indicating whether this user is a system user.
        /// </summary>
        [JsonIgnore]
        public bool IsSystemUser => Uid > 0 && Uid < 1000;

        /// <summary>
        /// Validates the user's password against the provided plain text password.
        /// </summary>
        /// <param name="password">The plain text password to validate.</param>
        /// <returns>True if the password is valid, false otherwise.</returns>
        public bool ValidatePassword(string password)
        {
            // In a real implementation, this would use a secure password hashing algorithm
            // such as BCrypt, Argon2, or PBKDF2
            var hashedInput = Security.TokenService.ComputeHash(password + PasswordSalt);
            return hashedInput == HashedPassword;
        }

        /// <summary>
        /// Sets a new password for the user.
        /// </summary>
        /// <param name="newPassword">The new plain text password.</param>
        public void SetPassword(string newPassword)
        {
            // Generate a new random salt
            PasswordSalt = Guid.NewGuid().ToString("N");
            
            // Hash the password with the salt
            HashedPassword = Security.TokenService.ComputeHash(newPassword + PasswordSalt);
            
            // Update the password change timestamp
            LastPasswordChange = DateTime.UtcNow;
        }

        /// <summary>
        /// Checks if the user's password has expired.
        /// </summary>
        /// <returns>True if the password has expired, false otherwise.</returns>
        public bool HasPasswordExpired()
        {
            if (Security.PasswordExpiryDays <= 0)
            {
                return false; // No expiration policy
            }

            var expiryDate = LastPasswordChange.AddDays(Security.PasswordExpiryDays);
            return DateTime.UtcNow > expiryDate;
        }

        /// <summary>
        /// Updates the last login timestamp to the current time.
        /// </summary>
        public void UpdateLastLogin()
        {
            LastLogin = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents the status of a user account.
    /// </summary>
    public enum UserStatus
    {
        /// <summary>
        /// The user account is active and can be used for login.
        /// </summary>
        Active,

        /// <summary>
        /// The user account is temporarily locked due to failed login attempts.
        /// </summary>
        Locked,

        /// <summary>
        /// The user account is disabled and cannot be used for login.
        /// </summary>
        Disabled,

        /// <summary>
        /// The user account is pending activation.
        /// </summary>
        PendingActivation
    }

    /// <summary>
    /// Represents user preferences in the HackerOS system.
    /// </summary>
    public class UserPreferences
    {
        /// <summary>
        /// Gets or sets the theme preference for this user.
        /// </summary>
        public string Theme { get; set; } = "gothic-hacker";

        /// <summary>
        /// Gets or sets the desktop background preference for this user.
        /// </summary>
        public string Wallpaper { get; set; } = "default";

        /// <summary>
        /// Gets or sets whether desktop icons should be shown.
        /// </summary>
        public bool ShowDesktopIcons { get; set; } = true;

        /// <summary>
        /// Gets or sets the terminal transparency level.
        /// </summary>
        public double TerminalTransparency { get; set; } = 0.8;

        /// <summary>
        /// Gets or sets the shell prompt format.
        /// </summary>
        public string Prompt { get; set; } = "\\u@\\h:\\w\\$ ";

        /// <summary>
        /// Gets or sets the list of commands to run at shell startup.
        /// </summary>
        public List<string> StartupCommands { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the environment variables for this user.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the shell aliases for this user.
        /// </summary>
        public Dictionary<string, string> Aliases { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the list of pinned applications.
        /// </summary>
        public List<string> PinnedApplications { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the application-specific settings.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> ApplicationSettings { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// Gets or sets whether command history should be enabled.
        /// </summary>
        public bool EnableCommandHistory { get; set; } = true;

        /// <summary>
        /// Gets or sets whether activity logging should be enabled.
        /// </summary>
        public bool EnableActivityLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the timeout for automatic session locking.
        /// </summary>
        public TimeSpan AutoLockTimeout { get; set; } = TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Represents security settings for a user.
    /// </summary>
    public class SecuritySettings
    {
        /// <summary>
        /// Gets or sets the maximum number of failed login attempts before the account is locked.
        /// </summary>
        public int MaxFailedLogins { get; set; } = 5;

        /// <summary>
        /// Gets or sets the duration of the account lockout after too many failed login attempts.
        /// </summary>
        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether the user must change their password on next login.
        /// </summary>
        public bool RequirePasswordChange { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of days after which the password expires.
        /// A value of 0 or less indicates no expiration.
        /// </summary>
        public int PasswordExpiryDays { get; set; } = 0;

        /// <summary>
        /// Gets or sets the list of trusted device identifiers.
        /// </summary>
        public List<string> TrustedDevices { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether two-factor authentication is enabled.
        /// </summary>
        public bool EnableTwoFactor { get; set; } = false;

        /// <summary>
        /// Gets or sets the current number of failed login attempts.
        /// </summary>
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>
        /// Gets or sets the timestamp of the last failed login attempt.
        /// </summary>
        public DateTime? LastFailedLogin { get; set; }
    }
}
