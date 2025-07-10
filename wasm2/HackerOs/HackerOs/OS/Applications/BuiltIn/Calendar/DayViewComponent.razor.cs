using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Component for displaying a day view of the calendar
/// </summary>
public partial class DayViewComponent : ComponentBase, IDisposable
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
    /// Gets the time slots for the day view
    /// </summary>
    protected List<DateTime> TimeSlots { get; private set; } = new();
    
    /// <summary>
    /// Gets whether the selected date is today
    /// </summary>
    protected bool IsToday => SelectedDate.Date == DateTime.Today;
    
    /// <summary>
    /// Gets the all-day events for the selected date
    /// </summary>
    protected List<EventOccurrence> AllDayEvents => Events
        .Where(e => e.StartTime.Date <= SelectedDate.Date && e.EndTime.Date >= SelectedDate.Date && e.OriginalEvent.IsAllDay)
        .OrderBy(e => e.StartTime)
        .ToList();
    
    /// <summary>
    /// Gets the time-based events for the selected date
    /// </summary>
    protected List<EventOccurrence> TimeEvents => Events
        .Where(e => e.StartTime.Date <= SelectedDate.Date && e.EndTime.Date >= SelectedDate.Date && !e.OriginalEvent.IsAllDay)
        .OrderBy(e => e.StartTime)
        .ToList();
    
    // Private fields for the "more events" dialog
    private bool _showMoreEventsDialog;
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
        // Ensure time slots are using the selected date
        GenerateTimeSlots();
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
            await DragDropService.InitializeDropZonesAsync("Day");
            
            // Make events draggable and resizable
            foreach (var evt in TimeEvents)
            {
                var eventId = $"day-event-{evt.OriginalEvent.Id}";
                await DragDropService.MakeElementDraggableAsync(eventId, evt.OriginalEvent.Id.ToString(), "Day");
                await DragDropService.MakeElementResizableAsync(eventId, evt.OriginalEvent.Id.ToString(), "Day");
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
    /// Navigate to the previous day
    /// </summary>
    protected void NavigatePrevDay()
    {
        OnDateSelected.InvokeAsync(SelectedDate.AddDays(-1));
    }
    
    /// <summary>
    /// Navigate to today
    /// </summary>
    protected void NavigateToday()
    {
        OnDateSelected.InvokeAsync(DateTime.Today);
    }
    
    /// <summary>
    /// Navigate to the next day
    /// </summary>
    protected void NavigateNextDay()
    {
        OnDateSelected.InvokeAsync(SelectedDate.AddDays(1));
    }
    
    /// <summary>
    /// Shows the "more events" dialog
    /// </summary>
    protected void ShowMoreEvents()
    {
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
    /// Creates a new event at the specified time
    /// </summary>
    protected void CreateEventAtTime(DateTime time)
    {
        // Combine the selected date and time
        var eventStart = new DateTime(
            SelectedDate.Year, SelectedDate.Month, SelectedDate.Day, 
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
        
        // If the event starts before the day view range, cap it at the top
        if (hoursPassed < 0)
            hoursPassed = 0;
        
        // Convert to pixels
        return (int)(hoursPassed * HourHeight);
    }
    
    /// <summary>
    /// Gets the height for an event based on its duration
    /// </summary>
    protected int GetEventHeight(EventOccurrence evt)
    {
        // If the event starts before the current day, adjust the duration
        DateTime effectiveStart = evt.StartTime;
        if (effectiveStart.Date < SelectedDate.Date)
            effectiveStart = SelectedDate.Date;
            
        // If the event ends after the current day, adjust the duration
        DateTime effectiveEnd = evt.EndTime;
        if (effectiveEnd.Date > SelectedDate.Date)
            effectiveEnd = SelectedDate.Date.AddDays(1).AddSeconds(-1);
        
        // Calculate the duration in hours
        var durationHours = (effectiveEnd - effectiveStart).TotalHours;
        
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
