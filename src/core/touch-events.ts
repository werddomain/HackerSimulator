/**
 * Touch events helper module
 * Provides type definitions and utilities for touch event handling
 */

/**
 * Extends the standard TouchEvent interface with any additional properties
 * we might need for our mobile implementation
 */
export interface EnhancedTouchEvent extends TouchEvent {
  // Add any custom properties here if needed
}

/**
 * Helper function to add touch event handlers with correct typing
 * @param element DOM element to attach the handler to
 * @param eventName Touch event name (touchstart, touchmove, touchend, touchcancel)
 * @param handler Event handler function
 * @param options Optional addEventListener options
 */
export function addTouchEventListener(
  element: HTMLElement | Document | null,
  eventName: 'touchstart' | 'touchmove' | 'touchend' | 'touchcancel',
  handler: (e: TouchEvent) => void,
  options?: AddEventListenerOptions | boolean
): void {
  if (!element) return;
  
  element.addEventListener(eventName, ((e: Event) => {
    handler(e as TouchEvent);
  }) as EventListener, options);
}

/**
 * Helper function to get the first touch point coordinates
 * @param e Touch event
 * @returns Coordinates object with clientX and clientY properties
 */
export function getTouchCoordinates(e: TouchEvent): { clientX: number, clientY: number } {
  if (e.touches && e.touches.length > 0) {
    return {
      clientX: e.touches[0].clientX,
      clientY: e.touches[0].clientY
    };
  }
  
  return {
    clientX: 0,
    clientY: 0
  };
}

/**
 * Check if touch events are supported in the current browser
 * @returns True if touch events are supported
 */
export function isTouchSupported(): boolean {
  return 'ontouchstart' in window || 
         navigator.maxTouchPoints > 0 ||
         (navigator as any).msMaxTouchPoints > 0;
}

/**
 * Prevent default and stop propagation of a touch event
 * @param e Touch event
 */
export function preventTouchDefault(e: TouchEvent): void {
  e.preventDefault();
  e.stopPropagation();
}
