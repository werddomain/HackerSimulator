using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Main component for the Calendar application
/// </summary>
public partial class CalendarComponent : ComponentBase, IDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject] private CalendarDragDropService DragDropService { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the calendar engine service
    /// </summary>
    [Parameter] public ICalendarEngineService CalendarEngine { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the current view mode
    /// </summary>
    [Parameter] public CalendarViewMode CurrentView { get; set; } = CalendarViewMode.Month;
    
    /// <summary>
    /// Gets or sets the selected date
    /// </summary>
    [Parameter] public DateTime SelectedDate { get; set; } = DateTime.Today;
    
    /// <summary>
    /// Event callback for when the view mode changes
    /// </summary>
    [Parameter] public EventCallback<CalendarViewMode> ViewChanged { get; set; }
    
    /// <summary>
    /// Event callback for when a date is selected
    /// </summary>
    [Parameter] public EventCallback<DateTime> DateSelected { get; set; }
    
    /// <summary>
    /// Event callback for when an event is created
    /// </summary>
    [Parameter] public EventCallback<CalendarEvent> EventCreated { get; set; }
    
    /// <summary>
    /// Event callback for when an event is updated
    /// </summary>
    [Parameter] public EventCallback<CalendarEvent> EventUpdated { get; set; }
    
    // Private fields
    private List<EventOccurrence> _eventOccurrences = new();
    private EventOccurrence? _selectedEventOccurrence;
    private bool _showEventEditDialog;
    private bool _isInitialized;
    
    /// <summary>
    /// Called when the component is initialized
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // Subscribe to drag and drop events
        DragDropService.EventDragged += HandleEventDragged;
        DragDropService.EventResized += HandleEventResized;
        
        await LoadEventsAsync();
        _isInitialized = true;
    }
    
