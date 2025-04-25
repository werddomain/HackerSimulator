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
    timeout = window.setTimeout(() => fn.apply(this, args), delay);
  };
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
    itemCount,       // Total number of items
    renderBuffer = 5 // Number of items to render outside visible area
  } = config;
  
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
    },
    updateItemCount(newCount: number) {
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
