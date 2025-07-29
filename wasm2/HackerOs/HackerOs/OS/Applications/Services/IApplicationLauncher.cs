using System;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Lifecycle;

namespace HackerOs.OS.Applications.Services
{
    /// <summary>
    /// Interface for a service that can launch applications
    /// </summary>
    public interface IApplicationLauncher
    {
        /// <summary>
        /// Launches an application by its ID
        /// </summary>
        /// <param name="applicationId">The application ID to launch</param>
        /// <param name="args">Optional arguments to pass to the application</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task<IApplication> LaunchApplicationAsync(string applicationId, string[] args = null);

        /// <summary>
        /// Launches an application using a launch context
        /// </summary>
        /// <param name="context">The launch context containing application ID and arguments</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task<IApplication> LaunchApplicationAsync(ApplicationLaunchContext context);
    }
}
