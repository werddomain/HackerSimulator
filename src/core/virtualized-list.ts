/**
 * Virtualized List Component
 * Efficiently renders large lists by only creating DOM elements for visible items
 */

import * as DOMOptimizer from './dom-optimizer.js';

/**
 * Configuration options for the virtualized list
 */
export interface VirtualizedListOptions {
  // Required options
  container: HTMLElement;        // Container element for the list
  itemHeight: number;            // Fixed height of each item (px)
  itemTemplate: (index: number, data?: any) => HTMLElement; // Function to render an item
  itemCount: number;             // Total number of items in the list
  
  // Optional configuration
  bufferSize?: number;           // Number of items to render above/below viewport (default: 5)
  className?: string;            // CSS class for the virtualized list container
  onScroll?: (scrollTop: number, startIndex: number, endIndex: number) => void; // Scroll callback
  onItemVisible?: (index: number, element: HTMLElement) => void; // Called when an item becomes visible
  optimizeRendering?: boolean;   // Whether to apply rendering optimizations (default: true)
  overscanCount?: number;        // Additional items to render beyond buffer (for smoother scrolling)
  itemData?: any[];              // Optional data array to pass to item template
  estimatedItemHeight?: number;  // Used when itemHeight is variable (average height)
  variableHeights?: boolean;     // Whether items have variable heights
  scrollRestoration?: boolean;   // Restore scroll position on remount
  useWindowScroll?: boolean;     // Use window as scroll container instead of the element
  onItemsRendered?: (visibleStartIndex: number, visibleEndIndex: number) => void; // Callback after render
}

/**
 * Information about the currently rendered state of the virtualized list
 */
export interface VirtualizedListState {
  scrollOffset: number;
  startIndex: number;
  endIndex: number;
  visibleStartIndex: number;
  visibleEndIndex: number;
  isScrolling: boolean;
  renderedItems: number;
  totalItems: number;
}

/**
 * Class for implementing efficient virtualized lists
 */
export class VirtualizedList {
  private options: Required<VirtualizedListOptions>;
  private container: HTMLElement;
  private scrollContainer: HTMLElement | Window;
  private itemContainer!: HTMLElement; // Using definite assignment assertion to ensure initialization
  private containerHeight: number = 0;
  private totalHeight: number = 0;
  private scrollPosition: number = 0;
  private renderedItems: Map<number, HTMLElement> = new Map();
  private renderedRange: { start: number; end: number } = { start: 0, end: 0 };
  private scrollTicking: boolean = false;
  private resizeObserver: ResizeObserver | null = null;
  private intersectionObserver: IntersectionObserver | null = null;
  private lastScrollTop: number = 0;
  private lastScrollDirection: 'up' | 'down' = 'down';
  private scrollRestorer: string | null = null;
  private isDestroyed: boolean = false;
  private scheduledUpdate: number | null = null;
  private measuredHeights: Map<number, number> = new Map();
  private heightCache: number[] = [];
  private itemPositions: number[] = [];
  
  /**
   * Create a new virtualized list
   */
  constructor(options: VirtualizedListOptions) {
    // Set default options
    this.options = {
      container: options.container,
      itemHeight: options.itemHeight,
      itemTemplate: options.itemTemplate,
      itemCount: options.itemCount,
      bufferSize: options.bufferSize || 5,
      className: options.className || 'virtualized-list',
      onScroll: options.onScroll || (() => {}),
      onItemVisible: options.onItemVisible || (() => {}),
      optimizeRendering: options.optimizeRendering !== false,
      overscanCount: options.overscanCount || 3,
      itemData: options.itemData || [],
      estimatedItemHeight: options.estimatedItemHeight || options.itemHeight,
      variableHeights: options.variableHeights || false,
      scrollRestoration: options.scrollRestoration !== false,
      useWindowScroll: options.useWindowScroll || false,
      onItemsRendered: options.onItemsRendered || (() => {})
    };
    
    this.container = options.container;
    this.scrollContainer = options.useWindowScroll ? window : options.container;
    
    // Initialize
    this.initialize();
    
    // Set up scroll position restoration
    if (this.options.scrollRestoration) {
      this.scrollRestorer = `virtualized-list-scroll-${Math.random().toString(36).substring(2, 11)}`;
      this.restoreScrollPosition();
    }
  }
  
