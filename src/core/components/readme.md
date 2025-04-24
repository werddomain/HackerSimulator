# Core Components

This directory contains reusable UI components for the HackerSimulator application.

## Notification Component

The notification component provides a standardized way to show temporary messages to users. Notifications will appear above the taskbar and automatically disappear after a set time.

### Usage

```typescript
import { notification } from '../core/components/notification';

// Basic usage with different notification types
notification.success("Operation completed successfully!");
notification.error("An error occurred while processing your request");
notification.info("New update available");
notification.warning("Your session will expire in 5 minutes");

// Advanced options
notification.show({
  message: "Custom notification",
  type: "success",     // 'success', 'error', 'info', or 'warning'
  duration: 5000,      // 5 seconds
  position: "top-right", // Position on screen
  className: "custom-notification-class" // Optional additional CSS class
});
```

### Positions

The notification component supports 6 different positions:

- `top-left`: Notifications appear in the top left corner
- `top-center`: Notifications appear at the top center
- `top-right`: Notifications appear in the top right corner
- `bottom-left`: Notifications appear in the bottom left corner (above taskbar)
- `bottom-center`: Notifications appear at the bottom center (above taskbar)
- `bottom-right`: Notifications appear in the bottom right corner (above taskbar)

### Customization

Notifications are styled using CSS variables from the current theme:

- `--success-color`: Used for success notifications
- `--error-color`: Used for error notifications
- `--info-color`: Used for info notifications
- `--warning-color`: Used for warning notifications

### Example in an Application

Here's how to use the notification component in an application:

```typescript
import { GuiApplication } from '../core/gui-application';
import { notification } from '../core/components/notification';

export class MyApplication extends GuiApplication {
  protected initApplication(): void {
    // Initialize app...
    
    // Set up a button that shows a notification
    const button = document.createElement('button');
    button.textContent = 'Save';
    button.addEventListener('click', () => {
      this.saveData();
    });
    this.container?.appendChild(button);
  }
  
  private async saveData(): Promise<void> {
    try {
      // Save data logic...
      
      // Show success notification
      notification.success('Data saved successfully!');
    } catch (error) {
      // Show error notification
      notification.error('Failed to save data: ' + error.message);
    }
  }
}
```

### Integration with ThemeManager

Notifications automatically use theme colors from your current theme, ensuring they match the overall UI styling of your application.
