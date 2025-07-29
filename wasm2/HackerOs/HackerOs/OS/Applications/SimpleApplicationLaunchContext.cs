using HackerOs.OS.User;
using System.Collections.Generic;

namespace HackerOs.OS.Applications
{
    /// <summary>
    /// Simple application launch context
    /// </summary>
    public class SimpleApplicationLaunchContext
    {
        /// <summary>
        /// Create a basic application launch context
        /// </summary>
        /// <returns>A simplified launch context for testing</returns>
        public static ApplicationLaunchContext CreateSimpleContext()
        {
            return new ApplicationLaunchContext 
            { 
                UserSession = null, 
                Parameters = new Dictionary<string, object>() 
            };
        }
    }
}
