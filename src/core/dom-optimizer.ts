/**
 * DOM Optimization Module
 * Provides utilities for optimizing DOM operations on mobile devices
 * to minimize reflows and repaints and improve rendering performance
 */

/**
 * Batch DOM operations to reduce reflows and repaints
 * @param updateFn - Function that performs DOM updates
 */
export function batchDOMUpdates(updateFn: () => void): void {
  // Use requestAnimationFrame to ensure updates happen during the next repaint
  requestAnimationFrame(() => {
    // Measure anything that would cause a reflow before making changes
    updateFn();
  });
}

/**
 * Create and return a document fragment for batching DOM insertions
 * @returns A document fragment for batched insertions
 */
export function createBatchFragment(): DocumentFragment {
  return document.createDocumentFragment();
}

/**
 * Optimizes an element for animations and transformations
 * by promoting it to its own compositor layer
 * @param element - Element to optimize
 */
export function optimizeForAnimation(element: HTMLElement): void {
  if (!element) return;
  
  // Promote to GPU layer with will-change
  element.style.willChange = 'transform, opacity';
  
  // Force hardware acceleration
  element.style.transform = 'translateZ(0)';
}

/**
 * Reset optimization properties when animation is complete
 * to free up memory and GPU resources
 * @param element - Element to reset
 */
export function resetOptimization(element: HTMLElement): void {
  if (!element) return;
  
  // Reset will-change after animations complete
  element.style.willChange = 'auto';
}

/**
 * Throttles a function to avoid excessive DOM updates
 * @param fn - Function to throttle
 * @param delay - Throttle delay in ms
 * @returns Throttled function
 */
export function throttle<T extends (...args: any[]) => any>(
  fn: T, 
  delay: number = 16
): (...args: Parameters<T>) => ReturnType<T> | undefined {
  let lastCall = 0;
  let timeout: number | null = null;
  
  return function(this: any, ...args: Parameters<T>): ReturnType<T> | undefined {
    const now = Date.now();
    const timeSinceLastCall = now - lastCall;
    
    if (timeSinceLastCall >= delay) {
      lastCall = now;
      return fn.apply(this, args);
    } else {
      if (timeout !== null) {
        clearTimeout(timeout);
      }
      timeout = window.setTimeout(() => {
        lastCall = Date.now();
        fn.apply(this, args);
      }, delay - timeSinceLastCall);
      return undefined;
    }
  };
}

/**
 * Debounces a function to reduce frequent updates
 * @param fn - Function to debounce
 * @param delay - Debounce delay in ms
 * @returns Debounced function
 */
export function debounce<T extends (...args: any[]) => any>(
  fn: T, 
  delay: number = 150
): (...args: Parameters<T>) => void {
  let timeout: number | null = null;
  
  return function(this: any, ...args: Parameters<T>): void {
    if (timeout !== null) {
      clearTimeout(timeout);
    }
    
    timeout = window.setTimeout(() => {
      fn.apply(this, args);
      timeout = null;
    }, delay);
  };
}

/**
 * Create a virtualized list view for large data sets
 * Only renders the visible items and a small buffer
 * @param container - Container element for the list
 * @param items - Array of items to render
 * @param renderItem - Function to render a single item
 * @param itemHeight - Height of each item (fixed height required)
 * @param bufferItems - Number of buffer items to render outside viewport
 */
