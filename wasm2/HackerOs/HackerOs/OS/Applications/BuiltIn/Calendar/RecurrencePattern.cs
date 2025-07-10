using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Represents a recurrence pattern for calendar events
/// </summary>
public class RecurrencePattern
{
    /// <summary>
    /// Gets or sets the type of recurrence pattern
    /// </summary>
    public RecurrenceType Type { get; set; } = RecurrenceType.Daily;
    
    /// <summary>
    /// Gets or sets the interval between occurrences
    /// </summary>
    public int Interval { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the day of the week for weekly recurrence
    /// </summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the day of the month for monthly recurrence
    /// </summary>
    public int? DayOfMonth { get; set; }
    
    /// <summary>
    /// Gets or sets the month of the year for yearly recurrence
    /// </summary>
    public int? MonthOfYear { get; set; }
    
    /// <summary>
    /// Gets or sets the position in the month (first, second, third, fourth, last)
    /// </summary>
    public int? PositionInMonth { get; set; }
    
    /// <summary>
    /// Gets or sets the end date of the recurrence
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Gets or sets the number of occurrences before the recurrence ends
    /// </summary>
    public int? NumberOfOccurrences { get; set; }
    
    /// <summary>
    /// Gets or sets the dates to exclude from the recurrence
    /// </summary>
    public List<DateTime> ExcludedDates { get; set; } = new();
    
    /// <summary>
    /// Determines if a specific date is included in the recurrence pattern
    /// </summary>
    /// <param name="startDate">The start date of the original event</param>
    /// <param name="date">The date to check</param>
    /// <returns>True if the date is included in the pattern, otherwise false</returns>
    public bool IncludesDate(DateTime startDate, DateTime date)
    {
        // Check if we're past the end date
        if (EndDate.HasValue && date > EndDate.Value)
        {
            return false;
        }
        
        // Check if the date is in the excluded dates list
        if (ExcludedDates.Contains(date.Date))
        {
            return false;
        }
        
        // If the date is before the start date, it can't be included
        if (date.Date < startDate.Date)
        {
            return false;
        }
        
        // Check based on recurrence type
        switch (Type)
        {
            case RecurrenceType.Daily:
                return IsDateInDailyPattern(startDate, date);
                
            case RecurrenceType.Weekly:
                return IsDateInWeeklyPattern(startDate, date);
                
            case RecurrenceType.Monthly:
                return IsDateInMonthlyPattern(startDate, date);
                
            case RecurrenceType.Yearly:
                return IsDateInYearlyPattern(startDate, date);
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Gets all occurrences of the event within a specified date range
    /// </summary>
    /// <param name="startDate">The start date of the original event</param>
    /// <param name="rangeStart">The start of the date range to check</param>
    /// <param name="rangeEnd">The end of the date range to check</param>
    /// <returns>A list of dates representing occurrences within the range</returns>
    public List<DateTime> GetOccurrencesInRange(DateTime startDate, DateTime rangeStart, DateTime rangeEnd)
    {
        var occurrences = new List<DateTime>();
        
        // Ensure rangeStart is not before the event's start date
        rangeStart = rangeStart < startDate.Date ? startDate.Date : rangeStart.Date;
        
        // Ensure we don't go past the end date
        if (EndDate.HasValue && rangeEnd > EndDate.Value)
        {
            rangeEnd = EndDate.Value;
        }
        
        switch (Type)
        {
            case RecurrenceType.Daily:
                occurrences = GetDailyOccurrences(startDate, rangeStart, rangeEnd);
                break;
                
            case RecurrenceType.Weekly:
                occurrences = GetWeeklyOccurrences(startDate, rangeStart, rangeEnd);
                break;
                
            case RecurrenceType.Monthly:
                occurrences = GetMonthlyOccurrences(startDate, rangeStart, rangeEnd);
                break;
                
            case RecurrenceType.Yearly:
                occurrences = GetYearlyOccurrences(startDate, rangeStart, rangeEnd);
                break;
        }
        
        // Remove excluded dates
        occurrences.RemoveAll(d => ExcludedDates.Contains(d.Date));
        
        // Limit by number of occurrences if specified
        if (NumberOfOccurrences.HasValue)
        {
            // Count how many occurrences have happened before the range start
            int previousOccurrences = CountOccurrencesBefore(startDate, rangeStart);
            
            // Limit the occurrences based on the total number allowed
            if (previousOccurrences >= NumberOfOccurrences.Value)
            {
                return new List<DateTime>();
            }
            
            int remainingOccurrences = NumberOfOccurrences.Value - previousOccurrences;
            if (occurrences.Count > remainingOccurrences)
            {
                occurrences = occurrences.Take(remainingOccurrences).ToList();
            }
        }
        
        return occurrences;
    }
    
    /// <summary>
    /// Creates a copy of the recurrence pattern
    /// </summary>
    /// <returns>A new RecurrencePattern with the same properties</returns>
    public RecurrencePattern Clone()
    {
        var clone = new RecurrencePattern
        {
            Type = Type,
            Interval = Interval,
            DayOfMonth = DayOfMonth,
            MonthOfYear = MonthOfYear,
            PositionInMonth = PositionInMonth,
            EndDate = EndDate,
            NumberOfOccurrences = NumberOfOccurrences
        };
        
        // Clone lists
        foreach (var dayOfWeek in DaysOfWeek)
        {
            clone.DaysOfWeek.Add(dayOfWeek);
        }
        
        foreach (var excludedDate in ExcludedDates)
        {
            clone.ExcludedDates.Add(excludedDate);
        }
        
        return clone;
    }
    
    #region Private Helper Methods
    
    private bool IsDateInDailyPattern(DateTime startDate, DateTime date)
    {
        var days = (date.Date - startDate.Date).TotalDays;
        return days % Interval == 0;
    }
    
    private bool IsDateInWeeklyPattern(DateTime startDate, DateTime date)
    {
        // Check if the day of the week is included
        if (!DaysOfWeek.Contains(date.DayOfWeek) && DaysOfWeek.Any())
        {
            return false;
        }
        
        // If no specific days are set, use the day of the original start date
        if (!DaysOfWeek.Any() && date.DayOfWeek != startDate.DayOfWeek)
        {
            return false;
        }
        
        // Calculate the number of weeks between the start date and the check date
        var daysDifference = (date.Date - startDate.Date).TotalDays;
        var weeksDifference = Math.Floor(daysDifference / 7);
        
        return weeksDifference % Interval == 0;
    }
    
    private bool IsDateInMonthlyPattern(DateTime startDate, DateTime date)
    {
        // If we're checking by day of month
        if (DayOfMonth.HasValue)
        {
            // Check if the day matches
            if (date.Day != DayOfMonth.Value)
            {
                return false;
            }
            
            // Calculate months difference
            var monthsDifference = (date.Year - startDate.Year) * 12 + date.Month - startDate.Month;
            return monthsDifference % Interval == 0;
        }
        
        // If we're checking by position in month (e.g., "First Monday")
        if (PositionInMonth.HasValue && DaysOfWeek.Any())
        {
            var dayOfWeek = DaysOfWeek.First();
            var position = PositionInMonth.Value;
            
            // Calculate the Nth occurrence of the day in the month
            var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
            var firstOccurrence = firstDayOfMonth.DayOfWeek == dayOfWeek
                ? firstDayOfMonth
                : firstDayOfMonth.AddDays((7 + (int)dayOfWeek - (int)firstDayOfMonth.DayOfWeek) % 7);
            
            // For "last" occurrence (position = -1)
            if (position < 0)
            {
                var lastDayOfMonth = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
                var lastOccurrence = lastDayOfMonth.DayOfWeek == dayOfWeek
                    ? lastDayOfMonth
                    : lastDayOfMonth.AddDays(-((int)lastDayOfMonth.DayOfWeek - (int)dayOfWeek + 7) % 7);
                
                if (date.Date != lastOccurrence.Date)
                {
                    return false;
                }
            }
            else
            {
                var occurrence = firstOccurrence.AddDays((position - 1) * 7);
                
                // Check if we're still in the correct month
                if (occurrence.Month != date.Month || date.Date != occurrence.Date)
                {
                    return false;
                }
            }
            
            // Calculate months difference
            var monthsDifference = (date.Year - startDate.Year) * 12 + date.Month - startDate.Month;
            return monthsDifference % Interval == 0;
        }
        
        return false;
    }
    
    private bool IsDateInYearlyPattern(DateTime startDate, DateTime date)
    {
        // Simple case: same month and day every N years
        if (MonthOfYear.HasValue)
        {
            if (date.Month != MonthOfYear.Value || date.Day != (DayOfMonth ?? startDate.Day))
            {
                return false;
            }
            
            var yearsDifference = date.Year - startDate.Year;
            return yearsDifference % Interval == 0;
        }
        
        // Complex case: Nth occurrence of a day in a month every N years
        if (PositionInMonth.HasValue && DaysOfWeek.Any())
        {
            // Check if the month is correct
            if (date.Month != (MonthOfYear ?? startDate.Month))
            {
                return false;
            }
            
            var dayOfWeek = DaysOfWeek.First();
            var position = PositionInMonth.Value;
            
            // Calculate the Nth occurrence of the day in the month
            var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
            var firstOccurrence = firstDayOfMonth.DayOfWeek == dayOfWeek
                ? firstDayOfMonth
                : firstDayOfMonth.AddDays((7 + (int)dayOfWeek - (int)firstDayOfMonth.DayOfWeek) % 7);
            
            // For "last" occurrence (position = -1)
            if (position < 0)
            {
                var lastDayOfMonth = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
                var lastOccurrence = lastDayOfMonth.DayOfWeek == dayOfWeek
                    ? lastDayOfMonth
                    : lastDayOfMonth.AddDays(-((int)lastDayOfMonth.DayOfWeek - (int)dayOfWeek + 7) % 7);
                
                if (date.Date != lastOccurrence.Date)
                {
                    return false;
                }
            }
            else
            {
                var occurrence = firstOccurrence.AddDays((position - 1) * 7);
                
                // Check if we're still in the correct month
                if (occurrence.Month != date.Month || date.Date != occurrence.Date)
                {
                    return false;
                }
            }
            
            var yearsDifference = date.Year - startDate.Year;
            return yearsDifference % Interval == 0;
        }
        
        return false;
    }
    
    private List<DateTime> GetDailyOccurrences(DateTime startDate, DateTime rangeStart, DateTime rangeEnd)
    {
        var occurrences = new List<DateTime>();
        
        // Find the first occurrence on or after rangeStart
        var currentDate = startDate.Date;
        var daysSinceStart = (int)Math.Ceiling((rangeStart.Date - startDate.Date).TotalDays);
        var daysToAdd = (daysSinceStart / Interval) * Interval;
        if (daysToAdd < daysSinceStart)
        {
            daysToAdd += Interval;
        }
        
        currentDate = startDate.Date.AddDays(daysToAdd);
        
        // Add all occurrences until rangeEnd
        while (currentDate <= rangeEnd)
        {
            occurrences.Add(currentDate);
            currentDate = currentDate.AddDays(Interval);
        }
        
        return occurrences;
    }
    
    private List<DateTime> GetWeeklyOccurrences(DateTime startDate, DateTime rangeStart, DateTime rangeEnd)
    {
        var occurrences = new List<DateTime>();
        var daysToCheck = DaysOfWeek.Any() ? DaysOfWeek : new List<DayOfWeek> { startDate.DayOfWeek };
        
        // Find the first week that might contain occurrences
        var daysSinceStart = (int)Math.Floor((rangeStart.Date - startDate.Date).TotalDays);
        var weeksSinceStart = daysSinceStart / 7;
        var weeksToAdd = (weeksSinceStart / Interval) * Interval;
        if (weeksToAdd < weeksSinceStart)
        {
            weeksToAdd += Interval;
        }
        
        var firstWeekStart = startDate.Date.AddDays(weeksToAdd * 7);
        
        // Check each week for occurrences
        var currentWeekStart = firstWeekStart;
        
        while (currentWeekStart <= rangeEnd)
        {
            foreach (var day in daysToCheck)
            {
                // Calculate the date for this day in the current week
                var dayDiff = (int)day - (int)currentWeekStart.DayOfWeek;
                if (dayDiff < 0) dayDiff += 7;
                
                var date = currentWeekStart.AddDays(dayDiff);
                
                if (date >= rangeStart && date <= rangeEnd)
                {
                    occurrences.Add(date);
                }
            }
            
            currentWeekStart = currentWeekStart.AddDays(7 * Interval);
        }
        
        return occurrences;
    }
    
    private List<DateTime> GetMonthlyOccurrences(DateTime startDate, DateTime rangeStart, DateTime rangeEnd)
    {
        var occurrences = new List<DateTime>();
        
        // Calculate the month range to check
        var startYear = rangeStart.Year;
        var startMonth = rangeStart.Month;
        var endYear = rangeEnd.Year;
        var endMonth = rangeEnd.Month;
        
        // Find the first month that might contain occurrences
        var monthsSinceStart = (startYear - startDate.Year) * 12 + (startMonth - startDate.Month);
        var monthsToAdd = (monthsSinceStart / Interval) * Interval;
        if (monthsToAdd < monthsSinceStart)
        {
            monthsToAdd += Interval;
        }
        
        var currentYear = startDate.Year + (startDate.Month + monthsToAdd - 1) / 12;
        var currentMonth = ((startDate.Month + monthsToAdd - 1) % 12) + 1;
        
        while (currentYear < endYear || (currentYear == endYear && currentMonth <= endMonth))
        {
            DateTime? occurrence = null;
            
            // If using day of month
            if (DayOfMonth.HasValue)
            {
                var day = Math.Min(DayOfMonth.Value, DateTime.DaysInMonth(currentYear, currentMonth));
                occurrence = new DateTime(currentYear, currentMonth, day);
            }
            // If using position in month (e.g., "First Monday")
            else if (PositionInMonth.HasValue && DaysOfWeek.Any())
            {
                var dayOfWeek = DaysOfWeek.First();
                var position = PositionInMonth.Value;
                
                // Calculate the Nth occurrence of the day in the month
                var firstDayOfMonth = new DateTime(currentYear, currentMonth, 1);
                var firstOccurrence = firstDayOfMonth.DayOfWeek == dayOfWeek
                    ? firstDayOfMonth
                    : firstDayOfMonth.AddDays((7 + (int)dayOfWeek - (int)firstDayOfMonth.DayOfWeek) % 7);
                
                // For "last" occurrence (position = -1)
                if (position < 0)
                {
                    var lastDayOfMonth = new DateTime(currentYear, currentMonth, DateTime.DaysInMonth(currentYear, currentMonth));
                    occurrence = lastDayOfMonth.DayOfWeek == dayOfWeek
                        ? lastDayOfMonth
                        : lastDayOfMonth.AddDays(-((int)lastDayOfMonth.DayOfWeek - (int)dayOfWeek + 7) % 7);
                }
                else
                {
                    occurrence = firstOccurrence.AddDays((position - 1) * 7);
                    
                    // Ensure we're still in the correct month
                    if (occurrence.Value.Month != currentMonth)
                    {
                        occurrence = null;
                    }
                }
            }
            
            if (occurrence.HasValue && occurrence.Value >= rangeStart && occurrence.Value <= rangeEnd)
            {
                occurrences.Add(occurrence.Value);
            }
            
            // Move to next interval month
            currentMonth += Interval;
            if (currentMonth > 12)
            {
                currentYear += currentMonth / 12;
                currentMonth = ((currentMonth - 1) % 12) + 1;
            }
        }
        
        return occurrences;
    }
    
    private List<DateTime> GetYearlyOccurrences(DateTime startDate, DateTime rangeStart, DateTime rangeEnd)
    {
        var occurrences = new List<DateTime>();
        
        // Find the first year that might contain occurrences
        var yearsSinceStart = rangeStart.Year - startDate.Year;
        var yearsToAdd = (yearsSinceStart / Interval) * Interval;
        if (yearsToAdd < yearsSinceStart)
        {
            yearsToAdd += Interval;
        }
        
        var currentYear = startDate.Year + yearsToAdd;
        
        while (currentYear <= rangeEnd.Year)
        {
            DateTime? occurrence = null;
            var month = MonthOfYear ?? startDate.Month;
            
            // If using specific day of month
            if (DayOfMonth.HasValue)
            {
                var day = Math.Min(DayOfMonth.Value, DateTime.DaysInMonth(currentYear, month));
                occurrence = new DateTime(currentYear, month, day);
            }
            // If using position in month (e.g., "First Monday")
            else if (PositionInMonth.HasValue && DaysOfWeek.Any())
            {
                var dayOfWeek = DaysOfWeek.First();
                var position = PositionInMonth.Value;
                
                // Calculate the Nth occurrence of the day in the month
                var firstDayOfMonth = new DateTime(currentYear, month, 1);
                var firstOccurrence = firstDayOfMonth.DayOfWeek == dayOfWeek
                    ? firstDayOfMonth
                    : firstDayOfMonth.AddDays((7 + (int)dayOfWeek - (int)firstDayOfMonth.DayOfWeek) % 7);
                
                // For "last" occurrence (position = -1)
                if (position < 0)
                {
                    var lastDayOfMonth = new DateTime(currentYear, month, DateTime.DaysInMonth(currentYear, month));
                    occurrence = lastDayOfMonth.DayOfWeek == dayOfWeek
                        ? lastDayOfMonth
                        : lastDayOfMonth.AddDays(-((int)lastDayOfMonth.DayOfWeek - (int)dayOfWeek + 7) % 7);
                }
                else
                {
                    occurrence = firstOccurrence.AddDays((position - 1) * 7);
                    
                    // Ensure we're still in the correct month
                    if (occurrence.Value.Month != month)
                    {
                        occurrence = null;
                    }
                }
            }
            // Default to same day as start date
            else
            {
                var day = Math.Min(startDate.Day, DateTime.DaysInMonth(currentYear, month));
                occurrence = new DateTime(currentYear, month, day);
            }
            
            if (occurrence.HasValue && occurrence.Value >= rangeStart && occurrence.Value <= rangeEnd)
            {
                occurrences.Add(occurrence.Value);
            }
            
            currentYear += Interval;
        }
        
        return occurrences;
    }
    
    private int CountOccurrencesBefore(DateTime startDate, DateTime beforeDate)
    {
        int count = 0;
        
        switch (Type)
        {
            case RecurrenceType.Daily:
                var days = (beforeDate.Date - startDate.Date).TotalDays;
                count = (int)Math.Floor(days / Interval);
                break;
                
            case RecurrenceType.Weekly:
                var weeks = (beforeDate.Date - startDate.Date).TotalDays / 7;
                count = (int)Math.Floor(weeks / Interval) * DaysOfWeek.Count;
                break;
                
            case RecurrenceType.Monthly:
            case RecurrenceType.Yearly:
                // For these more complex patterns, get all occurrences and count
                var occurrences = GetOccurrencesInRange(startDate, startDate, beforeDate.AddDays(-1));
                count = occurrences.Count;
                break;
        }
        
        return count;
    }
    
    #endregion
}

/// <summary>
/// Defines the type of recurrence pattern
/// </summary>
public enum RecurrenceType
{
    /// <summary>
    /// Recurs daily
    /// </summary>
    Daily,
    
    /// <summary>
    /// Recurs weekly
    /// </summary>
    Weekly,
    
    /// <summary>
    /// Recurs monthly
    /// </summary>
    Monthly,
    
    /// <summary>
    /// Recurs yearly
    /// </summary>
    Yearly
}
