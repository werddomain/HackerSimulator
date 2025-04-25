/**
 * Mobile Input Handler
 * Central module for managing mobile-specific input handling
 * Integrates virtual keyboard, gesture recognition, and context menus
 */

import { VirtualKeyboard } from './virtual-keyboard';
import { GestureDetector, GestureType, SwipeDirection } from './gesture-detector';
import { TouchContextMenu, ContextMenuItem } from './touch-context-menu';
import { PlatformType, platformDetector } from './platform-detector';

/**
 * Input Handler class
 * Provides centralized access to mobile input systems
 */
export class MobileInputHandler {
  private static instance: MobileInputHandler;
  
  // Store gesture detectors by element ID for reference
  private gestureDetectors: Map<string, GestureDetector> = new Map();
  
  /**
   * Get singleton instance
   */
  public static getInstance(): MobileInputHandler {
    if (!MobileInputHandler.instance) {
      MobileInputHandler.instance = new MobileInputHandler();
    }
    return MobileInputHandler.instance;
  }
  
  /**
   * Private constructor (singleton)
   */
  private constructor() {
    // Initialize input handling components
    this.initializeComponents();
  }
  
  /**
   * Initialize all input handling components
   */
  private initializeComponents(): void {
    // Only initialize on mobile platforms
    if (platformDetector.getPlatformType() !== PlatformType.Mobile) {
      console.log('Mobile input handler skipped on desktop platform');
      return;
    }
    
    console.log('Initializing mobile input handler');
    
    // Initialize virtual keyboard
    const keyboard = VirtualKeyboard.getInstance();
    keyboard.init({
      autoShow: true,
      predictiveText: true
    });
    
    // Add haptic feedback for touch events (if supported)
    this.enableHapticFeedback();
  }
  
  /**
   * Enable haptic feedback for touch events
   */
  private enableHapticFeedback(): void {
    // Check if vibration API is supported
    if (!navigator.vibrate) {
      console.log('Haptic feedback not supported on this device');
      return;
    }
    
    // Add touch feedback to common interactive elements
    document.addEventListener('click', (e) => {
      // Find closest clickable element
      const target = e.target as HTMLElement;
      const clickable = target.closest('button, .btn, .nav-item, [role="button"]');
      
      if (clickable) {
        // Short vibration for button clicks
        navigator.vibrate(10);
      }
    });
  }
  
  /**
   * Get the virtual keyboard instance
   */
  public getKeyboard(): VirtualKeyboard {
    return VirtualKeyboard.getInstance();
  }
  
