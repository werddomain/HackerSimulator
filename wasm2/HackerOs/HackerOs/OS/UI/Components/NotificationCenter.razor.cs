using HackerOs.OS.UI.Models;
using HackerOs.OS.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Component for the system notification center
    /// </summary>
    public partial class NotificationCenter : ComponentBase, IDisposable
    {
        [Inject] protected NotificationService NotificationService { get; set; } = null!;
        [Inject] protected ILogger<NotificationCenter> Logger { get; set; } = null!;

        /// <summary>
        /// Event called when the notification center open state changes
        /// </summary>
        [Parameter] public EventCallback<bool> OnNotificationCenterOpenChanged { get; set; }

        /// <summary>
        /// Whether the notification center is open
        /// </summary>
        private bool _isOpen;

        /// <summary>
        /// Current list of notifications
        /// </summary>
        private List<NotificationModel> _notifications = new();

        /// <summary>
        /// Initialize the component
        /// </summary>
        protected override void OnInitialized()
        {
            // Subscribe to notification events
            NotificationService.NotificationAdded += OnNotificationAdded;
            NotificationService.NotificationRemoved += OnNotificationRemoved;
            NotificationService.NotificationRead += OnNotificationRead;

            // Load initial notifications
            LoadNotifications();

            base.OnInitialized();
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from events
            NotificationService.NotificationAdded -= OnNotificationAdded;
            NotificationService.NotificationRemoved -= OnNotificationRemoved;
            NotificationService.NotificationRead -= OnNotificationRead;
        }

        /// <summary>
        /// Toggle the notification center
        /// </summary>
        public void ToggleNotificationCenter()
        {
            _isOpen = !_isOpen;
            OnNotificationCenterOpenChanged.InvokeAsync(_isOpen);
            
            // Mark notifications as read when opening
            if (_isOpen)
            {
                MarkAllAsRead();
            }
            
            StateHasChanged();
        }

        /// <summary>
        /// Close the notification center
        /// </summary>
        public void CloseNotificationCenter()
        {
            _isOpen = false;
            OnNotificationCenterOpenChanged.InvokeAsync(false);
            StateHasChanged();
        }

        /// <summary>
        /// Clear all notifications
        /// </summary>
        protected async Task ClearAllNotifications()
        {
            await NotificationService.ClearAllNotificationsAsync();
            _notifications.Clear();
            StateHasChanged();
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        protected async Task MarkAllAsRead()
        {
            await NotificationService.MarkAllNotificationsAsReadAsync();
            
            // Update local notifications
            foreach (var notification in _notifications)
            {
                notification.IsRead = true;
            }
            
            StateHasChanged();
        }

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        protected async Task MarkAsRead(string notificationId)
        {
            await NotificationService.MarkNotificationAsReadAsync(notificationId);
            
            // Update local notification
            var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Clear a specific notification
        /// </summary>
        protected async Task ClearNotification(string notificationId)
        {
            await NotificationService.RemoveNotificationAsync(notificationId);
            
            // Remove from local list
            _notifications.RemoveAll(n => n.Id == notificationId);
            StateHasChanged();
        }

        /// <summary>
        /// Load all notifications
        /// </summary>
        private async Task LoadNotifications()
        {
            _notifications = NotificationService.GetNotifications().ToList();
            StateHasChanged();
        }

        /// <summary>
        /// Get formatted time string for notification
        /// </summary>
        protected string GetFormattedTime(DateTime timestamp)
        {
            var now = DateTime.Now;
            var diff = now - timestamp;
            
            if (diff.TotalMinutes < 1)
            {
                return "Just now";
            }
            else if (diff.TotalHours < 1)
            {
                int minutes = (int)diff.TotalMinutes;
                return $"{minutes} {(minutes == 1 ? "minute" : "minutes")} ago";
            }
            else if (diff.TotalDays < 1)
            {
                int hours = (int)diff.TotalHours;
                return $"{hours} {(hours == 1 ? "hour" : "hours")} ago";
            }
            else if (diff.TotalDays < 7)
            {
                int days = (int)diff.TotalDays;
                return $"{days} {(days == 1 ? "day" : "days")} ago";
            }
            else
            {
                return timestamp.ToString("MMM d, yyyy");
            }
        }

        /// <summary>
        /// Helper used by the UI to display a relative time string.
        /// </summary>
        /// <param name="timestamp">Notification time stamp</param>
        /// <returns>Formatted relative time string</returns>
        protected string GetTimeAgo(DateTime timestamp) => GetFormattedTime(timestamp);

        /// <summary>
        /// Called when a notification is clicked. Marks it as read and closes the center.
        /// </summary>
        /// <param name="notification">The clicked notification</param>
        protected async Task OnNotificationClick(NotificationModel notification)
        {
            await MarkAsRead(notification.Id);
            CloseNotificationCenter();
        }

        /// <summary>
        /// Called when an action button on a notification is clicked.
        /// </summary>
        protected async Task OnNotificationActionClick(MouseEventArgs e, NotificationModel notification, NotificationAction action)
        {
            await MarkAsRead(notification.Id);
            if (action.DismissesNotification)
            {
                await ClearNotification(notification.Id);
            }
        }

        /// <summary>
        /// Dismisses a notification by id.
        /// </summary>
        protected async Task DismissNotification(string id)
        {
            await ClearNotification(id);
        }

        /// <summary>
        /// Handle notification added event
        /// </summary>
        private void OnNotificationAdded(object? sender, NotificationEventArgs e)
        {
            // Add notification to the list
            if (e.Notification != null)
            {
                _notifications.Insert(0, e.Notification);
                InvokeAsync(StateHasChanged);
            }
        }

        /// <summary>
        /// Handle notification removed event
        /// </summary>
        private void OnNotificationRemoved(object? sender, NotificationEventArgs e)
        {
            // Remove notification from the list
            if (e.Notification != null)
            {
                _notifications.RemoveAll(n => n.Id == e.Notification.Id);
                InvokeAsync(StateHasChanged);
            }
        }

        /// <summary>
        /// Handle notification read event
        /// </summary>
        private void OnNotificationRead(object? sender, NotificationEventArgs e)
        {
            // Update notification read status
            if (e.Notification != null)
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == e.Notification.Id);
                if (notification != null)
                {
                    notification.IsRead = true;
                    InvokeAsync(StateHasChanged);
                }
            }
        }
    }
}