export function createVirtualizedList<T>(
  container: HTMLElement,
  items: T[],
  renderItem: (item: T, index: number) => HTMLElement,
  itemHeight: number,
  bufferItems: number = 5
): {
  refresh: () => void;
  scrollTo: (index: number) => void;
} {
  if (!container) return { refresh: () => {}, scrollTo: () => {} };
  
  // Create inner container that will have the full height
  const innerContainer = document.createElement('div');
  innerContainer.style.position = 'relative';
  innerContainer.style.width = '100%';
  innerContainer.style.height = `${items.length * itemHeight}px`;
  
  // Create container for visible items
  const visibleItemsContainer = document.createElement('div');
  visibleItemsContainer.style.position = 'absolute';
  visibleItemsContainer.style.width = '100%';
  visibleItemsContainer.style.left = '0';
  
  innerContainer.appendChild(visibleItemsContainer);
  container.appendChild(innerContainer);
  
  // Set container style for scrolling
  container.style.overflowY = 'auto';
  container.style.position = 'relative';
  
  // Keep track of rendered items
  const renderedItems: Map<number, HTMLElement> = new Map();
  let visibleStartIndex = 0;
  let visibleEndIndex = 0;
  
  // Render visible items
  const renderVisibleItems = () => {
    const scrollTop = container.scrollTop;
    const containerHeight = container.clientHeight;
    
    // Calculate visible range
    const newVisibleStartIndex = Math.max(0, Math.floor(scrollTop / itemHeight) - bufferItems);
    const newVisibleEndIndex = Math.min(
      items.length - 1,
      Math.ceil((scrollTop + containerHeight) / itemHeight) + bufferItems
    );
    
    // Remove items that are no longer visible
    for (const [index, element] of renderedItems.entries()) {
      if (index < newVisibleStartIndex || index > newVisibleEndIndex) {
        element.remove();
        renderedItems.delete(index);
      }
    }
    
    // Add newly visible items
    for (let i = newVisibleStartIndex; i <= newVisibleEndIndex; i++) {
      if (!renderedItems.has(i) && i >= 0 && i < items.length) {
        const item = items[i];
        const element = renderItem(item, i);
        element.style.position = 'absolute';
        element.style.top = `${i * itemHeight}px`;
        element.style.width = '100%';
        element.style.height = `${itemHeight}px`;
        visibleItemsContainer.appendChild(element);
        renderedItems.set(i, element);
      }
    }
    
    visibleStartIndex = newVisibleStartIndex;
    visibleEndIndex = newVisibleEndIndex;
  };
  
  // Initial render
  renderVisibleItems();
  
  // Add scroll handler with throttling
  const throttledRenderVisibleItems = throttle(renderVisibleItems, 16);
  container.addEventListener('scroll', throttledRenderVisibleItems);
  
  // Handle window resize
  window.addEventListener('resize', throttledRenderVisibleItems);
  
  return {
    refresh: renderVisibleItems,
    scrollTo: (index: number) => {
      if (index >= 0 && index < items.length) {
        container.scrollTop = index * itemHeight;
      }
    }
  };
}

/**
 * Create a skeleton loading screen for content that's loading
 * @param container - Container to add the skeleton to
 * @param template - Template for the skeleton layout (HTML string or element)
 * @returns Object with show and hide methods
 */
export function createSkeletonScreen(
  container: HTMLElement,
  template: string | HTMLElement
): { show: () => void; hide: () => void } {
  // Create skeleton container
  const skeletonContainer = document.createElement('div');
  skeletonContainer.className = 'skeleton-container';
  skeletonContainer.style.width = '100%';
  skeletonContainer.style.height = '100%';
  
  // Add pulse animation
  const style = document.createElement('style');
  style.textContent = `
    .skeleton-container .skeleton-item {
      background: linear-gradient(90deg, rgba(0,0,0,0.06) 25%, rgba(0,0,0,0.15) 50%, rgba(0,0,0,0.06) 75%);
      background-size: 200% 100%;
      animation: skeleton-pulse 1.5s ease-in-out infinite;
      border-radius: 4px;
    }
    
    @keyframes skeleton-pulse {
      0% {
        background-position: 200% 0;
      }
      100% {
        background-position: -200% 0;
      }
    }
  `;
  
  document.head.appendChild(style);
  
  // Add template content
  if (typeof template === 'string') {
    skeletonContainer.innerHTML = template;
  } else {
    skeletonContainer.appendChild(template.cloneNode(true));
  }
  
  // Find all elements to apply skeleton style
  const elements = skeletonContainer.querySelectorAll('.skeleton-item');
  elements.forEach(el => {
    if (el instanceof HTMLElement) {
      el.style.minHeight = '1em';
    }
  });
  
  return {
    show: () => {
      container.appendChild(skeletonContainer);
    },
    hide: () => {
      skeletonContainer.remove();
    }
  };
}

/**
 * Optimize layout for mobile by deferring non-critical rendering
 * @param priorityElements - Array of critical elements to render immediately
 * @param deferredElements - Array of non-critical elements to defer
 * @param delay - Milliseconds to defer rendering (0 = requestIdleCallback)
 */
