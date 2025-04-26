/**
 * Simple event emitter implementation for component communication
 * Supports subscribing to events and emitting events
 */
export class EventEmitter {
  private eventListeners: Map<string, Array<(data: any) => void>> = new Map();

  /**
   * Subscribe to an event
   * @param eventName Name of the event to listen for
   * @param callback Function to call when the event is emitted
   */
  public on(eventName: string, callback: (data: any) => void): void {
    if (!this.eventListeners.has(eventName)) {
      this.eventListeners.set(eventName, []);
    }
    
    this.eventListeners.get(eventName)!.push(callback);
  }

  /**
   * Subscribe to an event, but only trigger the callback once
   * @param eventName Name of the event to listen for
   * @param callback Function to call when the event is emitted
   */
  public once(eventName: string, callback: (data: any) => void): void {
    const onceCallback = (data: any) => {
      this.off(eventName, onceCallback);
      callback(data);
    };
    
    this.on(eventName, onceCallback);
  }

  /**
   * Unsubscribe from an event
   * @param eventName Name of the event to stop listening for
   * @param callback Function to remove from the listeners
   */
  public off(eventName: string, callback: (data: any) => void): void {
    if (!this.eventListeners.has(eventName)) {
      return;
    }
    
    const listeners = this.eventListeners.get(eventName)!;
    const index = listeners.indexOf(callback);
    
    if (index !== -1) {
      listeners.splice(index, 1);
      
      // Clean up empty listener arrays
      if (listeners.length === 0) {
        this.eventListeners.delete(eventName);
      }
    }
  }

  /**
   * Emit an event with optional data
   * @param eventName Name of the event to emit
   * @param data Optional data to pass to listeners
   */
  public emit(eventName: string, data?: any): void {
    if (!this.eventListeners.has(eventName)) {
      return;
    }
    
    // Create a copy of the listeners array to avoid issues if callbacks modify the array
    const listeners = [...this.eventListeners.get(eventName)!];
    
    for (const listener of listeners) {
      try {
        listener(data);
      } catch (err) {
        console.error(`Error in event listener for '${eventName}':`, err);
      }
    }
  }

  /**
   * Remove all event listeners
   * @param eventName Optional event name to remove listeners for. If not provided, all listeners are removed.
   */
  public removeAllListeners(eventName?: string): void {
    if (eventName) {
      this.eventListeners.delete(eventName);
    } else {
      this.eventListeners.clear();
    }
  }

  /**
   * Get the number of listeners for an event
   * @param eventName Name of the event
   * @returns Number of listeners for the event
   */
  public listenerCount(eventName: string): number {
    if (!this.eventListeners.has(eventName)) {
      return 0;
    }
    
    return this.eventListeners.get(eventName)!.length;
  }
}
