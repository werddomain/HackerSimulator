namespace HackerOs.OS.Applications.BuiltIn.Calendar;

using System;

/// <summary>
/// Defines the type of reminder
/// </summary>
public enum ReminderType
{
    /// <summary>
    /// System notification
    /// </summary>
    Notification,
    
    /// <summary>
    /// Popup dialog
    /// </summary>
    Popup,
    
    /// <summary>
    /// Sound alert
    /// </summary>
    Sound,
    
    /// <summary>
    /// Email reminder
    /// </summary>
    Email,
    
    /// <summary>
    /// Combination of notification and sound
    /// </summary>
    NotificationWithSound
}
