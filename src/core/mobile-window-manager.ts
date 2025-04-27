/**
 * Mobile Window Manager
 * Provides a touch-friendly window management system for mobile devices
 */

import { WindowOptions } from './window';
import { IWindowManager } from './window-manager-interface';
import { PlatformDetector, platformDetector } from './platform-detector';

/**
 * Mobile-specific window options extending the base WindowOptions
 */
export interface MobileWindowOptions extends WindowOptions {
  fullscreen?: boolean;
  swipeToClose?: boolean;
  swipeToMinimize?: boolean;
  showBackButton?: boolean;
  historyStack?: string[];
}

/**
 * Mobile Window Manager class for managing application windows on mobile devices
 */
export class MobileWindowManager implements IWindowManager {
  private windows: Map<string, MobileWindowOptions> = new Map();
  private windowElements: Map<string, HTMLElement> = new Map();
  private activeWindowId: string | null = null;
  private minimizedWindows: Set<string> = new Set();
  private maximizedWindows: Set<string> = new Set();
  private windowsContainer: HTMLElement | null = null;
  private mobileNavbar: HTMLElement | null = null;
  private windowHistory: string[] = [];
  private platformDetector: PlatformDetector;
  
  constructor() {
    this.platformDetector = platformDetector;
  }
  /**
   * Show a notification to the user
   * @param options Notification options
   */
  showNotification(options: { title: string; message: string; type?: 'info' | 'success' | 'warning' | 'error'; }): void {
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `mobile-notification ${options.type || 'info'}`;
    
    // Set notification content
    notification.innerHTML = `
      <div class="notification-title">${options.title}</div>
      <div class="notification-message">${options.message}</div>
    `;
    
    // Add appropriate icon based on type
    const iconElement = document.createElement('div');
    iconElement.className = 'notification-icon';
    
    switch(options.type) {
      case 'success':
        iconElement.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>';
        break;
      case 'warning':
        iconElement.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>';
        break;
      case 'error':
        iconElement.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>';
        break;
      default: // info
        iconElement.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';
    }
    
    notification.prepend(iconElement);
    
    // Add to document body
    document.body.appendChild(notification);
    
    // Animate notification
    setTimeout(() => notification.classList.add('visible'), 10);
    
    // Auto-hide after 3 seconds
    setTimeout(() => {
      notification.classList.remove('visible');
      
      // Remove from DOM after animation completes
      setTimeout(() => {
        notification.remove();
      }, 300); // Match CSS transition duration
    }, 3000);
  }
  