export function optimizeMobileLayout(
  priorityElements: HTMLElement[],
  deferredElements: HTMLElement[],
  delay: number = 0
): void {
  // Immediately display priority elements
  priorityElements.forEach(element => {
    element.style.visibility = 'visible';
  });
  
  // Hide deferred elements initially
  deferredElements.forEach(element => {
    element.style.visibility = 'hidden';
  });
  
  const renderDeferred = () => {
    // Render non-critical elements
    deferredElements.forEach(element => {
      element.style.visibility = 'visible';
    });
  };
  
  if (delay === 0 && 'requestIdleCallback' in window) {
    // Use requestIdleCallback when browser is idle
    (window as any).requestIdleCallback(renderDeferred);
  } else {
    // Fallback to setTimeout
    setTimeout(renderDeferred, delay || 100);
  }
}

/**
 * Optimize animations for mobile performance by reducing complexity
 * @param elements - Elements with animations to optimize
 * @param reduceMotion - Whether to apply reduced motion
 */
export function optimizeMobileAnimations(elements: HTMLElement[], reduceMotion: boolean = false): void {
  elements.forEach(element => {
    // Promote to GPU layer
    optimizeForAnimation(element);
    
    if (reduceMotion) {
      // Apply reduced motion
      element.style.animationDuration = '0.001s';
      element.style.transitionDuration = '0.001s';
    } else {
      // Optimize existing animations
      element.style.animationTimingFunction = 'cubic-bezier(0.1, 0.7, 1.0, 0.1)';
      element.style.transitionTimingFunction = 'cubic-bezier(0.1, 0.7, 1.0, 0.1)';
    }
  });
}

/**
 * Detect and suggest optimizations for DOM elements
 * @param container - Container to scan for optimization opportunities
 * @returns Array of optimization suggestions
 */
export function detectOptimizationOpportunities(container: HTMLElement): Array<{
  element: HTMLElement;
  issue: string;
  suggestion: string;
}> {
  const suggestions: Array<{
    element: HTMLElement;
    issue: string;
    suggestion: string;
  }> = [];
  
  // Find elements that might cause layout thrashing
  const potentialIssues = container.querySelectorAll('*');
  
  potentialIssues.forEach(element => {
    if (!(element instanceof HTMLElement)) return;
    
    const style = window.getComputedStyle(element);
    
    // Check for expensive box-shadow on animated elements
    if (style.boxShadow !== 'none' && 
        (style.transition.includes('transform') || style.animation !== 'none')) {
      suggestions.push({
        element,
        issue: 'Box shadow with animation',
        suggestion: 'Remove box-shadow from animated elements or use optimizeForAnimation'
      });
    }
    
    // Check for expensive filters
    if (style.filter && style.filter !== 'none') {
      suggestions.push({
        element,
        issue: 'Expensive filter property',
        suggestion: 'Replace with optimized CSS or pre-rendered images'
      });
    }
    
    // Check for large images without dimensions
    if (element instanceof HTMLImageElement && 
        (!element.width || !element.height)) {
      suggestions.push({
        element,
        issue: 'Image without explicit dimensions',
        suggestion: 'Add width and height attributes to prevent layout shifts'
      });
    }
    
    // Check for expensive text-shadow on large text blocks
    if (style.textShadow !== 'none' && 
        element.textContent && 
        element.textContent.length > 100) {
      suggestions.push({
        element,
        issue: 'Text shadow on large text block',
        suggestion: 'Remove text-shadow or split into smaller elements'
      });
    }
  });
  
  return suggestions;
}

/**
 * Clear memory leaks from DOM elements
 * @param elements - Elements to clean up
 */
export function cleanupDOMElements(elements: HTMLElement[]): void {
  elements.forEach(element => {
    // Remove event listeners using cloneNode trick
    const clone = element.cloneNode(false) as HTMLElement;
    if (element.parentNode) {
      element.parentNode.replaceChild(clone, element);
    }
    
    // Clear internal properties
    for (const prop in element) {
      if ((element as any)[prop] instanceof Object) {
        (element as any)[prop] = null;
      }
    }  });
}



/**
 * Apply CSS properties in batch to minimize reflows
 * @param element - The element to update
 * @param properties - CSS properties to set
 */
export function batchCSSUpdates(
  element: HTMLElement, 
  properties: Record<string, string>
): void {
  if (!element) return;
  
  requestAnimationFrame(() => {
    for (const prop in properties) {
      element.style[prop as any] = properties[prop];
    }
  });
}

