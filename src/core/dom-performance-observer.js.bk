/**
 * DOM Performance Observer
 * Automatically detects and optimizes problematic UI patterns that cause performance issues
 */

import * as DOMOptimizer from './dom-optimizer';

/**
 * Default configuration for performance optimizations
 */
const DEFAULT_CONFIG = {
  // Whether to enable automatic optimization
  enabled: true,
  
  // Elements to ignore (CSS selectors)
  ignoreSelectors: ['.ignore-optimize', '[data-no-optimize]'],
  
  // Optimization targets
  targets: {
    // Detect and optimize elements being animated
    animations: true,
    
    // Optimize elements with scroll events
    scrollContainers: true,
    
    // Fix layout thrashing in lists and tables
    listRendering: true,
    
    // Optimize frequent DOM updates
    frequentUpdates: true,
    
    // Optimize images and media loading
    mediaLoading: true
  },
  
  // Performance thresholds
  thresholds: {
    // Maximum number of elements to optimize at once
    batchSize: 20,
    
    // Minimum number of DOM mutations to trigger optimization
    minMutations: 5,
    
    // Minimum ms between optimization runs
    throttleInterval: 100,
    
    // Consider elements larger than this for virtualization (px)
    largeListThreshold: 30
  }
};

/**
 * Class that observes DOM changes and optimizes elements for mobile performance
 */
export class DOMPerformanceObserver {
  constructor(config = {}) {
    // Merge default config with provided config
    this.config = { 
      ...DEFAULT_CONFIG,
      ...config,
      targets: { ...DEFAULT_CONFIG.targets, ...config.targets },
      thresholds: { ...DEFAULT_CONFIG.thresholds, ...config.thresholds }
    };
    
    // Track optimized elements to avoid redundant operations
    this.optimizedElements = new WeakSet();
    
    // Track frequent updates to detect potential issues
    this.updateFrequency = new Map();
    
    // Track scroll containers
    this.scrollContainers = new Set();
    
    // Track known virtualized lists
    this.virtualizedLists = new WeakMap();
    
    // Create mutation observer
    this.mutationObserver = new MutationObserver(
      DOMOptimizer.throttle(this.handleMutations.bind(this), this.config.thresholds.throttleInterval)
    );
    
    // Create intersection observer for lazy loading
    this.lazyLoadObserver = DOMOptimizer.createLazyLoadObserver(this.handleIntersection.bind(this));
    
    // Bind handlers to instance
    this.handleScroll = DOMOptimizer.throttle(this.optimizeScrollContainer.bind(this), 200);
    this.handleResize = DOMOptimizer.debounce(this.refreshOptimizations.bind(this), 250);
    
    // Initialize
    this.initialize();
  }
  