  /**
   * Initialize the virtualized list
   */
  private initialize(): void {
    // Prepare container
    this.container.classList.add(this.options.className);
    
    if (!this.options.useWindowScroll) {      this.container.style.position = 'relative';
      this.container.style.overflow = 'auto';
      this.container.style.willChange = 'transform';
      // Use CSS properties with type safety
      (this.container.style as any)['-webkit-overflow-scrolling'] = 'touch'; // For iOS momentum scrolling
    }
    
    // Get container height
    this.containerHeight = this.options.useWindowScroll 
      ? window.innerHeight 
      : this.container.clientHeight;
    
    // Calculate total content height based on item count and height
    this.updateTotalHeight();
    
    // Create spacer to represent the total scroll height
    const spacer = document.createElement('div');
    spacer.className = `${this.options.className}-spacer`;
    spacer.style.height = `${this.totalHeight}px`;
    spacer.style.position = 'relative';
    spacer.style.width = '100%';
    spacer.style.pointerEvents = 'none';
    this.container.appendChild(spacer);
    
    // Create item container for visible items
    this.itemContainer = document.createElement('div');
    this.itemContainer.className = `${this.options.className}-items`;
    this.itemContainer.style.position = 'absolute';
    this.itemContainer.style.top = '0';
    this.itemContainer.style.left = '0';
    this.itemContainer.style.width = '100%';
    this.itemContainer.style.pointerEvents = 'auto';
    
    // Add hardware acceleration for smoother rendering if optimization is enabled
    if (this.options.optimizeRendering) {
      DOMOptimizer.optimizeForAnimation(this.itemContainer);
    }
    
    this.container.appendChild(this.itemContainer);
    
    // Set up scroll handler
    this.setupScrollHandler();
    
    // Set up resize handler
    this.setupResizeHandler();
      // Set up intersection observer if needed
    // Check if the callback is not the default empty function
    const hasVisibilityHandler = 
      this.options.onItemVisible && 
      this.options.onItemVisible.toString() !== (() => {}).toString();
      
    if (hasVisibilityHandler) {
      this.setupIntersectionObserver();
    }
    
    // Precalculate item positions for variable height mode
    if (this.options.variableHeights) {
      this.precalculateItemPositions();
    }
    
    // Initial render
    this.updateVisibleItems();
  }
  
  /**
   * Set up scroll event handler
   */
  private setupScrollHandler(): void {
    const handleScroll = () => {
      if (this.scrollTicking || this.isDestroyed) return;
      
      this.scrollTicking = true;
      requestAnimationFrame(() => {
        const scrollTop = this.getScrollTop();
        const direction = scrollTop > this.lastScrollTop ? 'down' : 'up';
        
        // Update scroll direction if it changed
        if (direction !== this.lastScrollDirection) {
          this.lastScrollDirection = direction;
        }
        
        this.scrollPosition = scrollTop;
        this.updateVisibleItems();
        
        // Call onScroll callback with current state
        const { start, end } = this.renderedRange;
        this.options.onScroll(scrollTop, start, end);
        
        // Save last scroll position for direction detection
        this.lastScrollTop = scrollTop;
        this.scrollTicking = false;
        
        // Save scroll position if restoration is enabled
        if (this.options.scrollRestoration && this.scrollRestorer) {
          this.saveScrollPosition();
        }
      });
    };
    
    // Use passive event listener to improve scroll performance
    DOMOptimizer.addPassiveEventListener(
      this.scrollContainer as HTMLElement, 
      'scroll', 
      handleScroll
    );
  }
  
  /**
   * Set up resize observer to update on container resizing
   */
  private setupResizeHandler(): void {
    // Use optimal resize observer with debounce
    this.resizeObserver = DOMOptimizer.optimizedResizeObserver(
      this.options.useWindowScroll ? document.documentElement : this.container,
      (entry) => {
        // Update container height
        this.containerHeight = this.options.useWindowScroll 
          ? window.innerHeight 
          : this.container.clientHeight;
        
        // Update visible items
        this.updateVisibleItems();
      },
      100 // 100ms debounce
    );
  }
  