/**
 * Configuration for virtualized container
 */
export interface VirtualizedContainerConfig {
  container: HTMLElement;       // Container element
  itemHeight: number;          // Height of each item in pixels
  itemTemplate: (index: number) => HTMLElement; // Function to create item elements
  itemCount: number;           // Total number of items
  renderBuffer?: number;       // Number of items to render outside visible area
}

/**
 * Virtualized container controller
 */
export interface VirtualizedContainer {
  refresh: () => void;
  scrollToIndex: (index: number) => void;
  updateItemCount: (newCount: number) => void;
  destroy: () => void;
}

/**
 * Creates a virtualized container for efficiently rendering large lists
 * Only renders items that are visible in the viewport
 * @param config - Configuration object
 * @returns Virtualized list controller
 */
export function createVirtualizedContainer(config: VirtualizedContainerConfig): VirtualizedContainer | null {
  const {
    container,       // Container element
    itemHeight,      // Height of each item in pixels
    itemTemplate,    // Function to create item elements
    renderBuffer = 5 // Number of items to render outside visible area
  } = config;
  
  // Extract itemCount separately so we can update it
  let itemCount = config.itemCount;
  
  if (!container || !itemHeight || !itemTemplate) {
    console.error('Missing required configuration for virtualized container');
    return null;
  }
  
  let scrollPosition = 0;
  let renderedRange = { start: 0, end: 0 };
  
  // Initialize container
  container.style.position = 'relative';
  container.style.overflow = 'auto';
  container.style.willChange = 'transform';
  
  // Create spacer to represent total scroll height
  const spacer = document.createElement('div');
  spacer.style.height = `${itemCount * itemHeight}px`;
  spacer.style.position = 'relative';
  container.appendChild(spacer);
  
  // Create item container for visible items
  const itemContainer = document.createElement('div');
  itemContainer.style.position = 'absolute';
  itemContainer.style.top = '0';
  itemContainer.style.left = '0';
  itemContainer.style.width = '100%';
  container.appendChild(itemContainer);
  
  // Calculate visible range based on scroll position
  function calculateVisibleRange(): { start: number; end: number } {
    const containerHeight = container.clientHeight;
    const startIndex = Math.max(0, Math.floor(scrollPosition / itemHeight) - renderBuffer);
    const endIndex = Math.min(
      itemCount - 1,
      Math.ceil((scrollPosition + containerHeight) / itemHeight) + renderBuffer
    );
    
    return { start: startIndex, end: endIndex };
  }
  
  // Render items in visible range
  function renderItems(): void {
    const newRange = calculateVisibleRange();
    
    // Only re-render if visible range has changed
    if (newRange.start !== renderedRange.start || newRange.end !== renderedRange.end) {
      // Clear existing items
      itemContainer.innerHTML = '';
      
      // Create document fragment for batched DOM updates
      const fragment = document.createDocumentFragment();
      
      // Render visible items
      for (let i = newRange.start; i <= newRange.end; i++) {
        const item = itemTemplate(i);
        item.style.position = 'absolute';
        item.style.top = `${i * itemHeight}px`;
        item.style.height = `${itemHeight}px`;
        item.style.width = '100%';
        fragment.appendChild(item);
      }
      
      // Batch DOM update
      itemContainer.appendChild(fragment);
      renderedRange = newRange;
    }
  }
  
  // Handle scroll events (throttled)
  const handleScroll = throttle(() => {
    scrollPosition = container.scrollTop;
    renderItems();
  }, 16); // ~60fps
  
  container.addEventListener('scroll', handleScroll);
  
  // Initial render
  renderItems();
  
  // API for controlling the virtualized list
  return {
    refresh() {
      renderItems();
    },
    scrollToIndex(index: number) {
      container.scrollTop = index * itemHeight;
    },    updateItemCount(newCount: number) {
      // Update the mutable itemCount variable
      itemCount = newCount;
      spacer.style.height = `${itemCount * itemHeight}px`;
      renderItems();
    },
    destroy() {
      container.removeEventListener('scroll', handleScroll);
    }
  };
}

/**
 * Applies style and attribute changes to elements without causing reflow
 * Useful for batch updates to many elements
 * @param elements - Elements to update
 * @param updateFn - Function that updates each element
 */
