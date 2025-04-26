  /**
   * Optimize a large list for rendering performance
   * @param listElement - List to optimize
   */
  private optimizeList(listElement: HTMLElement): void {
    if (!listElement || this.virtualizedLists.has(listElement)) return;
    
    // Batch operations for existing list items
    const items = listElement.tagName === 'UL' || listElement.tagName === 'OL'
      ? Array.from(listElement.querySelectorAll('li'))
      : listElement.tagName === 'TABLE'
        ? Array.from(listElement.querySelectorAll('tr'))
        : Array.from(listElement.children);
    
    if (items.length < 5) return; // Don't optimize very small lists
    
    // Check if all items have roughly the same height (good candidate for virtualization)
    const sampleSize = Math.min(5, items.length);
    const heights: number[] = [];
    
    for (let i = 0; i < sampleSize; i++) {
      heights.push((items[i] as HTMLElement).offsetHeight);
    }
    
    // Calculate average height and deviation
    const avgHeight = heights.reduce((sum, h) => sum + h, 0) / heights.length;
    const maxDeviation = Math.max(...heights.map(h => Math.abs(h - avgHeight)));
    
    // If items have consistent height, use virtualization
    if (items.length > 20 && maxDeviation < avgHeight * 0.2) {
      // Get item template
      const itemTemplate = (index: number): HTMLElement => {
        // Clone an existing item as template
        const clone = items[Math.min(index, items.length - 1)].cloneNode(true) as HTMLElement;
        
        // Clear content for placeholder items outside view
        if (index >= items.length) {
          Array.from(clone.querySelectorAll('img')).forEach(img => {
            img.src = '';
            img.removeAttribute('src');
          });
          
          if (clone.childElementCount === 0) {
            clone.textContent = ''; // Clear text for simple items
          }
        }
        
        return clone;
      };
      
      // Create virtualized list
      const virtualizedList = DOMOptimizer.createVirtualizedContainer({
        container: listElement,
        itemHeight: Math.ceil(avgHeight),
        itemTemplate,
        itemCount: items.length,
        renderBuffer: 5
      });
      
      // Store reference to the virtualized list controller
      if (virtualizedList) {
        this.virtualizedLists.set(listElement, virtualizedList);
        registerWeakMapKey(this.virtualizedLists, listElement);
        console.log(`Virtualized list created for ${listElement.tagName}`, listElement);
      }
    } 
    // Otherwise, just optimize the list items for rendering
    else {
      DOMOptimizer.batchElementUpdates(items as HTMLElement[], (item) => {
        if (!this.optimizedElements.has(item)) {
          DOMOptimizer.batchCSSUpdates(item, {
            transform: 'translateZ(0)', 
            willChange: 'auto', // Only enable will-change when needed
            backfaceVisibility: 'hidden'
          });
          this.optimizedElements.add(item);
        }
      });
    }
  }