  /**
   * Initialize the observer
   */
  initialize() {
    if (!this.config.enabled) return;
    
    // Observe the entire document for changes
    this.mutationObserver.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['style', 'class']
    });
    
    // Set up resize handler
    window.addEventListener('resize', this.handleResize, { passive: true });
    
    // Initial optimization
    this.scanDOMForOptimizations();
    
    console.log('DOM Performance Observer initialized');
  }
  
  /**
   * Scan the entire DOM for elements that can be optimized
   */
  scanDOMForOptimizations() {
    console.log('Scanning DOM for optimization opportunities...');
    
    // Find and optimize animated elements
    if (this.config.targets.animations) {
      this.findAndOptimizeAnimatedElements();
    }
    
    // Find and optimize scroll containers
    if (this.config.targets.scrollContainers) {
      this.findAndOptimizeScrollContainers();
    }
    
    // Find and optimize large lists
    if (this.config.targets.listRendering) {
      this.findAndOptimizeLargeLists();
    }
    
    // Set up lazy loading for images and media
    if (this.config.targets.mediaLoading) {
      this.setupLazyLoading();
    }
  }
  
  /**
   * Handle DOM mutations
   * @param {MutationRecord[]} mutations - List of DOM mutations
   */
  handleMutations(mutations) {
    if (mutations.length < this.config.thresholds.minMutations) return;
    
    // Find added nodes that need optimization
    const addedNodes = new Set();
    
    // Process mutations
    mutations.forEach(mutation => {
      // Handle added nodes
      if (mutation.type === 'childList') {
        mutation.addedNodes.forEach(node => {
          if (node.nodeType === Node.ELEMENT_NODE) {
            addedNodes.add(node);
            
            // Also add all element children for potential optimization
            if (node.querySelectorAll) {
              node.querySelectorAll('*').forEach(child => addedNodes.add(child));
            }
          }
        });
      }
      
      // Handle attribute changes
      if (mutation.type === 'attributes') {
        const target = mutation.target;
        if (target.nodeType === Node.ELEMENT_NODE) {
          // Track update frequency for this element
          const count = this.updateFrequency.get(target) || 0;
          this.updateFrequency.set(target, count + 1);
          
          // If element is updated frequently, optimize it
          if (count > 10 && this.config.targets.frequentUpdates) {
            this.optimizeFrequentlyUpdatedElement(target);
          }
          
          // Check for animation-related changes
          if (mutation.attributeName === 'style' || mutation.attributeName === 'class') {
            if (this.hasAnimationStyles(target) && this.config.targets.animations) {
              this.optimizeForAnimation(target);
            }
          }
        }
      }
    });
    
    // Batch optimize added nodes
    if (addedNodes.size > 0) {
      DOMOptimizer.deferOperation(() => {
        this.optimizeAddedNodes(Array.from(addedNodes));
      });
    }
  }
  
  /**
   * Optimize newly added DOM nodes
   * @param {HTMLElement[]} nodes - List of nodes to optimize
   */
  optimizeAddedNodes(nodes) {
    // Process in batches to avoid blocking the main thread
    DOMOptimizer.batchElementUpdates(nodes, (node) => {
      // Skip nodes that should be ignored
      if (this.shouldIgnoreElement(node)) return;
      
      // Check if node is animated
      if (this.config.targets.animations && this.hasAnimationStyles(node)) {
        this.optimizeForAnimation(node);
      }
      
      // Check if node is a scroll container
      if (this.config.targets.scrollContainers && this.isScrollContainer(node)) {
        this.optimizeScrollContainer(node);
      }
      
      // Check if node is a large list
      if (this.config.targets.listRendering && this.isLargeList(node)) {
        this.optimizeList(node);
      }
      
      // Check if node is an image or video that could be lazy loaded
      if (this.config.targets.mediaLoading && this.isLazyLoadable(node)) {
        this.setupLazyLoadForElement(node);
      }
    });
  }
  
  /**
   * Check if an element should be ignored for optimization
   * @param {HTMLElement} element - Element to check
   * @returns {boolean} - Whether to ignore the element
   */
  shouldIgnoreElement(element) {
    if (!element || !element.matches) return true;
    
    // Check against ignore selectors
    return this.config.ignoreSelectors.some(selector => element.matches(selector));
  }
  
  /**
   * Find and optimize elements with animations and transitions
   */
  findAndOptimizeAnimatedElements() {
    // Look for elements with CSS animations, transitions, or transform properties
    const animatedElements = document.querySelectorAll(
      '.animate, .transition, [style*="animation"], [style*="transition"], [style*="transform"]'
    );
    
    animatedElements.forEach(element => {
      if (!this.shouldIgnoreElement(element)) {
        this.optimizeForAnimation(element);
      }
    });
    
    // Also check for elements with animation styles in computed styles
    const allElements = document.querySelectorAll('*');
    DOMOptimizer.batchElementUpdates(allElements, (element) => {
      if (!this.shouldIgnoreElement(element) && !this.optimizedElements.has(element)) {
        if (this.hasAnimationStyles(element)) {
          this.optimizeForAnimation(element);
        }
      }
    });
  }
  
  /**
   * Check if an element has animation styles
   * @param {HTMLElement} element - Element to check
   * @returns {boolean} - Whether the element has animation styles
   */
  hasAnimationStyles(element) {
    if (!element || !window.getComputedStyle) return false;
    
    const style = window.getComputedStyle(element);
    return (
      style.animation !== 'none' ||
      style.transition !== 'none' ||
      style.transform !== 'none' ||
      style.opacity !== '1'
    );
  }
  
  /**
   * Optimize an element for animation
   * @param {HTMLElement} element - Element to optimize
   */
  optimizeForAnimation(element) {
    if (!element || this.optimizedElements.has(element)) return;
    
    // Optimize the element
    DOMOptimizer.optimizeForAnimation(element);
    
    // Mark as optimized
    this.optimizedElements.add(element);
    
    // Set up auto-cleanup after animations
    this.setupAnimationCleanup(element);
    
    // Also optimize parent for some cases
    if (element.parentElement && !this.optimizedElements.has(element.parentElement)) {
      const parent = element.parentElement;
      
      // Only optimize parent if it has specific properties
      const parentStyle = window.getComputedStyle(parent);
      if (parentStyle.overflow !== 'visible' || parentStyle.transform !== 'none') {
        DOMOptimizer.optimizeForAnimation(parent);
        this.optimizedElements.add(parent);
      }
    }
  }
  
  /**
   * Set up cleanup after animations complete
   * @param {HTMLElement} element - Element to clean up
   */
  setupAnimationCleanup(element) {
    // Listen for animation and transition ends to clean up
    const cleanupHandler = DOMOptimizer.debounce(() => {
      // Only reset if the element still exists and doesn't have active animations
      if (element && document.contains(element)) {
        const style = window.getComputedStyle(element);
        
        // Check if animations are complete
        if (style.animationPlayState !== 'running' && 
            style.transitionProperty === 'none') {
          DOMOptimizer.resetOptimization(element);
          this.optimizedElements.delete(element);
          
          // Remove event listeners
          element.removeEventListener('animationend', cleanupHandler);
          element.removeEventListener('transitionend', cleanupHandler);
        }
      }
    }, 300);
    
    element.addEventListener('animationend', cleanupHandler);
    element.addEventListener('transitionend', cleanupHandler);
  }
  
  /**
   * Find and optimize scroll containers
   */
  findAndOptimizeScrollContainers() {
    // Find elements that might be scroll containers
    const scrollableElements = Array.from(document.querySelectorAll('*')).filter(el => {
      const style = window.getComputedStyle(el);
      return (
        (style.overflow === 'auto' || style.overflow === 'scroll' ||
         style.overflowY === 'auto' || style.overflowY === 'scroll' ||
         style.overflowX === 'auto' || style.overflowX === 'scroll') &&
        (el.scrollHeight > el.clientHeight || el.scrollWidth > el.clientWidth)
      );
    });
    
    // Optimize each scroll container
    scrollableElements.forEach(element => {
      if (!this.shouldIgnoreElement(element)) {
        this.optimizeScrollContainer(element);
      }
    });
  }
  
  /**
   * Check if an element is a scroll container
   * @param {HTMLElement} element - Element to check
   * @returns {boolean} - Whether the element is a scroll container
   */
  isScrollContainer(element) {
    if (!element) return false;
    
    const style = window.getComputedStyle(element);
    return (
      (style.overflow === 'auto' || style.overflow === 'scroll' ||
       style.overflowY === 'auto' || style.overflowY === 'scroll' ||
       style.overflowX === 'auto' || style.overflowX === 'scroll') &&
      (element.scrollHeight > element.clientHeight || element.scrollWidth > element.clientWidth)
    );
  }
  
  /**
   * Optimize a scroll container for smooth scrolling
   * @param {HTMLElement} element - Scroll container to optimize
   */
  optimizeScrollContainer(element) {
    if (!element || this.scrollContainers.has(element)) return;
    
    // Use passive listeners for scroll events
    DOMOptimizer.addPassiveEventListener(element, 'scroll', this.handleScroll);
    
    // Mark scrollable area for GPU acceleration
    DOMOptimizer.batchCSSUpdates(element, {
      willChange: 'transform',
      backfaceVisibility: 'hidden',
      WebkitOverflowScrolling: 'touch' // For older iOS devices
    });
    
    // Optimize children for smoother scrolling
    DOMOptimizer.deferOperation(() => {
      Array.from(element.children).forEach(child => {
        if (!this.optimizedElements.has(child)) {
          DOMOptimizer.batchCSSUpdates(child, {
            transform: 'translateZ(0)',
            backfaceVisibility: 'hidden'
          });
          this.optimizedElements.add(child);
        }
      });
    });
    
    // Track this container
    this.scrollContainers.add(element);
  }
  
  /**
   * Find and optimize large lists that could benefit from virtualization
   */
  findAndOptimizeLargeLists() {
    // Look for potential lists with many items
    const potentialLists = [
      ...document.querySelectorAll('ul, ol, table, [role="grid"], [role="list"], .list, .table'),
      ...document.querySelectorAll('[class*="list"], [class*="table"], [id*="list"], [id*="table"]')
    ];
    
    potentialLists.forEach(list => {
      if (!this.shouldIgnoreElement(list) && this.isLargeList(list)) {
        this.optimizeList(list);
      }
    });
  }
  
  /**
   * Check if an element is a large list that could benefit from virtualization
   * @param {HTMLElement} element - Element to check
   * @returns {boolean} - Whether the element is a large list
   */
  isLargeList(element) {
    if (!element) return false;
    
    // Identify element's children (list items, rows, etc.)
    let items;
    
    if (element.tagName === 'UL' || element.tagName === 'OL') {
      items = element.querySelectorAll('li');
    } else if (element.tagName === 'TABLE') {
      items = element.querySelectorAll('tr');
    } else {
      items = Array.from(element.children).filter(child => {
        // Filter to get only the repeated item elements
        const childStyles = window.getComputedStyle(child);
        return childStyles.display !== 'none';
      });
    }
    
    // Consider a list large if it has many items
    return items.length >= this.config.thresholds.largeListThreshold;
  }
  
  /**
   * Optimize a large list for rendering performance
   * @param {HTMLElement} listElement - List to optimize
   */
  optimizeList(listElement) {
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
    const heights = [];
    
    for (let i = 0; i < sampleSize; i++) {
      heights.push(items[i].offsetHeight);
    }
    
    // Calculate average height and deviation
    const avgHeight = heights.reduce((sum, h) => sum + h, 0) / heights.length;
    const maxDeviation = Math.max(...heights.map(h => Math.abs(h - avgHeight)));
    
    // If items have consistent height, use virtualization
    if (items.length > 20 && maxDeviation < avgHeight * 0.2) {
      // Get item template
      const itemTemplate = (index) => {
        // Clone an existing item as template
        const clone = items[Math.min(index, items.length - 1)].cloneNode(true);
        
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
        console.log(`Virtualized list created for ${listElement.tagName}`, listElement);
      }
    } 
    // Otherwise, just optimize the list items for rendering
    else {
      DOMOptimizer.batchElementUpdates(items, (item) => {
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
  
  /**
   * Optimize an element that is updated frequently
   * @param {HTMLElement} element - Element to optimize
   */
  optimizeFrequentlyUpdatedElement(element) {
    if (!element || this.optimizedElements.has(element)) return;
    
    // Find the real update target (might be a child element)
    let updateTarget = element;
    
    // If this is a container, find the actual child that changes
    if (element.children.length > 0) {
      const childrenUpdateCount = new Map();
      
      Array.from(element.children).forEach(child => {
        const count = this.updateFrequency.get(child) || 0;
        childrenUpdateCount.set(child, count);
      });
      
      // Find child with highest update count
      let maxCount = 0;
      let maxChild = null;
      
      for (const [child, count] of childrenUpdateCount.entries()) {
        if (count > maxCount) {
          maxCount = count;
          maxChild = child;
        }
      }
      
      // Use child as target if it has more updates
      if (maxChild && maxCount > (this.updateFrequency.get(element) || 0)) {
        updateTarget = maxChild;
      }
    }
    
    // Add to optimized set
    this.optimizedElements.add(updateTarget);
    
    // Apply optimizations
    DOMOptimizer.batchCSSUpdates(updateTarget, {
      transform: 'translateZ(0)'
    });
    
    // Reset update frequency
    this.updateFrequency.delete(element);
  }
  
  /**
   * Set up lazy loading for images and media
   */
  setupLazyLoading() {
    // Find images and videos that can be lazy loaded
    const mediaElements = [
      ...document.querySelectorAll('img:not([loading="eager"])'),
      ...document.querySelectorAll('video:not([autoplay])'),
      ...document.querySelectorAll('iframe'),
      ...document.querySelectorAll('[data-src], [data-srcset], [data-background-src]')
    ];
    
    mediaElements.forEach(element => {
      if (!this.shouldIgnoreElement(element)) {
        this.setupLazyLoadForElement(element);
      }
    });
  }
  
  /**
   * Check if element can be lazy loaded
   * @param {HTMLElement} element - Element to check
   * @returns {boolean} - Whether the element can be lazy loaded
   */
  isLazyLoadable(element) {
    if (!element) return false;
    
    // Check tag name
    const tagName = element.tagName.toLowerCase();
    if (tagName === 'img' || tagName === 'video' || tagName === 'iframe') {
      return true;
    }
    
    // Check for data attributes
    return (
      element.hasAttribute('data-src') ||
      element.hasAttribute('data-srcset') ||
      element.hasAttribute('data-background-src')
    );
  }
  
  /**
   * Set up lazy loading for an element
   * @param {HTMLElement} element - Element to lazy load
   */
  setupLazyLoadForElement(element) {
    if (!element) return;
    
    // Add native lazy loading attribute if supported
    const tagName = element.tagName.toLowerCase();
    if ((tagName === 'img' || tagName === 'iframe') && !element.hasAttribute('loading')) {
      element.setAttribute('loading', 'lazy');
    }
    
    // Handle data-src attributes with IntersectionObserver
    if (element.hasAttribute('data-src') || element.hasAttribute('data-srcset')) {
      this.lazyLoadObserver.observe(element);
    }
  }
  
  /**
   * Handle intersection for lazy loaded elements
   * @param {HTMLElement} element - Element that's now visible
   * @param {IntersectionObserver} observer - The observer instance
   */
  handleIntersection(element, observer) {
    // Load the actual image/media source
    if (element.hasAttribute('data-src')) {
      element.src = element.getAttribute('data-src');
      element.removeAttribute('data-src');
    }
    
    if (element.hasAttribute('data-srcset')) {
      element.srcset = element.getAttribute('data-srcset');
      element.removeAttribute('data-srcset');
    }
    
    if (element.hasAttribute('data-background-src')) {
      element.style.backgroundImage = `url('${element.getAttribute('data-background-src')}')`;
      element.removeAttribute('data-background-src');
    }
    
    // Stop observing this element
    observer.unobserve(element);
  }
  
  /**
   * Refresh optimizations when window is resized
   */
  refreshOptimizations() {
    // Update virtualized lists
    for (const [element, controller] of this.virtualizedLists.entries()) {
      if (document.contains(element)) {
        controller.refresh();
      } else {
        // Clean up references to removed elements
        this.virtualizedLists.delete(element);
      }
    }
    
    // Re-scan for new optimization opportunities
    DOMOptimizer.deferOperation(() => {
      this.scanDOMForOptimizations();
    });
  }
  
  /**
   * Dispose all resources and stop monitoring
   */
  dispose() {
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
    for (const controller of this.virtualizedLists.values()) {
      controller.destroy();
    }
    
    // Clear data structures
    this.optimizedElements = new WeakSet();
    this.updateFrequency.clear();
    this.scrollContainers.clear();
    this.virtualizedLists = new WeakMap();
    
    console.log('DOM Performance Observer disposed');
  }
}

/**
 * Create and return a singleton instance of the DOMPerformanceObserver
 * @param {Object} config - Configuration for the observer
 * @returns {DOMPerformanceObserver} - Observer instance
 */
let observerInstance = null;
export function initDOMPerformanceOptimization(config = {}) {
  if (!observerInstance) {
    observerInstance = new DOMPerformanceObserver(config);
  } else {
    // Update config if instance already exists
    observerInstance.config = {
      ...observerInstance.config,
      ...config,
      targets: { ...observerInstance.config.targets, ...config.targets },
      thresholds: { ...observerInstance.config.thresholds, ...config.thresholds }
    };
  }
  
  return observerInstance;
}

/**
 * Get the current observer instance
 * @returns {DOMPerformanceObserver|null} - The current observer instance or null
 */
export function getDOMPerformanceObserver() {
  return observerInstance;
}
