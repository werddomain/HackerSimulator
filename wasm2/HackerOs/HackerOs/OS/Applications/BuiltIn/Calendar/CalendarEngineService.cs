using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.IO;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Interface for Calendar Engine Service
/// </summary>
public interface ICalendarEngineService
{
    /// <summary>
    /// Gets or sets the calendar settings
    /// </summary>
    CalendarSettings Settings { get; set; }
    
    /// <summary>
    /// Gets the user's selected date
    /// </summary>
    DateTime SelectedDate { get; set; }
    
    /// <summary>
    /// Gets the user's selected view mode
    /// </summary>
    CalendarViewMode ViewMode { get; set; }
    
    /// <summary>
    /// Adds a new event to the calendar
    /// </summary>
    /// <param name="calendarEvent">The event to add</param>
    /// <returns>The added event with assigned ID</returns>
    Task<CalendarEvent> AddEventAsync(CalendarEvent calendarEvent);
    
    /// <summary>
    /// Updates an existing event
    /// </summary>
    /// <param name="calendarEvent">The updated event</param>
    /// <returns>True if the event was updated successfully</returns>
    Task<bool> UpdateEventAsync(CalendarEvent calendarEvent);
    
    /// <summary>
    /// Deletes an event
    /// </summary>
    /// <param name="eventId">The ID of the event to delete</param>
    /// <returns>True if the event was deleted successfully</returns>
    Task<bool> DeleteEventAsync(Guid eventId);
    
    /// <summary>
    /// Gets an event by its ID
    /// </summary>
    /// <param name="eventId">The ID of the event to retrieve</param>
    /// <returns>The event, or null if not found</returns>
    Task<CalendarEvent?> GetEventAsync(Guid eventId);
    
    /// <summary>
    /// Gets all events in the calendar
    /// </summary>
    /// <returns>A list of all events</returns>
    Task<List<CalendarEvent>> GetAllEventsAsync();
    
    /// <summary>
    /// Gets all events within a specified date range
    /// </summary>
    /// <param name="start">The start date of the range</param>
    /// <param name="end">The end date of the range</param>
    /// <returns>A list of event occurrences within the range</returns>
    Task<List<EventOccurrence>> GetEventsInRangeAsync(DateTime start, DateTime end);
    
    /// <summary>
    /// Gets all occurrences of a specific event
    /// </summary>
    /// <param name="eventId">The ID of the event</param>
    /// <param name="start">The start date of the range</param>
    /// <param name="end">The end date of the range</param>
    /// <returns>A list of occurrences of the event within the range</returns>
    Task<List<EventOccurrence>> GetEventOccurrencesAsync(Guid eventId, DateTime start, DateTime end);
    
    /// <summary>
    /// Gets upcoming reminders
    /// </summary>
    /// <param name="fromTime">The time to check from</param>
    /// <param name="lookaheadMinutes">How many minutes to look ahead</param>
    /// <returns>A list of event occurrences with active reminders</returns>
    Task<List<UpcomingReminder>> GetUpcomingRemindersAsync(DateTime fromTime, int lookaheadMinutes);
    
    /// <summary>
    /// Searches for events matching the search term
    /// </summary>
    /// <param name="searchTerm">The term to search for</param>
    /// <returns>A list of matching events</returns>
    Task<List<CalendarEvent>> SearchEventsAsync(string searchTerm);
    
    /// <summary>
    /// Imports events from an iCalendar file
    /// </summary>
    /// <param name="iCalData">The iCalendar data as a string</param>
    /// <returns>The number of events imported</returns>
    Task<int> ImportEventsAsync(string iCalData);
    
    /// <summary>
    /// Exports events to iCalendar format
    /// </summary>
    /// <param name="eventIds">Optional list of event IDs to export; if empty, exports all events</param>
    /// <returns>The events as iCalendar data</returns>
    Task<string> ExportEventsAsync(List<Guid>? eventIds = null);
    
    /// <summary>
    /// Saves the current calendar settings
    /// </summary>
    /// <returns>True if settings were saved successfully</returns>
    Task<bool> SaveSettingsAsync();
    
    /// <summary>
    /// Loads the calendar settings
    /// </summary>
    /// <returns>True if settings were loaded successfully</returns>
    Task<bool> LoadSettingsAsync();
}

