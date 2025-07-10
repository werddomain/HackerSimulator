using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Component for displaying a week view of the calendar
/// </summary>
public partial class WeekViewComponent : ComponentBase, IDisposable
{
    [Inject] private CalendarDragDropService DragDropService { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the selected date
    /// </summary>
    [Parameter] public DateTime SelectedDate { get; set; } = DateTime.Today;
    
    /// <summary>
    /// Gets or sets the list of events to display
    /// </summary>
    [Parameter] public List<EventOccurrence> Events { get; set; } = new();
    
    /// <summary>
    /// Event callback for when a date is selected
    /// </summary>
    [Parameter] public EventCallback<DateTime> OnDateSelected { get; set; }
    
    /// <summary>
    /// Event callback for when an event is selected
    /// </summary>
    [Parameter] public EventCallback<EventOccurrence> OnEventSelected { get; set; }
    
    /// <summary>
    /// Event callback for when an event is dragged to a new date/time
    /// </summary>
    [Parameter] public EventCallback<(Guid EventId, DateTime NewDate, TimeSpan NewTime)> OnEventDragged { get; set; }
    
    /// <summary>
    /// Event callback for when an event is resized
    /// </summary>
    [Parameter] public EventCallback<(Guid EventId, TimeSpan Duration)> OnEventResized { get; set; }
    
    /// <summary>
    /// Gets the days in the current week
    /// </summary>
    protected List<DayInfo> DaysInWeek { get; private set; } = new();
    
    /// <summary>
    /// Gets the time slots for the week view
    /// </summary>
    protected List<DateTime> TimeSlots { get; private set; } = new();
    
    // Private fields for the "more events" dialog
    private bool _showMoreEventsDialog;
    private DateTime _currentMoreEventsDate;
    private List<EventOccurrence> _eventsInMoreDialog = new();
    private bool _isDragDropInitialized;
    
    // Time slot configuration
    private const int HourHeight = 60; // Height in pixels for each hour
    private const int DayStartHour = 0; // 12 AM
    private const int DayEndHour = 24; // 12 AM next day
    
    /// <summary>
    /// Called when the component is initialized
    /// </summary>
    protected override void OnInitialized()
    {
        GenerateDaysInWeek();
        GenerateTimeSlots();
        
        // Subscribe to drag drop events
        DragDropService.EventDragged += HandleEventDragged;
        DragDropService.EventResized += HandleEventResized;
    }
    
    /// <summary>
    /// Called when parameters have changed
    /// </summary>
    protected override void OnParametersSet()
    {
        GenerateDaysInWeek();
    }
    
    /// <summary>
    /// Called after the component has been rendered
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await DragDropService.InitializeDragDropAsync();
            _isDragDropInitialized = true;
        }
        
