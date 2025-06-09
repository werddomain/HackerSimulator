namespace ProxyServer.Authentication
{
    /// <summary>
    /// Configuration settings for the authentication system.
    /// </summary>
    public class AuthenticationConfiguration
    {
        /// <summary>
        /// Configuration section name for authentication settings.
        /// </summary>
        public const string SectionName = "Authentication";

        /// <summary>
        /// Gets or sets the valid API keys for authentication.
        /// </summary>
        public List<ApiKeyConfiguration> ApiKeys { get; set; } = new();

        /// <summary>
        /// Gets or sets the session timeout in minutes.
        /// </summary>
        public int SessionTimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Gets or sets the session cleanup interval in minutes.
        /// </summary>
        public int SessionCleanupIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum number of sessions per client.
        /// </summary>
        public int MaxSessionsPerClient { get; set; } = 5;

        /// <summary>
        /// Gets or sets a value indicating whether to require strong API keys.
        /// </summary>
        public bool RequireStrongApiKeys { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum API key length.
        /// </summary>
        public int MinApiKeyLength { get; set; } = 32;
    }

    /// <summary>
    /// Configuration for individual API keys.
    /// </summary>
    public class ApiKeyConfiguration
    {
        /// <summary>
        /// Gets or sets the API key value.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user identifier associated with this key.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user role.
        /// </summary>
        public string Role { get; set; } = "user";

        /// <summary>
        /// Gets or sets the display name for this key/user.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this key is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets custom permissions for this key (overrides role-based permissions).
        /// </summary>
        public List<string>? CustomPermissions { get; set; }
    }
}
