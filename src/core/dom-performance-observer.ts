/**
 * DOM Performance Observer
 * Automatically detects and optimizes problematic UI patterns that cause performance issues
 */

import * as DOMOptimizer from './dom-optimizer';
import { forEachWeakMapEntry, registerWeakMapKey, unregisterWeakMapKey } from './weak-map-helpers';

/**
 * Configuration for performance optimizations
 */
export interface PerformanceObserverConfig {
  // Whether to enable automatic optimization
  enabled?: boolean;
  
  // Elements to ignore (CSS selectors)
  ignoreSelectors?: string[];
  
  // Optimization targets
  targets?: {
    // Detect and optimize elements being animated
    animations?: boolean;
    
    // Optimize elements with scroll events
    scrollContainers?: boolean;
    
    // Fix layout thrashing in lists and tables
    listRendering?: boolean;
    
    // Optimize frequent DOM updates
    frequentUpdates?: boolean;
    
    // Optimize images and media loading
    mediaLoading?: boolean;
  };
  
  // Performance thresholds
  thresholds?: {
    // Maximum number of elements to optimize at once
    batchSize?: number;
    
    // Minimum number of DOM mutations to trigger optimization
    minMutations?: number;
    
    // Minimum ms between optimization runs
    throttleInterval?: number;
    
    // Consider elements larger than this for virtualization (px)
    largeListThreshold?: number;
  };
}

/**
 * Default configuration for performance optimizations
 */