/// <summary>
/// Service for managing calendar data and operations
/// </summary>
public class CalendarEngineService : ICalendarEngineService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CalendarEngineService> _logger;
    private readonly string _eventsDirectory = "/user/AppData/Calendar/Events";
    private readonly string _settingsPath = "/user/AppData/Calendar/settings.json";
    private List<CalendarEvent> _events = new();
    private bool _eventsLoaded = false;
    
    /// <summary>
    /// Gets or sets the calendar settings
    /// </summary>
    public CalendarSettings Settings { get; set; } = new CalendarSettings();
    
    /// <summary>
    /// Gets or sets the user's selected date
    /// </summary>
    public DateTime SelectedDate { get; set; } = DateTime.Today;
    
    /// <summary>
    /// Gets or sets the user's selected view mode
    /// </summary>
    public CalendarViewMode ViewMode { get; set; } = CalendarViewMode.Month;
    
    /// <summary>
    /// Initializes a new instance of the CalendarEngineService class
    /// </summary>
    /// <param name="fileSystem">The file system service</param>
    /// <param name="logger">The logger</param>
    public CalendarEngineService(IFileSystem fileSystem, ILogger<CalendarEngineService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        
        // Create the events directory if it doesn't exist
        EnsureDirectoriesExist();
    }
    
    /// <summary>
    /// Adds a new event to the calendar
    /// </summary>
    /// <param name="calendarEvent">The event to add</param>
    /// <returns>The added event with assigned ID</returns>
    public async Task<CalendarEvent> AddEventAsync(CalendarEvent calendarEvent)
    {
        await EnsureEventsLoadedAsync();
        
        // Make sure the event has a unique ID
        if (calendarEvent.Id == Guid.Empty)
        {
            calendarEvent.Id = Guid.NewGuid();
        }
        
        // Set created and modified timestamps
        calendarEvent.Created = DateTime.Now;
        calendarEvent.LastModified = DateTime.Now;
        
        _events.Add(calendarEvent);
        
        await SaveEventAsync(calendarEvent);
        
        return calendarEvent;
    }
    
    /// <summary>
    /// Updates an existing event
    /// </summary>
    /// <param name="calendarEvent">The updated event</param>
    /// <returns>True if the event was updated successfully</returns>
    public async Task<bool> UpdateEventAsync(CalendarEvent calendarEvent)
    {
        await EnsureEventsLoadedAsync();
        
        var existingEvent = _events.FirstOrDefault(e => e.Id == calendarEvent.Id);
        if (existingEvent == null)
        {
            return false;
        }
        
        // Update modified timestamp
        calendarEvent.LastModified = DateTime.Now;
        
        // Replace the event in the list
        _events.Remove(existingEvent);
        _events.Add(calendarEvent);
        
        await SaveEventAsync(calendarEvent);
        
        return true;
    }
    
    /// <summary>
    /// Deletes an event
    /// </summary>
    /// <param name="eventId">The ID of the event to delete</param>
    /// <returns>True if the event was deleted successfully</returns>
    public async Task<bool> DeleteEventAsync(Guid eventId)
    {
        await EnsureEventsLoadedAsync();
        
        var existingEvent = _events.FirstOrDefault(e => e.Id == eventId);
        if (existingEvent == null)
        {
            return false;
        }
        
        _events.Remove(existingEvent);
        
        string filePath = Path.Combine(_eventsDirectory, $"{eventId}.json");
        
        try
        {
            if (await _fileSystem.ExistsAsync(filePath))
            {
                await _fileSystem.DeleteAsync(filePath);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting event file: {FilePath}", filePath);
            return false;
        }
    }
    
    /// <summary>
    /// Gets an event by its ID
    /// </summary>
    /// <param name="eventId">The ID of the event to retrieve</param>
    /// <returns>The event, or null if not found</returns>
    public async Task<CalendarEvent?> GetEventAsync(Guid eventId)
    {
        await EnsureEventsLoadedAsync();
        
        return _events.FirstOrDefault(e => e.Id == eventId);
    }
    
    /// <summary>
    /// Gets all events in the calendar
    /// </summary>
    /// <returns>A list of all events</returns>
    public async Task<List<CalendarEvent>> GetAllEventsAsync()
    {
        await EnsureEventsLoadedAsync();
        
        return _events.ToList();
    }
    
    /// <summary>
    /// Gets all events within a specified date range
    /// </summary>
    /// <param name="start">The start date of the range</param>
    /// <param name="end">The end date of the range</param>
    /// <returns>A list of event occurrences within the range</returns>
    public async Task<List<EventOccurrence>> GetEventsInRangeAsync(DateTime start, DateTime end)
    {
        await EnsureEventsLoadedAsync();
        
        var occurrences = new List<EventOccurrence>();
        
        foreach (var calendarEvent in _events)
        {
            var eventOccurrences = calendarEvent.GetOccurrencesInRange(start, end);
            occurrences.AddRange(eventOccurrences);
        }
        
        return occurrences.OrderBy(o => o.StartTime).ToList();
    }
    
    /// <summary>
    /// Gets all occurrences of a specific event
    /// </summary>
    /// <param name="eventId">The ID of the event</param>
    /// <param name="start">The start date of the range</param>
    /// <param name="end">The end date of the range</param>
    /// <returns>A list of occurrences of the event within the range</returns>
    public async Task<List<EventOccurrence>> GetEventOccurrencesAsync(Guid eventId, DateTime start, DateTime end)
    {
        await EnsureEventsLoadedAsync();
        
        var calendarEvent = _events.FirstOrDefault(e => e.Id == eventId);
        if (calendarEvent == null)
        {
            return new List<EventOccurrence>();
        }
        
        return calendarEvent.GetOccurrencesInRange(start, end);
    }
    
    /// <summary>
    /// Gets upcoming reminders
    /// </summary>
    /// <param name="fromTime">The time to check from</param>
    /// <param name="lookaheadMinutes">How many minutes to look ahead</param>
    /// <returns>A list of event occurrences with active reminders</returns>
    public async Task<List<UpcomingReminder>> GetUpcomingRemindersAsync(DateTime fromTime, int lookaheadMinutes)
    {
        await EnsureEventsLoadedAsync();
        
        var reminders = new List<UpcomingReminder>();
        var toTime = fromTime.AddMinutes(lookaheadMinutes);
        
        // Get all events that might have reminders in the specified time window
        // We need to look at events that start within lookaheadMinutes + max reminder time
        var maxReminderMinutes = 24 * 60; // Default to 24 hours
        
        foreach (var calendarEvent in _events)
        {
            if (calendarEvent.Reminders.Any())
            {
                var maxEventReminderMinutes = calendarEvent.Reminders.Max(r => r.MinutesBefore);
                var eventLookahead = lookaheadMinutes + maxEventReminderMinutes;
                
                // Get occurrences in the extended range
                var occurrences = calendarEvent.GetOccurrencesInRange(
                    fromTime.AddMinutes(-maxEventReminderMinutes),
                    toTime);
                
                foreach (var occurrence in occurrences)
                {
                    foreach (var reminder in calendarEvent.Reminders)
                    {
                        var reminderTime = occurrence.StartTime.AddMinutes(-reminder.MinutesBefore);
                        
                        // Check if the reminder time falls within our window
                        if (reminderTime >= fromTime && reminderTime <= toTime)
                        {
                            reminders.Add(new UpcomingReminder(occurrence, reminder));
                        }
                    }
                }
            }
        }
        
        return reminders.OrderBy(r => r.Occurrence.StartTime).ToList();
    }
    
    /// <summary>
    /// Searches for events matching the search term
    /// </summary>
    /// <param name="searchTerm">The term to search for</param>
    /// <returns>A list of matching events</returns>
    public async Task<List<CalendarEvent>> SearchEventsAsync(string searchTerm)
    {
        await EnsureEventsLoadedAsync();
        
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<CalendarEvent>();
        }
        
        searchTerm = searchTerm.ToLower();
        
        return _events.Where(e =>
            e.Title.ToLower().Contains(searchTerm) ||
            e.Description.ToLower().Contains(searchTerm) ||
            e.Location.ToLower().Contains(searchTerm) ||
            e.Category.ToLower().Contains(searchTerm)
        ).ToList();
    }
    
    /// <summary>
    /// Imports events from an iCalendar file
    /// </summary>
    /// <param name="iCalData">The iCalendar data as a string</param>
    /// <returns>The number of events imported</returns>
    public async Task<int> ImportEventsAsync(string iCalData)
    {
        // Basic implementation for now - can be enhanced with full iCal parsing
        // For a real implementation, we would use a library like Ical.Net
        
        // For demonstration purposes, we'll just create a simple event
        if (string.IsNullOrWhiteSpace(iCalData))
        {
            return 0;
        }
        
        var newEvent = new CalendarEvent
        {
            Title = "Imported Event",
            Description = "This event was imported from an iCal file.",
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            Created = DateTime.Now,
            LastModified = DateTime.Now
        };
        
        await AddEventAsync(newEvent);
        
        return 1;
    }
    
    /// <summary>
    /// Exports events to iCalendar format
    /// </summary>
    /// <param name="eventIds">Optional list of event IDs to export; if empty, exports all events</param>
    /// <returns>The events as iCalendar data</returns>
    public async Task<string> ExportEventsAsync(List<Guid>? eventIds = null)
    {
        await EnsureEventsLoadedAsync();
        
        // Basic implementation for now - can be enhanced with full iCal generation
        // For a real implementation, we would use a library like Ical.Net
        
        var eventsToExport = eventIds != null && eventIds.Any()
            ? _events.Where(e => eventIds.Contains(e.Id)).ToList()
            : _events;
            
        // Simple iCal format for demonstration
        var iCal = new System.Text.StringBuilder();
        iCal.AppendLine("BEGIN:VCALENDAR");
        iCal.AppendLine("VERSION:2.0");
        iCal.AppendLine("PRODID:-//HackerOS//Calendar//EN");
        
        foreach (var calendarEvent in eventsToExport)
        {
            iCal.AppendLine("BEGIN:VEVENT");
            iCal.AppendLine($"UID:{calendarEvent.Id}");
            iCal.AppendLine($"SUMMARY:{calendarEvent.Title}");
            iCal.AppendLine($"DESCRIPTION:{calendarEvent.Description}");
            iCal.AppendLine($"LOCATION:{calendarEvent.Location}");
            iCal.AppendLine($"DTSTART:{calendarEvent.StartTime:yyyyMMddTHHmmssZ}");
            iCal.AppendLine($"DTEND:{calendarEvent.EndTime:yyyyMMddTHHmmssZ}");
            iCal.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            iCal.AppendLine($"CREATED:{calendarEvent.Created:yyyyMMddTHHmmssZ}");
            iCal.AppendLine($"LAST-MODIFIED:{calendarEvent.LastModified:yyyyMMddTHHmmssZ}");
            
            if (calendarEvent.Recurrence != null)
            {
                // Basic recurrence rule
                iCal.AppendLine($"RRULE:FREQ={calendarEvent.Recurrence.Type};INTERVAL={calendarEvent.Recurrence.Interval}");
            }
            
            iCal.AppendLine("END:VEVENT");
        }
        
        iCal.AppendLine("END:VCALENDAR");
        
        return iCal.ToString();
    }
    
    /// <summary>
    /// Saves the current calendar settings
    /// </summary>
    /// <returns>True if settings were saved successfully</returns>
    public async Task<bool> SaveSettingsAsync()
    {
        try
        {
            var json = Settings.ToJson();
            await _fileSystem.EnsureDirectoryExistsAsync(Path.GetDirectoryName(_settingsPath) ?? "/user/AppData/Calendar");
            await _fileSystem.WriteTextAsync(_settingsPath, json);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving calendar settings");
            return false;
        }
    }
    
    /// <summary>
    /// Loads the calendar settings
    /// </summary>
    /// <returns>True if settings were loaded successfully</returns>
    public async Task<bool> LoadSettingsAsync()
    {
        try
        {
            if (await _fileSystem.FileExistsAsync(_settingsPath))
            {
                var json = await _fileSystem.ReadTextAsync(_settingsPath);
                Settings = CalendarSettings.FromJson(json);
                ViewMode = Settings.DefaultViewMode;
                return true;
            }
            
            // If no settings file exists, create default settings
            Settings = new CalendarSettings();
            await SaveSettingsAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading calendar settings");
            Settings = new CalendarSettings();
            return false;
        }
    }
    
    #region Private Helper Methods
    
    private void EnsureDirectoriesExist()
    {
        _fileSystem.EnsureDirectoryExistsAsync(_eventsDirectory).GetAwaiter().GetResult();
    }
    
    private async Task EnsureEventsLoadedAsync()
    {
        if (_eventsLoaded)
        {
            return;
        }
        
        await LoadEventsAsync();
        _eventsLoaded = true;
    }
    
    private async Task LoadEventsAsync()
    {
        _events.Clear();
        
        try
        {
            await _fileSystem.EnsureDirectoryExistsAsync(_eventsDirectory);
            
            var files = await _fileSystem.GetFilesAsync(_eventsDirectory, "*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await _fileSystem.ReadTextAsync(file);
                    var calendarEvent = JsonSerializer.Deserialize<CalendarEvent>(json);
                    
                    if (calendarEvent != null)
                    {
                        _events.Add(calendarEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading event file: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading calendar events");
        }
    }
    
    private async Task SaveEventAsync(CalendarEvent calendarEvent)
    {
        try
        {
            await _fileSystem.EnsureDirectoryExistsAsync(_eventsDirectory);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            string json = JsonSerializer.Serialize(calendarEvent, options);
            string filePath = Path.Combine(_eventsDirectory, $"{calendarEvent.Id}.json");
            
            await _fileSystem.WriteTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving event: {EventId}", calendarEvent.Id);
        }
    }
    
    #endregion
}
