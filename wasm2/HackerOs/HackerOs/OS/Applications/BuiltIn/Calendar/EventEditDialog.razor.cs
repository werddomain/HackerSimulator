using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Dialog component for creating and editing calendar events
/// </summary>
public partial class EventEditDialog : ComponentBase
{
    /// <summary>
    /// Gets or sets the event being edited
    /// </summary>
    [Parameter] public CalendarEvent Event { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets whether this is a new event
    /// </summary>
    [Parameter] public bool IsNew { get; set; }
    
    /// <summary>
    /// Event callback for when the event is saved
    /// </summary>
    [Parameter] public EventCallback<CalendarEvent> OnSave { get; set; }
    
    /// <summary>
    /// Event callback for when the event is deleted
    /// </summary>
    [Parameter] public EventCallback<CalendarEvent> OnDelete { get; set; }
    
    /// <summary>
    /// Event callback for when the dialog is cancelled
    /// </summary>
    [Parameter] public EventCallback OnCancel { get; set; }
    
    /// <summary>
    /// Gets the available categories
    /// </summary>
    protected List<string> Categories { get; } = new()
    {
        "Default",
        "Work",
        "Personal",
        "Holiday",
        "Meeting"
    };
    
    /// <summary>
    /// Gets the available colors
    /// </summary>
    protected List<string> Colors { get; } = new()
    {
        "#3498db", // Blue
        "#e74c3c", // Red
        "#2ecc71", // Green
        "#f39c12", // Orange
        "#9b59b6", // Purple
        "#1abc9c", // Teal
        "#34495e", // Dark Blue
        "#e67e22", // Dark Orange
        "#95a5a6"  // Gray
    };
    
    /// <summary>
    /// Gets the days of the week for recurrence
    /// </summary>
    protected Dictionary<DayOfWeek, string> DaysOfWeek { get; } = new()
    {
        { DayOfWeek.Sunday, "Sun" },
        { DayOfWeek.Monday, "Mon" },
        { DayOfWeek.Tuesday, "Tue" },
        { DayOfWeek.Wednesday, "Wed" },
        { DayOfWeek.Thursday, "Thu" },
        { DayOfWeek.Friday, "Fri" },
        { DayOfWeek.Saturday, "Sat" }
    };
    
    // Properties for start and end dates
    protected DateTime StartDate
    {
        get => Event.StartTime.Date;
        set
        {
            Event.StartTime = new DateTime(
                value.Year,
                value.Month,
                value.Day,
                Event.StartTime.Hour,
                Event.StartTime.Minute,
                0);
                
            // If end date is before start date, update it
            if (Event.EndTime.Date < Event.StartTime.Date)
            {
                Event.EndTime = new DateTime(
                    Event.StartTime.Year,
                    Event.StartTime.Month,
                    Event.StartTime.Day,
                    Event.EndTime.Hour,
                    Event.EndTime.Minute,
                    0);
            }
        }
    }
    
    protected string StartTime
    {
        get => Event.StartTime.ToString("HH:mm");
        set
        {
            if (TimeSpan.TryParse(value, out var time))
            {
                Event.StartTime = new DateTime(
                    Event.StartTime.Year,
                    Event.StartTime.Month,
                    Event.StartTime.Day,
                    time.Hours,
                    time.Minutes,
                    0);
                    
                // If end time is before start time on the same day, update it
                if (Event.EndTime.Date == Event.StartTime.Date && 
                    Event.EndTime.TimeOfDay < Event.StartTime.TimeOfDay)
                {
                    // Add an hour to the start time
                    Event.EndTime = Event.StartTime.AddHours(1);
                }
            }
        }
    }
    
    protected DateTime EndDate
    {
        get => Event.EndTime.Date;
        set
        {
            Event.EndTime = new DateTime(
                value.Year,
                value.Month,
                value.Day,
                Event.EndTime.Hour,
                Event.EndTime.Minute,
                0);
                
            // If end date is before start date, update start date
            if (Event.EndTime.Date < Event.StartTime.Date)
            {
                Event.StartTime = new DateTime(
                    Event.EndTime.Year,
                    Event.EndTime.Month,
                    Event.EndTime.Day,
                    Event.StartTime.Hour,
                    Event.StartTime.Minute,
                    0);
            }
        }
    }
    
    protected string EndTime
    {
        get => Event.EndTime.ToString("HH:mm");
        set
        {
            if (TimeSpan.TryParse(value, out var time))
            {
                Event.EndTime = new DateTime(
                    Event.EndTime.Year,
                    Event.EndTime.Month,
                    Event.EndTime.Day,
                    time.Hours,
                    time.Minutes,
                    0);
                    
                // If end time is before start time on the same day, update start time
                if (Event.EndTime.Date == Event.StartTime.Date && 
                    Event.EndTime.TimeOfDay < Event.StartTime.TimeOfDay)
                {
                    // Set start time to an hour before end time
                    Event.StartTime = Event.EndTime.AddHours(-1);
                }
            }
        }
    }
    
    // Recurrence properties
    protected string RecurrenceType
    {
        get
        {
            if (Event.Recurrence == null)
            {
                return "none";
            }
            
            return Event.Recurrence.Type.ToString().ToLower();
        }
        set
        {
            if (value == "none")
            {
                Event.Recurrence = null;
            }
            else
            {
                Event.Recurrence ??= new RecurrencePattern();
                Event.Recurrence.Type = value.ToLower() switch
                {
                    "daily" => RecurrenceType.Daily,
                    "weekly" => RecurrenceType.Weekly,
                    "monthly" => RecurrenceType.Monthly,
                    "yearly" => RecurrenceType.Yearly,
                    _ => RecurrenceType.Daily
                };
                
                // If it's weekly and no days are selected, select the day of the event
                if (Event.Recurrence.Type == RecurrenceType.Weekly && !Event.Recurrence.DaysOfWeek.Any())
                {
                    Event.Recurrence.DaysOfWeek.Add(Event.StartTime.DayOfWeek);
                }
            }
        }
    }
    
    protected int RecurrenceInterval
    {
        get => Event.Recurrence?.Interval ?? 1;
        set
        {
            if (Event.Recurrence != null)
            {
                Event.Recurrence.Interval = Math.Max(1, value);
            }
        }
    }
    
    protected string RecurrenceEnd
    {
        get
        {
            if (Event.Recurrence == null)
            {
                return "never";
            }
            
            if (Event.Recurrence.EndDate.HasValue)
            {
                return "date";
            }
            
            if (Event.Recurrence.NumberOfOccurrences.HasValue)
            {
                return "count";
            }
            
            return "never";
        }
        set
        {
            if (Event.Recurrence == null)
            {
                return;
            }
            
            switch (value)
            {
                case "never":
                    Event.Recurrence.EndDate = null;
                    Event.Recurrence.NumberOfOccurrences = null;
                    break;
                case "date":
                    Event.Recurrence.EndDate = Event.StartTime.AddMonths(1);
                    Event.Recurrence.NumberOfOccurrences = null;
                    break;
                case "count":
                    Event.Recurrence.EndDate = null;
                    Event.Recurrence.NumberOfOccurrences = 10;
                    break;
            }
        }
    }
    
    protected DateTime RecurrenceEndDate
    {
        get => Event.Recurrence?.EndDate ?? DateTime.Today.AddMonths(1);
        set
        {
            if (Event.Recurrence != null)
            {
                Event.Recurrence.EndDate = value;
            }
        }
    }
    
    protected int RecurrenceCount
    {
        get => Event.Recurrence?.NumberOfOccurrences ?? 10;
        set
        {
            if (Event.Recurrence != null)
            {
                Event.Recurrence.NumberOfOccurrences = Math.Max(1, value);
            }
        }
    }
    
    /// <summary>
    /// Checks if a day of the week is selected in the recurrence pattern
    /// </summary>
    protected bool IsWeekDaySelected(DayOfWeek day)
    {
        return Event.Recurrence?.DaysOfWeek.Contains(day) ?? false;
    }
    
    /// <summary>
    /// Toggles a day of the week in the recurrence pattern
    /// </summary>
    protected void ToggleWeekDay(DayOfWeek day, object? value)
    {
        if (Event.Recurrence == null || Event.Recurrence.Type != RecurrenceType.Weekly)
        {
            return;
        }
        
        bool isChecked = (bool)(value ?? false);
        
        if (isChecked && !Event.Recurrence.DaysOfWeek.Contains(day))
        {
            Event.Recurrence.DaysOfWeek.Add(day);
        }
        else if (!isChecked && Event.Recurrence.DaysOfWeek.Contains(day))
        {
            Event.Recurrence.DaysOfWeek.Remove(day);
        }
    }
    
    /// <summary>
    /// Adds a new reminder to the event
    /// </summary>
    protected void AddReminder()
    {
        Event.Reminders.Add(new ReminderInfo { MinutesBefore = 15 });
    }
    
    /// <summary>
    /// Removes a reminder from the event
    /// </summary>
    protected void RemoveReminder(ReminderInfo reminder)
    {
        Event.Reminders.Remove(reminder);
    }
    
    /// <summary>
    /// Saves the event
    /// </summary>
    protected async Task SaveEvent()
    {
        // Ensure the event has a valid title
        if (string.IsNullOrWhiteSpace(Event.Title))
        {
            Event.Title = "Untitled Event";
        }
        
        // Set created/modified times
        if (IsNew)
        {
            Event.Created = DateTime.Now;
        }
        
        Event.LastModified = DateTime.Now;
        
        // For all-day events, ensure times are at midnight
        if (Event.IsAllDay)
        {
            Event.StartTime = Event.StartTime.Date;
            Event.EndTime = Event.EndTime.Date.AddDays(1).AddSeconds(-1);
        }
        
        await OnSave.InvokeAsync(Event);
    }
    
    /// <summary>
    /// Deletes the event
    /// </summary>
    protected async Task DeleteEvent()
    {
        await OnDelete.InvokeAsync(Event);
    }
}
