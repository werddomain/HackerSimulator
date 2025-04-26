/**
 * WindowDomManager module
 * Handles DOM creation and manipulation for window elements
 */

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
  onTaskbarItemClick: (id: string, isMinimized: boolean, isActive: boolean) => void;
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

  /**
   * Create a new WindowDomManager
   * @param callbacks Callbacks for window events
   */
  constructor(callbacks: WindowDomCallbacks) {
    this.callbacks = callbacks;
  }

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

  private createTaskbarItem(id: string, options: WindowOptions): void {
    if (!this.taskbarItems) return;
    
    const taskbarItem = document.createElement('div');
    taskbarItem.className = 'taskbar-item';
    taskbarItem.dataset.windowId = id;
    taskbarItem.textContent = options.title;
    taskbarItem.addEventListener('click', () => {
      const isMinimized = this.isWindowMinimized(id);
      const isActive = this.isWindowActive(id);
      this.callbacks.onTaskbarItemClick(id, isMinimized, isActive);
    });
    
    this.taskbarItems.appendChild(taskbarItem);
  }

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

  public getWindowElement(id: string): HTMLElement | null {
    return this.windowElements.get(id) || null;
  }

  public getWindowContentElement(id: string): HTMLElement | null {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return null;
    
    return windowElement.querySelector('.window-content');
  }

  public setWindowClass(id: string, className: string, add: boolean): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    if (add) {
      windowElement.classList.add(className);
    } else {
      windowElement.classList.remove(className);
    }
  }

  public updateWindowStyles(id: string, styles: Partial<CSSStyleDeclaration>): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    Object.entries(styles).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        (windowElement.style as any)[key] = value;
      }
    });
  }

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

  public getAllWindowElements(): Map<string, HTMLElement> {
    return new Map(this.windowElements);
  }

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

  public isWindowMinimized(id: string): boolean {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return false;
    
    return windowElement.classList.contains('window-minimized');
  }

  public isWindowMaximized(id: string): boolean {
    const windowElement = this.getWindowElement(id);
    if (!windowElement) return false;
    
    return windowElement.classList.contains('window-maximized');
  }

  public isWindowActive(id: string): boolean {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return false;
    
    return windowElement.classList.contains('window-active');
  }

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

  public minimizeWindow(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    windowElement.classList.add('window-minimized');
    
    // Update taskbar
    const taskbarItem = document.querySelector(`.taskbar-item[data-window-id="${id}"]`);
    if (taskbarItem) {
      taskbarItem.classList.remove('taskbar-item-active');
    }
  }

  public restoreWindow(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    // Show the window
    windowElement.classList.remove('window-minimized');
  }

  public maximizeWindow(id: string): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    windowElement.classList.add('window-maximized');
    windowElement.style.width = '100%';
    windowElement.style.height = `calc(100% - ${document.getElementById('taskbar')?.offsetHeight || 0}px)`;
    windowElement.style.left = '0';
    windowElement.style.top = '0';
  }

  public restoreMaximizedWindow(id: string, originalWidth: number, originalHeight: number, 
                               originalLeft: number, originalTop: number): void {
    const windowElement = this.windowElements.get(id);
    if (!windowElement) return;
    
    windowElement.classList.remove('window-maximized');
    windowElement.style.width = `${originalWidth}px`;
    windowElement.style.height = `${originalHeight}px`;
    windowElement.style.left = `${originalLeft}px`;
    windowElement.style.top = `${originalTop}px`;
  }

  public setWindowContent(id: string, content: HTMLElement | string): void {
    const contentElement = this.getWindowContentElement(id);
    if (!contentElement) return;
    
    // Clear existing content
    contentElement.innerHTML = '';
    
    // Add new content
    if (typeof content === 'string') {
      contentElement.innerHTML = content;
    } else {
      contentElement.appendChild(content);
    }
  }
}
