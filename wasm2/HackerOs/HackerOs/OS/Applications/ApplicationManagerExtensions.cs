using HackerOs.OS.Applications;
using HackerOs.OS.User;
using System.Threading.Tasks;

namespace HackerOs.OS.Applications
{
    /// <summary>
    /// Extension methods for the IApplicationManager interface
    /// </summary>
    public static class ApplicationManagerExtensions
    {
        /// <summary>
        /// Launch an application by its ID with a default context
        /// </summary>
        /// <param name="applicationManager">The application manager</param>
        /// <param name="applicationId">Application ID to launch</param>
        /// <returns>The launched application, or null if launch failed</returns>
        public static async Task<IApplication?> LaunchAsync(this IApplicationManager applicationManager, string applicationId)
        {
            // Create a default launch context
            var context = new ApplicationLaunchContext
            {
                Parameters = new Dictionary<string, object>()
            };

            return await applicationManager.LaunchApplicationAsync(applicationId, context);
        }

        /// <summary>
        /// Launch an application by its ID with the specified user session
        /// </summary>
        /// <param name="applicationManager">The application manager</param>
        /// <param name="applicationId">Application ID to launch</param>
        /// <param name="userSession">User session for the launch context</param>
        /// <returns>The launched application, or null if launch failed</returns>
        public static async Task<IApplication?> LaunchAsync(this IApplicationManager applicationManager, string applicationId, UserSession userSession)
        {
            // Create a launch context with the specified user session
            var context = new ApplicationLaunchContext
            {
                UserSession = userSession,
                Parameters = new Dictionary<string, object>()
            };

            return await applicationManager.LaunchApplicationAsync(applicationId, context);
        }

        /// <summary>
        /// Launch an application by its ID with the specified user session and parameters
        /// </summary>
        /// <param name="applicationManager">The application manager</param>
        /// <param name="applicationId">Application ID to launch</param>
        /// <param name="userSession">User session for the launch context</param>
        /// <param name="parameters">Launch parameters</param>
        /// <returns>The launched application, or null if launch failed</returns>
        public static async Task<IApplication?> LaunchAsync(this IApplicationManager applicationManager, string applicationId, UserSession userSession, Dictionary<string, object> parameters)
        {
            // Create a launch context with the specified user session and parameters
            var context = new ApplicationLaunchContext
            {
                UserSession = userSession,
                Parameters = parameters
            };

            return await applicationManager.LaunchApplicationAsync(applicationId, context);
        }
    }
}
