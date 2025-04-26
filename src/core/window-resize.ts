/**
 * WindowResizeHandler module
 * Handles window resize functionality
 */

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
  
  /**
   * Create a new WindowResizeHandler
   * @param callbacks Callbacks for resize events
   */
  constructor(private callbacks: ResizeCallbacks) {}
  
  /**
   * Make a window resizable
   * @param id Window ID
   * @param windowElement Window element
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
   * @param pos Position of the handle (n, ne, e, se, s, sw, w, nw)
   * @returns Resize handle element
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
  
  /**
   * Configure resize handle size
   * @param size Size in pixels
   */
  public setResizeHandleSize(size: number): void {
    this.resizeHandleSize = size;
  }
}
