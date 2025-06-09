using System;
using System.Collections.Generic;

namespace HackerOs.OS.UI.Models
{
    /// <summary>
    /// Represents a notification in the system
    /// </summary>
    public class NotificationModel
    {
        /// <summary>
        /// The notification ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// The notification title
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// The notification content
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// The notification icon path
        /// </summary>
        public string IconPath { get; set; } = string.Empty;
        
        /// <summary>
        /// The source of the notification (application name or system)
        /// </summary>
        public string Source { get; set; } = string.Empty;
        
        /// <summary>
        /// The notification type
        /// </summary>
        public NotificationType Type { get; set; } = NotificationType.Info;
        
        /// <summary>
        /// The notification timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Whether the notification has been read
        /// </summary>
        public bool IsRead { get; set; }
        
        /// <summary>
        /// The notification actions
        /// </summary>
        public List<NotificationAction> Actions { get; set; } = new List<NotificationAction>();
        
        /// <summary>
        /// Optional target application ID to launch when clicking the notification
        /// </summary>
        public string? TargetApplicationId { get; set; }
        
        /// <summary>
        /// Optional action data for the target application
        /// </summary>
        public string? ActionData { get; set; }
    }
    
    /// <summary>
    /// Represents an action that can be taken on a notification
    /// </summary>
    public class NotificationAction
    {
        /// <summary>
        /// The action ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// The action label
        /// </summary>
        public string Label { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional icon path for the action
        /// </summary>
        public string? IconPath { get; set; }
        
        /// <summary>
        /// Whether this is the primary action
        /// </summary>
        public bool IsPrimary { get; set; }
        
        /// <summary>
        /// Whether this action dismisses the notification
        /// </summary>
        public bool DismissesNotification { get; set; }
    }
    
    /// <summary>
    /// Notification types
    /// </summary>
    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success,
        System
    }
}
