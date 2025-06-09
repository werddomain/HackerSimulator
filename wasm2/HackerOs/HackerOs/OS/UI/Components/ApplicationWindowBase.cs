using BlazorWindowManager.Components;
using HackerOs.OS.Applications;
using HackerOs.OS.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Base component for application windows
    /// </summary>
    public class ApplicationWindowBase : ComponentBase, IDisposable
    {
        [Inject] protected IApplicationManager ApplicationManager { get; set; } = null!;
        [Inject] protected ApplicationWindowManager WindowManager { get; set; } = null!;
        [Inject] protected ILogger<ApplicationWindowBase> Logger { get; set; } = null!;        /// <summary>
        /// The window this component is rendered in
        /// </summary>
        [CascadingParameter] protected BlazorWindowManager.Components.WindowBase? Window { get; set; }

        /// <summary>
        /// The ID of the application to display in this window
        /// </summary>
        [Parameter] public string? ApplicationId { get; set; }

        /// <summary>
        /// The application being displayed
        /// </summary>
        protected IApplication? Application { get; private set; }

        /// <summary>
        /// The application window bridge
        /// </summary>
        /// <summary>
        /// Bridge object between the application and its window
        /// </summary>
        protected HackerOs.OS.UI.ApplicationWindow? AppWindowBridge { get; private set; }

        /// <summary>
        /// Error message if application can't be loaded
        /// </summary>
        protected string? ErrorMessage { get; private set; }

        /// <summary>
        /// Whether the application is loading
        /// </summary>
        protected bool IsLoading { get; private set; } = true;

        /// <summary>
        /// Initialize the component
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            IsLoading = true;
            
            try
            {
                // Find the application
                if (string.IsNullOrEmpty(ApplicationId))
                {
                    ErrorMessage = "No application ID specified";
                    return;
                }

                // Look up the application
                Application = ApplicationManager.GetRunningApplication(ApplicationId) ??
                              ApplicationManager.GetRunningApplications().FirstOrDefault(a => a.Id == ApplicationId);
                if (Application == null)
                {
                    ErrorMessage = $"Application not found: {ApplicationId}";
                    return;
                }

                // Get the application window
                AppWindowBridge = WindowManager.GetApplicationWindow(ApplicationId);
                if (AppWindowBridge == null)
                {
                    ErrorMessage = $"Application window not found: {ApplicationId}";
                    return;
                }

                // Subscribe to application events
                Application.StateChanged += OnApplicationStateChanged;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading application: {ex.Message}";
                Logger.LogError(ex, "Error loading application {ApplicationId}", ApplicationId);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public virtual void Dispose()
        {
            if (Application != null)
            {
                Application.StateChanged -= OnApplicationStateChanged;
            }
        }

        /// <summary>
        /// Handle application state changes
        /// </summary>
        protected virtual void OnApplicationStateChanged(object? sender, ApplicationStateChangedEventArgs e)
        {
            // Request UI update when application state changes
            InvokeAsync(StateHasChanged);
        }
    }
}
