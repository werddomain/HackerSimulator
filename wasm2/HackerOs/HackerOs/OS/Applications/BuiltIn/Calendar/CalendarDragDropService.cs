using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Service for handling drag and drop operations in the calendar
/// </summary>
public class CalendarDragDropService
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<CalendarDragDropService> _objRef;
    
    /// <summary>
    /// Event triggered when an event is dragged to a new date/time
    /// </summary>
    public event EventHandler<EventDragInfo> EventDragged;
    
    /// <summary>
    /// Event triggered when an event is resized
    /// </summary>
    public event EventHandler<EventResizeInfo> EventResized;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarDragDropService"/> class
    /// </summary>
    /// <param name="jsRuntime">The JS runtime</param>
    public CalendarDragDropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _objRef = DotNetObjectReference.Create(this);
    }
    
    /// <summary>
    /// Initializes the drag and drop functionality
    /// </summary>
    public async Task InitializeDragDropAsync()
    {
        await _jsRuntime.InvokeVoidAsync("calendarDragDrop.initialize", _objRef);
    }
    
    /// <summary>
    /// Makes an element draggable
    /// </summary>
    /// <param name="elementId">The ID of the element to make draggable</param>
    /// <param name="eventId">The ID of the event</param>
    /// <param name="viewType">The type of view (Month, Week, Day)</param>
    public async Task MakeElementDraggableAsync(string elementId, string eventId, string viewType)
    {
        await _jsRuntime.InvokeVoidAsync("calendarDragDrop.makeElementDraggable", elementId, eventId, viewType);
    }
    
    /// <summary>
    /// Makes an element resizable
    /// </summary>
    /// <param name="elementId">The ID of the element to make resizable</param>
    /// <param name="eventId">The ID of the event</param>
    /// <param name="viewType">The type of view (Week, Day)</param>
    public async Task MakeElementResizableAsync(string elementId, string eventId, string viewType)
    {
        await _jsRuntime.InvokeVoidAsync("calendarDragDrop.makeElementResizable", elementId, eventId, viewType);
    }
    
    /// <summary>
    /// Initializes drop zones for a view
    /// </summary>
    /// <param name="viewType">The type of view (Month, Week, Day)</param>
    public async Task InitializeDropZonesAsync(string viewType)
    {
        await _jsRuntime.InvokeVoidAsync("calendarDragDrop.initializeDropZones", viewType);
    }
    
    /// <summary>
    /// Handles the event being dragged to a new date/time
    /// </summary>
    /// <param name="eventId">The ID of the event</param>
    /// <param name="newDate">The new date</param>
    /// <param name="newTimeMinutes">The new time in minutes from midnight</param>
    [JSInvokable]
    public void OnEventDragged(string eventId, DateTime newDate, int newTimeMinutes)
    {
        var dragInfo = new EventDragInfo
        {
            EventId = Guid.Parse(eventId),
            NewDate = newDate,
            NewTimeMinutes = newTimeMinutes
        };
        
        EventDragged?.Invoke(this, dragInfo);
    }
    
    /// <summary>
    /// Handles the event being resized
    /// </summary>
    /// <param name="eventId">The ID of the event</param>
    /// <param name="durationMinutes">The new duration in minutes</param>
    [JSInvokable]
    public void OnEventResized(string eventId, int durationMinutes)
    {
        var resizeInfo = new EventResizeInfo
        {
            EventId = Guid.Parse(eventId),
            DurationMinutes = durationMinutes
        };
        
        EventResized?.Invoke(this, resizeInfo);
    }
    
    /// <summary>
    /// Disposes the service
    /// </summary>
    public void Dispose()
    {
        _objRef?.Dispose();
    }
}

/// <summary>
/// Information about an event being dragged
/// </summary>
public class EventDragInfo
{
    /// <summary>
    /// Gets or sets the ID of the event
    /// </summary>
    public Guid EventId { get; set; }
    
    /// <summary>
    /// Gets or sets the new date for the event
    /// </summary>
    public DateTime NewDate { get; set; }
    
    /// <summary>
    /// Gets or sets the new time in minutes from midnight
    /// </summary>
    public int NewTimeMinutes { get; set; }
    
    /// <summary>
    /// Gets the new time as a TimeSpan
    /// </summary>
    public TimeSpan NewTime => TimeSpan.FromMinutes(NewTimeMinutes);
}

/// <summary>
/// Information about an event being resized
/// </summary>
public class EventResizeInfo
{
    /// <summary>
    /// Gets or sets the ID of the event
    /// </summary>
    public Guid EventId { get; set; }
    
    /// <summary>
    /// Gets or sets the new duration in minutes
    /// </summary>
    public int DurationMinutes { get; set; }
    
    /// <summary>
    /// Gets the new duration as a TimeSpan
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);
}
