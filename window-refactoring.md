# WindowManager Refactoring - Implementation Plan

## 1. Create `window-dom.ts` Module

The `WindowDomManager` class will handle all DOM creation and manipulation for windows.

```typescript
// window-dom.ts
import { WindowOptions } from './window';

/**
 * Callbacks for window DOM events
 */
export interface WindowDomCallbacks {
  onMinimize: (id: string) => void;
  onMaximize: (id: string) => void;
  onClose: (id: string) => void;
  onActivate: (id: string) => void;
  onResize: (id: string) => void;
  onResized: (id: string) => void;
}

/**
 * WindowDomManager handles DOM manipulation for window elements
 */
export class WindowDomManager {
  private windowElements: Map<string, HTMLElement> = new Map();
  private windowsContainer: HTMLElement | null = null;
  private taskbarItems: HTMLElement | null = null;
  private callbacks: WindowDomCallbacks;
  private zIndex: number = 100;

  constructor(callbacks: WindowDomCallbacks) {
    this.callbacks = callbacks;
  }

  /**
   * Initialize the DOM manager
   */
  public init(): void {
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
  }

  /**
   * Create window element
   */
  public createWindowElement(id: string, options: WindowOptions): void {
    if (!this.windowsContainer || !this.taskbarItems) return;
    
    // Create window element
    const windowElement = document.createElement('div');
    windowElement.className = 'window';
    windowElement.id = id;
    windowElement.style.width = `${options.width}px`;
    windowElement.style.height = `${options.height}px`;
    windowElement.style.left = `${options.x}px`;
    windowElement.style.top = `${options.y}px`;
    windowElement.style.zIndex = (this.zIndex++).toString();
    
    // Create window header
    const windowHeader = this.createWindowHeader(id, options);
    
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
    
    // Create taskbar item
    this.createTaskbarItem(id, options);
  }

  /**
   * Create window header
   */
  private createWindowHeader(id: string, options: WindowOptions): HTMLElement {
    const windowHeader = document.createElement('div');
    windowHeader.className = 'window-header';
    
    // Create window title
    const windowTitle = document.createElement('div');
    windowTitle.className = 'window-title';
    windowTitle.textContent = options.title;
    
    // Create window controls
    const windowControls = document.createElement('div');
    windowControls.className = 'window-controls';
    
    if (options.minimizable) {
      const minimizeButton = document.createElement('div');
      minimizeButton.className = 'window-control window-minimize';
      minimizeButton.addEventListener('click', () => this.callbacks.onMinimize(id));
      windowControls.appendChild(minimizeButton);
    }
    
    if (options.maximizable) {
      const maximizeButton = document.createElement('div');
      maximizeButton.className = 'window-control window-maximize';
      maximizeButton.addEventListener('click', () => this.callbacks.onMaximize(id));
      windowControls.appendChild(maximizeButton);
    }
    
    if (options.closable) {
      const closeButton = document.createElement('div');
      closeButton.className = 'window-control window-close';
      closeButton.addEventListener('click', () => this.callbacks.onClose(id));
      windowControls.appendChild(closeButton);
    }
    
    // Add elements to window header
    windowHeader.appendChild(windowTitle);
    windowHeader.appendChild(windowControls);
    
    return windowHeader;
  }

  /**
   * Create taskbar item
   */
  private createTaskbarItem(id: string, options: WindowOptions): void {
    if (!this.taskbarItems) return;
    
    const taskbarItem = document.createElement('div');
    taskbarItem.className = 'taskbar-item';
    taskbarItem.dataset.windowId = id;
    taskbarItem.textContent = options.title;
    taskbarItem.addEventListener('click', () => this.callbacks.onActivate(id));
    
    this.taskbarItems.appendChild(taskbarItem);
  }

  /**
   * Make window draggable
   */
  public makeWindowDraggable(id: string, headerElement: HTMLElement): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    let offsetX = 0;
    let offsetY = 0;
    let isDragging = false;
    
    headerElement.addEventListener('mousedown', (e) => {
      // Ignore if clicking on window controls
      if ((e.target as HTMLElement).classList.contains('window-control')) return;
      
      // Activate window
      this.callbacks.onActivate(id);
      
      // Start dragging
      isDragging = true;
      offsetX = e.clientX - windowElement.offsetLeft;
      offsetY = e.clientY - windowElement.offsetTop;
      
      // Prevent text selection during drag
      e.preventDefault();
    });
    
    document.addEventListener('mousemove', (e) => {
      if (!isDragging) return;
      
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
   * Get window element
   */
  public getWindowElement(id: string): HTMLElement | null {
    return this.windowElements.get(id) || null;
  }

  /**
   * Get window content element
   */
  public getWindowContentElement(id: string): HTMLElement | null {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return null;
    
    return windowElement.querySelector('.window-content');
  }

  /**
   * Set window class
   */
  public setWindowClass(id: string, className: string, add: boolean): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    if (add) {
      windowElement.classList.add(className);
    } else {
      windowElement.classList.remove(className);
    }
  }

  /**
   * Update window styles
   */
  public updateWindowStyles(id: string, styles: Partial<CSSStyleDeclaration>): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    Object.entries(styles).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        windowElement.style[key as any] = value;
      }
    });
  }

  /**
   * Update window title
   */
  public updateWindowTitle(id: string, title: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    // Update window title
    const titleElement = windowElement.querySelector('.window-title');
    if (titleElement) {
      titleElement.textContent = title;
    }
    
    // Update taskbar item
    const taskbarItem = document.querySelector(`.taskbar-item[data-window-id="${id}"]`);
    if (taskbarItem) {
      taskbarItem.textContent = title;
    }
  }

  /**
   * Get all window elements
   */
  public getAllWindowElements(): Map<string, HTMLElement> {
    return new Map(this.windowElements);
  }

  /**
   * Remove window element
   */
  public removeWindowElement(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    // Remove window element
    windowElement.remove();
    
    // Remove taskbar item
    const taskbarItem = document.querySelector(`.taskbar-item[data-window-id="${id}"]`);
    if (taskbarItem) {
      taskbarItem.remove();
    }
    
    // Remove element from map
    this.windowElements.delete(id);
  }

  /**
   * Activate window (set active styling)
   */
  public activateWindow(id: string): void {
    // Deactivate all windows
    this.deactivateAllWindows();
    
    // Activate the specified window
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    windowElement.classList.add('window-active');
    windowElement.style.zIndex = (this.zIndex++).toString();
    
    // Update taskbar
    const taskbarItem = document.querySelector(`.taskbar-item[data-window-id="${id}"]`);
    if (taskbarItem) {
      taskbarItem.classList.add('taskbar-item-active');
    }
  }

  /**
   * Deactivate all windows
   */
  public deactivateAllWindows(): void {
    // Remove active class from all windows
    this.windowElements.forEach(element => {
      element.classList.remove('window-active');
    });
    
    // Remove active class from all taskbar items
    document.querySelectorAll('.taskbar-item').forEach(item => {
      item.classList.remove('taskbar-item-active');
    });
  }
}
```