        if (_isDragDropInitialized)
        {
            // Initialize drop zones
            await DragDropService.InitializeDropZonesAsync("Week");
            
            // Make events draggable and resizable
            foreach (var dayInfo in DaysInWeek)
            {
                foreach (var evt in TimeEventsOnDate(dayInfo.Date))
                {
                    var eventId = $"week-event-{evt.OriginalEvent.Id}-{dayInfo.Date.ToString("yyyyMMdd")}";
                    await DragDropService.MakeElementDraggableAsync(eventId, evt.OriginalEvent.Id.ToString(), "Week");
                    await DragDropService.MakeElementResizableAsync(eventId, evt.OriginalEvent.Id.ToString(), "Week");
                }
            }
        }
    }
    
    /// <summary>
    /// Handles when an event is dragged to a new date/time
    /// </summary>
    private void HandleEventDragged(object? sender, EventDragInfo e)
    {
        // Invoke callback with the event ID, new date, and new time
        OnEventDragged.InvokeAsync((e.EventId, e.NewDate, e.NewTime));
    }
    
    /// <summary>
    /// Handles when an event is resized
    /// </summary>
    private void HandleEventResized(object? sender, EventResizeInfo e)
    {
        // Invoke callback with the event ID and new duration
        OnEventResized.InvokeAsync((e.EventId, e.Duration));
    }
    
    /// <summary>
    /// Gets all-day events for a specific date
    /// </summary>
    protected List<EventOccurrence> AllDayEventsOnDate(DateTime date)
    {
        return Events
            .Where(e => e.StartTime.Date <= date.Date && e.EndTime.Date >= date.Date && e.OriginalEvent.IsAllDay)
            .OrderBy(e => e.StartTime)
            .ToList();
    }
    
    /// <summary>
    /// Gets time-based events for a specific date
    /// </summary>
    protected List<EventOccurrence> TimeEventsOnDate(DateTime date)
    {
        return Events
            .Where(e => e.StartTime.Date <= date.Date && e.EndTime.Date >= date.Date && !e.OriginalEvent.IsAllDay)
            .OrderBy(e => e.StartTime)
            .ToList();
    }
    
    /// <summary>
    /// Shows the "more events" dialog for a date
    /// </summary>
    protected void ShowMoreEvents(DateTime date)
    {
        _currentMoreEventsDate = date;
        _eventsInMoreDialog = AllDayEventsOnDate(date);
        _showMoreEventsDialog = true;
    }
    
    /// <summary>
    /// Handles when an event is clicked in the "more events" dialog
    /// </summary>
    protected void HandleMoreEventClick(EventOccurrence evt)
    {
        OnEventSelected.InvokeAsync(evt);
        CloseMoreEventsDialog();
    }
    
    /// <summary>
    /// Closes the "more events" dialog
    /// </summary>
    protected void CloseMoreEventsDialog()
    {
        _showMoreEventsDialog = false;
        StateHasChanged();
    }
    
    /// <summary>
    /// Creates a new event at the specified date and time
    /// </summary>
    protected void CreateEventAtTime(DateTime date, DateTime time)
    {
        // Combine the date and time
        var eventStart = new DateTime(
            date.Year, date.Month, date.Day, 
            time.Hour, time.Minute, 0
        );
        
        // Create an event that lasts for 1 hour
        var eventEnd = eventStart.AddHours(1);
        
        // Create a new event occurrence representing a new event to be created
        var newEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "New Event",
            StartTime = eventStart,
            EndTime = eventEnd,
            IsAllDay = false,
            Color = "#3498db" // Default blue color
        };
        
        var occurrence = new EventOccurrence
        {
            OriginalEvent = newEvent,
            StartTime = eventStart,
            EndTime = eventEnd
        };
        
        // Select the event to open the edit dialog
        OnEventSelected.InvokeAsync(occurrence);
    }
    
    /// <summary>
    /// Gets the top position for an event based on its start time
    /// </summary>
    protected int GetEventTop(EventOccurrence evt)
    {
        // Calculate the number of hours from the start of the day
        var eventDate = evt.StartTime.Date;
        var hoursPassed = (evt.StartTime - eventDate).TotalHours - DayStartHour;
        
        // Convert to pixels
        return (int)(hoursPassed * HourHeight);
    }
    
    /// <summary>
    /// Gets the height for an event based on its duration
    /// </summary>
    protected int GetEventHeight(EventOccurrence evt)
    {
        // Calculate the duration in hours
        var durationHours = (evt.EndTime - evt.StartTime).TotalHours;
        
        // Ensure minimum height for very short events
        var height = (int)(durationHours * HourHeight);
        return Math.Max(height, 25); // Minimum height of 25px
    }
    
    /// <summary>
    /// Gets the current time position for the time indicator
    /// </summary>
    protected int GetCurrentTimePosition()
    {
        var now = DateTime.Now;
        var today = now.Date;
        var hoursPassed = (now - today).TotalHours - DayStartHour;
        
        return (int)(hoursPassed * HourHeight);
    }
    
    private void GenerateDaysInWeek()
    {
        DaysInWeek.Clear();
        
        // Get the first day of the week containing the selected date
        var culture = CultureInfo.CurrentCulture;
        var firstDayOfWeek = DayOfWeek.Sunday; // Use Sunday as the first day of the week
        
        var diff = SelectedDate.DayOfWeek - firstDayOfWeek;
        if (diff < 0)
            diff += 7;
            
        var weekStart = SelectedDate.AddDays(-diff);
        
        // Generate 7 days starting from the week start
        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var dayName = culture.DateTimeFormat.GetDayName(date.DayOfWeek);
            
            DaysInWeek.Add(new DayInfo
            {
                Date = date,
                DayName = dayName
            });
        }
    }
    
    private void GenerateTimeSlots()
    {
        TimeSlots.Clear();
        
        // Use the selected date as the base for time slots
        var baseDate = SelectedDate.Date;
        
        // Generate time slots from DayStartHour to DayEndHour
        for (int hour = DayStartHour; hour < DayEndHour; hour++)
        {
            TimeSlots.Add(baseDate.AddHours(hour));
        }
    }
    
    /// <summary>
    /// Clean up event handlers when the component is disposed
    /// </summary>
    public void Dispose()
    {
        DragDropService.EventDragged -= HandleEventDragged;
        DragDropService.EventResized -= HandleEventResized;
    }
}

/// <summary>
/// Class to hold information about a day in the week view
/// </summary>
public class DayInfo
{
    /// <summary>
    /// Gets or sets the date
    /// </summary>
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Gets or sets the day name
    /// </summary>
    public string DayName { get; set; } = string.Empty;
}
