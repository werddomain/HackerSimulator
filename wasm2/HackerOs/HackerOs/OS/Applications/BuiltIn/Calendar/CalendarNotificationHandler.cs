using System;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Services;
using HackerOs.OS.UI.Models;
using HackerOs.OS.UI.Services;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Handles calendar notification actions
/// </summary>
public class CalendarNotificationHandler
{
    private readonly IApplicationLauncher _applicationLauncher;
    private readonly ICalendarEngineService _calendarEngine;
    private readonly ReminderService _reminderService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<CalendarNotificationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the CalendarNotificationHandler class
    /// </summary>
    public CalendarNotificationHandler(
        IApplicationLauncher applicationLauncher,
        ICalendarEngineService calendarEngine,
        ReminderService reminderService,
        NotificationService notificationService,
        ILogger<CalendarNotificationHandler> logger)
    {
        _applicationLauncher = applicationLauncher ?? throw new ArgumentNullException(nameof(applicationLauncher));
        _calendarEngine = calendarEngine ?? throw new ArgumentNullException(nameof(calendarEngine));
        _reminderService = reminderService ?? throw new ArgumentNullException(nameof(reminderService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Subscribe to notification events
        _notificationService.NotificationActionTriggered += OnNotificationActionTriggered;
    }

    /// <summary>
    /// Handles notification actions
    /// </summary>
    private async void OnNotificationActionTriggered(object? sender, NotificationActionEventArgs e)
    {
        if (e.Notification.Source != "Calendar" || string.IsNullOrEmpty(e.Notification.ActionData))
        {
            // Not a calendar notification or no event ID
            return;
        }

        try
        {
            // Get the event ID from the action data
            if (!Guid.TryParse(e.Notification.ActionData, out var eventId))
            {
                _logger.LogWarning("Invalid event ID in calendar notification: {ActionData}", e.Notification.ActionData);
                return;
            }

            // Handle different actions
            switch (e.Action.Id)
            {
                case "open":
                    await HandleOpenAction(eventId, e.Notification.Id);
                    break;
                    
                case "dismiss":
                    await HandleDismissAction(e.Notification.Id);
                    break;
                    
                case "snooze5":
                    await HandleSnoozeAction(eventId, e.Notification.Id, 5); // 5 minutes
                    break;
                    
                case "snooze15":
                    await HandleSnoozeAction(eventId, e.Notification.Id, 15); // 15 minutes
                    break;
                    
                case "snooze30":
                    await HandleSnoozeAction(eventId, e.Notification.Id, 30); // 30 minutes
                    break;
                    
                default:
                    _logger.LogWarning("Unknown calendar notification action: {ActionId}", e.Action.Id);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling calendar notification action");
        }
    }

    /// <summary>
    /// Handles the "Open" action to open the event in the calendar
    /// </summary>
    private async Task HandleOpenAction(Guid eventId, string notificationId)
    {
        // Launch the calendar application
        var app = await _applicationLauncher.LaunchApplicationAsync("builtin.Calendar");
        
        if (app is CalendarApplication calendarApp)
        {
            // Tell the calendar to navigate to the event
            await calendarApp.NavigateToEventAsync(eventId);
        }
        
        // Remove the notification
        await _notificationService.RemoveNotificationAsync(notificationId);
    }

    /// <summary>
    /// Handles the "Dismiss" action to dismiss the notification
    /// </summary>
    private async Task HandleDismissAction(string notificationId)
    {
        // Simply remove the notification
        await _notificationService.RemoveNotificationAsync(notificationId);
    }

    /// <summary>
    /// Handles the "Snooze" action to snooze the reminder
    /// </summary>
    private async Task HandleSnoozeAction(Guid eventId, string notificationId, int minutes)
    {
        // Snooze the reminder
        await _reminderService.SnoozeReminderAsync(notificationId, minutes, eventId);
    }
}
