  /**
   * Refresh optimizations when window is resized
   */
  private refreshOptimizations(): void {
    // Update virtualized lists using compatibility helper
    forEachWeakMapEntry(this.virtualizedLists, (element, controller) => {
      if (document.contains(element)) {
        controller.refresh();
      } else {
        // Clean up references to removed elements
        this.virtualizedLists.delete(element);
        unregisterWeakMapKey(this.virtualizedLists, element);
      }
    });
    
    // Re-scan for new optimization opportunities
    DOMOptimizer.deferOperation(() => {
      this.scanDOMForOptimizations();
    });
  }
