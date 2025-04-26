import { NotificationOptions, PromptOptions, ConfirmOptions } from './window-types';
/**
 * WindowEffectsManager module
 * Handles UI effects for windows including animations, notifications, and dialogs
 */


/**
 * WindowEffectsManager handles UI effects for windows
 */
export class WindowEffectsManager {
  /**
   * Show a notification
   * @param options Notification options
   */
  public showNotification(options: NotificationOptions): void {
    // Create notification container if it doesn't exist
    let notificationContainer = document.getElementById('notification-container');
    if (!notificationContainer) {
      notificationContainer = document.createElement('div');
      notificationContainer.id = 'notification-container';
      document.body.appendChild(notificationContainer);
    }
    
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `notification notification-${options.type || 'info'}`;
    
    // Create notification title
    const notificationTitle = document.createElement('div');
    notificationTitle.className = 'notification-title';
    notificationTitle.textContent = options.title;
    
    // Create notification message
    const notificationMessage = document.createElement('div');
    notificationMessage.className = 'notification-message';
    notificationMessage.textContent = options.message;
    
    // Create close button
    const closeButton = document.createElement('div');
    closeButton.className = 'notification-close';
    closeButton.textContent = '×';
    closeButton.addEventListener('click', () => {
      notification.classList.add('notification-hiding');
      setTimeout(() => {
        notification.remove();
      }, 300);
    });
    
    // Assemble notification
    notification.appendChild(notificationTitle);
    notification.appendChild(notificationMessage);
    notification.appendChild(closeButton);
    
    // Add to container
    notificationContainer.appendChild(notification);
    
    // Add show animation
    notification.classList.add('notification-showing');
    setTimeout(() => {
      notification.classList.remove('notification-showing');
    }, 300);
    
    // Auto-hide after a delay
    setTimeout(() => {
      if (notification.parentNode) {
        notification.classList.add('notification-hiding');
        setTimeout(() => {
          if (notification.parentNode) {
            notification.remove();
          }
        }, 300);
      }
    }, 5000);
  }

  /**
   * Create minimize animation for a window
   * @param id Window ID
   * @param windowElement Window element
   */
  public createMinimizeAnimation(id: string, windowElement: HTMLElement): void {
    // Get the taskbar item for the window
    const taskbarItem = document.querySelector(`.taskbar-item[data-window-id="${id}"]`);
    if (!taskbarItem) {
      // If taskbar item not found, just hide without animation
      windowElement.classList.add('window-minimized');
      return;
    }
    
    // Get the positions of the window and taskbar item
    const windowRect = windowElement.getBoundingClientRect();
    const taskbarRect = taskbarItem.getBoundingClientRect();
    
    // Calculate the target position for the animation
    const translateX = taskbarRect.left + (taskbarRect.width / 2) - (windowRect.left + (windowRect.width / 2));
    const translateY = taskbarRect.top + (taskbarRect.height / 2) - (windowRect.top + (windowRect.height / 2));
    
    // Create animation class for this specific window
    const style = document.createElement('style');
    style.textContent = `
      @keyframes minimizeAnimation${id} {
        0% {
          transform: scale(1);
          opacity: 1;
        }
        100% {
          transform: translate(${translateX}px, ${translateY}px) scale(0.1);
          opacity: 0.3;
        }
      }
    `;
    document.head.appendChild(style);
    
    // Apply the animation
    windowElement.style.animation = `minimizeAnimation${id} 0.3s forwards ease-in-out`;
    
    // After animation completes, hide the window
    setTimeout(() => {
      windowElement.classList.add('window-minimized');
      windowElement.style.animation = '';
      document.head.removeChild(style); // Clean up the style
    }, 300);
  }

