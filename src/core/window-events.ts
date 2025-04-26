/**
 * WindowEventManager module
 * Manages window events and subscriptions
 */

/**
 * WindowEventManager handles the window event system
 */
export class WindowEventManager {
  private eventListeners: Map<string, Array<(id: string) => void>> = new Map();
  
  /**
   * Subscribe to window events
   * @param type Event type (show, minimize, maximize, close, resize, focus, etc)
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
   * Emit a window event
   * @param type Event type
   * @param id Window ID
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
   * @param type Event type
   * @param id Window ID
   */
  private notifyListeners(type: string, id: string): void {
    const listeners = this.eventListeners.get(type);
    if (listeners) {
      listeners.forEach(callback => callback(id));
    }
  }
  
  /**
   * Remove all listeners for a specific event type
   * @param type Event type
   */
  public removeAllListeners(type: string): void {
    this.eventListeners.delete(type);
  }
  
  /**
   * Remove all event listeners
   */
  public removeAllEventListeners(): void {
    this.eventListeners.clear();
  }
}