export function batchElementUpdates<T extends HTMLElement>(
  elements: T[], 
  updateFn: (element: T, index: number) => void
): void {
  if (!elements || !elements.length) return;
  
  // Process in chunks to avoid long-running scripts
  const CHUNK_SIZE = 20;
  const totalElements = elements.length;
  let processedCount = 0;
  
  function processChunk(): void {
    const limit = Math.min(processedCount + CHUNK_SIZE, totalElements);
    
    requestAnimationFrame(() => {
      // Process a chunk of elements
      for (let i = processedCount; i < limit; i++) {
        updateFn(elements[i], i);
      }
      
      processedCount = limit;
      
      // Continue processing if there are more elements
      if (processedCount < totalElements) {
        processChunk();
      }
    });
  }
  
  // Start processing
  processChunk();
}

/**
 * Safely reads layout properties without causing reflow
 * then makes style changes in batch
 * @param element - Element to optimize
 * @param readProps - Properties to read
 * @param updateFn - Function that applies updates based on read values
 */
export function readThenUpdate(
  element: HTMLElement, 
  readProps: string[], 
  updateFn: (measurements: Record<string, any>) => void
): void {
  if (!element) return;
  
  // Collect all measurements before making any updates
  const measurements: Record<string, any> = {};
  
  // Force layout calculation once by accessing offsetWidth
  // This ensures subsequent property reads don't trigger additional reflows
  element.offsetHeight; // Force reflow
  
  // Read all properties
  readProps.forEach(prop => {
    // Handle computed style properties
    if (prop.startsWith('style.')) {
      const styleProp = prop.substring(6);
      measurements[prop] = window.getComputedStyle(element)[styleProp as any];
    } 
    // Handle direct properties
    else {
      measurements[prop] = (element as any)[prop];
    }
  });
  
  // Schedule updates for next frame
  requestAnimationFrame(() => {
    updateFn(measurements);
  });
}

/**
 * Creates a ResizeObserver that efficiently handles element resizing
 * with debounced callback to minimize performance impact
 * @param element - Element to observe
 * @param callback - Callback function when resize occurs
 * @param delay - Debounce delay in ms
 * @returns ResizeObserver instance
 */
export function optimizedResizeObserver(
  element: HTMLElement, 
  callback: (entry: ResizeObserverEntry) => void, 
  delay: number = 100
): ResizeObserver | null {
  if (!element) return null;
  
  // Create debounced handler
  const debouncedCallback = debounce((entries: ResizeObserverEntry[]) => {
    callback(entries[0]);
  }, delay);
  
  // Create and start ResizeObserver
  const observer = new ResizeObserver(debouncedCallback);
  observer.observe(element);
  
  return observer;
}

/**
 * Creates an IntersectionObserver for lazy loading content
 * @param callback - Function to call when element enters viewport
 * @param options - IntersectionObserver options
 * @returns IntersectionObserver instance
 */
export function createLazyLoadObserver(
  callback: (target: Element, observer: IntersectionObserver) => void, 
  options: IntersectionObserverInit = {}
): IntersectionObserver {
  return new IntersectionObserver((entries, observer) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        callback(entry.target, observer);
      }
    });
  }, {
    rootMargin: '200px 0px', // Start loading before element is visible
    threshold: 0.01, // Trigger when just 1% is visible
    ...options
  });
}

/**
 * Adds passive event listeners to improve touch scrolling performance
 * @param element - Element to add listener to
 * @param eventType - Event type (e.g., 'touchstart')
 * @param handler - Event handler function
 * @param useCapture - Whether to use capture phase
 */
export function addPassiveEventListener(
  element: HTMLElement | Window, 
  eventType: string, 
  handler: EventListenerOrEventListenerObject, 
  useCapture: boolean = false
): void {
  element.addEventListener(eventType, handler, {
    passive: true,
    capture: useCapture
  });
}

/**
 * Defers non-critical operations until after critical rendering is complete
 * @param fn - Function to defer
 * @param timeout - Timeout in ms
 */
export function deferOperation(fn: () => void, timeout: number = 100): void {
  if (window.requestIdleCallback) {
    // Use requestIdleCallback if available
    window.requestIdleCallback(() => fn(), { timeout });
  } else {
    // Fall back to setTimeout
    setTimeout(fn, timeout);
  }
}
