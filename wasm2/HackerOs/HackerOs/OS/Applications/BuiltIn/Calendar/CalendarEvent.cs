using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackerOs.OS.User;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;


/// <summary>
/// Represents a calendar event with title, description, date/time, and related properties
/// </summary>
public class CalendarEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for the event
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Gets or sets the title of the event
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the description of the event
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the start time of the event
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the end time of the event
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Gets or sets whether the event is an all-day event
    /// </summary>
    public bool IsAllDay { get; set; }
    
    /// <summary>
    /// Gets or sets the location of the event
    /// </summary>
    public string Location { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the category of the event (e.g., Work, Personal, Holiday)
    /// </summary>
    public string Category { get; set; } = "Default";
    
    /// <summary>
    /// Gets or sets the color of the event for display
    /// </summary>
    public string Color { get; set; } = "#3498db"; // Default blue
    
    /// <summary>
    /// Gets or sets the recurrence pattern for the event
    /// </summary>
    public RecurrencePattern? Recurrence { get; set; }
    
    /// <summary>
    /// Gets or sets the reminder times for the event
    /// </summary>
    public List<ReminderInfo> Reminders { get; set; } = new();
    
    /// <summary>
    /// Gets or sets additional custom properties for the event
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the creation time of the event
    /// </summary>
    public DateTime Created { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets or sets the last modified time of the event
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// Determines if the event occurs on a specific date
    /// </summary>
    /// <param name="date">The date to check</param>
    /// <returns>True if the event occurs on the specified date, otherwise false</returns>
    public bool OccursOnDate(DateTime date)
    {
        // For all-day events, check if the date falls within the event dates
        if (IsAllDay)
        {
            return date.Date >= StartTime.Date && date.Date <= EndTime.Date;
        }
        
        // For non-recurring events, check if the date is within the event timeframe
        if (Recurrence == null)
        {
            return date.Date >= StartTime.Date && date.Date <= EndTime.Date;
        }
        
        // For recurring events, use the recurrence pattern to determine
        return Recurrence.IncludesDate(StartTime, date);
    }
    
    /// <summary>
    /// Gets all occurrences of the event within a date range
    /// </summary>
    /// <param name="start">The start date of the range</param>
    /// <param name="end">The end date of the range</param>
    /// <returns>A list of event occurrences</returns>
    public List<EventOccurrence> GetOccurrencesInRange(DateTime start, DateTime end)
    {
        var occurrences = new List<EventOccurrence>();
        
        // If it's not a recurring event, check if it falls within the range
        if (Recurrence == null)
        {
            if (!(EndTime < start || StartTime > end))
            {
                occurrences.Add(new EventOccurrence
                {
                    OriginalEvent = this,
                    StartTime = StartTime,
                    EndTime = EndTime
                });
            }
            
            return occurrences;
        }
        
        // For recurring events, calculate all occurrences in the range
        var dates = Recurrence.GetOccurrencesInRange(StartTime, start, end);
        
        foreach (var date in dates)
        {
            // Skip excluded dates
            if (Recurrence.ExcludedDates.Contains(date.Date))
            {
                continue;
            }
            
            // Calculate the start and end times for this occurrence
            TimeSpan duration = EndTime - StartTime;
            DateTime occurrenceStart;
            DateTime occurrenceEnd;
            
            if (IsAllDay)
            {
                occurrenceStart = date.Date;
                occurrenceEnd = date.Date.Add(duration);
            }
            else
            {
                occurrenceStart = date.Date.Add(StartTime.TimeOfDay);
                occurrenceEnd = occurrenceStart.Add(duration);
            }
            
            occurrences.Add(new EventOccurrence
            {
                OriginalEvent = this,
                StartTime = occurrenceStart,
                EndTime = occurrenceEnd
            });
        }
        
        return occurrences;
    }
    
    /// <summary>
    /// Gets the next reminder time after the specified time
    /// </summary>
    /// <param name="after">The time to check after</param>
    /// <returns>The next reminder time, or null if no reminders are set</returns>
    public DateTime? GetNextReminderTime(DateTime after)
    {
        if (Reminders.Count == 0)
        {
            return null;
        }
        
        // For non-recurring events
        if (Recurrence == null)
        {
            var nextReminder = Reminders
                .Select(r => StartTime.AddMinutes(-r.MinutesBefore))
                .Where(t => t > after)
                .OrderBy(t => t)
                .FirstOrDefault();
                
            return nextReminder == default ? null : nextReminder;
        }
        
        // For recurring events, find the next occurrence and its reminders
        var nextOccurrences = GetOccurrencesInRange(after, after.AddYears(1));
        
        var reminderTimes = new List<DateTime>();
        foreach (var occurrence in nextOccurrences)
        {
            foreach (var reminder in Reminders)
            {
                var reminderTime = occurrence.StartTime.AddMinutes(-reminder.MinutesBefore);
                if (reminderTime > after)
                {
                    reminderTimes.Add(reminderTime);
                }
            }
        }
        
        return reminderTimes.Any() ? reminderTimes.Min() : null;
    }
    
    /// <summary>
    /// Creates a copy of the event
    /// </summary>
    /// <returns>A new CalendarEvent with the same properties</returns>
    public CalendarEvent Clone()
    {
        var clone = new CalendarEvent
        {
            Id = Guid.NewGuid(), // Generate a new ID for the clone
            Title = Title,
            Description = Description,
            StartTime = StartTime,
            EndTime = EndTime,
            IsAllDay = IsAllDay,
            Location = Location,
            Category = Category,
            Color = Color,
            Created = DateTime.Now,
            LastModified = DateTime.Now
        };
        
        // Clone the recurrence pattern if it exists
        if (Recurrence != null)
        {
            clone.Recurrence = Recurrence.Clone();
        }
        
        // Clone the reminders
        foreach (var reminder in Reminders)
        {
            clone.Reminders.Add(new ReminderInfo
            {
                MinutesBefore = reminder.MinutesBefore,
                ReminderType = reminder.ReminderType
            });
        }
        
        // Clone custom properties
        foreach (var property in CustomProperties)
        {
            clone.CustomProperties[property.Key] = property.Value;
        }
        
        return clone;
    }
}

/// <summary>
/// Represents a specific occurrence of a recurring event
/// </summary>
public class EventOccurrence
{
    /// <summary>
    /// Gets or sets the original event that this occurrence is based on
    /// </summary>
    public CalendarEvent OriginalEvent { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the start time of this occurrence
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the end time of this occurrence
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Gets whether this occurrence is an exception to the recurrence pattern
    /// </summary>
    public bool IsException => OriginalEvent.Recurrence?.ExcludedDates.Contains(StartTime.Date) ?? false;
}

/// <summary>
/// Represents information about an event reminder
/// </summary>
public class ReminderInfo
{
    /// <summary>
    /// Gets or sets the number of minutes before the event to trigger the reminder
    /// </summary>
    public int MinutesBefore { get; set; } = 15; // Default 15 minutes before
    
    /// <summary>
    /// Gets or sets the type of reminder
    /// </summary>
    public ReminderType ReminderType { get; set; } = ReminderType.Notification;
}


