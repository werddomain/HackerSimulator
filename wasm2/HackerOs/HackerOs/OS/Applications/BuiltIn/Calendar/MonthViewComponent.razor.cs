using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Component for displaying a month view of the calendar
/// </summary>
public partial class MonthViewComponent : ComponentBase
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
    /// Event callback for when an event is dragged to a new date
    /// </summary>
    [Parameter] public EventCallback<(Guid EventId, DateTime NewDate, TimeSpan NewTime)> OnEventDragged { get; set; }
    
    /// <summary>
    /// Gets the day names for the header
    /// </summary>
    protected List<string> DayNames { get; private set; } = new();
    
    /// <summary>
    /// Gets the weeks in the month
    /// </summary>
    protected List<List<DateTime>> Weeks { get; private set; } = new();
    
    // Private fields for the "more events" dialog
    private bool _showMoreEventsDialog;
    private DateTime _currentMoreEventsDate;
    private List<EventOccurrence> _eventsInMoreDialog = new();
    private bool _isDragDropInitialized;
    
    /// <summary>
    /// Called when the component is initialized
    /// </summary>
    protected override void OnInitialized()
    {
        GenerateDayNames();
        GenerateCalendarDays();
        
        // Subscribe to drag drop events
        DragDropService.EventDragged += HandleEventDragged;
    }
    
    /// <summary>
    /// Called when parameters have changed
    /// </summary>
    protected override void OnParametersSet()
    {
        GenerateCalendarDays();
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
            await DragDropService.InitializeDropZonesAsync("Month");
            
            // Make events draggable
            foreach (var date in Weeks.SelectMany(week => week))
            {
                var eventsForDay = EventsOnDate(date);
                var displayCount = Math.Min(eventsForDay.Count, 3);
                
                for (int i = 0; i < displayCount; i++)
                {
                    var evt = eventsForDay[i];
                    var eventId = $"month-event-{evt.OriginalEvent.Id}-{date.ToString("yyyyMMdd")}";
                    await DragDropService.MakeElementDraggableAsync(eventId, evt.OriginalEvent.Id.ToString(), "Month");
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
    /// Gets the events for a specific date
    /// </summary>
    protected List<EventOccurrence> EventsOnDate(DateTime date)
    {
        return Events
            .Where(e => e.StartTime.Date <= date.Date && e.EndTime.Date >= date.Date)
            .OrderBy(e => e.OriginalEvent.IsAllDay ? 0 : 1)
            .ThenBy(e => e.StartTime)
            .ToList();
    }
    
    /// <summary>
    /// Shows the "more events" dialog for a date
    /// </summary>
    protected void ShowMoreEvents(DateTime date)
    {
        _currentMoreEventsDate = date;
        _eventsInMoreDialog = EventsOnDate(date);
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
    
    private void GenerateDayNames()
    {
        DayNames.Clear();
        var culture = CultureInfo.CurrentCulture;
        
        // Start with Sunday (0) as the default first day of week
        int firstDayOfWeek = (int)DayOfWeek.Sunday;
        
        for (int i = 0; i < 7; i++)
        {
            int dayOfWeek = (firstDayOfWeek + i) % 7;
            string dayName = culture.DateTimeFormat.GetDayName((DayOfWeek)dayOfWeek);
            DayNames.Add(dayName);
        }
    }
    
    private void GenerateCalendarDays()
    {
        Weeks.Clear();
        
        // Get the first day of the month
        DateTime firstDayOfMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        
        // Get the last day of the month
        DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        
        // Get the day of the week for the first day of the month
        int firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
        
        // Calculate the first date to display (can be from the previous month)
        DateTime currentDate = firstDayOfMonth.AddDays(-firstDayOfWeek);
        
        // Generate a 6-week calendar (enough to show any month)
        for (int week = 0; week < 6; week++)
        {
            var weekDays = new List<DateTime>();
            
            for (int day = 0; day < 7; day++)
            {
                weekDays.Add(currentDate);
                currentDate = currentDate.AddDays(1);
            }
            
            Weeks.Add(weekDays);
            
            // If we're past the end of the month and have completed at least 4 weeks,
            // check if we need to continue
            if (week >= 3 && weekDays[6].Month != SelectedDate.Month)
            {
                break;
            }
        }
    }
    
    /// <summary>
    /// Clean up event handlers when the component is disposed
    /// </summary>
    public void Dispose()
    {
        DragDropService.EventDragged -= HandleEventDragged;
    }
}
