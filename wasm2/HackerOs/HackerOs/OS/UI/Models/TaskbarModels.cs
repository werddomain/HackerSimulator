using System;
using System.Collections.Generic;

namespace HackerOs.OS.UI.Models
{
    /// <summary>
    /// Model for representing an application in the taskbar
    /// </summary>
    public class TaskbarAppModel
    {
        /// <summary>
        /// The application ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// The application name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// The path to the application icon
        /// </summary>
        public string IconPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the application is currently active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Whether the application is currently minimized
        /// </summary>
        public bool IsMinimized { get; set; }
        
        /// <summary>
        /// Path to the application preview image
        /// </summary>
        public string PreviewImagePath { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the application was last launched
        /// </summary>
        public DateTime? LastLaunched { get; set; }

        /// <summary>
        /// Whether to show the restored animation
        /// </summary>
        public bool ShowRestoredAnimation { get; set; }
        
        /// <summary>
        /// Whether to show the minimized animation
        /// </summary>
        public bool ShowMinimizedAnimation { get; set; }
        
        /// <summary>
        /// Whether to show the maximized animation
        /// </summary>
        public bool ShowMaximizedAnimation { get; set; }
    }
    
    /// <summary>
    /// Model for representing a day in the calendar
    /// </summary>
    public class CalendarDayModel
    {
        /// <summary>
        /// The day number
        /// </summary>
        public int Day { get; set; }
        
        /// <summary>
        /// Whether the day is in the current month
        /// </summary>
        public bool IsCurrentMonth { get; set; }
        
        /// <summary>
        /// Whether the day is today
        /// </summary>
        public bool IsToday { get; set; }
    }
}