const DEFAULT_CONFIG: Required<PerformanceObserverConfig> = {
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
  private config: Required<PerformanceObserverConfig>;
  private optimizedElements: WeakSet<Element>;
  private updateFrequency: Map<Element, number>;
  private scrollContainers: Set<HTMLElement>;
  private virtualizedLists: WeakMap<HTMLElement, DOMOptimizer.VirtualizedContainer>;
  private mutationObserver: MutationObserver;
  private lazyLoadObserver: IntersectionObserver;
  private handleScroll: (event: Event) => void;
  private handleResize: (event: Event) => void;
  
  constructor(config: PerformanceObserverConfig = {}) {
    // Merge default config with provided config
    this.config = { 
      ...DEFAULT_CONFIG,
      ...config,
      targets: { ...DEFAULT_CONFIG.targets, ...(config.targets || {}) },
      thresholds: { ...DEFAULT_CONFIG.thresholds, ...(config.thresholds || {}) }
    };
    
    // Track optimized elements to avoid redundant operations
    this.optimizedElements = new WeakSet<Element>();
    
    // Track frequent updates to detect potential issues
    this.updateFrequency = new Map<Element, number>();
    
    // Track scroll containers
    this.scrollContainers = new Set<HTMLElement>();
    
    // Track known virtualized lists
    this.virtualizedLists = new WeakMap<HTMLElement, DOMOptimizer.VirtualizedContainer>();
    
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
  private initialize(): void {
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
  private scanDOMForOptimizations(): void {
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
   * @param mutations - List of DOM mutations
   */
  private handleMutations(mutations: MutationRecord[]): void {
    if (mutations.length < this.config.thresholds.minMutations) return;
    
    // Find added nodes that need optimization
    const addedNodes = new Set<Element>();
    
    // Process mutations
    mutations.forEach(mutation => {
      // Handle added nodes
      if (mutation.type === 'childList') {
        mutation.addedNodes.forEach(node => {
          if (node.nodeType === Node.ELEMENT_NODE) {
            addedNodes.add(node as Element);
            
            // Also add all element children for potential optimization
            if ((node as Element).querySelectorAll) {
              (node as Element).querySelectorAll('*').forEach(child => addedNodes.add(child));
            }
          }
        });
      }
      
      // Handle attribute changes
      if (mutation.type === 'attributes') {
        const target = mutation.target as Element;
        if (target.nodeType === Node.ELEMENT_NODE) {
          // Track update frequency for this element
          const count = this.updateFrequency.get(target) || 0;
          this.updateFrequency.set(target, count + 1);
          
          // If element is updated frequently, optimize it
          if (count > 10 && this.config.targets.frequentUpdates) {
            this.optimizeFrequentlyUpdatedElement(target as HTMLElement);
          }
          
          // Check for animation-related changes
          if (mutation.attributeName === 'style' || mutation.attributeName === 'class') {
            if (this.hasAnimationStyles(target as HTMLElement) && this.config.targets.animations) {
              this.optimizeForAnimation(target as HTMLElement);
            }
          }
        }
      }
    });
    
    // Batch optimize added nodes
    if (addedNodes.size > 0) {
      DOMOptimizer.deferOperation(() => {
        this.optimizeAddedNodes(Array.from(addedNodes) as HTMLElement[]);
      });
    }
  }
  
  /**
   * Optimize newly added DOM nodes
   * @param nodes - List of nodes to optimize
   */
  private optimizeAddedNodes(nodes: HTMLElement[]): void {
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
   * @param element - Element to check
   * @returns Whether to ignore the element
   */
  private shouldIgnoreElement(element: Element): boolean {
    if (!element || !element.matches) return true;
    
    // Check against ignore selectors
    return this.config.ignoreSelectors.some(selector => element.matches(selector));
  }
  
  /**
   * Find and optimize elements with animations and transitions
   */
  private findAndOptimizeAnimatedElements(): void {
    // Look for elements with CSS animations, transitions, or transform properties
    const animatedElements = document.querySelectorAll(
      '.animate, .transition, [style*="animation"], [style*="transition"], [style*="transform"]'
    );
    
    animatedElements.forEach(element => {
      if (!this.shouldIgnoreElement(element)) {
        this.optimizeForAnimation(element as HTMLElement);
      }
    });
    
    // Also check for elements with animation styles in computed styles
    const allElements = document.querySelectorAll('*');
    DOMOptimizer.batchElementUpdates(Array.from(allElements) as HTMLElement[], (element) => {
      if (!this.shouldIgnoreElement(element) && !this.optimizedElements.has(element)) {
        if (this.hasAnimationStyles(element)) {
          this.optimizeForAnimation(element);
        }
      }
    });
  }
  
  /**
   * Check if an element has animation styles
   * @param element - Element to check
   * @returns Whether the element has animation styles
   */
  private hasAnimationStyles(element: HTMLElement): boolean {
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
   * @param element - Element to optimize
   */
  private optimizeForAnimation(element: HTMLElement): void {
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
   * @param element - Element to clean up
   */
  private setupAnimationCleanup(element: HTMLElement): void {
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
  private findAndOptimizeScrollContainers(): void {
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
        this.optimizeScrollContainer(element as HTMLElement);
      }
    });
  }
  
  /**
   * Check if an element is a scroll container
   * @param element - Element to check
   * @returns Whether the element is a scroll container
   */
  private isScrollContainer(element: HTMLElement): boolean {
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
   * @param element - Scroll container to optimize
   */
  private optimizeScrollContainer(element: HTMLElement): void {
    if (!element || this.scrollContainers.has(element)) return;
    
    // Use passive listeners for scroll events
    DOMOptimizer.addPassiveEventListener(element, 'scroll', this.handleScroll);
      // Mark scrollable area for GPU acceleration
    const cssProperties: Record<string, string> = {
      willChange: 'transform',
      backfaceVisibility: 'hidden'
    };
    
    // Add vendor-prefixed property separately
    (cssProperties as any)['-webkit-overflow-scrolling'] = 'touch'; // For older iOS devices
    
    DOMOptimizer.batchCSSUpdates(element, cssProperties);
    
    // Optimize children for smoother scrolling
    DOMOptimizer.deferOperation(() => {
      Array.from(element.children).forEach(child => {
        if (!this.optimizedElements.has(child)) {
          DOMOptimizer.batchCSSUpdates(child as HTMLElement, {
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
  private findAndOptimizeLargeLists(): void {
    // Look for potential lists with many items
    const potentialLists = [
      ...document.querySelectorAll('ul, ol, table, [role="grid"], [role="list"], .list, .table'),
      ...document.querySelectorAll('[class*="list"], [class*="table"], [id*="list"], [id*="table"]')
    ];
    
    potentialLists.forEach(list => {
      if (!this.shouldIgnoreElement(list) && this.isLargeList(list as HTMLElement)) {
        this.optimizeList(list as HTMLElement);
      }
    });
  }
  
  /**
   * Check if an element is a large list that could benefit from virtualization
   * @param element - Element to check
   * @returns Whether the element is a large list
   */
  private isLargeList(element: HTMLElement): boolean {
    if (!element) return false;
    
    // Identify element's children (list items, rows, etc.)
    let items: NodeListOf<Element> | Element[];
    
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
      heights.push(items[i].offsetHeight);
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
  
  /**
   * Optimize an element that is updated frequently
   * @param element - Element to optimize
   */
  private optimizeFrequentlyUpdatedElement(element: HTMLElement): void {
    if (!element || this.optimizedElements.has(element)) return;
    
    // Find the real update target (might be a child element)
    let updateTarget = element;
    
    // If this is a container, find the actual child that changes
    if (element.children.length > 0) {
      const childrenUpdateCount = new Map<Element, number>();
      
      Array.from(element.children).forEach(child => {
        const count = this.updateFrequency.get(child) || 0;
        childrenUpdateCount.set(child, count);
      });
      
      // Find child with highest update count
      let maxCount = 0;
      let maxChild: Element | null = null;
      
      for (const [child, count] of childrenUpdateCount.entries()) {
        if (count > maxCount) {
          maxCount = count;
          maxChild = child;
        }
      }
      
      // Use child as target if it has more updates
      if (maxChild && maxCount > (this.updateFrequency.get(element) || 0)) {
        updateTarget = maxChild as HTMLElement;
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
  private setupLazyLoading(): void {
    // Find images and videos that can be lazy loaded
    const mediaElements = [
      ...document.querySelectorAll('img:not([loading="eager"])'),
      ...document.querySelectorAll('video:not([autoplay])'),
      ...document.querySelectorAll('iframe'),
      ...document.querySelectorAll('[data-src], [data-srcset], [data-background-src]')
    ];
    
    mediaElements.forEach(element => {
      if (!this.shouldIgnoreElement(element)) {
        this.setupLazyLoadForElement(element as HTMLElement);
      }
    });
  }
  
  /**
   * Check if element can be lazy loaded
   * @param element - Element to check
   * @returns Whether the element can be lazy loaded
   */
  private isLazyLoadable(element: HTMLElement): boolean {
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
   * @param element - Element to lazy load
   */
  private setupLazyLoadForElement(element: HTMLElement): void {
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
   * @param element - Element that's now visible
   * @param observer - The observer instance
   */
  private handleIntersection(element: Element, observer: IntersectionObserver): void {
    // Load the actual image/media source
    if (element.hasAttribute('data-src')) {
      (element as HTMLImageElement | HTMLIFrameElement | HTMLVideoElement).src = 
        element.getAttribute('data-src')!;
      element.removeAttribute('data-src');
    }
    
    if (element.hasAttribute('data-srcset')) {
      (element as HTMLImageElement).srcset = element.getAttribute('data-srcset')!;
      element.removeAttribute('data-srcset');
    }
    
    if (element.hasAttribute('data-background-src')) {
      (element as HTMLElement).style.backgroundImage = 
        `url('${element.getAttribute('data-background-src')}')`;
      element.removeAttribute('data-background-src');
    }
    
    // Stop observing this element
    observer.unobserve(element);
  }
    /**
   * Refresh optimizations when window is resized
   */
  private refreshOptimizations(): void {
    // Update virtualized lists - using compatibility helper
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
}

/**
 * Create and return a singleton instance of the DOMPerformanceObserver
 * @param config - Configuration for the observer
 * @returns Observer instance
 */
let observerInstance: DOMPerformanceObserver | null = null;
export function initDOMPerformanceOptimization(config: PerformanceObserverConfig = {}): DOMPerformanceObserver {
  if (!observerInstance) {
    observerInstance = new DOMPerformanceObserver(config);
  } else {
    // Update config if instance already exists
    observerInstance.config = {
      ...observerInstance.config,
      ...config,
      targets: { ...observerInstance.config.targets, ...(config.targets || {}) },
      thresholds: { ...observerInstance.config.thresholds, ...(config.thresholds || {}) }
    } as Required<PerformanceObserverConfig>;
  }
  
  return observerInstance;
}

/**
 * Get the current observer instance
 * @returns The current observer instance or null
 */
export function getDOMPerformanceObserver(): DOMPerformanceObserver | null {
  return observerInstance;
}
