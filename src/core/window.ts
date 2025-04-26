/**
 * Window Manager implementation
 */
 
import { IWindowManager } from './window-manager-interface';
import { WindowDomManager } from './window-dom';
import { WindowResizeHandler } from './window-resize';
import { WindowEventManager } from './window-events';
import { WindowEffectsManager } from './window-effects';
import { WindowOptions } from './window-types';

// Re-export types for backward compatibility
export { WindowOptions } from './window-types';

/**
 * Window Manager class for managing application windows
 */
export class WindowManager implements IWindowManager {
  private windows: Map<string, WindowOptions> = new Map();
  private activeWindowId: string | null = null;
  
  // Specialized managers
  private domManager: WindowDomManager;
  private resizeHandler: WindowResizeHandler;
  private eventManager: WindowEventManager;
  private effectsManager: WindowEffectsManager;
  
  constructor() {
    // Initialize event manager
    this.eventManager = new WindowEventManager();
    
    // Initialize DOM manager with callbacks
    this.domManager = new WindowDomManager({
      onMinimize: (id) => this.minimizeWindow(id),
      onMaximize: (id) => this.toggleMaximizeWindow(id),
      onClose: (id) => this.closeWindow(id),
      onActivate: (id) => this.activateWindow(id),
      onResize: (id) => this.eventManager.emit('resize', id),
      onResized: (id) => this.eventManager.emit('resized', id),
      onTaskbarItemClick: (id, isMinimized, isActive) => {
        if (isMinimized) {
          this.restoreWindow(id);
        } else if (isActive) {
          this.minimizeWindow(id);
        } else {
          this.activateWindow(id);
        }
      }
    });
    
    // Initialize resize handler with callbacks
    this.resizeHandler = new WindowResizeHandler({
      onActivate: (id) => this.activateWindow(id),
      onResize: (id) => this.eventManager.emit('resize', id),
      onResized: (id) => this.eventManager.emit('resized', id)
    });
    
    // Initialize effects manager
    this.effectsManager = new WindowEffectsManager();
  }

  /**
   * Initialize the window manager
   */
  public init(): void {
    console.log('Initializing Window Manager...');
    this.domManager.init();
    
    // Add event listener for desktop to deactivate windows
    document.getElementById('desktop')?.addEventListener('mousedown', (e) => {
      if ((e.target as HTMLElement).id === 'desktop' || 
          (e.target as HTMLElement).id === 'desktop-icons') {
        this.deactivateAllWindows();
      }
    });
  }