  /**
   * Create a gesture detector for an element
   */
  public createGestureDetector(
    element: HTMLElement,
    options?: any
  ): GestureDetector {
    // Create a unique ID for the element if it doesn't have one
    if (!element.id) {
      element.id = `gesture-element-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
    }
    
    // Check if detector already exists for this element
    if (this.gestureDetectors.has(element.id)) {
      return this.gestureDetectors.get(element.id)!;
    }
    
    // Create new detector
    const detector = new GestureDetector(element, options);
    
    // Store for reference
    this.gestureDetectors.set(element.id, detector);
    
    return detector;
  }
  
  /**
   * Remove a gesture detector
   */
  public removeGestureDetector(element: HTMLElement): void {
    if (!element.id || !this.gestureDetectors.has(element.id)) {
      return;
    }
    
    // Get detector
    const detector = this.gestureDetectors.get(element.id)!;
    
    // Destroy it
    detector.destroy();
    
    // Remove from map
    this.gestureDetectors.delete(element.id);
  }
  
  /**
   * Show a context menu
   */
  public showContextMenu(
    x: number,
    y: number,
    items: ContextMenuItem[],
    options?: any
  ): void {
    TouchContextMenu.getInstance().show(x, y, items, options);
  }
  
  /**
   * Hide current context menu
   */
  public hideContextMenu(): void {
    TouchContextMenu.getInstance().hide();
  }
  
  /**
   * Add long-press context menu to an element
   */
  public addLongPressMenu(
    element: HTMLElement,
    items: ContextMenuItem[] | (() => ContextMenuItem[]),
    options?: any
  ): void {
    TouchContextMenu.createLongPressHandler(element, items, options);
  }
  
  /**
   * Enable swipe navigation for an element
   * @param element Element to add swipe detection to
   * @param callbacks Object with callback functions for each direction
   */
  public enableSwipeNavigation(
    element: HTMLElement,
    callbacks: {
      onSwipeLeft?: () => void;
      onSwipeRight?: () => void;
      onSwipeUp?: () => void;
      onSwipeDown?: () => void;
    }
  ): GestureDetector {
    // Create gesture detector
    const detector = this.createGestureDetector(element, {
      enableSwipe: true
    });
    
    // Add swipe handler
    detector.on(GestureType.Swipe, (event) => {
      // Cast to any to access direction property
      const swipeEvent = event as any;
      
      // Call appropriate callback based on direction
      switch (swipeEvent.direction) {
        case SwipeDirection.Left:
          if (callbacks.onSwipeLeft) callbacks.onSwipeLeft();
          break;
        case SwipeDirection.Right:
          if (callbacks.onSwipeRight) callbacks.onSwipeRight();
          break;
        case SwipeDirection.Up:
          if (callbacks.onSwipeUp) callbacks.onSwipeUp();
          break;
        case SwipeDirection.Down:
          if (callbacks.onSwipeDown) callbacks.onSwipeDown();
          break;
      }
    });
    
    return detector;
  }
  
  /**
   * Make an element draggable (for mobile)
   */
  public makeDraggable(
    element: HTMLElement,
    options: {
      handle?: HTMLElement;
      bounds?: HTMLElement | 'parent' | 'window';
      axis?: 'x' | 'y' | 'both';
      onDragStart?: (x: number, y: number) => void;
      onDragMove?: (x: number, y: number, dx: number, dy: number) => void;
      onDragEnd?: (x: number, y: number) => void;
    } = {}
  ): void {
    // Default options
    const handle = options.handle || element;
    const axis = options.axis || 'both';
    
    // Initial positions
    let startX = 0;
    let startY = 0;
    let lastX = 0;
    let lastY = 0;
    let isDragging = false;
    
    // Create gesture detector for handle
    const detector = this.createGestureDetector(handle, {
      enablePan: true
    });
    
    // Add pan handlers
    detector.on(GestureType.Pan, (event) => {
      // Cast to any to access specific properties
      const panEvent = event as any;
      
      // Start dragging
      if (panEvent.isFirst) {
        startX = element.offsetLeft;
        startY = element.offsetTop;
        isDragging = true;
        
        // Call start callback if defined
        if (options.onDragStart) {
          options.onDragStart(startX, startY);
        }
      }
      
      // Continue dragging
      if (isDragging && !panEvent.isFinal) {
        // Calculate new position
        let newX = startX + (axis === 'y' ? 0 : panEvent.deltaX);
        let newY = startY + (axis === 'x' ? 0 : panEvent.deltaY);
        
        // Apply constraints if bounds are specified
        if (options.bounds) {
          let boundingRect: DOMRect;
          
          if (options.bounds === 'parent' && element.parentElement) {
            boundingRect = element.parentElement.getBoundingClientRect();
          } else if (options.bounds === 'window') {
            boundingRect = new DOMRect(0, 0, window.innerWidth, window.innerHeight);
          } else if (options.bounds instanceof HTMLElement) {
            boundingRect = options.bounds.getBoundingClientRect();
          } else {
            boundingRect = new DOMRect(0, 0, window.innerWidth, window.innerHeight);
          }
          
          // Apply constraints
          const elementRect = element.getBoundingClientRect();
          
          // Ensure element stays within bounds
          if (newX < 0) newX = 0;
          if (newY < 0) newY = 0;
          if (newX + elementRect.width > boundingRect.width) {
            newX = boundingRect.width - elementRect.width;
          }
          if (newY + elementRect.height > boundingRect.height) {
            newY = boundingRect.height - elementRect.height;
          }
        }
        
        // Update element position
        element.style.left = `${newX}px`;
        element.style.top = `${newY}px`;
        
        // Track last position
        lastX = newX;
        lastY = newY;
        
        // Call move callback if defined
        if (options.onDragMove) {
          options.onDragMove(newX, newY, panEvent.deltaX, panEvent.deltaY);
        }
      }
      
      // End dragging
      if (panEvent.isFinal) {
        isDragging = false;
        
        // Call end callback if defined
        if (options.onDragEnd) {
          options.onDragEnd(lastX, lastY);
        }
      }
    });
  }
  
  /**
   * Cleanup resources
   */
  public cleanup(): void {
    // Clean up all gesture detectors
    this.gestureDetectors.forEach(detector => {
      detector.destroy();
    });
    
    // Clear map
    this.gestureDetectors.clear();
  }
}
