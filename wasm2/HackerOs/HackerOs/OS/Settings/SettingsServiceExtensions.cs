using HackerOs.OS.Settings;
using HackerOsUser = HackerOs.OS.User.User;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Extension methods for <see cref="ISettingsService"/>
    /// to simplify retrieval of <see cref="UserSettings"/> instances.
    /// </summary>
    public static class SettingsServiceExtensions
    {
        /// <summary>
        /// Gets a <see cref="UserSettings"/> wrapper for the specified user.
        /// </summary>
        /// <param name="service">The underlying settings service.</param>
        /// <param name="user">The user for which to retrieve settings.</param>
        public static UserSettings GetUserSettings(this ISettingsService service, HackerOsUser user)
        {
            var systemSettings = new SystemSettings(service, Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemSettings>.Instance);
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<UserSettings>.Instance;
            return new UserSettings(service, systemSettings, logger, user.Username);
        }
    }
}
