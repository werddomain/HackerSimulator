using HackerOs.OS.Applications;
using HackerOs.OS.UI.Models;
using HackerOs.OS.UI.Services;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Application launcher component for accessing and launching applications
    /// </summary>
    public partial class ApplicationLauncher : ComponentBase, IDisposable
    {
        [Inject] protected IApplicationManager ApplicationManager { get; set; } = null!;
        [Inject] protected IApplicationInstaller ApplicationInstaller { get; set; } = null!;
        [Inject] protected LauncherService LauncherService { get; set; } = null!;
        [Inject] protected ILogger<ApplicationLauncher> Logger { get; set; } = null!;
        [Inject] protected IUserManager UserManager { get; set; } = null!;

        /// <summary>
        /// Event called when the launcher open state changes
        /// </summary>
        [Parameter] public EventCallback<bool> OnLauncherOpenChanged { get; set; }

        /// <summary>
        /// Whether the launcher is currently open
        /// </summary>
        private bool _isOpen;

        /// <summary>
        /// Current search query
        /// </summary>
        private string _searchQuery = string.Empty;

        /// <summary>
        /// Currently selected section (category, recent, all)
        /// </summary>
        private string _selectedSection = "recent";

        /// <summary>
        /// List of application categories
        /// </summary>
        private List<AppCategoryModel> _categories = new();

        /// <summary>
        /// List of recent applications
        /// </summary>
        private List<LauncherAppModel> _recentApps = new();

        /// <summary>
        /// List of pinned applications
        /// </summary>
        private List<LauncherAppModel> _pinnedApps = new();

        /// <summary>
        /// IDs of currently running applications
        /// </summary>
        private HashSet<string> _runningApplications = new();

        /// <summary>
        /// List of search results
        /// </summary>
        private List<LauncherAppModel> _searchResults = new();

        /// <summary>
        /// Initialize the component
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            // Subscribe to application events
            ApplicationInstaller.ApplicationInstalled += OnApplicationInstalled;
            ApplicationInstaller.ApplicationUninstalled += OnApplicationUninstalled;
            ApplicationManager.ApplicationLaunched += OnApplicationStarted;
            ApplicationManager.ApplicationTerminated += OnApplicationClosed;

            // Load initial data
            await LoadDataAsync();

            await base.OnInitializedAsync();
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from events
            ApplicationInstaller.ApplicationInstalled -= OnApplicationInstalled;
            ApplicationInstaller.ApplicationUninstalled -= OnApplicationUninstalled;
            ApplicationManager.ApplicationLaunched -= OnApplicationStarted;
            ApplicationManager.ApplicationTerminated -= OnApplicationClosed;
        }

        /// <summary>
        /// Toggle the launcher visibility
        /// </summary>
        public void ToggleLauncher()
        {
            _isOpen = !_isOpen;
            
            // When opening, refresh data
            if (_isOpen)
            {
                _ = LoadDataAsync();
            }
            else
            {
                // Clear search when closing
                _searchQuery = string.Empty;
            }
            
            // Notify parent
            OnLauncherOpenChanged.InvokeAsync(_isOpen);
            
            StateHasChanged();
        }

        /// <summary>        /// Close the launcher
        /// </summary>
        protected async Task CloseLauncher()
        {
            _isOpen = false;
            _searchQuery = string.Empty;
            await OnLauncherOpenChanged.InvokeAsync(false);
            StateHasChanged();
        }

        /// <summary>
        /// Load all data for the launcher
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {                // Load categories and applications
                var serviceCategories = await LauncherService.GetApplicationCategoriesAsync();
                _categories = serviceCategories.Select(c => c.ToComponentModel()).ToList();
                  // Load recent applications
                var serviceRecentApps = await LauncherService.GetRecentApplicationsAsync();
                _recentApps = serviceRecentApps.Select(a => a.ToComponentModel()).ToList();
                  // Load pinned applications
                var servicePinnedApps = await LauncherService.GetPinnedApplicationsAsync();
                _pinnedApps = servicePinnedApps.Select(a => a.ToComponentModel()).ToList();

                RefreshRunningApplications();
                
                // Ensure we have at least one category expanded
                if (!_categories.Any(c => c.IsExpanded) && _categories.Any())
                {
                    _categories.First().IsExpanded = true;
                }
                
                // Update UI
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading launcher data");
            }
        }

        /// <summary>
        /// Select a section in the launcher (category, recent, all)
        /// </summary>
        protected void SelectSection(string sectionId)
        {
            _selectedSection = sectionId;
            StateHasChanged();
        }

        /// <summary>
        /// Toggle a category's expanded state
        /// </summary>
        protected void ToggleCategoryExpanded(string categoryId)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category != null)
            {
                category.IsExpanded = !category.IsExpanded;
                StateHasChanged();
            }
        }        /// <summary>
        /// Launch an application by ID
        /// </summary>
        protected async Task LaunchApplication(string appId)
        {
            try
            {
                // Close the launcher
                _isOpen = false;
                await OnLauncherOpenChanged.InvokeAsync(false);
                
                // Launch the application with a basic context
                var session = new UserSession(HackerOs.OS.User.UserManager.SystemUser, "system");
                var context = ApplicationLaunchContext.Create(session);
                await ApplicationManager.LaunchApplicationAsync(appId, context);
                
                // Track as recently used
                await LauncherService.RecordApplicationLaunchAsync(appId);
                
                // Refresh recent apps list
                var serviceRecent = await LauncherService.GetRecentApplicationsAsync();
                _recentApps = serviceRecent.Select(a => a.ToComponentModel()).ToList();
                
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error launching application {AppId}", appId);
            }
        }        /// <summary>
        /// Toggle whether an application is pinned
        /// </summary>
        protected async Task ToggleApplicationPinned(string appId)
        {
            try
            {
                var app = _pinnedApps.FirstOrDefault(a => a.Id == appId);
                if (app != null)
                {
                    // Unpin the application
                    await LauncherService.ToggleApplicationPinAsync(appId);
                    app.IsPinned = false;
                    _pinnedApps.Remove(app);
                }
                else
                {
                    // Pin the application
                    await LauncherService.ToggleApplicationPinAsync(appId);
                    
                    // Find the app in all categories
                    foreach (var category in _categories)
                    {
                        var appToPin = category.Applications.FirstOrDefault(a => a.Id == appId);
                        if (appToPin != null)
                        {
                            appToPin.IsPinned = true;
                            _pinnedApps.Add(appToPin);
                            break;
                        }
                    }
                }
                
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling pin state for application {AppId}", appId);
            }
        }        /// <summary>
        /// Handle search input
        /// </summary>
        protected async Task OnSearchChanged()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _searchResults.Clear();
                return;
            }
            
            try
            {
                var serviceResults = await LauncherService.SearchApplicationsAsync(_searchQuery);
                _searchResults = serviceResults.Select(a => a.ToComponentModel()).ToList();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error searching applications");
            }
        }

        /// <summary>
        /// Handle key down event in the search box
        /// </summary>
        private void OnSearchKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_searchQuery) && _searchResults.Count > 0)
            {
                // Launch the first search result when Enter is pressed
                _ = LaunchApplication(_searchResults[0].Id);
            }
            else if (e.Key == "Escape")
            {
                // Close the launcher when Escape is pressed
                CloseLauncher();
            }
        }

        /// <summary>
        /// Format a timestamp as a human-readable time ago string
        /// </summary>
        protected string GetTimeAgo(DateTime timestamp)
        {
            var span = DateTime.Now - timestamp;
            
            if (span.TotalDays > 365)
            {
                int years = (int)(span.TotalDays / 365);
                return $"{years} {(years == 1 ? "year" : "years")} ago";
            }
            
            if (span.TotalDays > 30)
            {
                int months = (int)(span.TotalDays / 30);
                return $"{months} {(months == 1 ? "month" : "months")} ago";
            }
            
            if (span.TotalDays > 1)
            {
                int days = (int)span.TotalDays;
                return $"{days} {(days == 1 ? "day" : "days")} ago";
            }
            
            if (span.TotalHours > 1)
            {
                int hours = (int)span.TotalHours;
                return $"{hours} {(hours == 1 ? "hour" : "hours")} ago";
            }
            
            if (span.TotalMinutes > 1)
            {
                int minutes = (int)span.TotalMinutes;
                return $"{minutes} {(minutes == 1 ? "minute" : "minutes")} ago";
            }
            
            return "Just now";
        }

        /// <summary>
        /// Handle application installed event
        /// </summary>
        private async void OnApplicationInstalled(object? sender, ApplicationEventArgs e)
        {
            await LoadDataAsync();
        }

        /// <summary>
        /// Handle application uninstalled event
        /// </summary>
        private async void OnApplicationUninstalled(object? sender, ApplicationEventArgs e)
        {
            await LoadDataAsync();
        }

        /// <summary>
        /// Handle application started event
        /// </summary>
        private async void OnApplicationStarted(object? sender, ApplicationEventArgs e)
        {
            // Refresh recent apps when an application is started
            var recent = await LauncherService.GetRecentApplicationsAsync();
            _recentApps = recent.Select(a => a.ToComponentModel()).ToList();
            RefreshRunningApplications();
            await InvokeAsync(StateHasChanged);
        }

        private async void OnApplicationClosed(object? sender, ApplicationEventArgs e)
        {
            RefreshRunningApplications();
            await InvokeAsync(StateHasChanged);
        }

        private void RefreshRunningApplications()
        {
            _runningApplications = ApplicationManager.GetRunningApplications().Select(a => a.Id).ToHashSet();
        }
    }
}
