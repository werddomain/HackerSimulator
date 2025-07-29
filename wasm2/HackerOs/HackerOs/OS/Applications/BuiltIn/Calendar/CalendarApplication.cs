using System;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Lifecycle;
using HackerOs.OS.Applications.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BlazorWindowManager.Components;
using BlazorWindowManager.Models;
using HackerOs.OS.UI.Models;
using HackerOs.OS.Applications.BuiltIn.Calendar;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Calendar application for managing events and schedules
/// </summary>
[App("Calendar", "builtin.Calendar")]
[AppDescription("A calendar application for managing events and reminders.")]
public class CalendarApplication : WindowApplicationBase
{
    private readonly ILogger<CalendarApplication> _logger;
    private readonly ICalendarEngineService _calendarEngine;
    private CalendarViewMode _currentView = CalendarViewMode.Month;
    private DateTime _selectedDate = DateTime.Today;
    
    /// <summary>
    /// Initializes a new instance of the CalendarApplication class
    /// </summary>
    public CalendarApplication(
        ILogger<CalendarApplication> logger)
    {
        _logger = logger;
        _calendarEngine = ServiceProvider.GetRequiredService<ICalendarEngineService>();
    }
    
    /// <inheritdoc />    /// <inheritdoc />
    protected override Task<bool> OnStartAsync(ApplicationLaunchContext context)
    {
        _logger.LogInformation("Starting calendar application");
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    protected override Task<bool> OnStopAsync()
    {
        _logger.LogInformation("Stopping calendar application");
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    protected override RenderFragment GetWindowContent()
    {
        return builder =>
        {
            builder.OpenComponent<CalendarComponent>(0);
            builder.AddAttribute(1, "CalendarEngine", _calendarEngine);
            builder.AddAttribute(2, "CurrentView", _currentView);
            builder.AddAttribute(3, "SelectedDate", _selectedDate);
            builder.AddAttribute(4, "ViewChanged", EventCallback.Factory.Create<CalendarViewMode>(this, OnViewChanged));
            builder.AddAttribute(5, "DateSelected", EventCallback.Factory.Create<DateTime>(this, OnDateSelected));
            builder.CloseComponent();
        };
    }

    /// <inheritdoc />
    public override async Task<bool> OnStartAsync(ApplicationLifecycleContext context)
    {
        await base.OnStartAsync(context);
        await _calendarEngine.LoadSettingsAsync();
        return true;
    }

    /// <inheritdoc />
    public override async Task<bool> OnCloseAsync(ApplicationLifecycleContext context)
    {
        await _calendarEngine.SaveSettingsAsync();
        await base.OnCloseAsync(context);
        return true;
    }
    
    #region Event Handlers
    
    private void OnViewChanged(CalendarViewMode viewMode)
    {
        _currentView = viewMode;
        _calendarEngine.ViewMode = viewMode;
    }
    
    private void OnDateSelected(DateTime date)
    {
        _selectedDate = date;
        _calendarEngine.SelectedDate = date;
    }
    
    private async void OnEventCreated(CalendarEvent calendarEvent)
    {
        await _calendarEngine.AddEventAsync(calendarEvent);
        NotifyStateChanged();
    }
    
    private async void OnEventUpdated(CalendarEvent calendarEvent)
    {
        await _calendarEngine.UpdateEventAsync(calendarEvent);
        NotifyStateChanged();
    }
    
    private async void OnEventDeleted(Guid eventId)
    {
        await _calendarEngine.DeleteEventAsync(eventId);
        NotifyStateChanged();
    }
    
    #endregion
    
    /// <summary>
    /// Navigates to a specific event in the calendar
    /// </summary>
    /// <param name="eventId">The ID of the event to navigate to</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task NavigateToEventAsync(Guid eventId)
    {
        try
        {
            // Get the event
            var calendarEvent = await _calendarEngine.GetEventAsync(eventId);
            if (calendarEvent == null)
            {
                _logger.LogWarning("Event not found: {EventId}", eventId);
                return;
            }
            
            // Navigate to the event date
            _selectedDate = calendarEvent.StartTime.Date;
            _calendarEngine.SelectedDate = _selectedDate;
            
            // Determine the best view based on the event duration
            var duration = calendarEvent.EndTime - calendarEvent.StartTime;
            if (duration.TotalDays >= 7)
            {
                // For events spanning a week or more, month view is best
                _currentView = CalendarViewMode.Month;
            }
            else if (duration.TotalDays > 1)
            {
                // For multi-day events, week view is best
                _currentView = CalendarViewMode.Week;
            }
            else
            {
                // For single-day events, day view provides the most detail
                _currentView = CalendarViewMode.Day;
            }
            
            _calendarEngine.ViewMode = _currentView;
            
            // Request a window content update
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to event: {EventId}", eventId);
        }
    }
    
    /// <summary>
    /// Notifies that the application state has changed and the window content should be updated
    /// </summary>
    private void NotifyStateChanged()
    {
        // Re-render the window content with the updated state
        if (Window != null)
        {
            // Get a fresh instance of the window content
            var content = GetWindowContent();
            
            // Update the window title if needed
            string title = WindowTitle;
            if (Window.Title != title)
            {
                Window.Title = title;
            }
            
            // Request a UI update
            InvokeAsync(StateHasChanged);
        }
    }
}

/// <summary>
/// Represents the serializable state of the calendar application
/// </summary>
public class CalendarApplicationState
{
    /// <summary>
    /// Gets or sets the current view mode
    /// </summary>
    public CalendarViewMode CurrentView { get; set; } = CalendarViewMode.Month;
    
    /// <summary>
    /// Gets or sets the selected date
    /// </summary>
    public DateTime SelectedDate { get; set; } = DateTime.Today;
}