  /**
   * Show a prompt dialog
   * @param options Prompt options
   * @param callback Function to call with the user's input
   */
  public showPrompt(options: PromptOptions, callback: (value: string | null) => void): void {
    // Create dialog container
    const dialogContainer = document.createElement('div');
    dialogContainer.className = 'dialog-container';
    
    // Create dialog content
    const dialogContent = document.createElement('div');
    dialogContent.className = 'dialog-content prompt-dialog';
    
    // Create dialog header
    const dialogHeader = document.createElement('div');
    dialogHeader.className = 'dialog-header';
    dialogHeader.textContent = options.title;
    
    // Create dialog message
    const dialogMessage = document.createElement('div');
    dialogMessage.className = 'dialog-message';
    dialogMessage.textContent = options.message;
    
    // Create input field
    const inputField = document.createElement('input');
    inputField.className = 'dialog-input';
    inputField.type = 'text';
    inputField.value = options.defaultValue || '';
    inputField.placeholder = options.placeholder || '';
    
    // Create dialog buttons
    const dialogButtons = document.createElement('div');
    dialogButtons.className = 'dialog-buttons';
    
    // Create cancel button
    const cancelButton = document.createElement('button');
    cancelButton.className = 'dialog-button cancel-button';
    cancelButton.textContent = 'Cancel';
    cancelButton.addEventListener('click', () => {
      document.body.removeChild(dialogContainer);
      callback(null);
    });
    
    // Create ok button
    const okButton = document.createElement('button');
    okButton.className = 'dialog-button ok-button';
    okButton.textContent = 'OK';
    okButton.addEventListener('click', () => {
      document.body.removeChild(dialogContainer);
      callback(inputField.value);
    });
    
    // Setup enter key to submit
    inputField.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        okButton.click();
      }
    });
    
    // Assemble dialog
    dialogButtons.appendChild(cancelButton);
    dialogButtons.appendChild(okButton);
    
    dialogContent.appendChild(dialogHeader);
    dialogContent.appendChild(dialogMessage);
    dialogContent.appendChild(inputField);
    dialogContent.appendChild(dialogButtons);
    
    dialogContainer.appendChild(dialogContent);
    
    // Add to document body
    document.body.appendChild(dialogContainer);
    
    // Focus input field
    setTimeout(() => {
      inputField.focus();
      inputField.select();
    }, 0);
  }

  /**
   * Show a confirmation dialog
   * @param options Confirmation options
   * @param callback Function to call with the user's response
   */
  public showConfirm(options: ConfirmOptions, callback: (confirmed: boolean) => void): void {
    // Create confirmation dialog container
    const dialogContainer = document.createElement('div');
    dialogContainer.className = 'dialog-container';
    
    // Create dialog content
    const dialogContent = document.createElement('div');
    dialogContent.className = 'dialog-content confirmation-dialog';
    
    // Create dialog header
    const dialogHeader = document.createElement('div');
    dialogHeader.className = 'dialog-header';
    dialogHeader.textContent = options.title;
    
    // Create dialog message
    const dialogMessage = document.createElement('div');
    dialogMessage.className = 'dialog-message';
    dialogMessage.textContent = options.message;
    
    // Create dialog buttons
    const dialogButtons = document.createElement('div');
    dialogButtons.className = 'dialog-buttons';
    
    // Create cancel button
    const cancelButton = document.createElement('button');
    cancelButton.className = 'dialog-button cancel-button';
    cancelButton.textContent = options.cancelText || 'Cancel';
    cancelButton.addEventListener('click', () => {
      document.body.removeChild(dialogContainer);
      callback(false);
    });
    
    // Create confirm button
    const confirmButton = document.createElement('button');
    confirmButton.className = 'dialog-button confirm-button';
    confirmButton.textContent = options.okText || 'Confirm';
    confirmButton.addEventListener('click', () => {
      document.body.removeChild(dialogContainer);
      callback(true);
    });
    
    // Assemble dialog
    dialogButtons.appendChild(cancelButton);
    dialogButtons.appendChild(confirmButton);
    
    dialogContent.appendChild(dialogHeader);
    dialogContent.appendChild(dialogMessage);
    dialogContent.appendChild(dialogButtons);
    
    dialogContainer.appendChild(dialogContent);
    
    // Add to document body
    document.body.appendChild(dialogContainer);
    
    // Focus confirm button
    setTimeout(() => {
      confirmButton.focus();
    }, 0);
  }
}
