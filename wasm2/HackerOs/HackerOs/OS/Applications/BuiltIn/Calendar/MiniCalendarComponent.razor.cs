using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Component for displaying a mini calendar in the sidebar
/// </summary>
public partial class MiniCalendarComponent : ComponentBase
{
    /// <summary>
    /// Gets or sets the selected date
    /// </summary>
    [Parameter] public DateTime SelectedDate { get; set; } = DateTime.Today;
    
    /// <summary>
    /// Gets or sets the list of events
    /// </summary>
    [Parameter] public List<EventOccurrence> Events { get; set; } = new();
    
    /// <summary>
    /// Event callback for when a date is selected
    /// </summary>
    [Parameter] public EventCallback<DateTime> OnDateSelected { get; set; }
    
    /// <summary>
    /// Gets the day names for the header
    /// </summary>
    protected List<string> DayNames { get; private set; } = new();
    
    /// <summary>
    /// Gets the weeks in the month
    /// </summary>
    protected List<List<DateTime>> Weeks { get; private set; } = new();
    
    /// <summary>
    /// Gets the month being displayed
    /// </summary>
    protected int DisplayMonth { get; private set; }
    
    /// <summary>
    /// Called when the component is initialized
    /// </summary>
    protected override void OnInitialized()
    {
        GenerateDayNames();
        GenerateCalendarDays();
        DisplayMonth = SelectedDate.Month;
    }
    
    /// <summary>
    /// Called when parameters have changed
    /// </summary>
    protected override void OnParametersSet()
    {
        GenerateCalendarDays();
        DisplayMonth = SelectedDate.Month;
    }
    
    /// <summary>
    /// Navigate to the previous month
    /// </summary>
    protected void NavigatePrevMonth()
    {
        OnDateSelected.InvokeAsync(SelectedDate.AddMonths(-1));
    }
    
    /// <summary>
    /// Navigate to today
    /// </summary>
    protected void NavigateToToday()
    {
        OnDateSelected.InvokeAsync(DateTime.Today);
    }
    
    /// <summary>
    /// Navigate to the next month
    /// </summary>
    protected void NavigateNextMonth()
    {
        OnDateSelected.InvokeAsync(SelectedDate.AddMonths(1));
    }
    
    /// <summary>
    /// Checks if there are events on a specific date
    /// </summary>
    protected bool HasEventsOnDate(DateTime date)
    {
        return Events.Any(e => e.StartTime.Date <= date.Date && e.EndTime.Date >= date.Date);
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
            // Only take the first letter for mini calendar
            DayNames.Add(dayName.Substring(0, 1));
        }
    }
    
    private void GenerateCalendarDays()
    {
        Weeks.Clear();
        
        // Get the first day of the month
        DateTime firstDayOfMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        
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
}