## 2. Create `window-resize.ts` Module

The `WindowResizeHandler` class will manage window resize functionality.

```typescript
// window-resize.ts
/**
 * Interface for resize callbacks
 */
export interface ResizeCallbacks {
  onActivate: (id: string) => void;
  onResize: (id: string) => void;
  onResized: (id: string) => void;
}

/**
 * WindowResizeHandler manages window resize functionality
 */
export class WindowResizeHandler {
  private resizeHandleSize: number = 8;
  
  constructor(private callbacks: ResizeCallbacks) {}
  
  /**
   * Make a window resizable
   */
  public makeWindowResizable(id: string, windowElement: HTMLElement): void {
    // Create resize handles
    const positions = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'];
    positions.forEach(pos => {
      const handle = this.createResizeHandle(pos);
      
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
        this.callbacks.onActivate(id);
        
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
        this.callbacks.onResize(id);
      };
      
      const stopResize = () => {
        document.removeEventListener('mousemove', resize);
        document.removeEventListener('mouseup', stopResize);
        this.callbacks.onResized(id);
      };
      
      windowElement.appendChild(handle);
    });
  }
  
  /**
   * Create a resize handle element
   */
  private createResizeHandle(pos: string): HTMLElement {
    const handle = document.createElement('div');
    handle.className = `window-resize-handle window-resize-${pos}`;
    handle.style.position = 'absolute';
    
    // Set position and size of handle
    switch (pos) {
      case 'n':
        handle.style.top = `-${this.resizeHandleSize / 2}px`;
        handle.style.left = `${this.resizeHandleSize}px`;
        handle.style.right = `${this.resizeHandleSize}px`;
        handle.style.height = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'ns-resize';
        break;
      case 'ne':
        handle.style.top = `-${this.resizeHandleSize / 2}px`;
        handle.style.right = `-${this.resizeHandleSize / 2}px`;
        handle.style.width = `${this.resizeHandleSize}px`;
        handle.style.height = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'nesw-resize';
        break;
      case 'e':
        handle.style.top = `${this.resizeHandleSize}px`;
        handle.style.right = `-${this.resizeHandleSize / 2}px`;
        handle.style.bottom = `${this.resizeHandleSize}px`;
        handle.style.width = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'ew-resize';
        break;
      case 'se':
        handle.style.bottom = `-${this.resizeHandleSize / 2}px`;
        handle.style.right = `-${this.resizeHandleSize / 2}px`;
        handle.style.width = `${this.resizeHandleSize}px`;
        handle.style.height = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'nwse-resize';
        break;
      case 's':
        handle.style.bottom = `-${this.resizeHandleSize / 2}px`;
        handle.style.left = `${this.resizeHandleSize}px`;
        handle.style.right = `${this.resizeHandleSize}px`;
        handle.style.height = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'ns-resize';
        break;
      case 'sw':
        handle.style.bottom = `-${this.resizeHandleSize / 2}px`;
        handle.style.left = `-${this.resizeHandleSize / 2}px`;
        handle.style.width = `${this.resizeHandleSize}px`;
        handle.style.height = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'nesw-resize';
        break;
      case 'w':
        handle.style.top = `${this.resizeHandleSize}px`;
        handle.style.left = `-${this.resizeHandleSize / 2}px`;
        handle.style.bottom = `${this.resizeHandleSize}px`;
        handle.style.width = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'ew-resize';
        break;
      case 'nw':
        handle.style.top = `-${this.resizeHandleSize / 2}px`;
        handle.style.left = `-${this.resizeHandleSize / 2}px`;
        handle.style.width = `${this.resizeHandleSize}px`;
        handle.style.height = `${this.resizeHandleSize}px`;
        handle.style.cursor = 'nwse-resize';
        break;
    }
    
    return handle;
  }
}
```