  /**
   * Create a new window
   * @param options Window options
   * @returns Window ID
   */
  public createWindow(options: WindowOptions): string {
    // Generate a unique ID for the window
    const id = `window-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
    
    // Set default values
    const windowOptions: WindowOptions = {
      ...options,
      x: options.x ?? (window.innerWidth / 2) - (options.width / 2),
      y: options.y ?? (window.innerHeight / 2) - (options.height / 2),
      resizable: options.resizable !== undefined ? options.resizable : true,
      minimizable: options.minimizable !== undefined ? options.minimizable : true,
      maximizable: options.maximizable !== undefined ? options.maximizable : true,
      closable: options.closable !== undefined ? options.closable : true,
    };
    
    // Store window options
    this.windows.set(id, windowOptions);
    
    // Create window elements via DOM manager
    this.domManager.createWindowElement(id, windowOptions);
    
    // Set up window behavior
    const windowElement = this.domManager.getWindowElement(id);
    if (windowElement) {
      const windowHeader = windowElement.querySelector('.window-header') as HTMLElement;
      if (windowHeader) {
        // Make window draggable by header
        this.domManager.makeWindowDraggable(id, windowHeader);
        
        // Make window resizable if specified
        if (windowOptions.resizable) {
          this.resizeHandler.makeWindowResizable(id, windowElement);
        }
      }
    }
    
    // Activate the window
    this.activateWindow(id);
    
    return id;
  }

  /**
   * Get a window's content element
   * @param id Window ID
   * @returns Window content element
   */
  public getWindowContentElement(id: string): HTMLElement | null {
    return this.domManager.getWindowContentElement(id);
  }

  /**
   * Activate a window (bring to front)
   * @param id Window ID
   */
  public activateWindow(id: string): void {
    this.domManager.activateWindow(id);
    this.activeWindowId = id;
    this.eventManager.emit('focus', id);
  }

  /**
   * Deactivate all windows
   */
  private deactivateAllWindows(): void {
    this.domManager.deactivateAllWindows();
    this.activeWindowId = null;
  }

  /**
   * Minimize a window with animation
   * @param id Window ID
   */
  public minimizeWindow(id: string): void {
    const windowElement = this.domManager.getWindowElement(id);
    if (!windowElement) return;
    
    // Create minimize animation using effects manager
    this.effectsManager.createMinimizeAnimation(id, windowElement);
    
    // Update internal state
    if (this.activeWindowId === id) {
      this.activeWindowId = null;
    }
    
    // Emit minimize event
    this.eventManager.emit('minimize', id);
  }

  /**
   * Restore a minimized window
   * @param id Window ID
   */
  public restoreWindow(id: string): void {
    this.domManager.restoreWindow(id);
    this.activateWindow(id);
  }

  /**
   * Toggle maximize/restore window
   * @param id Window ID
   */
  public toggleMaximizeWindow(id: string): void {
    const windowElement = this.domManager.getWindowElement(id);
    if (!windowElement) return;
    
    const options = this.windows.get(id);
    if (!options) return;
    
    if (windowElement.classList.contains('window-maximized')) {
      // Restore window
      this.domManager.restoreMaximizedWindow(
        id, 
        options.width, 
        options.height, 
        options.x || 0, 
        options.y || 0
      );
    } else {
      // Save current dimensions and position
      options.width = windowElement.offsetWidth;
      options.height = windowElement.offsetHeight;
      options.x = windowElement.offsetLeft;
      options.y = windowElement.offsetTop;
      
      // Maximize window
      this.domManager.maximizeWindow(id);
      
      // Emit maximize event
      this.eventManager.emit('maximize', id);
    }
    
    // Activate the window
    this.activateWindow(id);
  }

  /**
   * Close a window
   * @param id Window ID
   */
  public closeWindow(id: string): void {
    // Get window options to check for processId
    const windowOptions = this.windows.get(id);
    
    // Remove window element via DOM manager
    this.domManager.removeWindowElement(id);
    
    // Terminate the associated process if it exists
    if (windowOptions && windowOptions.processId !== undefined) {
      // Get process manager from global OS
      const processManager = (window as any).os?.getProcessManager();
      if (processManager) {
        // Use setTimeout to avoid recursion issues in case the process's onKill tries to close this window
        setTimeout(() => {
          processManager.killProcess(windowOptions.processId);
        }, 0);
      }
    }
    
    // Remove window from map
    this.windows.delete(id);
    
    // Clear active window ID if this window was active
    if (this.activeWindowId === id) {
      this.activeWindowId = null;
    }
    
    // Emit window close event
    this.eventManager.emit('close', id);
  }

  /**
   * Close all windows
   */
  public closeAllWindows(): void {
    const windowIds = Array.from(this.windows.keys());
    for (const id of windowIds) {
      this.closeWindow(id);
    }
  }

  /**
   * Check if a window is minimized
   * @param id Window ID
   * @returns True if window is minimized
   */
  public isWindowMinimized(id: string): boolean { return this.domManager.isWindowMinimized(id); }

  /**
   * Check if a window is maximized
   * @param id Window ID
   * @returns True if window is maximized
   */
  public isWindowMaximized(id: string): boolean { return this.domManager.isWindowMaximized(id); }

  /**
   * Get active window ID
   * @returns Active window ID or null if no window is active
   */
  public getActiveWindowId(): string | null { return this.activeWindowId; }

  /**
   * Get window element
   * @param id Window ID
   * @returns Window element or null if not found
   */
  public getWindowElement(id: string): HTMLElement | null {
    return this.domManager.getWindowElement(id);
  }

  /**
   * Get window options
   * @param id Window ID
   * @returns Window options or null if not found
   */
  public getWindowOptions(id: string): WindowOptions | null {
    return this.windows.get(id) || null;
  }

  /**
   * Maximize window
   * @param id Window ID
   */
  public maximizeWindow(id: string): void {
    const windowElement = this.domManager.getWindowElement(id);
    if (!windowElement) return;
    
    // Don't maximize if already maximized
    if (windowElement.classList.contains('window-maximized')) {
      return;
    }
    
    // Save current dimensions and position
    const options = this.windows.get(id);
    if (options) {
      options.width = windowElement.offsetWidth;
      options.height = windowElement.offsetHeight;
      options.x = windowElement.offsetLeft;
      options.y = windowElement.offsetTop;
    }
    
    // Maximize window via DOM manager
    this.domManager.maximizeWindow(id);
    
    // Activate the window
    this.activateWindow(id);
    
    // Emit maximize event
    this.eventManager.emit('maximize', id);
  }

  /**
   * Set window content
   * @param id Window ID
   * @param content Window content
   */
  public setWindowContent(id: string, content: HTMLElement | string): void {
    return this.domManager.setWindowContent(id, content);
  }

  /**
   * Update window title
   * @param id Window ID
   * @param title New window title
   */
  public updateWindowTitle(id: string, title: string): void {
    this.domManager.updateWindowTitle(id, title);
    
    // Update window options
    const windowOptions = this.windows.get(id);
    if (windowOptions) {
      windowOptions.title = title;
    }
  }

  /**
   * Show a notification to the user
   * @param options Notification options
   */
  public showNotification(options: { title: string; message: string; type?: 'info' | 'success' | 'warning' | 'error' }): void {
    this.effectsManager.showNotification(options);
  }

  /**
   * Show a prompt dialog to get user input
   * @param options Prompt options
   * @param callback Function to call with the user's input
   */
  public showPrompt(options: { title: string; message: string; defaultValue?: string; placeholder?: string }, 
                   callback: (value: string | null) => void): void {
    this.effectsManager.showPrompt(options, callback);
  }

  /**
   * Show a confirmation dialog
   * @param options Confirmation options
   * @param callback Function to call with the user's response
   */
  public showConfirm(options: { title: string; message: string; okText?: string; cancelText?: string }, 
                    callback: (confirmed: boolean) => void): void {
    this.effectsManager.showConfirm(options, callback);
  }

  /**
   * Subscribe to window events
   * @param type Event type (show, minimize, maximize, close, resize, focus)
   * @param callback Function to call when event occurs
   * @returns Unsubscribe function
   */
  public on(type: string, callback: (id: string) => void): () => void {
    return this.eventManager.on(type, callback);
  }
}