    /// <summary>
    /// Called when parameters have changed
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (_isInitialized)
        {
            await LoadEventsAsync();
        }
    }
    
    /// <summary>
    /// Load events for the current view
    /// </summary>
    private async Task LoadEventsAsync()
    {
        // Determine the date range based on the current view and selected date
        DateTime startDate, endDate;
        
        switch (CurrentView)
        {
            case CalendarViewMode.Month:
                var firstDayOfMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                
                // Extend to include the surrounding weeks for proper month view display
                startDate = firstDayOfMonth.AddDays(-(int)firstDayOfMonth.DayOfWeek);
                endDate = lastDayOfMonth.AddDays(6 - (int)lastDayOfMonth.DayOfWeek);
                break;
                
            case CalendarViewMode.Week:
                var firstDayOfWeek = SelectedDate.AddDays(-(int)SelectedDate.DayOfWeek);
                startDate = firstDayOfWeek;
                endDate = firstDayOfWeek.AddDays(6);
                break;
                
            case CalendarViewMode.Day:
                startDate = SelectedDate.Date;
                endDate = startDate;
                break;
                
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        // Get events from the calendar engine
        _eventOccurrences = await CalendarEngine.GetEventsInRangeAsync(startDate, endDate);
    }
    
    /// <summary>
    /// Handles the selected date changing
    /// </summary>
    private async Task HandleDateSelected(DateTime date)
    {
        SelectedDate = date;
        await DateSelected.InvokeAsync(date);
        await LoadEventsAsync();
    }
    
    /// <summary>
    /// Handles the view mode changing
    /// </summary>
    private async Task HandleViewChanged(CalendarViewMode viewMode)
    {
        CurrentView = viewMode;
        await ViewChanged.InvokeAsync(viewMode);
        await LoadEventsAsync();
    }
    
    /// <summary>
    /// Handles an event being selected
    /// </summary>
    private void HandleEventSelected(EventOccurrence eventOccurrence)
    {
        _selectedEventOccurrence = eventOccurrence;
        _showEventEditDialog = true;
    }
    
    /// <summary>
    /// Handles creating a new event
    /// </summary>
    private void HandleCreateEvent()
    {
        // Create a new event at the selected date
        var newEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "New Event",
            StartTime = SelectedDate.Date.AddHours(9), // Default to 9 AM
            EndTime = SelectedDate.Date.AddHours(10),  // Default to 10 AM
            IsAllDay = false,
            Color = "#3498db" // Default blue color
        };
        
        var occurrence = new EventOccurrence
        {
            OriginalEvent = newEvent,
            StartTime = newEvent.StartTime,
            EndTime = newEvent.EndTime
        };
        
        _selectedEventOccurrence = occurrence;
        _showEventEditDialog = true;
    }
    
    /// <summary>
    /// Handles event edit dialog saving
    /// </summary>
    private async Task HandleEventEditSave(CalendarEvent calendarEvent)
    {
        // Check if this is a new event or an update
        var existingEvent = await CalendarEngine.GetEventAsync(calendarEvent.Id);
        
        if (existingEvent == null)
        {
            // This is a new event
            await CalendarEngine.AddEventAsync(calendarEvent);
            await EventCreated.InvokeAsync(calendarEvent);
        }
        else
        {
            // This is an update to an existing event
            await CalendarEngine.UpdateEventAsync(calendarEvent);
            await EventUpdated.InvokeAsync(calendarEvent);
        }
        
        // Close the dialog and reload events
        _showEventEditDialog = false;
        await LoadEventsAsync();
    }
    
    /// <summary>
    /// Handles event edit dialog cancel
    /// </summary>
    private void HandleEventEditCancel()
    {
        _showEventEditDialog = false;
    }
    
    /// <summary>
    /// Handles event edit dialog delete
    /// </summary>
    private async Task HandleEventDelete(Guid eventId)
    {
        await CalendarEngine.DeleteEventAsync(eventId);
        _showEventEditDialog = false;
        await LoadEventsAsync();
    }
    
    /// <summary>
    /// Handles an event being dragged to a new date/time
    /// </summary>
    private void HandleEventDragged(object? sender, EventDragInfo e)
    {
        HandleEventDraggedAsync(sender, e).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Handles an event being dragged to a new date/time (async implementation)
    /// </summary>
    private async Task HandleEventDraggedAsync(object? sender, EventDragInfo e)
    {
        // Get the original event
        var originalEvent = await CalendarEngine.GetEventAsync(e.EventId);
        if (originalEvent == null) return;
        
        // Calculate the time difference between the original and new start time
        var timeDifference = e.NewDate.Date.Add(e.NewTime) - originalEvent.StartTime;
        
        // Create a copy of the event with updated times
        var updatedEvent = new CalendarEvent
        {
            Id = originalEvent.Id,
            Title = originalEvent.Title,
            Description = originalEvent.Description,
            Location = originalEvent.Location,
            StartTime = originalEvent.StartTime.Add(timeDifference),
            EndTime = originalEvent.EndTime.Add(timeDifference),
            IsAllDay = originalEvent.IsAllDay,
            Color = originalEvent.Color,
            Recurrence = originalEvent.Recurrence,
            Reminders = originalEvent.Reminders
        };
        
        // If the event is recurring, show a dialog to ask if this is a one-time change or affects all occurrences
        if (originalEvent.Recurrence != null)
        {
            // For now, just update this occurrence and future ones
            // In a real implementation, this would show a dialog with options
            updatedEvent.Recurrence = originalEvent.Recurrence;
        }
        
        // Update the event
        await CalendarEngine.UpdateEventAsync(updatedEvent);
        await EventUpdated.InvokeAsync(updatedEvent);
        
        // Reload events to reflect the changes
        await LoadEventsAsync();
    }
    
    /// <summary>
    /// Handles an event being resized
    /// </summary>
    private void HandleEventResized(object? sender, EventResizeInfo e)
    {
        HandleEventResizedAsync(sender, e).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Handles an event being resized (async implementation)
    /// </summary>
    private async Task HandleEventResizedAsync(object? sender, EventResizeInfo e)
    {
        // Get the original event
        var originalEvent = await CalendarEngine.GetEventAsync(e.EventId);
        if (originalEvent == null) return;
        
        // Create a copy of the event with updated end time
        var updatedEvent = new CalendarEvent
        {
            Id = originalEvent.Id,
            Title = originalEvent.Title,
            Description = originalEvent.Description,
            Location = originalEvent.Location,
            StartTime = originalEvent.StartTime,
            EndTime = originalEvent.StartTime.Add(e.Duration),
            IsAllDay = originalEvent.IsAllDay,
            Color = originalEvent.Color,
            Recurrence = originalEvent.Recurrence,
            Reminders = originalEvent.Reminders
        };
        
        // If the event is recurring, show a dialog to ask if this is a one-time change or affects all occurrences
        if (originalEvent.Recurrence != null)
        {
            // For now, just update this occurrence and future ones
            // In a real implementation, this would show a dialog with options
            updatedEvent.Recurrence = originalEvent.Recurrence;
        }
        
        // Update the event
        await CalendarEngine.UpdateEventAsync(updatedEvent);
        await EventUpdated.InvokeAsync(updatedEvent);
        
        // Reload events to reflect the changes
        await LoadEventsAsync();
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
