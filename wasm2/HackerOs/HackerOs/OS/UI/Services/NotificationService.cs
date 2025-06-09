using HackerOs.OS.Core.Settings;
using HackerOs.OS.UI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HackerOs.OS.UI.Services
{
    /// <summary>
    /// Service for managing system notifications
    /// </summary>
    public class NotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly ISettingsService _settingsService;
        
        // Settings keys
        private const string NOTIFICATIONS_KEY = "notifications.list";
        
        // In-memory cache of notifications
        private List<NotificationModel> _notifications = new();
        
        // Event for notification changes
        public event EventHandler<NotificationEventArgs>? NotificationAdded;
        public event EventHandler<NotificationEventArgs>? NotificationRemoved;
        public event EventHandler<NotificationEventArgs>? NotificationRead;
        
        /// <summary>
        /// Gets the count of unread notifications
        /// </summary>
        public int UnreadCount => _notifications.Count(n => !n.IsRead);
        
        /// <summary>
        /// Constructor
        /// </summary>
        public NotificationService(
            ISettingsService settingsService,
            ILogger<NotificationService> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Load notifications
            LoadNotificationsAsync().ConfigureAwait(false);
        }
        
        /// <summary>
        /// Gets all notifications
        /// </summary>
        public List<NotificationModel> GetNotifications()
        {
            return _notifications.OrderByDescending(n => n.Timestamp).ToList();
        }
        
        /// <summary>
        /// Gets unread notifications
        /// </summary>
        public List<NotificationModel> GetUnreadNotifications()
        {
            return _notifications.Where(n => !n.IsRead).OrderByDescending(n => n.Timestamp).ToList();
        }
        
        /// <summary>
        /// Adds a new notification
        /// </summary>
        public async Task AddNotificationAsync(NotificationModel notification)
        {
            try
            {
                // Set ID and timestamp if not already set
                if (string.IsNullOrEmpty(notification.Id))
                {
                    notification.Id = Guid.NewGuid().ToString();
                }
                
                if (notification.Timestamp == default)
                {
                    notification.Timestamp = DateTime.Now;
                }
                
                // Add to cache
                _notifications.Add(notification);
                
                // Save to storage
                await SaveNotificationsAsync();
                
                // Raise event
                NotificationAdded?.Invoke(this, new NotificationEventArgs(notification));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding notification");
            }
        }
        
        /// <summary>
        /// Removes a notification
        /// </summary>
        public async Task RemoveNotificationAsync(string notificationId)
        {
            try
            {
                // Find and remove from cache
                var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    _notifications.Remove(notification);
                    
                    // Save to storage
                    await SaveNotificationsAsync();
                    
                    // Raise event
                    NotificationRemoved?.Invoke(this, new NotificationEventArgs(notification));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing notification {notificationId}");
            }
        }
        
        /// <summary>
        /// Marks a notification as read
        /// </summary>
        public async Task MarkNotificationAsReadAsync(string notificationId)
        {
            try
            {
                // Find and update notification
                var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null && !notification.IsRead)
                {
                    notification.IsRead = true;
                    
                    // Save to storage
                    await SaveNotificationsAsync();
                    
                    // Raise event
                    NotificationRead?.Invoke(this, new NotificationEventArgs(notification));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking notification {notificationId} as read");
            }
        }
        
        /// <summary>
        /// Marks all notifications as read
        /// </summary>
        public async Task MarkAllNotificationsAsReadAsync()
        {
            try
            {
                // Update all unread notifications
                var unreadNotifications = _notifications.Where(n => !n.IsRead).ToList();
                if (unreadNotifications.Any())
                {
                    foreach (var notification in unreadNotifications)
                    {
                        notification.IsRead = true;
                        NotificationRead?.Invoke(this, new NotificationEventArgs(notification));
                    }
                    
                    // Save to storage
                    await SaveNotificationsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
            }
        }
        
        /// <summary>
        /// Clears all notifications
        /// </summary>
        public async Task ClearAllNotificationsAsync()
        {
            try
            {
                // Clear cache
                var oldNotifications = _notifications.ToList();
                _notifications.Clear();
                
                // Save to storage
                await SaveNotificationsAsync();
                
                // Raise events for each removed notification
                foreach (var notification in oldNotifications)
                {
                    NotificationRemoved?.Invoke(this, new NotificationEventArgs(notification));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all notifications");
            }
        }
        
        /// <summary>
        /// Loads notifications from storage
        /// </summary>
        private async Task LoadNotificationsAsync()
        {
            try
            {
                var notificationsJson = await _settingsService.GetSettingAsync<string>("notifications", NOTIFICATIONS_KEY, string.Empty);
                if (!string.IsNullOrEmpty(notificationsJson))
                {
                    _notifications = JsonSerializer.Deserialize<List<NotificationModel>>(notificationsJson) ?? new List<NotificationModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notifications");
                _notifications = new List<NotificationModel>();
            }
        }
        
        /// <summary>
        /// Saves notifications to storage
        /// </summary>
        private async Task SaveNotificationsAsync()
        {
            try
            {
                var notificationsJson = JsonSerializer.Serialize(_notifications);
                await _settingsService.SetSettingAsync("notifications", NOTIFICATIONS_KEY, notificationsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving notifications");
            }
        }
    }
    
    /// <summary>
    /// Event arguments for notification events
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        public NotificationModel Notification { get; }
        
        public NotificationEventArgs(NotificationModel notification)
        {
            Notification = notification;
        }
    }
}