  /**
   * Set up intersection observer to detect when items enter/exit viewport
   */
  private setupIntersectionObserver(): void {
    this.intersectionObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            const element = entry.target as HTMLElement;
            const index = parseInt(element.dataset.index || '-1', 10);
            
            if (index !== -1) {
              this.options.onItemVisible(index, element);
            }
          }
        });
      },
      {
        root: this.options.useWindowScroll ? null : this.container,
        rootMargin: '50px 0px',
        threshold: 0.1
      }
    );
  }
  
  /**
   * Update the total height of the list based on item count
   */
  private updateTotalHeight(): void {
    if (this.options.variableHeights && this.heightCache.length > 0) {
      // Use measured heights for items we've seen
      let totalHeight = 0;
      
      for (let i = 0; i < this.options.itemCount; i++) {
        totalHeight += this.heightCache[i] || this.options.estimatedItemHeight;
      }
      
      this.totalHeight = totalHeight;
    } else {
      // Use fixed height
      this.totalHeight = this.options.itemCount * this.options.itemHeight;
    }
    
    // Update spacer height
    const spacer = this.container.querySelector(`.${this.options.className}-spacer`) as HTMLElement;
    if (spacer) {
      spacer.style.height = `${this.totalHeight}px`;
    }
  }
  
  /**
   * Precalculate item positions for variable height mode
   */
  private precalculateItemPositions(): void {
    this.itemPositions = [0]; // First item starts at 0
    
    for (let i = 1; i < this.options.itemCount; i++) {
      const prevHeight = this.heightCache[i - 1] || this.options.estimatedItemHeight;
      this.itemPositions[i] = this.itemPositions[i - 1] + prevHeight;
    }
  }
  
  /**
   * Get the current scroll position
   */
  private getScrollTop(): number {
    return this.options.useWindowScroll 
      ? window.pageYOffset || document.documentElement.scrollTop
      : this.container.scrollTop;
  }
  
  /**
   * Calculate the visible range of items based on current scroll position
   */
  private calculateVisibleRange(): { start: number; end: number } {
    const scrollTop = this.scrollPosition;
    const viewportHeight = this.containerHeight;
    
    let startIndex, endIndex;
    
    if (this.options.variableHeights) {
      // Binary search for the closest item based on scroll position
      startIndex = this.binarySearchClosestIndex(scrollTop);
      
      // Find end index by scanning from start position
      let currentHeight = this.itemPositions[startIndex];
      endIndex = startIndex;
      
      while (
        endIndex < this.options.itemCount - 1 && 
        currentHeight < scrollTop + viewportHeight
      ) {
        endIndex++;
        currentHeight += this.heightCache[endIndex] || this.options.estimatedItemHeight;
      }
    } else {
      // Simple calculation for fixed heights
      startIndex = Math.floor(scrollTop / this.options.itemHeight);
      endIndex = Math.min(
        this.options.itemCount - 1,
        Math.floor((scrollTop + viewportHeight) / this.options.itemHeight)
      );
    }
    
    // Add buffer and overscan
    startIndex = Math.max(0, startIndex - this.options.bufferSize - 
      (this.lastScrollDirection === 'up' ? this.options.overscanCount : 0));
      
    endIndex = Math.min(
      this.options.itemCount - 1,
      endIndex + this.options.bufferSize + 
      (this.lastScrollDirection === 'down' ? this.options.overscanCount : 0)
    );
    
    return { start: startIndex, end: endIndex };
  }
  
  /**
   * Binary search for the closest item index based on scroll position
   * Used for variable height mode
   */
  private binarySearchClosestIndex(scrollTop: number): number {
    let low = 0;
    let high = this.options.itemCount - 1;
    
    while (low <= high) {
      const mid = Math.floor((low + high) / 2);
      const midPosition = this.itemPositions[mid];
      
      if (midPosition < scrollTop) {
        low = mid + 1;
      } else if (midPosition > scrollTop) {
        high = mid - 1;
      } else {
        return mid;
      }
    }
    
    // Return the closest index
    return Math.max(0, high);
  }
  
  /**
   * Update the visible items based on current scroll position
   */
  private updateVisibleItems(): void {
    if (this.isDestroyed) return;
    
    const { start, end } = this.calculateVisibleRange();
    const visibleStartIndex = Math.max(0, Math.floor(this.scrollPosition / this.options.itemHeight));
    const visibleEndIndex = Math.min(
      this.options.itemCount - 1,
      Math.floor((this.scrollPosition + this.containerHeight) / this.options.itemHeight)
    );
    
    // Only update if the visible range has changed
    if (
      start !== this.renderedRange.start ||
      end !== this.renderedRange.end ||
      this.renderedItems.size === 0
    ) {
      // Update rendered range
      this.renderedRange = { start, end };
      
      // Keep track of items to remove
      const itemsToRemove = new Set(this.renderedItems.keys());
      
      // Create document fragment for batched DOM updates
      const fragment = document.createDocumentFragment();
      
      // Render visible items
      for (let i = start; i <= end; i++) {
        // Skip if item is already rendered
        if (this.renderedItems.has(i)) {
          itemsToRemove.delete(i);
          continue;
        }
        
        // Get data for this item if available
        const itemData = this.options.itemData[i];
        
        // Create item element
        const item = this.options.itemTemplate(i, itemData);
        
        // Set position and data attributes
        item.dataset.index = i.toString();
        item.style.position = 'absolute';
        
        if (this.options.variableHeights) {
          item.style.top = `${this.itemPositions[i] || 0}px`;
          
          // Listen for changes in item height
          this.observeItemHeight(item, i);
        } else {
          item.style.top = `${i * this.options.itemHeight}px`;
          item.style.height = `${this.options.itemHeight}px`;
        }
        
        item.style.width = '100%';
        
        // Add to document fragment
        fragment.appendChild(item);
        
        // Store reference to rendered item
        this.renderedItems.set(i, item);
        
        // Observe for visibility if needed
        if (this.intersectionObserver) {
          this.intersectionObserver.observe(item);
        }
      }
      
      // Add new items to DOM
      this.itemContainer.appendChild(fragment);
      
      // Remove items that are no longer visible
      itemsToRemove.forEach(index => {
        const item = this.renderedItems.get(index);
        if (item) {
          if (this.intersectionObserver) {
            this.intersectionObserver.unobserve(item);
          }
          this.itemContainer.removeChild(item);
          this.renderedItems.delete(index);
        }
      });
      
      // Call onItemsRendered callback
      this.options.onItemsRendered(visibleStartIndex, visibleEndIndex);
    }
  }
  
  /**
   * Observe item height changes for variable height mode
   */
  private observeItemHeight(item: HTMLElement, index: number): void {
    // Initial measurement
    setTimeout(() => {
      if (this.isDestroyed || !document.body.contains(item)) return;
      
      const height = item.offsetHeight;
      this.updateItemHeight(index, height);
    }, 0);
    
    // Create a ResizeObserver to detect height changes
    const resizeObserver = new ResizeObserver(entries => {
      const entry = entries[0];
      if (entry && entry.target === item) {
        const height = entry.contentRect.height;
        this.updateItemHeight(index, height);
      }
    });
    
    resizeObserver.observe(item);
    
    // Clean up observer when item is removed
    const mutationObserver = new MutationObserver(mutations => {
      mutations.forEach(mutation => {
        if (mutation.type === 'childList' && 
            Array.from(mutation.removedNodes).includes(item)) {
          resizeObserver.disconnect();
          mutationObserver.disconnect();
        }
      });
    });
    
    mutationObserver.observe(this.itemContainer, { childList: true });
  }
  
  /**
   * Update the stored height for an item and recalculate positions
   */
  private updateItemHeight(index: number, height: number): void {
    if (!this.options.variableHeights || this.heightCache[index] === height) return;
    
    this.heightCache[index] = height;
    this.measuredHeights.set(index, height);
    
    // Debounce recalculation to avoid too frequent updates
    if (this.scheduledUpdate !== null) {
      cancelAnimationFrame(this.scheduledUpdate);
    }
    
    this.scheduledUpdate = requestAnimationFrame(() => {
      this.scheduledUpdate = null;
      this.precalculateItemPositions();
      this.updateTotalHeight();
      this.updateVisibleItems();
    });
  }
  
  /**
   * Save the current scroll position for restoration
   */
  private saveScrollPosition(): void {
    if (!this.scrollRestorer) return;
    sessionStorage.setItem(this.scrollRestorer, this.scrollPosition.toString());
  }
  
  /**
   * Restore the saved scroll position
   */
  private restoreScrollPosition(): void {
    if (!this.scrollRestorer) return;
    
    const savedPosition = sessionStorage.getItem(this.scrollRestorer);
    if (savedPosition) {
      const position = parseInt(savedPosition, 10);
      setTimeout(() => {
        this.scrollToPosition(position);
      }, 0);
    }
  }
  
  /**
   * Public API methods
   */
  
  /**
   * Refresh the list (re-render all items)
   */
  public refresh(): void {
    // Clear currently rendered items
    this.renderedItems.forEach((item, index) => {
      if (this.intersectionObserver) {
        this.intersectionObserver.unobserve(item);
      }
      
      if (item.parentNode === this.itemContainer) {
        this.itemContainer.removeChild(item);
      }
    });
    
    this.renderedItems.clear();
    this.renderedRange = { start: 0, end: 0 };
    
    // Update container height
    this.containerHeight = this.options.useWindowScroll 
      ? window.innerHeight 
      : this.container.clientHeight;
    
    // Update total height and recalculate item positions
    if (this.options.variableHeights) {
      this.precalculateItemPositions();
    }
    
    this.updateTotalHeight();
    
    // Re-render visible items
    this.updateVisibleItems();
  }
  
  /**
   * Update the total number of items
   */
  public updateItemCount(newCount: number): void {
    this.options.itemCount = newCount;
    
    // Update total height
    this.updateTotalHeight();
    
    // Update visible items
    this.updateVisibleItems();
  }
  
  /**
   * Scroll to a specific item by index
   */
  public scrollToIndex(index: number, behavior: ScrollBehavior = 'auto'): void {
    if (index < 0 || index >= this.options.itemCount) return;
    
    let position;
    
    if (this.options.variableHeights) {
      position = this.itemPositions[index] || 0;
    } else {
      position = index * this.options.itemHeight;
    }
    
    this.scrollToPosition(position, behavior);
  }
  
  /**
   * Scroll to a specific position
   */
  public scrollToPosition(position: number, behavior: ScrollBehavior = 'auto'): void {
    if (this.options.useWindowScroll) {
      window.scrollTo({
        top: position,
        behavior
      });
    } else {
      this.container.scrollTo({
        top: position,
        behavior
      });
    }
  }
  
  /**
   * Get the current state of the virtualized list
   */
  public getState(): VirtualizedListState {
    const { start, end } = this.renderedRange;
    const visibleStartIndex = Math.max(0, Math.floor(this.scrollPosition / this.options.itemHeight));
    const visibleEndIndex = Math.min(
      this.options.itemCount - 1,
      Math.floor((this.scrollPosition + this.containerHeight) / this.options.itemHeight)
    );
    
    return {
      scrollOffset: this.scrollPosition,
      startIndex: start,
      endIndex: end,
      visibleStartIndex,
      visibleEndIndex,
      isScrolling: this.scrollTicking,
      renderedItems: this.renderedItems.size,
      totalItems: this.options.itemCount
    };
  }
  
  /**
   * Update the item data array
   */
  public updateItemData(newData: any[]): void {
    this.options.itemData = newData;
    this.refresh();
  }
  
  /**
   * Clean up resources
   */
  public destroy(): void {
    this.isDestroyed = true;
    
    // Clean up resize observer
    if (this.resizeObserver) {
      this.resizeObserver.disconnect();
      this.resizeObserver = null;
    }
    
    // Clean up intersection observer
    if (this.intersectionObserver) {
      this.intersectionObserver.disconnect();
      this.intersectionObserver = null;
    }
    
    // Remove scroll handler
    if (this.scrollContainer) {
      this.scrollContainer.removeEventListener('scroll', this.setupScrollHandler);
    }
    
    // Remove rendered items
    this.renderedItems.clear();
    
    // Save scroll position if enabled
    if (this.options.scrollRestoration && this.scrollRestorer) {
      this.saveScrollPosition();
    }
  }
}
