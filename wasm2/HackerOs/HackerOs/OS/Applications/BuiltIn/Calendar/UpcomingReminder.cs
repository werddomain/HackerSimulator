namespace HackerOs.OS.Applications.BuiltIn.Calendar;

public class UpcomingReminder (EventOccurrence Occurrence, ReminderInfo Reminder)
{
    /// <summary>
    /// Gets the occurrence of the event for this reminder
    /// </summary>
    public EventOccurrence Occurrence { get; } = Occurrence;
    
    /// <summary>
    /// Gets the reminder information for this reminder
    /// </summary>
    public ReminderInfo Reminder { get; } = Reminder;
}


