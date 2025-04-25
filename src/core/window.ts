/**
 * Interface for window properties
 */
export interface WindowOptions {
  title: string;
  width: number;
  height: number;
  x?: number;
  y?: number;
  resizable?: boolean;
  minimizable?: boolean;
  maximizable?: boolean;
  closable?: boolean;
  appId: string;
  icon?: string;
  processId?: number;
}

/**
 * Window Manager class for managing application windows
 */
export class WindowManager {
  private windows: Map<string, WindowOptions> = new Map();
  private windowElements: Map<string, HTMLElement> = new Map();
  private activeWindowId: string | null = null;
  private zIndex: number = 100;
  private windowsContainer: HTMLElement | null = null;
  private taskbarItems: HTMLElement | null = null;

  /**
   * Initialize the window manager
   */
  public init(): void {
    console.log('Initializing Window Manager...');
    
    this.windowsContainer = document.getElementById('windows-container');
    this.taskbarItems = document.getElementById('taskbar-items');
    
    if (!this.windowsContainer) {
      console.error('Window container element not found');
      return;
    }
    
    if (!this.taskbarItems) {
      console.error('Taskbar items element not found');
      return;
    }
    
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
   */
  public createWindow(options: WindowOptions): string {
    if (!this.windowsContainer || !this.taskbarItems) return '';
    
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
    
    // Create window element
    const windowElement = document.createElement('div');
    windowElement.className = 'window';
    windowElement.id = id;
    windowElement.style.width = `${windowOptions.width}px`;
    windowElement.style.height = `${windowOptions.height}px`;
    windowElement.style.left = `${windowOptions.x}px`;
    windowElement.style.top = `${windowOptions.y}px`;
    windowElement.style.zIndex = (this.zIndex++).toString();
    
    // Create window header
    const windowHeader = document.createElement('div');
    windowHeader.className = 'window-header';
    
    // Create window title
    const windowTitle = document.createElement('div');
    windowTitle.className = 'window-title';
    windowTitle.textContent = windowOptions.title;
    
    // Create window controls
    const windowControls = document.createElement('div');
    windowControls.className = 'window-controls';
    
    if (windowOptions.minimizable) {
      const minimizeButton = document.createElement('div');
      minimizeButton.className = 'window-control window-minimize';
      minimizeButton.addEventListener('click', () => this.minimizeWindow(id));
      windowControls.appendChild(minimizeButton);
    }
    
    if (windowOptions.maximizable) {
      const maximizeButton = document.createElement('div');
      maximizeButton.className = 'window-control window-maximize';
      maximizeButton.addEventListener('click', () => this.toggleMaximizeWindow(id));
      windowControls.appendChild(maximizeButton);
    }
    
    if (windowOptions.closable) {
      const closeButton = document.createElement('div');
      closeButton.className = 'window-control window-close';
      closeButton.addEventListener('click', () => this.closeWindow(id));
      windowControls.appendChild(closeButton);
    }
    
    // Add elements to window header
    windowHeader.appendChild(windowTitle);
    windowHeader.appendChild(windowControls);
    
    // Create window content container
    const windowContent = document.createElement('div');
    windowContent.className = 'window-content';
    
    // Add elements to window
    windowElement.appendChild(windowHeader);
    windowElement.appendChild(windowContent);
    
    // Add window to container
    this.windowsContainer.appendChild(windowElement);
    
    // Store window element reference
    this.windowElements.set(id, windowElement);
    
    // Add taskbar item
    const taskbarItem = document.createElement('div');
    taskbarItem.className = 'taskbar-item';
    taskbarItem.dataset.windowId = id;
    taskbarItem.textContent = windowOptions.title;
    taskbarItem.addEventListener('click', () => {
      if (this.isWindowMinimized(id)) {
        this.restoreWindow(id);
        
      } else if (this.activeWindowId === id) {
        this.minimizeWindow(id);
      } else {
        this.activateWindow(id);
      }
    });
    
    this.taskbarItems.appendChild(taskbarItem);
    
    // Make the window draggable
    this.makeWindowDraggable(id, windowHeader);
    
    // Make the window resizable if specified
    if (windowOptions.resizable) {
      this.makeWindowResizable(id);
    }
    
    // Activate the window
    this.activateWindow(id);
    
    return id;
  }

  /**
   * Get a window's content element
   */
  public getWindowContentElement(id: string): HTMLElement | null {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return null;
    
    return windowElement.querySelector('.window-content');
  }

  /**
   * Make a window draggable by its header
   */
  private makeWindowDraggable(id: string, headerElement: HTMLElement): void {
    let offsetX = 0;
    let offsetY = 0;
    let isDragging = false;
    
    headerElement.addEventListener('mousedown', (e) => {
      // Ignore if clicking on window controls
      if ((e.target as HTMLElement).classList.contains('window-control')) return;
      
      // Activate window
      this.activateWindow(id);
      
      // Start dragging
      isDragging = true;
      offsetX = e.clientX - this.windowElements.get(id)!.offsetLeft;
      offsetY = e.clientY - this.windowElements.get(id)!.offsetTop;
      
      // Prevent text selection during drag
      e.preventDefault();
    });
    
    document.addEventListener('mousemove', (e) => {
      if (!isDragging) return;
      
      const windowElement = this.windowElements.get(id)!;
      
      // Calculate new position
      const newLeft = e.clientX - offsetX;
      const newTop = e.clientY - offsetY;
      
      // Set new position
      windowElement.style.left = `${newLeft}px`;
      windowElement.style.top = `${newTop}px`;
    });
    
    document.addEventListener('mouseup', () => {
      isDragging = false;
    });
  }

  /**
   * Make a window resizable
   */
  private makeWindowResizable(id: string): void {
    const windowElement = this.windowElements.get(id)!;
    const resizeHandleSize = 8;
    
    // Create resize handles
    const positions = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'];
    positions.forEach(pos => {
      const handle = document.createElement('div');
      handle.className = `window-resize-handle window-resize-${pos}`;
      handle.style.position = 'absolute';
      
      // Set position and size of handle
      switch (pos) {
        case 'n':
          handle.style.top = `-${resizeHandleSize / 2}px`;
          handle.style.left = `${resizeHandleSize}px`;
          handle.style.right = `${resizeHandleSize}px`;
          handle.style.height = `${resizeHandleSize}px`;
          handle.style.cursor = 'ns-resize';
          break;
        case 'ne':
          handle.style.top = `-${resizeHandleSize / 2}px`;
          handle.style.right = `-${resizeHandleSize / 2}px`;
          handle.style.width = `${resizeHandleSize}px`;
          handle.style.height = `${resizeHandleSize}px`;
          handle.style.cursor = 'nesw-resize';
          break;
        case 'e':
          handle.style.top = `${resizeHandleSize}px`;
          handle.style.right = `-${resizeHandleSize / 2}px`;
          handle.style.bottom = `${resizeHandleSize}px`;
          handle.style.width = `${resizeHandleSize}px`;
          handle.style.cursor = 'ew-resize';
          break;
        case 'se':
          handle.style.bottom = `-${resizeHandleSize / 2}px`;
          handle.style.right = `-${resizeHandleSize / 2}px`;
          handle.style.width = `${resizeHandleSize}px`;
          handle.style.height = `${resizeHandleSize}px`;
          handle.style.cursor = 'nwse-resize';
          break;
        case 's':
          handle.style.bottom = `-${resizeHandleSize / 2}px`;
          handle.style.left = `${resizeHandleSize}px`;
          handle.style.right = `${resizeHandleSize}px`;
          handle.style.height = `${resizeHandleSize}px`;
          handle.style.cursor = 'ns-resize';
          break;
        case 'sw':
          handle.style.bottom = `-${resizeHandleSize / 2}px`;
          handle.style.left = `-${resizeHandleSize / 2}px`;
          handle.style.width = `${resizeHandleSize}px`;
          handle.style.height = `${resizeHandleSize}px`;
          handle.style.cursor = 'nesw-resize';
          break;
        case 'w':
          handle.style.top = `${resizeHandleSize}px`;
          handle.style.left = `-${resizeHandleSize / 2}px`;
          handle.style.bottom = `${resizeHandleSize}px`;
          handle.style.width = `${resizeHandleSize}px`;
          handle.style.cursor = 'ew-resize';
          break;
        case 'nw':
          handle.style.top = `-${resizeHandleSize / 2}px`;
          handle.style.left = `-${resizeHandleSize / 2}px`;
          handle.style.width = `${resizeHandleSize}px`;
          handle.style.height = `${resizeHandleSize}px`;
          handle.style.cursor = 'nwse-resize';
          break;
      }
      
      // Add event listeners for resizing
      let startX = 0;
      let startY = 0;
      let startWidth = 0;
      let startHeight = 0;
      let startLeft = 0;
      let startTop = 0;
      
      handle.addEventListener('mousedown', (e) => {
        e.preventDefault();
        e.stopPropagation();
        
        // Activate window
        this.activateWindow(id);
        
        // Initialize resize
        startX = e.clientX;
        startY = e.clientY;
        startWidth = windowElement.offsetWidth;
        startHeight = windowElement.offsetHeight;
        startLeft = windowElement.offsetLeft;
        startTop = windowElement.offsetTop;
        
        // Add resize event listeners
        document.addEventListener('mousemove', resize);
        document.addEventListener('mouseup', stopResize);
      });
      
      const resize = (e: MouseEvent) => {
        // Calculate new dimensions and position
        let newWidth = startWidth;
        let newHeight = startHeight;
        let newLeft = startLeft;
        let newTop = startTop;
        
        // Apply resize based on handle position
        if (pos.includes('e')) {
          newWidth = startWidth + (e.clientX - startX);
        }
        if (pos.includes('s')) {
          newHeight = startHeight + (e.clientY - startY);
        }
        if (pos.includes('w')) {
          newWidth = startWidth - (e.clientX - startX);
          newLeft = startLeft + (e.clientX - startX);
        }
        if (pos.includes('n')) {
          newHeight = startHeight - (e.clientY - startY);
          newTop = startTop + (e.clientY - startY);
        }
        
        // Apply minimum size
        const minWidth = 200;
        const minHeight = 120;
        
        if (newWidth < minWidth) {
          if (pos.includes('w')) {
            newLeft = startLeft + startWidth - minWidth;
          }
          newWidth = minWidth;
        }
        
        if (newHeight < minHeight) {
          if (pos.includes('n')) {
            newTop = startTop + startHeight - minHeight;
          }
          newHeight = minHeight;
        }
        
        // Apply new dimensions and position
        windowElement.style.width = `${newWidth}px`;
        windowElement.style.height = `${newHeight}px`;
        windowElement.style.left = `${newLeft}px`;
        windowElement.style.top = `${newTop}px`;
        // Emit resize event
        this.emitWindowEvent('resize', id);
      };
      
      const stopResize = () => {
        document.removeEventListener('mousemove', resize);
        document.removeEventListener('mouseup', stopResize);
        this.emitWindowEvent('resized', id);
      };
      
      windowElement.appendChild(handle);
    });
  }
  /**
   * Activate a window (bring to front)
   */
  public activateWindow(id: string): void {
    // Deactivate all windows
    this.deactivateAllWindows();
    
    // Activate the specified window
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    windowElement.classList.add('window-active');
    windowElement.style.zIndex = (this.zIndex++).toString();
    
    // Update active window ID
    this.activeWindowId = id;
    
    // Update taskbar
    const taskbarItem = document.querySelector(`.taskbar-item[data-window-id="${id}"]`);
    if (taskbarItem) {
      taskbarItem.classList.add('taskbar-item-active');
    }
    
    // Emit focus event when window is activated
    this.emitWindowEvent('focus', id);
  }

  /**
   * Deactivate all windows
   */
  private deactivateAllWindows(): void {
    // Remove active class from all windows
    this.windowElements.forEach(element => {
      element.classList.remove('window-active');
    });
    
    // Remove active class from all taskbar items
    document.querySelectorAll('.taskbar-item').forEach(item => {
      item.classList.remove('taskbar-item-active');
    });
    
    // Clear active window ID
    this.activeWindowId = null;
  }
  /**
   * Minimize a window with animation
   */
  public minimizeWindow(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
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
    
    // Update taskbar
    taskbarItem.classList.remove('taskbar-item-active');
    
    // After animation completes, hide the window
    setTimeout(() => {
      windowElement.classList.add('window-minimized');
      windowElement.style.animation = '';
      document.head.removeChild(style); // Clean up the style
    }, 300);
    
    // Clear active window ID if this window was active
    if (this.activeWindowId === id) {
      this.activeWindowId = null;
    }
    
    // Emit minimize event
    this.emitWindowEvent('minimize', id);
  }

  /**
   * Restore a minimized window
   */
  public restoreWindow(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    // Show the window
    windowElement.classList.remove('window-minimized');
    
    // Activate the window
    this.activateWindow(id);
  }

  /**
   * Toggle maximize/restore window
   */
  public toggleMaximizeWindow(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    // Toggle maximized class
    if (windowElement.classList.contains('window-maximized')) {
      // Restore window
      windowElement.classList.remove('window-maximized');
      
      // Restore original dimensions and position
      const options = this.windows.get(id);
      if (options) {
        windowElement.style.width = `${options.width}px`;
        windowElement.style.height = `${options.height}px`;
        windowElement.style.left = `${options.x}px`;
        windowElement.style.top = `${options.y}px`;
      }
    } else {
      // Save current dimensions and position
      const options = this.windows.get(id);
      if (options) {
        options.width = windowElement.offsetWidth;
        options.height = windowElement.offsetHeight;
        options.x = windowElement.offsetLeft;
        options.y = windowElement.offsetTop;
      }
      
      // Maximize window
      windowElement.classList.add('window-maximized');
      windowElement.style.width = '100%';
      windowElement.style.height = `calc(100% - ${document.getElementById('taskbar')?.offsetHeight || 0}px)`;
      windowElement.style.left = '0';
      windowElement.style.top = '0';
    }
    
    // Activate the window
    this.activateWindow(id);
  }

  /**
   * Close a window
   */
  public closeWindow(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    // Get window options to check for processId
    const windowOptions = this.windows.get(id);
    
    // Remove window element
    windowElement.remove();
    
    // Remove taskbar item
    const taskbarItem = document.querySelector(`.taskbar-item[data-window-id="${id}"]`);
    if (taskbarItem) {
      taskbarItem.remove();
    }
    
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
    
    // Remove window from maps
    this.windowElements.delete(id);
    this.windows.delete(id);
    
    // Clear active window ID if this window was active
    if (this.activeWindowId === id) {
      this.activeWindowId = null;
    }
    
    // Emit window close event
    this.emitWindowEvent('close', id);
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
   */
  public isWindowMinimized(id: string): boolean {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return false;
    
    return windowElement.classList.contains('window-minimized');
  }

  /**
   * Emit a window event
   */
  private emitWindowEvent(type: string, id: string): void {
    const event = new CustomEvent('window', {
      detail: { type, id }
    });
    
    document.dispatchEvent(event);
    
    // Also notify directly registered event handlers
    this.notifyEventListeners(type, id);
  }
  
  // Event handling system
  private eventListeners: Map<string, Array<(id: string) => void>> = new Map();
  
  /**
   * Subscribe to window events
   * @param type Event type (show, minimize, maximize, close, resize, focus)
   * @param callback Function to call when event occurs
   * @returns Unsubscribe function
   */
  public on(type: string, callback: (id: string) => void): () => void {
    if (!this.eventListeners.has(type)) {
      this.eventListeners.set(type, []);
    }
    
    const listeners = this.eventListeners.get(type)!;
    listeners.push(callback);
    
    // Return unsubscribe function
    return () => {
      const index = listeners.indexOf(callback);
      if (index >= 0) {
        listeners.splice(index, 1);
      }
    };
  }
  
  /**
   * Notify all event listeners for a given event type
   */
  private notifyEventListeners(type: string, id: string): void {
    const listeners = this.eventListeners.get(type);
    if (listeners) {
      listeners.forEach(callback => callback(id));
    }
  }

  /**
   * Show a confirmation dialog
   * @param title The dialog title
   * @param message The confirmation message
   * @param callback Function to call with the user's response (true for confirm, false for cancel)
   */
  public showConfirm(title: string, message: string, callback: (confirmed: boolean) => void): void {
    // Create confirmation dialog container
    const dialogContainer = document.createElement('div');
    dialogContainer.className = 'dialog-container';
    
    // Create dialog content
    const dialogContent = document.createElement('div');
    dialogContent.className = 'dialog-content confirmation-dialog';
    
    // Create dialog header
    const dialogHeader = document.createElement('div');
    dialogHeader.className = 'dialog-header';
    dialogHeader.textContent = title;
    
    // Create dialog message
    const dialogMessage = document.createElement('div');
    dialogMessage.className = 'dialog-message';
    dialogMessage.textContent = message;
    
    // Create dialog buttons
    const dialogButtons = document.createElement('div');
    dialogButtons.className = 'dialog-buttons';
    
    // Create cancel button
    const cancelButton = document.createElement('button');
    cancelButton.className = 'dialog-button cancel-button';
    cancelButton.textContent = 'Cancel';
    cancelButton.addEventListener('click', () => {
      document.body.removeChild(dialogContainer);
      callback(false);
    });
    
    // Create confirm button
    const confirmButton = document.createElement('button');
    confirmButton.className = 'dialog-button confirm-button';
    confirmButton.textContent = 'Confirm';
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
  }
}