  /**
   * Show a prompt dialog to get user input
   * @param options Prompt options
   * @param callback Function to call with the user's input
   */
  showPrompt(options: { title: string; message: string; defaultValue?: string; placeholder?: string; }, callback: (value: string | null) => void): void {
    // Create overlay
    const overlay = document.createElement('div');
    overlay.className = 'mobile-dialog-overlay';
    
    // Create dialog
    const dialog = document.createElement('div');
    dialog.className = 'mobile-dialog prompt-dialog';
    
    // Create dialog content
    dialog.innerHTML = `
      <div class="dialog-header">
        <h2>${options.title}</h2>
      </div>
      <div class="dialog-body">
        <p>${options.message}</p>
        <input type="text" id="prompt-input" class="dialog-input" 
          value="${options.defaultValue || ''}" 
          placeholder="${options.placeholder || ''}" />
      </div>
      <div class="dialog-footer">
        <button class="dialog-button cancel-button">Cancel</button>
        <button class="dialog-button ok-button">OK</button>
      </div>
    `;
    
    // Add to document body
    overlay.appendChild(dialog);
    document.body.appendChild(overlay);
    
    // Focus the input field
    setTimeout(() => {
      const input = document.getElementById('prompt-input') as HTMLInputElement;
      if (input) input.focus();
    }, 300);
    
    // Show the dialog with animation
    setTimeout(() => {
      overlay.classList.add('visible');
      dialog.classList.add('visible');
    }, 10);
    
    // Set up event handlers
    const handleOk = () => {
      const input = document.getElementById('prompt-input') as HTMLInputElement;
      const value = input ? input.value : '';
      
      closeDialog();
      callback(value);
    };
    
    const handleCancel = () => {
      closeDialog();
      callback(null);
    };
    
    const closeDialog = () => {
      overlay.classList.remove('visible');
      dialog.classList.remove('visible');
      
      // Remove from DOM after animation completes
      setTimeout(() => {
        overlay.remove();
      }, 300);
    };
    
    // Add click handlers
    const okButton = dialog.querySelector('.ok-button');
    if (okButton) okButton.addEventListener('click', handleOk);
    
    const cancelButton = dialog.querySelector('.cancel-button');
    if (cancelButton) cancelButton.addEventListener('click', handleCancel);
    
    // Handle Enter key
    dialog.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') handleOk();
      if (e.key === 'Escape') handleCancel();
    });
  }
  
  /**
   * Show a confirmation dialog to get user approval
   * @param options Confirmation dialog options
   * @param callback Function to call with the user's decision
   */
  showConfirm(options: { title: string; message: string; okText?: string; cancelText?: string; }, callback: (confirmed: boolean) => void): void {
    // Create overlay
    const overlay = document.createElement('div');
    overlay.className = 'mobile-dialog-overlay';
    
    // Create dialog
    const dialog = document.createElement('div');
    dialog.className = 'mobile-dialog confirm-dialog';
    
    // Create dialog content
    dialog.innerHTML = `
      <div class="dialog-header">
        <h2>${options.title}</h2>
      </div>
      <div class="dialog-body">
        <p>${options.message}</p>
      </div>
      <div class="dialog-footer">
        <button class="dialog-button cancel-button">${options.cancelText || 'Cancel'}</button>
        <button class="dialog-button ok-button">${options.okText || 'OK'}</button>
      </div>
    `;
    
    // Add to document body
    overlay.appendChild(dialog);
    document.body.appendChild(overlay);
    
    // Show the dialog with animation
    setTimeout(() => {
      overlay.classList.add('visible');
      dialog.classList.add('visible');
    }, 10);
    
    // Set up event handlers
    const handleOk = () => {
      closeDialog();
      callback(true);
    };
    
    const handleCancel = () => {
      closeDialog();
      callback(false);
    };
    
    const closeDialog = () => {
      overlay.classList.remove('visible');
      dialog.classList.remove('visible');
      
      // Remove from DOM after animation completes
      setTimeout(() => {
        overlay.remove();
      }, 300);
    };
    
    // Add click handlers
    const okButton = dialog.querySelector('.ok-button');
    if (okButton) okButton.addEventListener('click', handleOk);
    
    const cancelButton = dialog.querySelector('.cancel-button');
    if (cancelButton) cancelButton.addEventListener('click', handleCancel);
    
    // Handle Enter/Escape keys
    dialog.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') handleOk();
      if (e.key === 'Escape') handleCancel();
    });
  }

  
  /**
   * Initialize the mobile window manager
   */
  public init(): void {
    console.log('Initializing Mobile Window Manager...');
    
    // Get or create the windows container
    this.windowsContainer = document.getElementById('mobile-windows-container');
    if (!this.windowsContainer) {
      this.windowsContainer = document.createElement('div');
      this.windowsContainer.id = 'mobile-windows-container';
      this.windowsContainer.className = 'mobile-windows-container';
      document.body.appendChild(this.windowsContainer);
    }
    
    // Get or create the mobile navigation bar
    this.mobileNavbar = document.getElementById('mobile-navbar');
    if (!this.mobileNavbar) {
      this.mobileNavbar = document.createElement('div');
      this.mobileNavbar.id = 'mobile-navbar';
      this.mobileNavbar.className = 'mobile-navbar';
      document.body.appendChild(this.mobileNavbar);
      
      // Add back button
      const backButton = document.createElement('button');
      backButton.className = 'mobile-back-button';
      backButton.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>';
      backButton.addEventListener('click', () => this.navigateBack());
      this.mobileNavbar.appendChild(backButton);
      
      // Add app switcher button
      const appSwitcherButton = document.createElement('button');
      appSwitcherButton.className = 'mobile-app-switcher-button';
      appSwitcherButton.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7"></rect><rect x="14" y="3" width="7" height="7"></rect><rect x="14" y="14" width="7" height="7"></rect><rect x="3" y="14" width="7" height="7"></rect></svg>';
      appSwitcherButton.addEventListener('click', () => this.showAppSwitcher());
      this.mobileNavbar.appendChild(appSwitcherButton);
    }
    
    // Set up gesture detection for the window container
    this.setupGestureDetection();
    
    // Listen for orientation changes to adjust window sizes
    window.addEventListener('orientationchange', () => this.handleOrientationChange());
    window.addEventListener('resize', () => this.handleResize());
    
    console.log('Mobile Window Manager initialized successfully');
  }
  
  /**
   * Create a new window
   * @param options Window options
   * @returns Window ID
   */
  public createWindow(options: WindowOptions | MobileWindowOptions): string {
    // Generate a unique window ID
    const windowId = `window-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
    
    // Convert to mobile window options if needed
    const mobileOptions: MobileWindowOptions = {
      ...options,
      // Default mobile window options
      fullscreen: (options as MobileWindowOptions).fullscreen !== undefined 
        ? (options as MobileWindowOptions).fullscreen 
        : true,
      swipeToClose: (options as MobileWindowOptions).swipeToClose !== undefined 
        ? (options as MobileWindowOptions).swipeToClose 
        : true,
      swipeToMinimize: (options as MobileWindowOptions).swipeToMinimize !== undefined 
        ? (options as MobileWindowOptions).swipeToMinimize 
        : true,
      showBackButton: (options as MobileWindowOptions).showBackButton !== undefined 
        ? (options as MobileWindowOptions).showBackButton 
        : false
    };
    
    // Store window options
    this.windows.set(windowId, mobileOptions);
    
    // Create window element
    const windowElement = this.createWindowElement(windowId, mobileOptions);
    this.windowElements.set(windowId, windowElement);
    
    // Add window to container
    this.windowsContainer?.appendChild(windowElement);
    
    // Add window to history
    this.windowHistory.push(windowId);
    
    // Activate the window
    this.activateWindow(windowId);
    
    // Return window ID
    return windowId;
  }
  
  /**
   * Create a window element
   * @param windowId Window ID
   * @param options Window options
   * @returns Window element
   */
  private createWindowElement(windowId: string, options: MobileWindowOptions): HTMLElement {
    // Create window element
    const windowElement = document.createElement('div');
    windowElement.id = windowId;
    windowElement.className = 'mobile-window';
    windowElement.setAttribute('data-app-id', options.appId);
    
    // Apply fullscreen if needed
    if (options.fullscreen) {
      windowElement.classList.add('fullscreen');
    }
    
    // Create window header
    const windowHeader = document.createElement('div');
    windowHeader.className = 'mobile-window-header';
    
    // Add back button if needed
    if (options.showBackButton) {
      const backButton = document.createElement('button');
      backButton.className = 'mobile-window-back-button';
      backButton.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>';
      backButton.addEventListener('click', () => this.navigateBack());
      windowHeader.appendChild(backButton);
    }
    
    // Add window title
    const windowTitle = document.createElement('div');
    windowTitle.className = 'mobile-window-title';
    windowTitle.textContent = options.title;
    windowHeader.appendChild(windowTitle);
    
    // Add window controls
    const windowControls = document.createElement('div');
    windowControls.className = 'mobile-window-controls';
    
    // Add minimize button if needed
    if (options.minimizable !== false) {
      const minimizeButton = document.createElement('button');
      minimizeButton.className = 'mobile-window-minimize-button';
      minimizeButton.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"></line></svg>';
      minimizeButton.addEventListener('click', () => this.minimizeWindow(windowId));
      windowControls.appendChild(minimizeButton);
    }
    
    // Add close button if needed
    if (options.closable !== false) {
      const closeButton = document.createElement('button');
      closeButton.className = 'mobile-window-close-button';
      closeButton.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>';
      closeButton.addEventListener('click', () => this.closeWindow(windowId));
      windowControls.appendChild(closeButton);
    }
    
    windowHeader.appendChild(windowControls);
    windowElement.appendChild(windowHeader);
    
    // Create window content
    const windowContent = document.createElement('div');
    windowContent.className = 'mobile-window-content';
    windowElement.appendChild(windowContent);
    
    // Set up touch events for gesture detection
    this.setupWindowTouchEvents(windowElement, windowId);
    
    return windowElement;
  }
  
  /**
   * Set up window touch events
   * @param windowElement Window element
   * @param windowId Window ID
   */
  private setupWindowTouchEvents(windowElement: HTMLElement, windowId: string): void {
    let startX = 0;
    let startY = 0;
    let currentX = 0;
    let currentY = 0;
    
    windowElement.addEventListener('touchstart', (e) => {
      startX = e.touches[0].clientX;
      startY = e.touches[0].clientY;
      currentX = startX;
      currentY = startY;
    });
    
    windowElement.addEventListener('touchmove', (e) => {
      currentX = e.touches[0].clientX;
      currentY = e.touches[0].clientY;
      
      const options = this.windows.get(windowId);
      if (!options) return;
      
      // Handle swipe to close/minimize
      if (options.swipeToClose || options.swipeToMinimize) {
        const deltaX = currentX - startX;
        const deltaY = currentY - startY;
        
        // Horizontal swipe (for closing)
        if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 50) {
          windowElement.style.transform = `translateX(${deltaX}px)`;
        }
        
        // Vertical swipe (for minimizing)
        if (Math.abs(deltaY) > Math.abs(deltaX) && deltaY > 50) {
          windowElement.style.transform = `translateY(${deltaY}px)`;
        }
      }
    });
    
    windowElement.addEventListener('touchend', (e) => {
      const options = this.windows.get(windowId);
      if (!options) return;
      
      const deltaX = currentX - startX;
      const deltaY = currentY - startY;
      
      // Horizontal swipe (for closing)
      if (options.swipeToClose && Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 100) {
        // Swipe right to close
        if (deltaX > 0) {
          this.animateWindowClose(windowElement, 'right', () => this.closeWindow(windowId));
        } 
        // Swipe left to close
        else {
          this.animateWindowClose(windowElement, 'left', () => this.closeWindow(windowId));
        }
      }
      
      // Vertical swipe (for minimizing)
      else if (options.swipeToMinimize && Math.abs(deltaY) > Math.abs(deltaX) && deltaY > 100) {
        // Swipe down to minimize
        this.animateWindowMinimize(windowElement, () => this.minimizeWindow(windowId));
      } 
      
      // Reset transform if no action was taken
      else {
        windowElement.style.transition = 'transform 0.3s ease-out';
        windowElement.style.transform = 'none';
        
        // Remove transition after it completes
        setTimeout(() => {
          windowElement.style.transition = '';
        }, 300);
      }
    });
  }
  
  /**
   * Animate window closing
   * @param windowElement Window element
   * @param direction Direction to close (left or right)
   * @param callback Callback to execute after animation
   */
  private animateWindowClose(windowElement: HTMLElement, direction: 'left' | 'right', callback: () => void): void {
    const screenWidth = window.innerWidth;
    
    windowElement.style.transition = 'transform 0.3s ease-out';
    windowElement.style.transform = `translateX(${direction === 'right' ? screenWidth : -screenWidth}px)`;
    
    setTimeout(() => {
      callback();
    }, 300);
  }
  
  /**
   * Animate window minimizing
   * @param windowElement Window element
   * @param callback Callback to execute after animation
   */
  private animateWindowMinimize(windowElement: HTMLElement, callback: () => void): void {
    const screenHeight = window.innerHeight;
    
    windowElement.style.transition = 'transform 0.3s ease-out';
    windowElement.style.transform = `translateY(${screenHeight}px)`;
    
    setTimeout(() => {
      callback();
    }, 300);
  }
  
  /**
   * Close a window
   * @param windowId Window ID
   */
  public closeWindow(windowId: string): void {
    // Get window element
    const windowElement = this.windowElements.get(windowId);
    if (!windowElement) return;
    
    // Remove window element from DOM
    windowElement.remove();
    
    // Remove window from maps and sets
    this.windows.delete(windowId);
    this.windowElements.delete(windowId);
    this.minimizedWindows.delete(windowId);
    this.maximizedWindows.delete(windowId);
    
    // Remove window from history
    this.windowHistory = this.windowHistory.filter(id => id !== windowId);
    
    // Activate the previous window if this was the active window
    if (this.activeWindowId === windowId) {
      this.activeWindowId = null;
      
      // Get the most recent window from history
      const lastWindowId = this.windowHistory[this.windowHistory.length - 1];
      if (lastWindowId) {
        this.activateWindow(lastWindowId);
      }
    }
  }
  
  /**
   * Get window element
   * @param windowId Window ID
   * @returns Window element or null if not found
   */
  public getWindowElement(windowId: string): HTMLElement | null {
    return this.windowElements.get(windowId) || null;
  }
  
  /**
   * Get window options
   * @param windowId Window ID
   * @returns Window options or null if not found
   */
  public getWindowOptions(windowId: string): WindowOptions | null {
    return this.windows.get(windowId) || null;
  }
  
  /**
   * Minimize a window
   * @param windowId Window ID
   */
  public minimizeWindow(windowId: string): void {
    const windowElement = this.windowElements.get(windowId);
    if (!windowElement) return;
    
    // Add to minimized set
    this.minimizedWindows.add(windowId);
    
    // Add CSS class for minimized style
    windowElement.classList.add('minimized');
    
    // Create a preview card in the app switcher
    this.createOrUpdateAppSwitcherCard(windowId);
    
    // If this was the active window, activate another one
    if (this.activeWindowId === windowId) {
      this.activeWindowId = null;
      
      // Get the most recent non-minimized window from history
      const nonMinimizedWindowId = this.windowHistory
        .filter(id => !this.minimizedWindows.has(id))
        .pop();
      
      if (nonMinimizedWindowId) {
        this.activateWindow(nonMinimizedWindowId);
      }
    }
  }
  
  /**
   * Create or update an app switcher card for a window
   * @param windowId Window ID
   */
  private createOrUpdateAppSwitcherCard(windowId: string): void {
    const options = this.windows.get(windowId);
    if (!options) return;
    
    // Get app switcher container or create it
    let appSwitcher = document.getElementById('mobile-app-switcher');
    if (!appSwitcher) {
      appSwitcher = document.createElement('div');
      appSwitcher.id = 'mobile-app-switcher';
      appSwitcher.className = 'mobile-app-switcher';
      document.body.appendChild(appSwitcher);
    }
    
    // Check if card already exists
    let card = document.getElementById(`app-card-${windowId}`);
    if (!card) {
      // Create a new card
      card = document.createElement('div');
      card.id = `app-card-${windowId}`;
      card.className = 'app-switcher-card';
      card.setAttribute('data-window-id', windowId);
      
      // Add app icon
      if (options.icon) {
        const iconElement = document.createElement('div');
        iconElement.className = 'app-card-icon';
        iconElement.innerHTML = options.icon;
        card.appendChild(iconElement);
      }
      
      // Add app title
      const titleElement = document.createElement('div');
      titleElement.className = 'app-card-title';
      titleElement.textContent = options.title;
      card.appendChild(titleElement);
      
      // Add click handler to restore the window
      card.addEventListener('click', () => this.restoreWindow(windowId));
      
      // Add close button
      const closeButton = document.createElement('button');
      closeButton.className = 'app-card-close';
      closeButton.innerHTML = '×';
      closeButton.addEventListener('click', (e) => {
        e.stopPropagation();
        this.closeWindow(windowId);
      });
      card.appendChild(closeButton);
      
      // Add card to app switcher
      appSwitcher.appendChild(card);
    } else {
      // Update existing card if needed
      const titleElement = card.querySelector('.app-card-title');
      if (titleElement) {
        titleElement.textContent = options.title;
      }
    }
  }
  
  /**
   * Show the app switcher
   */
  private showAppSwitcher(): void {
    const appSwitcher = document.getElementById('mobile-app-switcher');
    if (!appSwitcher) return;
    
    // Toggle visibility
    const isVisible = appSwitcher.classList.contains('visible');
    
    if (isVisible) {
      appSwitcher.classList.remove('visible');
    } else {
      appSwitcher.classList.add('visible');
      
      // Update all cards
      this.windows.forEach((_, windowId) => {
        this.createOrUpdateAppSwitcherCard(windowId);
      });
    }
  }
  
  /**
   * Maximize a window
   * @param windowId Window ID
   */
  public maximizeWindow(windowId: string): void {
    const windowElement = this.windowElements.get(windowId);
    if (!windowElement) return;
    
    // Remove from minimized set if it was minimized
    this.minimizedWindows.delete(windowId);
    
    // Add to maximized set
    this.maximizedWindows.add(windowId);
    
    // Remove minimized class
    windowElement.classList.remove('minimized');
    
    // Add maximized class
    windowElement.classList.add('maximized');
    windowElement.classList.add('fullscreen');
    
    // Activate the window
    this.activateWindow(windowId);
  }
  
  /**
   * Restore a window (from minimized or maximized state)
   * @param windowId Window ID
   */
  public restoreWindow(windowId: string): void {
    const windowElement = this.windowElements.get(windowId);
    if (!windowElement) return;
    
    // Remove from sets
    this.minimizedWindows.delete(windowId);
    this.maximizedWindows.delete(windowId);
    
    // Remove classes
    windowElement.classList.remove('minimized');
    windowElement.classList.remove('maximized');
    
    // Check if window should remain fullscreen
    const options = this.windows.get(windowId);
    if (options && options.fullscreen) {
      windowElement.classList.add('fullscreen');
    } else {
      windowElement.classList.remove('fullscreen');
    }
    
    // Hide app switcher if it's visible
    const appSwitcher = document.getElementById('mobile-app-switcher');
    if (appSwitcher && appSwitcher.classList.contains('visible')) {
      appSwitcher.classList.remove('visible');
    }
    
    // Activate the window
    this.activateWindow(windowId);
  }
  
  /**
   * Activate a window (bring to front)
   * @param windowId Window ID
   */
  public activateWindow(windowId: string): void {
    // Deactivate currently active window
    if (this.activeWindowId) {
      const activeWindowElement = this.windowElements.get(this.activeWindowId);
      if (activeWindowElement) {
        activeWindowElement.classList.remove('active');
      }
    }
    
    // Get window element
    const windowElement = this.windowElements.get(windowId);
    if (!windowElement) return;
    
    // Activate window
    windowElement.classList.add('active');
    this.activeWindowId = windowId;
    
    // Remove from minimized set if it was minimized
    if (this.minimizedWindows.has(windowId)) {
      this.restoreWindow(windowId);
    }
    
    // Update window history
    this.windowHistory = this.windowHistory.filter(id => id !== windowId);
    this.windowHistory.push(windowId);
    
    // Update title in navbar
    const options = this.windows.get(windowId);
    if (options) {
      this.updateMobileNavbarTitle(options.title);
    }
  }
  
  /**
   * Update the mobile navbar title
   * @param title Title to display
   */
  private updateMobileNavbarTitle(title: string): void {
    if (!this.mobileNavbar) return;
    
    // Get or create title element
    let titleElement = this.mobileNavbar.querySelector('.mobile-navbar-title');
    if (!titleElement) {
      titleElement = document.createElement('div');
      titleElement.className = 'mobile-navbar-title';
      this.mobileNavbar.appendChild(titleElement);
    }
    
    // Update title
    (titleElement as HTMLElement).textContent = title;
  }
  
  /**
   * Navigate back in window history
   */
  private navigateBack(): void {
    // Remove current window from history
    const currentWindowId = this.windowHistory.pop();
    if (!currentWindowId) return;
    
    // Get previous window
    const previousWindowId = this.windowHistory[this.windowHistory.length - 1];
    if (!previousWindowId) {
      // No previous window, re-add current window to history
      this.windowHistory.push(currentWindowId);
      return;
    }
    
    // Check if previous window exists
    const previousWindowOptions = this.windows.get(previousWindowId);
    if (!previousWindowOptions) {
      // Previous window doesn't exist, remove it from history and try again
      this.windowHistory.pop();
      this.windowHistory.push(currentWindowId);
      this.navigateBack();
      return;
    }
    
    // Animate transition
    const currentWindowElement = this.windowElements.get(currentWindowId);
    const previousWindowElement = this.windowElements.get(previousWindowId);
    
    if (currentWindowElement && previousWindowElement) {
      // Prepare for animation
      previousWindowElement.classList.remove('minimized');
      previousWindowElement.style.transform = 'translateX(-100%)';
      previousWindowElement.style.display = 'block';
      
      // Start animation
      setTimeout(() => {
        currentWindowElement.style.transition = 'transform 0.3s ease-out';
        previousWindowElement.style.transition = 'transform 0.3s ease-out';
        
        currentWindowElement.style.transform = 'translateX(100%)';
        previousWindowElement.style.transform = 'translateX(0)';
        
        // After animation
        setTimeout(() => {
          // Minimize current window
          this.minimizeWindow(currentWindowId);
          
          // Activate previous window
          this.activateWindow(previousWindowId);
          
          // Reset transitions
          currentWindowElement.style.transition = '';
          previousWindowElement.style.transition = '';
          currentWindowElement.style.transform = '';
        }, 300);
      }, 50);
    } else {
      // Fallback if elements not found
      this.minimizeWindow(currentWindowId);
      this.activateWindow(previousWindowId);
    }
  }
  
  /**
   * Check if a window is minimized
   * @param windowId Window ID
   * @returns True if window is minimized
   */
  public isWindowMinimized(windowId: string): boolean {
    return this.minimizedWindows.has(windowId);
  }
  
  /**
   * Check if a window is maximized
   * @param windowId Window ID
   * @returns True if window is maximized
   */
  public isWindowMaximized(windowId: string): boolean {
    return this.maximizedWindows.has(windowId);
  }
  
  /**
   * Get active window ID
   * @returns Active window ID or null if no window is active
   */
  public getActiveWindowId(): string | null {
    return this.activeWindowId;
  }
  
  /**
   * Close all windows
   */
  public closeAllWindows(): void {
    // Create a copy of window IDs
    const windowIds = [...this.windows.keys()];
    
    // Close each window
    windowIds.forEach(windowId => this.closeWindow(windowId));
  }
  
  /**
   * Set window content
   * @param windowId Window ID
   * @param content Window content
   */
  public setWindowContent(windowId: string, content: HTMLElement | string): void {
    const windowElement = this.windowElements.get(windowId);
    if (!windowElement) return;
    
    // Get content element
    const contentElement = windowElement.querySelector('.mobile-window-content');
    if (!contentElement) return;
    
    // Set content
    if (typeof content === 'string') {
      contentElement.innerHTML = content;
    } else {
      contentElement.innerHTML = '';
      contentElement.appendChild(content);
    }
  }
  
  /**
   * Update window title
   * @param windowId Window ID
   * @param title New title
   */
  public updateWindowTitle(windowId: string, title: string): void {
    // Update window options
    const options = this.windows.get(windowId);
    if (options) {
      options.title = title;
    }
    
    // Update window element
    const windowElement = this.windowElements.get(windowId);
    if (windowElement) {
      const titleElement = windowElement.querySelector('.mobile-window-title');
      if (titleElement) {
        titleElement.textContent = title;
      }
    }
    
    // Update app switcher card
    const cardElement = document.getElementById(`app-card-${windowId}`);
    if (cardElement) {
      const cardTitleElement = cardElement.querySelector('.app-card-title');
      if (cardTitleElement) {
        cardTitleElement.textContent = title;
      }
    }
    
    // Update navbar title if this is the active window
    if (this.activeWindowId === windowId) {
      this.updateMobileNavbarTitle(title);
    }
  }
  
  /**
   * Set up gesture detection for the window container
   */
  private setupGestureDetection(): void {
    if (!this.windowsContainer) return;
    
    // Add event listeners for touch events
    // ... Implementation of complex gesture detection would go here
    // This can be expanded in the future
  }
  
  /**
   * Handle orientation change
   */
  private handleOrientationChange(): void {
    // Adjust window sizes and positions based on new orientation
    this.handleResize();
  }
  
  /**
   * Handle window resize
   */
  private handleResize(): void {
    // Adjust fullscreen windows to match new screen size
    this.windows.forEach((options, windowId) => {
      if (options.fullscreen || this.maximizedWindows.has(windowId)) {
        const windowElement = this.windowElements.get(windowId);
        if (windowElement) {
          // Ensure window covers full screen
          windowElement.style.width = '100%';
          windowElement.style.height = '100%';
        }
      }
    });
  }
}
