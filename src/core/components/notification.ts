/**
 * Notification Component
 * Reusable notification system that displays temporary messages to the user
 */

import { UserSettings } from '../UserSettings';

export type NotificationType = 'success' | 'error' | 'info' | 'warning';
export type TaskbarPosition = 'top' | 'bottom' | 'left' | 'right';

export interface NotificationOptions {
  /** The message to display */
  message: string;
  /** The type of notification */
  type?: NotificationType;
  /** Duration in milliseconds to show the notification (default: 3000ms) */
  duration?: number;
  /** Custom CSS class to add to the notification */
  className?: string;
  /** Position of the notification (default: 'bottom-center') */
  position?: 'top-left' | 'top-center' | 'top-right' | 'bottom-left' | 'bottom-center' | 'bottom-right';
}

export class NotificationManager {
  private static instance: NotificationManager;
  private container: HTMLElement | null = null;
  private userSettings: UserSettings | null = null;
  private currentTaskbarPosition: TaskbarPosition = 'bottom';
  
  private constructor() {
    // Create container for notifications
    this.createContainer();
    
    // Listen for taskbar position changes
    window.addEventListener('settingChanged', this.handleSettingChanged.bind(this));
  }
  
  /**
   * Get the NotificationManager instance (singleton)
   */
  public static getInstance(): NotificationManager {
    if (!NotificationManager.instance) {
      NotificationManager.instance = new NotificationManager();
    }
    return NotificationManager.instance;
  }
  
  /**
   * Set the UserSettings instance for accessing user preferences
   * @param userSettings The UserSettings instance
   */
  public setUserSettings(userSettings: UserSettings): void {
    this.userSettings = userSettings;
    this.updateTaskbarPosition();
  }
  
  /**
   * Handle setting changed event
   * @param event The event object
   */
  private handleSettingChanged(event: Event): void {
    const customEvent = event as CustomEvent;
    if (customEvent.detail && customEvent.detail.key === 'taskbarPosition') {
      this.currentTaskbarPosition = customEvent.detail.value;
    }
  }
  
  /**
   * Update the taskbar position from UserSettings
   */
  private async updateTaskbarPosition(): Promise<void> {
    if (this.userSettings) {
      try {
        this.currentTaskbarPosition = await this.userSettings.getPreference('taskbarPosition', 'bottom') as TaskbarPosition;
      } catch (error) {
        console.error('Error loading taskbar position:', error);
        this.currentTaskbarPosition = 'bottom';
      }
    }
  }
  
  /**
   * Create the notification container
   */
  private createContainer(): void {
    // Check if container already exists
    if (document.getElementById('notification-container')) {
      this.container = document.getElementById('notification-container');
      return;
    }
    
    // Create container
    this.container = document.createElement('div');
    this.container.id = 'notification-container';
    this.container.className = 'notification-container bottom-center';
    
    // Add to document
    document.body.appendChild(this.container);
  }
  
  /**
   * Get the appropriate position class based on the requested position and taskbar position
   * @param position The requested notification position
   * @returns The adjusted position class
   */
  private getAdjustedPosition(position: string): string {
    // If no explicit position is requested, choose based on taskbar position
    if (!position) {
      switch (this.currentTaskbarPosition) {
        case 'top':
          return 'bottom-center';
        case 'bottom':
          return 'top-center';
        case 'left':
          return 'top-right';
        case 'right':
          return 'top-left';
        default:
          return 'top-center';
      }
    }
    
    // If the position might conflict with the taskbar, adjust it
    if (this.currentTaskbarPosition === 'top' && position.startsWith('top-')) {
      return position.replace('top-', 'bottom-');
    } else if (this.currentTaskbarPosition === 'bottom' && position.startsWith('bottom-')) {
      return position.replace('bottom-', 'top-');
    } else if (this.currentTaskbarPosition === 'left' && position.endsWith('-left')) {
      return position.replace('-left', '-right');
    } else if (this.currentTaskbarPosition === 'right' && position.endsWith('-right')) {
      return position.replace('-right', '-left');
    }
    
    // Otherwise use the requested position
    return position;
  }
  
  /**
   * Show a notification
   * @param options The notification options
   */
  public show(options: NotificationOptions): void {
    const { 
      message, 
      type = 'success', 
      duration = 3000, 
      className = '', 
      position = 'auto' 
    } = options;
    
    // Determine the best position based on taskbar location
    const adjustedPosition = this.getAdjustedPosition(position === 'auto' ? '' : position);
    
    // Update container position if needed
    if (this.container) {
      // Remove all position classes
      this.container.classList.remove(
        'top-left', 'top-center', 'top-right', 
        'bottom-left', 'bottom-center', 'bottom-right'
      );
      // Add the new position class
      this.container.classList.add(adjustedPosition);
      
      // Add taskbar position data attribute for CSS positioning
      this.container.setAttribute('data-taskbar', this.currentTaskbarPosition);
    }
    
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `notification ${type} ${className}`;
    notification.textContent = message;
    
    // Add close button
    const closeButton = document.createElement('button');
    closeButton.className = 'notification-close';
    closeButton.innerHTML = '&times;';
    closeButton.addEventListener('click', () => {
      this.removeNotification(notification);
    });
    notification.appendChild(closeButton);
    
    // Add to container
    if (this.container) {
      this.container.appendChild(notification);
    } else {
      document.body.appendChild(notification);
    }
    
    // Add entry animation
    setTimeout(() => {
      notification.classList.add('show');
    }, 10);
    
    // Remove after delay
    if (duration > 0) {
      setTimeout(() => {
        this.removeNotification(notification);
      }, duration);
    }
    
    return;
  }
  
  /**
   * Remove a notification with exit animation
   * @param notification The notification element to remove
   */
  private removeNotification(notification: HTMLElement): void {
    // Add exit animation
    notification.classList.remove('show');
    notification.classList.add('hide');
    
    // Remove from DOM after animation completes
    setTimeout(() => {
      if (notification.parentNode) {
        notification.parentNode.removeChild(notification);
      }
    }, 300); // Match the CSS transition duration
  }
  
  /**
   * Show a success notification
   * @param message The message to show
   * @param duration Optional duration in milliseconds
   */
  public success(message: string, duration = 3000): void {
    this.show({ message, type: 'success', duration });
  }
  
  /**
   * Show an error notification
   * @param message The message to show
   * @param duration Optional duration in milliseconds
   */
  public error(message: string, duration = 3000): void {
    this.show({ message, type: 'error', duration });
  }
  
  /**
   * Show an info notification
   * @param message The message to show
   * @param duration Optional duration in milliseconds
   */
  public info(message: string, duration = 3000): void {
    this.show({ message, type: 'info', duration });
  }
  
  /**
   * Show a warning notification
   * @param message The message to show
   * @param duration Optional duration in milliseconds
   */
  public warning(message: string, duration = 3000): void {
    this.show({ message, type: 'warning', duration });
  }
  
  /**
   * Remove all notifications
   */
  public clearAll(): void {
    if (this.container) {
      this.container.innerHTML = '';
    }
  }
}

// Export a default instance for easy import
export const notification = NotificationManager.getInstance();
