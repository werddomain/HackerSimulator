using HackerOs.OS.Applications;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.Kernel.Process;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace HackerOs.OS.Applications.Extensions
{
    /// <summary>
    /// Extension methods for application type-specific launching
    /// </summary>
    public static class ApplicationManagerExtensions
    {
        /// <summary>
        /// Launch a windowed application
        /// </summary>
        /// <param name="applicationManager">Application manager</param>
        /// <param name="manifest">Application manifest</param>
        /// <param name="context">Launch context</param>
        /// <param name="logger">Logger</param>
        /// <param name="processManager">Process manager</param>
        /// <returns>Launched application if successful, null otherwise</returns>
        public static async Task<IApplication?> LaunchWindowedApplicationAsync(
            this ApplicationManager applicationManager,
            ApplicationManifest manifest,
            ApplicationLaunchContext context,
            ILogger logger,
            IProcessManager processManager)
        {
            logger.LogInformation("Launching windowed application {ApplicationId}", manifest.Id);
            
            // Set process start info specific to windowed applications
            var processStartInfo = new ProcessStartInfo
            {
                Name = manifest.Name,
                ExecutablePath = manifest.EntryPoint,
                Arguments = context.Arguments?.ToArray() ?? Array.Empty<string>(),
                WorkingDirectory = context.WorkingDirectory ?? "/",
                Owner = context.UserSession.User.Username,
                ParentProcessId = context.ParentProcessId ?? 0,
                Environment = new Dictionary<string, string>
                {
                    { "APPLICATION_ID", manifest.Id },
                    { "APPLICATION_VERSION", manifest.Version },
                    { "USER", context.UserSession.User.Username },
                    { "APPLICATION_TYPE", "windowed" }
                },
                CreateWindow = true, // Always create a window for windowed applications
                Priority = ProcessPriority.Normal, // Default priority for UI applications
                IsBackground = false // UI apps typically run in foreground
            };
            
            // Create the application instance
            var application = await applicationManager.CreateApplicationInstanceAsync(manifest, context);
            if (application == null)
            {
                logger.LogError("Failed to create windowed application instance for {ApplicationId}", manifest.Id);
                return null;
            }
            
            // Create the process
            var process = await processManager.CreateProcessAsync(processStartInfo);
            if (process == null)
            {
                logger.LogError("Failed to create process for windowed application {ApplicationId}", manifest.Id);
                return null;
            }
            
            // Assign the process ID to the application
            application.GetType().GetProperty("ProcessId")?.SetValue(application, process.ProcessId);
            
            // Subscribe to application events
            applicationManager.SubscribeToApplicationEvents(application);
            
            // Start the application with its context
            var started = await application.StartAsync(context);
            if (!started)
            {
                logger.LogError("Failed to start windowed application {ApplicationId}", manifest.Id);
                await processManager.TerminateProcessAsync(process.ProcessId);
                return null;
            }
            
            // Track the running application
            applicationManager.AddRunningApplication(application.ProcessId, application);
            
            logger.LogInformation("Successfully launched windowed application {ApplicationId} with PID {ProcessId}", 
                manifest.Id, application.ProcessId);
                
            return application;
        }
        
        /// <summary>
        /// Launch a service application
        /// </summary>
        /// <param name="applicationManager">Application manager</param>
        /// <param name="manifest">Application manifest</param>
        /// <param name="context">Launch context</param>
        /// <param name="logger">Logger</param>
        /// <param name="processManager">Process manager</param>
        /// <returns>Launched application if successful, null otherwise</returns>
        public static async Task<IApplication?> LaunchServiceApplicationAsync(
            this ApplicationManager applicationManager,
            ApplicationManifest manifest,
            ApplicationLaunchContext context,
            ILogger logger,
            IProcessManager processManager)
        {
            logger.LogInformation("Launching service application {ApplicationId}", manifest.Id);
            
            // Set process start info specific to service applications
            var processStartInfo = new ProcessStartInfo
            {
                Name = manifest.Name,
                ExecutablePath = manifest.EntryPoint,
                Arguments = context.Arguments?.ToArray() ?? Array.Empty<string>(),
                WorkingDirectory = context.WorkingDirectory ?? "/",
                Owner = context.UserSession.User.Username,
                ParentProcessId = context.ParentProcessId ?? 0,
                Environment = new Dictionary<string, string>
                {
                    { "APPLICATION_ID", manifest.Id },
                    { "APPLICATION_VERSION", manifest.Version },
                    { "USER", context.UserSession.User.Username },
                    { "APPLICATION_TYPE", "service" }
                },
                CreateWindow = false, // Services don't create windows
                Priority = ProcessPriority.BelowNormal, // Services typically run at lower priority
                IsBackground = true // Services run in the background
            };
            
            // Create the application instance
            var application = await applicationManager.CreateApplicationInstanceAsync(manifest, context);
            if (application == null)
            {
                logger.LogError("Failed to create service application instance for {ApplicationId}", manifest.Id);
                return null;
            }
            
            // Create the process
            var process = await processManager.CreateProcessAsync(processStartInfo);
            if (process == null)
            {
                logger.LogError("Failed to create process for service application {ApplicationId}", manifest.Id);
                return null;
            }
            
            // Assign the process ID to the application
            application.GetType().GetProperty("ProcessId")?.SetValue(application, process.ProcessId);
            
            // Subscribe to application events
            applicationManager.SubscribeToApplicationEvents(application);
            
            // Start the application with its context
            var started = await application.StartAsync(context);
            if (!started)
            {
                logger.LogError("Failed to start service application {ApplicationId}", manifest.Id);
                await processManager.TerminateProcessAsync(process.ProcessId);
                return null;
            }
            
            // Track the running application
            applicationManager.AddRunningApplication(application.ProcessId, application);
            
            logger.LogInformation("Successfully launched service application {ApplicationId} with PID {ProcessId}", 
                manifest.Id, application.ProcessId);
                
            return application;
        }
        
        /// <summary>
        /// Launch a command-line application
        /// </summary>
        /// <param name="applicationManager">Application manager</param>
        /// <param name="manifest">Application manifest</param>
        /// <param name="context">Launch context</param>
        /// <param name="logger">Logger</param>
        /// <param name="processManager">Process manager</param>
        /// <returns>Launched application if successful, null otherwise</returns>
        public static async Task<IApplication?> LaunchCommandLineApplicationAsync(
            this ApplicationManager applicationManager,
            ApplicationManifest manifest,
            ApplicationLaunchContext context,
            ILogger logger,
            IProcessManager processManager)
        {
            logger.LogInformation("Launching command-line application {ApplicationId}", manifest.Id);
            
            // Set process start info specific to command-line applications
            var processStartInfo = new ProcessStartInfo
            {
                Name = manifest.Name,
                ExecutablePath = manifest.EntryPoint,
                Arguments = context.Arguments?.ToArray() ?? Array.Empty<string>(),
                WorkingDirectory = context.WorkingDirectory ?? "/",
                Owner = context.UserSession.User.Username,
                ParentProcessId = context.ParentProcessId ?? 0,
                Environment = new Dictionary<string, string>
                {
                    { "APPLICATION_ID", manifest.Id },
                    { "APPLICATION_VERSION", manifest.Version },
                    { "USER", context.UserSession.User.Username },
                    { "APPLICATION_TYPE", "command" }
                },
                CreateWindow = false, // Command-line apps don't create windows
                Priority = ProcessPriority.Normal, // Default priority
                IsBackground = false // Command line tools typically run in foreground
            };
            
            // Create the application instance
            var application = await applicationManager.CreateApplicationInstanceAsync(manifest, context);
            if (application == null)
            {
                logger.LogError("Failed to create command-line application instance for {ApplicationId}", manifest.Id);
                return null;
            }
            
            // Create the process
            var process = await processManager.CreateProcessAsync(processStartInfo);
            if (process == null)
            {
                logger.LogError("Failed to create process for command-line application {ApplicationId}", manifest.Id);
                return null;
            }
            
            // Assign the process ID to the application
            application.GetType().GetProperty("ProcessId")?.SetValue(application, process.ProcessId);
            
            // Subscribe to application events
            applicationManager.SubscribeToApplicationEvents(application);
            
            // Start the application with its context
            var started = await application.StartAsync(context);
            if (!started)
            {
                logger.LogError("Failed to start command-line application {ApplicationId}", manifest.Id);
                await processManager.TerminateProcessAsync(process.ProcessId);
                return null;
            }
            
            // Track the running application
            applicationManager.AddRunningApplication(application.ProcessId, application);
            
            logger.LogInformation("Successfully launched command-line application {ApplicationId} with PID {ProcessId}", 
                manifest.Id, application.ProcessId);
                
            return application;
        }
    }
}