## 3. Create `window-events.ts` Module

The `WindowEventManager` class will handle the window event system.

```typescript
// window-events.ts
/**
 * WindowEventManager handles the window event system
 */
export class WindowEventManager {
  private eventListeners: Map<string, Array<(id: string) => void>> = new Map();
  
  /**
   * Subscribe to window events
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
   * Emit a window event
   */
  public emit(type: string, id: string): void {
    // Create and dispatch DOM event
    const event = new CustomEvent('window', {
      detail: { type, id }
    });
    
    document.dispatchEvent(event);
    
    // Notify registered event listeners
    this.notifyListeners(type, id);
  }
  
  /**
   * Notify all event listeners for a given event type
   */
  private notifyListeners(type: string, id: string): void {
    const listeners = this.eventListeners.get(type);
    if (listeners) {
      listeners.forEach(callback => callback(id));
    }
  }
}
```

## 4. Create `window-effects.ts` Module

The `WindowEffectsManager` class will handle UI effects like animations, notifications, and dialogs.

```typescript
// window-effects.ts
/**
 * WindowEffectsManager handles UI effects for windows
 */
export class WindowEffectsManager {
  /**
   * Show a notification
   */
  public showNotification(options: { title: string; message: string; type?: 'info' | 'success' | 'warning' | 'error' }): void {
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
   * Create minimize animation
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
   */
  public showPrompt(options: { title: string; message: string; defaultValue?: string; placeholder?: string }, 
                   callback: (value: string | null) => void): void {
    // Implementation...
  }

  /**
   * Show a confirmation dialog
   */
  public showConfirm(options: { title: string; message: string; okText?: string; cancelText?: string }, 
                    callback: (confirmed: boolean) => void): void {
    // Implementation...
  }
}
```

## 5. Update `window.ts` to use new modules

The refactored `WindowManager` class will use the new modules.

```typescript
// window.ts
import { IWindowManager } from './window-manager-interface';
import { WindowDomManager } from './window-dom';
import { WindowResizeHandler } from './window-resize';
import { WindowEventManager } from './window-events';
import { WindowEffectsManager } from './window-effects';

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
export class WindowManager implements IWindowManager {
  private windows: Map<string, WindowOptions> = new Map();
  private activeWindowId: string | null = null;
  
  private domManager: WindowDomManager;
  private resizeHandler: WindowResizeHandler;
  private eventManager: WindowEventManager;
  private effectsManager: WindowEffectsManager;
  
  constructor() {
    this.eventManager = new WindowEventManager();
    
    this.domManager = new WindowDomManager({
      onMinimize: (id) => this.minimizeWindow(id),
      onMaximize: (id) => this.toggleMaximizeWindow(id),
      onClose: (id) => this.closeWindow(id),
      onActivate: (id) => this.activateWindow(id),
      onResize: (id) => this.eventManager.emit('resize', id),
      onResized: (id) => this.eventManager.emit('resized', id)
    });
    
    this.resizeHandler = new WindowResizeHandler({
      onActivate: (id) => this.activateWindow(id),
      onResize: (id) => this.eventManager.emit('resize', id),
      onResized: (id) => this.eventManager.emit('resized', id)
    });
    
    this.effectsManager = new WindowEffectsManager();
  }

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
    
    // Create window elements
    this.domManager.createWindowElement(id, windowOptions);
    
    // Get window header for drag functionality
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

  // Implement rest of interface methods...
  // Each method will delegate to the appropriate manager

  public activateWindow(id: string): void {
    this.domManager.activateWindow(id);
    this.activeWindowId = id;
    this.eventManager.emit('focus', id);
  }
  
  public deactivateAllWindows(): void {
    this.domManager.deactivateAllWindows();
    this.activeWindowId = null;
  }
  
  // etc...
}
```

## 6. Implementation Plan

This refactoring will be implemented in the following phases:

### Phase 1: Extract basic modules
1. Create `window-dom.ts` with `WindowDomManager` class
2. Create `window-resize.ts` with `WindowResizeHandler` class
3. Create `window-events.ts` with `WindowEventManager` class
4. Create `window-effects.ts` with `WindowEffectsManager` class

### Phase 2: Update WindowManager
1. Update `window.ts` to use the new modules
2. Refactor `createWindow` method
3. Refactor window management methods
4. Test basic functionality

### Phase 3: Complete Implementation
1. Implement all remaining interface methods
2. Test thoroughly
3. Update any code that depends on WindowManager

### Phase 4: Documentation
1. Update documentation to reflect new architecture
2. Document public APIs of new modules
