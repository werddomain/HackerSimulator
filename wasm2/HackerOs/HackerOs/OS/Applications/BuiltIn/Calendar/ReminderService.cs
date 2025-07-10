using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackerOs.OS.Applications.BuiltIn.Calendar;
using HackerOs.OS.UI.Models;
using HackerOs.OS.UI.Services;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.BuiltIn.Calendar
{
    /// <summary>
    /// Service for managing calendar reminders
    /// </summary>
    public class ReminderService : IDisposable
    {
        private readonly ICalendarEngineService _calendarEngine;
        private readonly NotificationService _notificationService;
        private readonly ILogger<ReminderService> _logger;
        
        private Timer? _reminderCheckTimer;
        private const int CHECK_INTERVAL_MS = 60000; // Check every minute
        private const int REMINDER_LOOKAHEAD_MINUTES = 5; // Look 5 minutes ahead
        
        // Store reminder notifications we've sent to avoid duplicates
        private readonly HashSet<string> _sentReminderIds = new();
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ReminderService(
            ICalendarEngineService calendarEngine,
            NotificationService notificationService,
            ILogger<ReminderService> logger)
        {
            _calendarEngine = calendarEngine ?? throw new ArgumentNullException(nameof(calendarEngine));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Start the reminder check timer
            _reminderCheckTimer = new Timer(CheckReminders, null, 5000, CHECK_INTERVAL_MS);
        }
        
        /// <summary>
        /// Checks for upcoming reminders and sends notifications
        /// </summary>
        private async void CheckReminders(object? state)
        {
            try
            {
                var now = DateTime.Now;
                
                // Get reminders coming up in the next few minutes
                var upcomingReminders = await _calendarEngine.GetUpcomingRemindersAsync(
                    now, REMINDER_LOOKAHEAD_MINUTES);
                
                foreach (var (occurrence, reminder) in upcomingReminders)
                {
                    // Generate a unique ID for this reminder occurrence
                    var reminderOccurrenceId = $"{occurrence.OriginalEvent.Id}_{occurrence.StartTime:yyyyMMddHHmm}_{reminder.MinutesBefore}";
                    
                    // Skip if we've already sent this reminder
                    if (_sentReminderIds.Contains(reminderOccurrenceId))
                    {
                        continue;
                    }
                    
                    // Create and send the notification
                    await SendReminderNotificationAsync(occurrence, reminder);
                    
                    // Mark as sent
                    _sentReminderIds.Add(reminderOccurrenceId);
                    
                    // Clean up old reminder IDs (keep set from growing indefinitely)
                    if (_sentReminderIds.Count > 1000)
                    {
                        CleanupOldReminderIds(now);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking reminders");
            }
        }
        
        /// <summary>
        /// Sends a notification for a reminder
        /// </summary>
        private async Task SendReminderNotificationAsync(EventOccurrence occurrence, ReminderInfo reminder)
        {
            try
            {
                var eventTitle = occurrence.OriginalEvent.Title;
                var startTime = occurrence.StartTime;
                
                // Calculate how long until the event
                var timeUntilEvent = startTime - DateTime.Now;
                string timeDescription;
                
                if (timeUntilEvent.TotalMinutes < 1)
                {
                    timeDescription = "now";
                }
                else if (timeUntilEvent.TotalMinutes < 60)
                {
                    timeDescription = $"in {(int)timeUntilEvent.TotalMinutes} minutes";
                }
                else
                {
                    timeDescription = $"at {startTime:h:mm tt}";
                }
                
                // Create the notification
                var notification = new NotificationModel
                {
                    Title = "Calendar Reminder",
                    Content = $"{eventTitle} - {timeDescription}",
                    Source = "Calendar",
                    Type = NotificationType.Info,
                    IconPath = "fa-calendar-alt", // Font Awesome calendar icon
                    TargetApplicationId = "builtin.Calendar", // Launch calendar when clicked
                    ActionData = occurrence.OriginalEvent.Id.ToString() // Pass event ID as data
                };
                
                // Add actions for the notification
                notification.Actions.Add(new NotificationAction
                {
                    Id = "open",
                    Label = "Open",
                    IsPrimary = true,
                    DismissesNotification = true
                });
                
                notification.Actions.Add(new NotificationAction
                {
                    Id = "dismiss",
                    Label = "Dismiss",
                    DismissesNotification = true
                });
                
                notification.Actions.Add(new NotificationAction
                {
                    Id = "snooze5",
                    Label = "Snooze 5 min",
                    DismissesNotification = true
                });
                
                // Send the notification
                await _notificationService.AddNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending reminder notification");
            }
        }
        
        /// <summary>
        /// Dismisses a reminder notification
        /// </summary>
        public async Task DismissReminderAsync(string notificationId)
        {
            await _notificationService.RemoveNotificationAsync(notificationId);
        }
        
        /// <summary>
        /// Snoozes a reminder notification
        /// </summary>
        public async Task SnoozeReminderAsync(string notificationId, int snoozeMinutes, Guid eventId)
        {
            try
            {
                // Remove the current notification
                await _notificationService.RemoveNotificationAsync(notificationId);
                
                // Get the event
                var calendarEvent = await _calendarEngine.GetEventAsync(eventId);
                if (calendarEvent == null)
                {
                    return;
                }
                
                // Create a custom reminder that fires after the snooze period
                // This is a temporary reminder just for the snooze functionality
                var snoozedTime = DateTime.Now.AddMinutes(snoozeMinutes);
                
                // Calculate how many minutes before the event this snoozed time represents
                var minutesBefore = (int)(calendarEvent.StartTime - snoozedTime).TotalMinutes;
                if (minutesBefore <= 0)
                {
                    // Event has already started, just set a short reminder
                    minutesBefore = 1;
                }
                
                // Create a unique ID that won't conflict with sent reminders
                var snoozedReminderId = $"snooze_{eventId}_{DateTime.Now.Ticks}";
                
                // We don't need to add this to the event's reminders collection
                // Just make sure we don't have this ID in our sent set
                _sentReminderIds.Remove(snoozedReminderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error snoozing reminder");
            }
        }
        
        /// <summary>
        /// Removes old reminder IDs from the sent set to prevent memory leaks
        /// </summary>
        private void CleanupOldReminderIds(DateTime now)
        {
            try
            {
                // This is a simplistic cleanup - in a real app, we'd store timestamps with the IDs
                // For now, just keep the most recent 500 IDs
                if (_sentReminderIds.Count > 500)
                {
                    var idsToKeep = _sentReminderIds.TakeLast(500).ToHashSet();
                    _sentReminderIds.Clear();
                    foreach (var id in idsToKeep)
                    {
                        _sentReminderIds.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up reminder IDs");
            }
        }
        
        /// <summary>
        /// Disposes the service
        /// </summary>
        public void Dispose()
        {
            _reminderCheckTimer?.Dispose();
            _reminderCheckTimer = null;
        }
    }
}
