using System;
using System.Collections.Generic;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Represents a user in the HackerOS system.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Gets or sets the unique identifier for this user.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the username for this user.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hashed password for this user.
        /// </summary>
        public string HashedPassword { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the home directory path for this user.
        /// </summary>
        public string HomeDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default shell for this user.
        /// </summary>
        public string DefaultShell { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of groups this user belongs to.
        /// </summary>
        public List<string> Groups { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the date when this user was created.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the date of the last login for this user.
        /// </summary>
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this user is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the preferences for this user.
        /// </summary>
        public UserPreferences Preferences { get; set; } = new UserPreferences();
    }

    /// <summary>
    /// Represents user preferences in the HackerOS system.
    /// </summary>
    public class UserPreferences
    {
        /// <summary>
        /// Gets or sets the theme preference for this user.
        /// </summary>
        public string Theme { get; set; } = "default";

        /// <summary>
        /// Gets or sets the desktop background preference for this user.
        /// </summary>
        public string DesktopBackground { get; set; } = "default";

        /// <summary>
        /// Gets or sets the window manager preferences for this user.
        /// </summary>
        public Dictionary<string, string> WindowManagerSettings { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the application preferences for this user.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> ApplicationSettings { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    }
}
