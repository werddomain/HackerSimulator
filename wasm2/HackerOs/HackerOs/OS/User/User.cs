using System.Security.Cryptography;
using System.Text;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Represents a user in the HackerOS system, similar to Unix user accounts
    /// </summary>
    public class User
    {
        /// <summary>
        /// User ID (UID) - unique identifier for the user
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Username - the login name for the user
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Full name or display name of the user
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Hashed password for authentication
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Salt used for password hashing
        /// </summary>
        public string PasswordSalt { get; set; } = string.Empty;

        /// <summary>
        /// Primary group ID for the user
        /// </summary>
        public int PrimaryGroupId { get; set; }

        /// <summary>
        /// Home directory path for the user
        /// </summary>
        public string HomeDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Default shell for the user
        /// </summary>
        public string Shell { get; set; } = "/bin/bash";

        /// <summary>
        /// Additional groups the user belongs to
        /// </summary>
        public List<int> AdditionalGroups { get; set; } = new();

        /// <summary>
        /// User creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last login timestamp
        /// </summary>
        public DateTime? LastLogin { get; set; }

        /// <summary>
        /// Whether the user account is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Whether the user is a system user (UID < 1000)
        /// </summary>
        public bool IsSystemUser => UserId < 1000;

        /// <summary>
        /// Whether the user has administrative privileges
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// User preferences and settings
        /// </summary>
        public Dictionary<string, string> Preferences { get; set; } = new();

        /// <summary>
        /// Environment variables for the user
        /// </summary>
        public Dictionary<string, string> Environment { get; set; } = new();

        public User()
        {
            InitializeDefaultEnvironment();
        }

        public User(string username, string fullName, int userId = 0) : this()
        {
            Username = username;
            FullName = fullName;
            UserId = userId;
            HomeDirectory = $"/home/{username}";
        }

        /// <summary>
        /// Sets the user's password with proper hashing
        /// </summary>
        /// <param name="password">Plain text password</param>
        public void SetPassword(string password)
        {
            PasswordSalt = GenerateSalt();
            PasswordHash = HashPassword(password, PasswordSalt);
        }

        /// <summary>
        /// Verifies if the provided password matches the user's password
        /// </summary>
        /// <param name="password">Plain text password to verify</param>
        /// <returns>True if password matches, false otherwise</returns>
        public bool VerifyPassword(string password)
        {
            if (string.IsNullOrEmpty(PasswordHash) || string.IsNullOrEmpty(PasswordSalt))
                return false;

            string hashedInput = HashPassword(password, PasswordSalt);
            return hashedInput == PasswordHash;
        }

        /// <summary>
        /// Checks if the user belongs to a specific group
        /// </summary>
        /// <param name="groupId">Group ID to check</param>
        /// <returns>True if user belongs to the group</returns>
        public bool BelongsToGroup(int groupId)
        {
            return PrimaryGroupId == groupId || AdditionalGroups.Contains(groupId);
        }        /// <summary>
        /// Adds the user to an additional group
        /// </summary>
        /// <param name="groupId">Group ID to add</param>
        /// <returns>True if the user was added to the group, false if already a member</returns>
        public bool AddToGroup(int groupId)
        {
            if (!AdditionalGroups.Contains(groupId))
            {
                AdditionalGroups.Add(groupId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the user from an additional group
        /// </summary>
        /// <param name="groupId">Group ID to remove</param>
        public void RemoveFromGroup(int groupId)
        {
            AdditionalGroups.Remove(groupId);
        }

        /// <summary>
        /// Updates the last login timestamp
        /// </summary>
        public void UpdateLastLogin()
        {
            LastLogin = DateTime.UtcNow;
            CurrentLogedUser = this;
        }
        public static User? CurrentLogedUser { get; private set; }
        /// <summary>
        /// Initializes default environment variables for the user
        /// </summary>
        private void InitializeDefaultEnvironment()
        {
            Environment["HOME"] = HomeDirectory;
            Environment["USER"] = Username;
            Environment["SHELL"] = Shell;
            Environment["PATH"] = "/usr/local/bin:/usr/bin:/bin";
            Environment["LANG"] = "en_US.UTF-8";
            Environment["TERM"] = "xterm-256color";
        }

        /// <summary>
        /// Updates environment variables when user properties change
        /// </summary>
        public void UpdateEnvironment()
        {
            Environment["HOME"] = HomeDirectory;
            Environment["USER"] = Username;
            Environment["SHELL"] = Shell;
        }

        /// <summary>
        /// Generates a random salt for password hashing
        /// </summary>
        /// <returns>Base64 encoded salt</returns>
        private static string GenerateSalt()
        {
            byte[] saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// Hashes a password with the provided salt using PBKDF2
        /// </summary>
        /// <param name="password">Plain text password</param>
        /// <param name="salt">Base64 encoded salt</param>
        /// <returns>Base64 encoded hash</returns>
        private static string HashPassword(string password, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256))
            {
                byte[] hashBytes = pbkdf2.GetBytes(32);
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Creates a serializable representation of the user for storage
        /// </summary>
        /// <returns>Dictionary representation of the user</returns>
        public Dictionary<string, object> ToSerializable()
        {
            return new Dictionary<string, object>
            {
                ["userId"] = UserId,
                ["username"] = Username,
                ["fullName"] = FullName,
                ["passwordHash"] = PasswordHash,
                ["passwordSalt"] = PasswordSalt,
                ["primaryGroupId"] = PrimaryGroupId,
                ["homeDirectory"] = HomeDirectory,
                ["shell"] = Shell,
                ["additionalGroups"] = AdditionalGroups,
                ["createdAt"] = CreatedAt.ToString("O"),
                ["lastLogin"] = LastLogin?.ToString("O") ?? "",
                ["isActive"] = IsActive,
                ["isAdmin"] = IsAdmin,
                ["preferences"] = Preferences,
                ["environment"] = Environment
            };
        }

        /// <summary>
        /// Creates a User instance from a serializable representation
        /// </summary>
        /// <param name="data">Dictionary representation of the user</param>
        /// <returns>User instance</returns>
        public static User FromSerializable(Dictionary<string, object> data)
        {
            var user = new User
            {
                UserId = Convert.ToInt32(data["userId"]),
                Username = data["username"].ToString() ?? "",
                FullName = data["fullName"].ToString() ?? "",
                PasswordHash = data["passwordHash"].ToString() ?? "",
                PasswordSalt = data["passwordSalt"].ToString() ?? "",
                PrimaryGroupId = Convert.ToInt32(data["primaryGroupId"]),
                HomeDirectory = data["homeDirectory"].ToString() ?? "",
                Shell = data["shell"].ToString() ?? "/bin/bash",
                IsActive = Convert.ToBoolean(data["isActive"]),
                IsAdmin = Convert.ToBoolean(data["isAdmin"])
            };

            if (data.ContainsKey("additionalGroups") && data["additionalGroups"] is IEnumerable<object> groups)
            {
                user.AdditionalGroups = groups.Select(g => Convert.ToInt32(g)).ToList();
            }

            if (data.ContainsKey("createdAt") && DateTime.TryParse(data["createdAt"].ToString(), out var createdAt))
            {
                user.CreatedAt = createdAt;
            }

            if (data.ContainsKey("lastLogin") && !string.IsNullOrEmpty(data["lastLogin"].ToString()) && 
                DateTime.TryParse(data["lastLogin"].ToString(), out var lastLogin))
            {
                user.LastLogin = lastLogin;
            }

            if (data.ContainsKey("preferences") && data["preferences"] is Dictionary<string, object> prefs)
            {
                user.Preferences = prefs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? "");
            }

            if (data.ContainsKey("environment") && data["environment"] is Dictionary<string, object> env)
            {
                user.Environment = env.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? "");
            }

            user.UpdateEnvironment();
            return user;
        }

        public override string ToString()
        {
            return $"{Username} (UID: {UserId}, Groups: {PrimaryGroupId}{(AdditionalGroups.Any() ? "," + string.Join(",", AdditionalGroups) : "")})";
        }
    }
}
