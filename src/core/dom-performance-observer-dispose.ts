  /**
   * Dispose all resources and stop monitoring
   */
  public dispose(): void {
    // Disconnect observers
    this.mutationObserver.disconnect();
    this.lazyLoadObserver.disconnect();
    
    // Remove event listeners
    window.removeEventListener('resize', this.handleResize);
    
    // Clean up scroll containers
    this.scrollContainers.forEach(container => {
      container.removeEventListener('scroll', this.handleScroll);
    });
    
    // Dispose virtualized lists
    forEachWeakMapEntry(this.virtualizedLists, (_, controller) => {
      controller.destroy();
    });
    
    // Clear data structures
    this.optimizedElements = new WeakSet();
    this.updateFrequency.clear();
    this.scrollContainers.clear();
    this.virtualizedLists = new WeakMap();
    
    console.log('DOM Performance Observer disposed');
  }
