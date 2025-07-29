using HackerOs.OS.UI.Models;
using HackerOs.OS.UI.Services;
using HackerOs.OS.Applications;
using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::System.Timers;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Component for the system taskbar
    /// </summary>
    public partial class Taskbar : ComponentBase, IDisposable
    {
        [Inject] protected IApplicationManager ApplicationManager { get; set; } = null!;
        [Inject] protected ApplicationWindowManager WindowManager { get; set; } = null!;
        [Inject] protected NotificationService NotificationService { get; set; } = null!;
        [Inject] protected ILogger<Taskbar> Logger { get; set; } = null!;

        /// <summary>
        /// Event called when the launcher button is clicked
        /// </summary>
        [Parameter] public EventCallback OnLauncherToggled { get; set; }

        /// <summary>
        /// Event called when the notification center button is clicked
        /// </summary>
        [Parameter] public EventCallback OnNotificationCenterToggled { get; set; }

        /// <summary>
        /// Current date and time
        /// </summary>
        protected DateTime CurrentDateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Formatted current time
        /// </summary>
        protected string CurrentTime => CurrentDateTime.ToString("h:mm tt");

        /// <summary>
        /// List of running applications
        /// </summary>
        protected List<TaskbarAppModel> RunningApplications { get; set; } = new();

        /// <summary>
        /// Number of unread notifications
        /// </summary>
        protected int NotificationCount => NotificationService.UnreadCount;

        /// <summary>
        /// Whether the app preview is visible
        /// </summary>
        protected bool IsAppPreviewVisible { get; set; }

        /// <summary>
        /// Left position of the app preview
        /// </summary>
        protected int AppPreviewLeft { get; set; }

        /// <summary>
        /// Currently previewed application
        /// </summary>
        protected TaskbarAppModel? CurrentPreviewApp { get; set; }

        /// <summary>
        /// Whether the calendar is visible
        /// </summary>
        protected bool IsCalendarVisible { get; set; }

        /// <summary>
        /// List of days in the calendar
        /// </summary>
        protected List<CalendarDayModel> CalendarDays { get; set; } = new();        /// <summary>
        /// Timer for updating the clock
        /// </summary>
        private global::System.Timers.Timer? _clockTimer;

        /// <summary>
        /// Initialize the component
        /// </summary>
        protected override void OnInitialized()
        {
            // Subscribe to application events
            ApplicationManager.ApplicationLaunched += OnApplicationStarted;
            ApplicationManager.ApplicationTerminated += OnApplicationClosed;
            ApplicationManager.ApplicationStateChanged += OnApplicationStateChanged;

            // Subscribe to notification events
            NotificationService.NotificationAdded += OnNotificationAdded;
            NotificationService.NotificationRemoved += OnNotificationRemoved;
            NotificationService.NotificationRead += OnNotificationRead;

            // Initialize clock timer
            _clockTimer = new global::System.Timers.Timer(1000);
            _clockTimer.Elapsed += (s, e) =>
            {
                CurrentDateTime = DateTime.Now;
                InvokeAsync(StateHasChanged);
            };
            _clockTimer.Start();

            // Initialize calendar
            UpdateCalendar();

            // Load running applications
            LoadRunningApplications();

            base.OnInitialized();
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            // Stop and dispose the clock timer
            if (_clockTimer != null)
            {
                _clockTimer.Stop();
                _clockTimer.Dispose();
                _clockTimer = null;
            }

            // Unsubscribe from events
            ApplicationManager.ApplicationLaunched -= OnApplicationStarted;
            ApplicationManager.ApplicationTerminated -= OnApplicationClosed;
            ApplicationManager.ApplicationStateChanged -= OnApplicationStateChanged;

            NotificationService.NotificationAdded -= OnNotificationAdded;
            NotificationService.NotificationRemoved -= OnNotificationRemoved;
            NotificationService.NotificationRead -= OnNotificationRead;
        }

        /// <summary>
        /// Toggle the application launcher
        /// </summary>
        protected void ToggleLauncher()
        {
            OnLauncherToggled.InvokeAsync();
        }

        /// <summary>
        /// Toggle the notification center
        /// </summary>
        protected void ToggleNotificationCenter()
        {
            OnNotificationCenterToggled.InvokeAsync();
        }

        /// <summary>
        /// Toggle the calendar
        /// </summary>
        protected void ToggleCalendar()
        {
            IsCalendarVisible = !IsCalendarVisible;

            // Hide app preview if calendar is shown
            if (IsCalendarVisible)
            {
                IsAppPreviewVisible = false;
            }

            StateHasChanged();
        }

        /// <summary>
        /// Navigate to the previous month in the calendar
        /// </summary>
        protected void PreviousMonth()
        {
            CurrentDateTime = CurrentDateTime.AddMonths(-1);
            UpdateCalendar();
            StateHasChanged();
        }

        /// <summary>
        /// Navigate to the next month in the calendar
        /// </summary>
        protected void NextMonth()
        {
            CurrentDateTime = CurrentDateTime.AddMonths(1);
            UpdateCalendar();
            StateHasChanged();
        }

        /// <summary>
        /// Update the calendar days
        /// </summary>
        private void UpdateCalendar()
        {
            CalendarDays = new List<CalendarDayModel>();

            // Get the first day of the month
            var firstDay = new DateTime(CurrentDateTime.Year, CurrentDateTime.Month, 1);
            
            // Get the last day of the month
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            
            // Get the day of week for the first day (0 = Sunday, 6 = Saturday)
            int startDayOfWeek = (int)firstDay.DayOfWeek;
            
            // Add empty days for the start of the month
            for (int i = 0; i < startDayOfWeek; i++)
            {
                CalendarDays.Add(new CalendarDayModel
                {
                    Day = 0,
                    IsCurrentMonth = false,
                    IsToday = false
                });
            }
            
            // Add days for the current month
            for (int i = 1; i <= lastDay.Day; i++)
            {
                var day = new DateTime(CurrentDateTime.Year, CurrentDateTime.Month, i);
                CalendarDays.Add(new CalendarDayModel
                {
                    Day = i,
                    IsCurrentMonth = true,
                    IsToday = day.Date == DateTime.Today
                });
            }
            
            // Add empty days for the end of the month
            int endDayOfWeek = (int)lastDay.DayOfWeek;
            int daysToAdd = 6 - endDayOfWeek;
            for (int i = 0; i < daysToAdd; i++)
            {
                CalendarDays.Add(new CalendarDayModel
                {
                    Day = 0,
                    IsCurrentMonth = false,
                    IsToday = false
                });
            }
        }

        /// <summary>
        /// Load the list of running applications
        /// </summary>
        private void LoadRunningApplications()
        {
            RunningApplications.Clear();

            // Get all running applications
            var apps = ApplicationManager.GetRunningApplications();
            foreach (var app in apps)
            {
                // Find the main window for this application
                var window = WindowManager.GetApplicationWindow(app.Id);
                if (window == null) continue;

                // Get last launch time from app history if available
                DateTime? lastLaunchTime = null;
                // This would normally come from a user app history service
                // For now, we'll just use the current time
                lastLaunchTime = DateTime.Now;

                // Create taskbar app model
                var taskbarApp = new TaskbarAppModel
                {
                    Id = app.Id,
                    Name = app.Name,
                    IconPath = app.IconPath ?? "/images/app-icons/default.png",
                    IsActive = window.IsActive,
                    IsMinimized = window.IsMinimized,
                    PreviewImagePath = "/images/application-preview.png", // TODO: Generate actual preview
                    LastLaunched = lastLaunchTime
                };

                RunningApplications.Add(taskbarApp);
            }

            // Sort the applications by most recently used
            RunningApplications = RunningApplications
                .OrderByDescending(app => app.LastLaunched)
                .ToList();

            Logger.LogDebug("Loaded {Count} running applications", RunningApplications.Count);
            StateHasChanged();
        }

        /// <summary>
        /// Toggle an application's window state
        /// </summary>
        protected async Task ToggleApplication(string appId)
        {
            var app = RunningApplications.FirstOrDefault(a => a.Id == appId);
            if (app == null) return;

            // Log task switching action
            Logger.LogDebug("Task switching: {AppId}, IsActive: {IsActive}, IsMinimized: {IsMinimized}", 
                appId, app.IsActive, app.IsMinimized);

            if (app.IsActive)
            {
                // If active, minimize
                await WindowManager.MinimizeApplication(appId);
                Logger.LogDebug("Minimized application: {AppId}", appId);
            }
            else if (app.IsMinimized)
            {
                // If minimized, restore
                await WindowManager.RestoreApplication(appId);
                Logger.LogDebug("Restored application: {AppId}", appId);
            }
            else
            {
                // Otherwise, bring to front
                WindowManager.BringToFront(appId);
                Logger.LogDebug("Brought application to front: {AppId}", appId);
            }
            
            // Update the UI after a short delay to ensure state has propagated
            await Task.Delay(50);
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Show application preview
        /// </summary>
        protected void ShowAppPreview(string appId)
        {
            // Find the app in the taskbar
            var app = RunningApplications.FirstOrDefault(a => a.Id == appId);
            if (app == null) return;

            // Find the taskbar button element to position the preview
            // This would normally be done with JavaScript interop
            // For now, we'll use a simple position calculation
            int index = RunningApplications.IndexOf(app);
            AppPreviewLeft = 100 + (index * 50); // Rough estimate, would be better with JS interop

            // Set the current preview app
            CurrentPreviewApp = app;
            IsAppPreviewVisible = true;

            // Hide calendar if preview is shown
            if (IsCalendarVisible)
            {
                IsCalendarVisible = false;
            }

            StateHasChanged();
        }

        /// <summary>
        /// Hide application preview
        /// </summary>
        protected void HideAppPreview()
        {
            IsAppPreviewVisible = false;
            StateHasChanged();
        }

        /// <summary>
        /// Handle application started event
        /// </summary>
        private void OnApplicationStarted(object? sender, ApplicationLaunchedEventArgs e)
        {
            LoadRunningApplications();
        }

        /// <summary>
        /// Handle application closed event
        /// </summary>
        private void OnApplicationClosed(object? sender, ApplicationTerminatedEventArgs e)
        {
            LoadRunningApplications();
        }

        /// <summary>
        /// Handle application state changed event
        /// </summary>
        private void OnApplicationStateChanged(object? sender, ApplicationStateChangedEventArgs e)
        {
            // Update the application state in the taskbar
            var app = RunningApplications.FirstOrDefault(a => a.Id == e.Application.Id);
            
            if (app != null)
            {
                var window = WindowManager.GetApplicationWindow(app.Id);
                
                if (window != null)
                {
                    app.IsActive = window.IsActive;
                    app.IsMinimized = window.IsMinimized;
                    
                    // Track state change for visual feedback
                    if (e.PreviousState != e.NewState)
                    {
                        // Log state change for debugging
                        Logger.LogDebug("Application {AppId} state changed from {PrevState} to {NewState}", 
                            e.Application.Id, e.PreviousState, e.NewState);
                        
                        // Provide visual indication for certain state changes
                        switch (e.NewState)
                        {
                            case ApplicationState.Running:
                                if (e.PreviousState == ApplicationState.Minimized)
                                {
                                    // Application was restored from minimized state
                                    app.ShowRestoredAnimation = true;
                                }
                                break;
                                
                            case ApplicationState.Minimized:
                                // Application was minimized
                                app.ShowMinimizedAnimation = true;
                                break;
                                
                            case ApplicationState.Maximized:
                                // Application was maximized
                                app.ShowMaximizedAnimation = true;
                                break;
                        }
                        
                        // Reset animation flags after a short delay
                        _ = Task.Delay(500).ContinueWith(_ => 
                        {
                            app.ShowRestoredAnimation = false;
                            app.ShowMinimizedAnimation = false;
                            app.ShowMaximizedAnimation = false;
                            InvokeAsync(StateHasChanged);
                        });
                    }
                }
                
                InvokeAsync(StateHasChanged);
            }
            else if (e.NewState == ApplicationState.Running || e.NewState == ApplicationState.Maximized)
            {
                // New application started, refresh the list
                LoadRunningApplications();
            }
        }

        /// <summary>
        /// Handle notification added event
        /// </summary>
        private void OnNotificationAdded(object? sender, NotificationEventArgs e)
        {
            InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Handle notification removed event
        /// </summary>
        private void OnNotificationRemoved(object? sender, NotificationEventArgs e)
        {
            InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Handle notification read event
        /// </summary>
        private void OnNotificationRead(object? sender, NotificationEventArgs e)
        {
            InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Handle keyboard shortcuts for task switching (Alt+Tab)
        /// </summary>
        /// <param name="e">Keyboard event</param>
        /// <returns>True if the shortcut was handled</returns>
        public async Task<bool> HandleTaskSwitchingKeys(KeyboardEventArgs e)
        {
            // Alt+Tab implementation
            if (e.AltKey && e.Key == "Tab")
            {
                // If no running applications, do nothing
                if (!RunningApplications.Any())
                {
                    return false;
                }
                
                // Find the next application to switch to
                TaskbarAppModel? nextApp = null;
                
                // Get the current active app
                var activeApp = RunningApplications.FirstOrDefault(a => a.IsActive);
                
                if (activeApp != null)
                {
                    // Find the next app in the list
                    int currentIndex = RunningApplications.IndexOf(activeApp);
                    int nextIndex = (currentIndex + 1) % RunningApplications.Count;
                    nextApp = RunningApplications[nextIndex];
                }
                else
                {
                    // If no active app, select the first one
                    nextApp = RunningApplications.FirstOrDefault();
                }
                
                if (nextApp != null)
                {
                    // Switch to the next app
                    await ToggleApplication(nextApp.Id);
                    Logger.LogDebug("Task switching with keyboard shortcut to {AppId}", nextApp.Id);
                    return true;
                }
            }
            
            return false;
        }
    }
}
