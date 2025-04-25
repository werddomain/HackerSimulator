/**
 * DOM Optimization Module
 * Provides utilities for optimizing DOM operations on mobile devices
 * to minimize reflows and repaints and improve rendering performance
 */

/**
 * Batch DOM operations to reduce reflows and repaints
 * @param {Function} updateFn - Function that performs DOM updates
 */
export function batchDOMUpdates(updateFn) {
  // Use requestAnimationFrame to ensure updates happen during the next repaint
  requestAnimationFrame(() => {
    // Measure anything that would cause a reflow before making changes
    updateFn();
  });
}

/**
 * Create and return a document fragment for batching DOM insertions
 * @returns {DocumentFragment} A document fragment for batched insertions
 */
export function createBatchFragment() {
  return document.createDocumentFragment();
}

/**
 * Optimizes an element for animations and transformations
 * by promoting it to its own compositor layer
 * @param {HTMLElement} element - Element to optimize
 */
export function optimizeForAnimation(element) {
  if (!element) return;
  
  // Promote to GPU layer with will-change
  element.style.willChange = 'transform, opacity';
  
  // Force hardware acceleration
  element.style.transform = 'translateZ(0)';
}

/**
 * Reset optimization properties when animation is complete
 * to free up memory and GPU resources
 * @param {HTMLElement} element - Element to reset
 */
export function resetOptimization(element) {
  if (!element) return;
  
  // Reset will-change after animations complete
  element.style.willChange = 'auto';
}

/**
 * Throttles a function to avoid excessive DOM updates
 * @param {Function} fn - Function to throttle
 * @param {number} delay - Throttle delay in ms
 * @returns {Function} Throttled function
 */
export function throttle(fn, delay = 16) { // Default to 60fps (16ms)
  let lastCall = 0;
  let timeout = null;
  
  return function(...args) {
    const now = Date.now();
    const timeSinceLastCall = now - lastCall;
    
    if (timeSinceLastCall >= delay) {
      lastCall = now;
      return fn.apply(this, args);
    } else {
      clearTimeout(timeout);
      timeout = setTimeout(() => {
        lastCall = Date.now();
        fn.apply(this, args);
      }, delay - timeSinceLastCall);
    }
  };
}

/**
 * Debounces a function to reduce frequent updates
 * @param {Function} fn - Function to debounce
 * @param {number} delay - Debounce delay in ms
 * @returns {Function} Debounced function
 */
export function debounce(fn, delay = 150) {
  let timeout = null;
  
  return function(...args) {
    clearTimeout(timeout);
    timeout = setTimeout(() => fn.apply(this, args), delay);
  };
}

/**
 * Apply CSS properties in batch to minimize reflows
 * @param {HTMLElement} element - The element to update
 * @param {Object} properties - CSS properties to set
 */
export function batchCSSUpdates(element, properties) {
  if (!element) return;
  
  requestAnimationFrame(() => {
    for (const prop in properties) {
      element.style[prop] = properties[prop];
    }
  });
}

/**
 * Creates a virtualized container for efficiently rendering large lists
 * Only renders items that are visible in the viewport
 * @param {Object} config - Configuration object
 * @returns {Object} - Virtualized list controller
 */
export function createVirtualizedContainer(config) {
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
  let visibleItems = [];
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
  function calculateVisibleRange() {
    const containerHeight = container.clientHeight;
    const startIndex = Math.max(0, Math.floor(scrollPosition / itemHeight) - renderBuffer);
    const endIndex = Math.min(
      itemCount - 1,
      Math.ceil((scrollPosition + containerHeight) / itemHeight) + renderBuffer
    );
    
    return { start: startIndex, end: endIndex };
  }
  
  // Render items in visible range
  function renderItems() {
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
    scrollToIndex(index) {
      container.scrollTop = index * itemHeight;
    },
    updateItemCount(newCount) {
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
 * @param {Array<HTMLElement>} elements - Elements to update
 * @param {Function} updateFn - Function that updates each element
 */
export function batchElementUpdates(elements, updateFn) {
  if (!elements || !elements.length) return;
  
  // Process in chunks to avoid long-running scripts
  const CHUNK_SIZE = 20;
  const totalElements = elements.length;
  let processedCount = 0;
  
  function processChunk() {
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
 * @param {HTMLElement} element - Element to optimize
 * @param {Array<string>} readProps - Properties to read
 * @param {Function} updateFn - Function that applies updates based on read values
 */
export function readThenUpdate(element, readProps, updateFn) {
  if (!element) return;
  
  // Collect all measurements before making any updates
  const measurements = {};
  
  // Force layout calculation once by accessing offsetWidth
  // This ensures subsequent property reads don't trigger additional reflows
  element.offsetHeight; // Force reflow
  
  // Read all properties
  readProps.forEach(prop => {
    // Handle computed style properties
    if (prop.startsWith('style.')) {
      const styleProp = prop.substring(6);
      measurements[prop] = window.getComputedStyle(element)[styleProp];
    } 
    // Handle direct properties
    else {
      measurements[prop] = element[prop];
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
 * @param {HTMLElement} element - Element to observe
 * @param {Function} callback - Callback function when resize occurs
 * @param {number} delay - Debounce delay in ms
 * @returns {ResizeObserver} - ResizeObserver instance
 */
export function optimizedResizeObserver(element, callback, delay = 100) {
  if (!element) return null;
  
  // Create debounced handler
  const debouncedCallback = debounce((entries) => {
    callback(entries[0]);
  }, delay);
  
  // Create and start ResizeObserver
  const observer = new ResizeObserver(debouncedCallback);
  observer.observe(element);
  
  return observer;
}

/**
 * Creates an IntersectionObserver for lazy loading content
 * @param {Function} callback - Function to call when element enters viewport
 * @param {Object} options - IntersectionObserver options
 * @returns {IntersectionObserver} - IntersectionObserver instance
 */
export function createLazyLoadObserver(callback, options = {}) {
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
 * @param {HTMLElement} element - Element to add listener to
 * @param {string} eventType - Event type (e.g., 'touchstart')
 * @param {Function} handler - Event handler function
 * @param {boolean} useCapture - Whether to use capture phase
 */
export function addPassiveEventListener(element, eventType, handler, useCapture = false) {
  element.addEventListener(eventType, handler, {
    passive: true,
    capture: useCapture
  });
}

/**
 * Defers non-critical operations until after critical rendering is complete
 * @param {Function} fn - Function to defer
 * @param {number} timeout - Timeout in ms
 */
export function deferOperation(fn, timeout = 100) {
  if (window.requestIdleCallback) {
    // Use requestIdleCallback if available
    window.requestIdleCallback(() => fn(), { timeout });
  } else {
    // Fall back to setTimeout
    setTimeout(fn, timeout);
  }
}
