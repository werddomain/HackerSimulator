using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Represents user settings and preferences for the Calendar application
/// </summary>
public class CalendarSettings
{
    /// <summary>
    /// Gets or sets the default view mode (Month, Week, Day, Agenda)
    /// </summary>
    public CalendarViewMode DefaultViewMode { get; set; } = CalendarViewMode.Month;
    
    /// <summary>
    /// Gets or sets the first day of the week for calendar display
    /// </summary>
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;
    
    /// <summary>
    /// Gets or sets whether to show week numbers
    /// </summary>
    public bool ShowWeekNumbers { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the default reminder time in minutes
    /// </summary>
    public int DefaultReminderMinutes { get; set; } = 15;
    
    /// <summary>
    /// Gets or sets whether to show reminders for all-day events
    /// </summary>
    public bool RemindForAllDayEvents { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the default event duration in minutes
    /// </summary>
    public int DefaultEventDurationMinutes { get; set; } = 60;
    
    /// <summary>
    /// Gets or sets the default categories with their colors
    /// </summary>
    public Dictionary<string, string> EventCategories { get; set; } = new()
    {
        { "Default", "#3498db" },  // Blue
        { "Work", "#e74c3c" },     // Red
        { "Personal", "#2ecc71" }, // Green
        { "Holiday", "#f39c12" },  // Orange
        { "Meeting", "#9b59b6" }   // Purple
    };
    
    /// <summary>
    /// Gets or sets the work day start hour (0-23)
    /// </summary>
    public int WorkDayStartHour { get; set; } = 9;
    
    /// <summary>
    /// Gets or sets the work day end hour (0-23)
    /// </summary>
    public int WorkDayEndHour { get; set; } = 17;
    
    /// <summary>
    /// Gets or sets whether to highlight work hours in the week and day views
    /// </summary>
    public bool HighlightWorkHours { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to show declined events
    /// </summary>
    public bool ShowDeclinedEvents { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to show a mini calendar in the sidebar
    /// </summary>
    public bool ShowMiniCalendar { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to show week numbers in the mini calendar
    /// </summary>
    public bool ShowWeekNumbersInMiniCalendar { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the number of days to display in agenda view
    /// </summary>
    public int AgendaViewDays { get; set; } = 14;
    
    /// <summary>
    /// Gets or sets the time format (12-hour or 24-hour)
    /// </summary>
    public TimeFormat TimeFormat { get; set; } = TimeFormat.Hours12;
    
    /// <summary>
    /// Gets or sets whether to automatically add reminders to new events
    /// </summary>
    public bool AutoAddReminders { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to play sound alerts for reminders
    /// </summary>
    public bool PlayReminderSounds { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to show upcoming events in the sidebar
    /// </summary>
    public bool ShowUpcomingEvents { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the number of upcoming events to show
    /// </summary>
    public int NumberOfUpcomingEvents { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets whether to use category colors for event display
    /// </summary>
    public bool UseCategoryColors { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the default all-day event color
    /// </summary>
    public string AllDayEventColor { get; set; } = "#34495e"; // Dark blue
    
    /// <summary>
    /// Creates a clone of the settings
    /// </summary>
    /// <returns>A new CalendarSettings instance with the same values</returns>
    public CalendarSettings Clone()
    {
        var clone = new CalendarSettings
        {
            DefaultViewMode = DefaultViewMode,
            FirstDayOfWeek = FirstDayOfWeek,
            ShowWeekNumbers = ShowWeekNumbers,
            DefaultReminderMinutes = DefaultReminderMinutes,
            RemindForAllDayEvents = RemindForAllDayEvents,
            DefaultEventDurationMinutes = DefaultEventDurationMinutes,
            WorkDayStartHour = WorkDayStartHour,
            WorkDayEndHour = WorkDayEndHour,
            HighlightWorkHours = HighlightWorkHours,
            ShowDeclinedEvents = ShowDeclinedEvents,
            ShowMiniCalendar = ShowMiniCalendar,
            ShowWeekNumbersInMiniCalendar = ShowWeekNumbersInMiniCalendar,
            AgendaViewDays = AgendaViewDays,
            TimeFormat = TimeFormat,
            AutoAddReminders = AutoAddReminders,
            PlayReminderSounds = PlayReminderSounds,
            ShowUpcomingEvents = ShowUpcomingEvents,
            NumberOfUpcomingEvents = NumberOfUpcomingEvents,
            UseCategoryColors = UseCategoryColors,
            AllDayEventColor = AllDayEventColor
        };
        
        // Clone event categories
        foreach (var category in EventCategories)
        {
            clone.EventCategories[category.Key] = category.Value;
        }
        
        return clone;
    }
    
    /// <summary>
    /// Serializes the settings to JSON
    /// </summary>
    /// <returns>A JSON string representation of the settings</returns>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        return JsonSerializer.Serialize(this, options);
    }
    
    /// <summary>
    /// Deserializes settings from JSON
    /// </summary>
    /// <param name="json">The JSON string to deserialize</param>
    /// <returns>A CalendarSettings instance</returns>
    public static CalendarSettings FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new CalendarSettings();
        }
        
        try
        {
            return JsonSerializer.Deserialize<CalendarSettings>(json) ?? new CalendarSettings();
        }
        catch
        {
            // If deserialization fails, return default settings
            return new CalendarSettings();
        }
    }
}

/// <summary>
/// Defines the available calendar view modes
/// </summary>
public enum CalendarViewMode
{
    /// <summary>
    /// Month view showing a traditional calendar grid
    /// </summary>
    Month,
    
    /// <summary>
    /// Week view showing hourly schedule for each day
    /// </summary>
    Week,
    
    /// <summary>
    /// Day view showing detailed schedule for a single day
    /// </summary>
    Day,
    
    /// <summary>
    /// Agenda view showing upcoming events in a list
    /// </summary>
    Agenda,
    
    /// <summary>
    /// Year view showing an overview of the entire year
    /// </summary>
    Year
}

/// <summary>
/// Defines time format options
/// </summary>
public enum TimeFormat
{
    /// <summary>
    /// 12-hour format with AM/PM
    /// </summary>
    Hours12,
    
    /// <summary>
    /// 24-hour format
    /// </summary>
    Hours24
}


