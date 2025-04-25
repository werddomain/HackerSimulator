// filepath: c:\Users\clefw\source\repos\HackerSimulator\src\core\mobile-performance-optimizer.ts
/**
 * Mobile Performance Optimizer
 * Integrates all performance optimization modules and provides a simple API
 */

import * as DOMOptimizer from './dom-optimizer';
import { initDOMPerformanceOptimization, getDOMPerformanceObserver } from './dom-performance-observer';
import * as CSSOptimizer from './mobile-css-optimizer';
import { PerformanceMonitor } from './performance-monitor';

/**
 * Configuration for DOM optimizations
 */
interface DOMOptimizationConfig {
  enabled: boolean;
  optimizeAnimations: boolean;
  optimizeScrolling: boolean;
  optimizeLists: boolean;
  lazyLoadMedia: boolean;
}

/**
 * Configuration for CSS optimizations
 */
interface CSSOptimizationConfig {
  enabled: boolean;
  injectOptimizedRules: boolean;
  optimizeAnimations: boolean;
  analyzeSelectors: boolean;
}

/**
 * Configuration for hardware acceleration
 */
interface HardwareAccelerationConfig {
  enabled: boolean;
  forceAcceleration: boolean;
  detectGPUCapabilities: boolean;
}

/**
 * Configuration for performance thresholds
 */
interface PerformanceThresholdConfig {
  targetFps: number;
  criticalFrameTime: number;
  jankThreshold: number;
}

/**
 * Configuration for the performance optimizer
 */
interface PerformanceOptimizerConfig {
  enabled: boolean;
  showMetrics: boolean;
  dom: DOMOptimizationConfig;
  css: CSSOptimizationConfig;
  hardware: HardwareAccelerationConfig;
  thresholds: PerformanceThresholdConfig;
}

/**
 * Device capabilities information
 */
interface DeviceCapabilities {
  supportsPassiveEvents: boolean;
  supportsWillChange: boolean;
  supportsTransform3d: boolean;
  gpuMemoryEstimate: string;
  isLowEndDevice: boolean;
  isHighEndDevice: boolean;
}

/**
 * Default configuration for the performance optimizer
 */
const DEFAULT_CONFIG: PerformanceOptimizerConfig = {
  // Whether to enable performance optimizations
  enabled: true,
  
  // Whether to show performance metrics in the UI
  showMetrics: false,
  
  // Options for DOM optimizations
  dom: {
    enabled: true,
    optimizeAnimations: true,
    optimizeScrolling: true,
    optimizeLists: true,
    lazyLoadMedia: true
  },
  
  // Options for CSS optimizations
  css: {
    enabled: true,
    injectOptimizedRules: true,
    optimizeAnimations: true,
    analyzeSelectors: true
  },
  
  // Options for hardware acceleration
  hardware: {
    enabled: true,
    forceAcceleration: false, // Force acceleration even on lower-end devices
    detectGPUCapabilities: true
  },
  
  // Performance thresholds
  thresholds: {
    targetFps: 60,
    criticalFrameTime: 16.67, // ms (60fps = 16.67ms per frame)
    jankThreshold: 5 // % of frames that can be janky before warnings
  }
};

/**
 * Class that manages performance optimizations for mobile devices
 */
export class MobilePerformanceOptimizer {
  public config: PerformanceOptimizerConfig;
  private performanceMonitor: PerformanceMonitor | null;
  private domObserver: any | null;
  private acceleratedElements: WeakSet<HTMLElement>;
  private deviceCapabilities: DeviceCapabilities;
  
  constructor(config: Partial<PerformanceOptimizerConfig> = {}) {
    // Merge default config with provided config
    this.config = { 
      ...DEFAULT_CONFIG,
      ...config,
      dom: { ...DEFAULT_CONFIG.dom, ...(config.dom || {}) },
      css: { ...DEFAULT_CONFIG.css, ...(config.css || {}) },
      hardware: { ...DEFAULT_CONFIG.hardware, ...(config.hardware || {}) },
      thresholds: { ...DEFAULT_CONFIG.thresholds, ...(config.thresholds || {}) }
    };
    
    // Performance monitor instance
    this.performanceMonitor = null;
    
    // DOM observer instance
    this.domObserver = null;
    
    // Store for known hardware-accelerated elements
    this.acceleratedElements = new WeakSet<HTMLElement>();
    
    // Store detected device capabilities
    this.deviceCapabilities = {
      supportsPassiveEvents: false,
      supportsWillChange: false,
      supportsTransform3d: false,
      gpuMemoryEstimate: 'unknown',
      isLowEndDevice: false,
      isHighEndDevice: false
    };
    
    // Bind methods
    this.onFrameCallback = this.onFrameCallback.bind(this);
    this.handleTouchStart = this.handleTouchStart.bind(this);
    this.handleTouchMove = this.handleTouchMove.bind(this);
    this.handleTouchEnd = this.handleTouchEnd.bind(this);
    
    // Initialize
    this.initialize();
  }
  
  /**
   * Initialize performance optimizations
   */
  initialize(): void {
    if (!this.config.enabled) return;
    
    console.log('Initializing Mobile Performance Optimizations');
    
    // Detect device capabilities
    this.detectDeviceCapabilities();
    
    // Initialize performance monitor
    this.initializePerformanceMonitor();
    
    // Initialize DOM optimizations
    if (this.config.dom.enabled) {
      this.initializeDOMOptimizations();
    }
    
    // Initialize CSS optimizations
    if (this.config.css.enabled) {
      this.initializeCSSOptimizations();
    }
    
    // Set up global touch event handlers
    this.setupGlobalTouchHandlers();
    
    // Apply high-level optimizations
    this.applyHighLevelOptimizations();
  }
  
  /**
   * Detect device capabilities for optimization decisions
   */
  detectDeviceCapabilities(): void {
    // Check for passive event listener support
    try {
      const opts = Object.defineProperty({}, 'passive', {
        get: () => {
          this.deviceCapabilities.supportsPassiveEvents = true;
          return true;
        }
      });
      window.addEventListener('testPassive', null as any, opts);
      window.removeEventListener('testPassive', null as any, opts);
    } catch (e) {}
    
    // Check for will-change support
    this.deviceCapabilities.supportsWillChange = 'willChange' in document.documentElement.style;
    
    // Check for 3D transform support
    this.deviceCapabilities.supportsTransform3d = !!(
      document.documentElement.style.transform || 
      (document.documentElement.style as any).webkitTransform || 
      (document.documentElement.style as any).mozTransform
    );
    
    // Attempt to get GPU memory (where supported)
    if ((navigator as any).gpu && (navigator as any).gpu.wgslLanguageFeatures) {
      this.deviceCapabilities.gpuMemoryEstimate = 'high';
    } else if (window.performance && (window.performance as any).memory) {
      const memory = (window.performance as any).memory;
      if (memory.jsHeapSizeLimit) {
        if (memory.jsHeapSizeLimit < 200000000) {
          this.deviceCapabilities.gpuMemoryEstimate = 'low';
          this.deviceCapabilities.isLowEndDevice = true;
        } else if (memory.jsHeapSizeLimit > 1000000000) {
          this.deviceCapabilities.gpuMemoryEstimate = 'high';
          this.deviceCapabilities.isHighEndDevice = true;
        } else {
          this.deviceCapabilities.gpuMemoryEstimate = 'medium';
        }
      }
    }
    
    // Check if it's a low-end device based on navigator information
    if ((navigator as any).deviceMemory && (navigator as any).deviceMemory < 4) {
      this.deviceCapabilities.isLowEndDevice = true;
    }
    
    if (navigator.hardwareConcurrency && navigator.hardwareConcurrency <= 4) {
      this.deviceCapabilities.isLowEndDevice = true;
    }
    
    // Check if it's a high-end device
    if ((navigator as any).deviceMemory && (navigator as any).deviceMemory >= 8) {
      this.deviceCapabilities.isHighEndDevice = true;
    }
    
    if (navigator.hardwareConcurrency && navigator.hardwareConcurrency >= 8) {
      this.deviceCapabilities.isHighEndDevice = true;
    }
    
    console.log('Detected device capabilities:', this.deviceCapabilities);
  }
  
  /**
   * Initialize performance monitoring
   */
  initializePerformanceMonitor(): void {
    this.performanceMonitor = new PerformanceMonitor({
      targetFps: this.config.thresholds.targetFps,
      criticalFrameTime: this.config.thresholds.criticalFrameTime,
      smoothingFactor: 0.1
    });
    
    // Set up metrics update callback if performance metrics UI is enabled
    if (this.config.showMetrics) {
      this.performanceMonitor.onUpdate(this.onFrameCallback);
    }
    
    // Start monitoring
    this.performanceMonitor.start();
  }
  
  /**
   * Handle performance monitor frame updates
   * @param metrics - Updated performance metrics
   */
  onFrameCallback(metrics: any): void {
    // Check for performance issues
    if (metrics.fps < this.config.thresholds.targetFps * 0.8 || 
        metrics.jank > this.config.thresholds.jankThreshold) {
      
      this.handlePerformanceIssue(metrics);
    }
  }
  
  /**
   * Handle detected performance issues
   * @param metrics - Current performance metrics
   */
  handlePerformanceIssue(metrics: any): void {
    // Only react to sustained performance issues
    if (metrics.fps < this.config.thresholds.targetFps * 0.7 && 
        metrics.jank > this.config.thresholds.jankThreshold) {
      
      console.warn('Performance issue detected:', metrics);
      
      // Get optimization suggestions
      const suggestions = this.performanceMonitor.getOptimizationSuggestions();
      
      // Apply automatic optimizations to improve performance
      if (suggestions.length > 0) {
        this.applyAutomaticOptimizations();
      }
    }
  }
  
  /**
   * Initialize DOM optimizations
   */
  initializeDOMOptimizations(): void {
    // Initialize the DOM performance observer
    this.domObserver = initDOMPerformanceOptimization({
      enabled: true,
      targets: {
        animations: this.config.dom.optimizeAnimations,
        scrollContainers: this.config.dom.optimizeScrolling,
        listRendering: this.config.dom.optimizeLists,
        mediaLoading: this.config.dom.lazyLoadMedia,
        frequentUpdates: true
      },
      thresholds: {
        largeListThreshold: this.deviceCapabilities.isLowEndDevice ? 20 : 30
      }
    });
  }
  
  /**
   * Initialize CSS optimizations
   */
  initializeCSSOptimizations(): void {
    // Only inject optimized CSS rules if configured to do so
    if (this.config.css.injectOptimizedRules) {
      this.injectOptimizedCSS();
    }
    
    // Analyze existing stylesheets
    if (this.config.css.analyzeSelectors) {
      this.analyzeExistingStylesheets();
    }
  }
  
  /**
   * Inject optimized CSS rules
   */
  injectOptimizedCSS(): void {
    // Create a style element for our optimized CSS
    const styleElement = document.createElement('style');
    styleElement.id = 'mobile-performance-optimizations';
    styleElement.textContent = CSSOptimizer.createMobilePerformanceCSS();
    
    // Add to document head
    document.head.appendChild(styleElement);
  }
  
  /**
   * Analyze existing stylesheets for performance issues
   */
  analyzeExistingStylesheets(): void {
    // Defer this operation to avoid blocking rendering
    DOMOptimizer.deferOperation(() => {
      const styleSheets = Array.from(document.styleSheets);
      let totalIssues = 0;
      
      styleSheets.forEach(styleSheet => {
        try {
          // Skip cross-origin stylesheets
          if (styleSheet.href && 
              new URL(styleSheet.href).origin !== window.location.origin) {
            return;
          }
          
          const issues = CSSOptimizer.analyzeStylesheet(styleSheet);
          totalIssues += issues.length;
          
          if (issues.length > 0) {
            console.warn(`CSS Performance issues in ${styleSheet.href || 'inline stylesheet'}:`, issues);
          }
        } catch (e) {
          // Skip stylesheets we can't access (CORS issues)
          console.warn('Could not analyze stylesheet:', e);
        }
      });
      
      console.log(`CSS Analysis complete: ${totalIssues} issues found`);
    });
  }
  
  /**
   * Set up global touch event handlers
   */
  setupGlobalTouchHandlers(): void {
    // Use passive event listeners for touch events to improve scrolling
    if (this.deviceCapabilities.supportsPassiveEvents) {
      // Main touch events
      document.addEventListener('touchstart', this.handleTouchStart, { passive: true });
      document.addEventListener('touchmove', this.handleTouchMove, { passive: true });
      document.addEventListener('touchend', this.handleTouchEnd, { passive: true });
    }
  }
  
  /**
   * Apply high-level performance optimizations
   */
  applyHighLevelOptimizations(): void {
    // Apply hardware acceleration to key elements
    if (this.config.hardware.enabled) {
      this.applyHardwareAcceleration();
    }
  }
  
  /**
   * Apply hardware acceleration to key UI elements
   */
  applyHardwareAcceleration(): void {
    // Target elements that would benefit from hardware acceleration
    const accelerationTargets = [
      // Navigation elements
      '.navigation', '.nav', '.navbar', '.menu', 
      // Fixed elements
      '.header', '.footer', '.sidebar', '.toolbar',
      // Dialog and modal elements
      '.modal', '.dialog', '.popup', '.drawer',
      // Animation containers
      '.carousel', '.slider', '.swiper',
      // Scrollable containers with lots of content
      '.scroll-container', '.virtualized-list'
    ];
    
    // Query for these elements
    const elements = document.querySelectorAll(accelerationTargets.join(', '));
    
    // Apply hardware acceleration
    elements.forEach(element => {
      this.applyHardwareAccelerationToElement(element as HTMLElement);
    });
    
    // Also look for semantic elements that might benefit
    const semanticElements = [
      ...document.querySelectorAll('header, footer, nav'),
      ...document.querySelectorAll('div[role="dialog"], div[role="menu"]'),
      ...document.querySelectorAll('div[style*="position: fixed"], div[style*="position: sticky"]')
    ];
    
    semanticElements.forEach(element => {
      this.applyHardwareAccelerationToElement(element as HTMLElement);
    });
  }
  
  /**
   * Apply hardware acceleration to a specific element
   * @param element - Element to accelerate
   */
  applyHardwareAccelerationToElement(element: HTMLElement): void {
    if (!element || this.acceleratedElements.has(element)) return;
    
    // For low-end devices, only accelerate when necessary
    if (this.deviceCapabilities.isLowEndDevice && !this.config.hardware.forceAcceleration) {
      // Only accelerate elements that are animated or fixed
      const style = window.getComputedStyle(element);
      const needsAcceleration = 
        style.position === 'fixed' || 
        style.position === 'sticky' ||
        style.animation !== 'none' ||
        style.transition !== 'none';
      
      if (!needsAcceleration) return;
    }
    
    // Apply optimizations
    DOMOptimizer.optimizeForAnimation(element);
    
    // Mark as accelerated
    this.acceleratedElements.add(element);
    
    // Set up cleanup when element is no longer visible
    this.setupAccelerationCleanup(element);
  }
  
  /**
   * Set up cleanup for hardware accelerated elements
   * @param element - Element to monitor
   */
  setupAccelerationCleanup(element: HTMLElement): void {
    if (!element) return;
    
    // Use IntersectionObserver to detect when element is no longer visible
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (!entry.isIntersecting && this.acceleratedElements.has(element)) {
          // Element is no longer visible, remove hardware acceleration
          DOMOptimizer.resetOptimization(element);
          this.acceleratedElements.delete(element);
          observer.unobserve(element);
        }
      });
    }, { rootMargin: '0px', threshold: 0 });
    
    observer.observe(element);
  }
  
  /**
   * Apply automatic optimizations when performance issues are detected
   */
  applyAutomaticOptimizations(): void {
    // Reduce animation complexity
    this.reduceAnimationComplexity();
    
    // Optimize lists and tables
    this.optimizeListsAndTables();
    
    // Apply additional hardware acceleration
    this.applyAdditionalHardwareAcceleration();
  }
  
  /**
   * Reduce animation complexity to improve performance
   */
  reduceAnimationComplexity(): void {
    // Find all elements with animations
    const animatedElements = document.querySelectorAll(
      '.animate, .transition, [style*="animation"], [style*="transition"]'
    );
    
    animatedElements.forEach(element => {
      // Get current animation state
      const style = window.getComputedStyle(element);
      
      // Only modify elements that are using CPU-intensive properties
      const hasExpensiveAnimations = (DOMOptimizer as any).LAYOUT_THRASHING_PROPERTIES.some(
        (prop: string) => style.getPropertyValue(`transition-property`).includes(prop) ||
               style.getPropertyValue(`animation-name`)
      );
      
      if (hasExpensiveAnimations) {
        // Add a CSS class that will override with simpler animations
        element.classList.add('performance-optimized-animation');
        
        // Force hardware acceleration
        DOMOptimizer.optimizeForAnimation(element as HTMLElement);
      }
    });
    
    // Inject a style with simplified animations
    if (!document.getElementById('simplified-animations')) {
      const styleElement = document.createElement('style');
      styleElement.id = 'simplified-animations';
      styleElement.textContent = `
        .performance-optimized-animation {
          animation-duration: 300ms !important;
          transition-duration: 300ms !important;
          animation-timing-function: ease-out !important;
          transition-timing-function: ease-out !important;
          will-change: transform, opacity !important;
          transform: translateZ(0) !important;
        }
      `;
      document.head.appendChild(styleElement);
    }
  }
  
  /**
   * Optimize lists and tables for better performance
   */
  optimizeListsAndTables(): void {
    // Find large lists and tables
    const lists = document.querySelectorAll('ul, ol, table, [role="grid"], [role="list"]');
    
    lists.forEach(list => {
      const itemCount = list.querySelectorAll('li, tr').length;
      
      // Only virtualize large lists
      if (itemCount > 50) {
        // Check if this list already has a virtualized controller
        const observer = getDOMPerformanceObserver();
        if (observer && (observer as any).virtualizedLists && !(observer as any).virtualizedLists.has(list)) {
          // Force the DOM observer to virtualize this list
          (observer as any).optimizeList(list);
        }
      }
    });
  }
  
  /**
   * Apply additional hardware acceleration when performance issues are detected
   */
  applyAdditionalHardwareAcceleration(): void {
    // Find visible elements in viewport
    DOMOptimizer.deferOperation(() => {
      const visibleElements = Array.from(document.querySelectorAll(
        'div, section, main, aside, header, footer, nav'
      )).filter(el => {
        const rect = el.getBoundingClientRect();
        return (
          rect.width > 50 && 
          rect.height > 50 && 
          rect.top >= -rect.height && 
          rect.top <= window.innerHeight &&
          rect.left >= -rect.width && 
          rect.left <= window.innerWidth
        );
      });
      
      // Apply hardware acceleration to these elements
      visibleElements.forEach(element => {
        this.applyHardwareAccelerationToElement(element as HTMLElement);
      });
    });
  }
  
  /**
   * Handle touch start events
   * @param event - Touch start event
   */
  handleTouchStart(event: TouchEvent): void {
    // No-op for now, just using passive listeners for performance
  }
  
  /**
   * Handle touch move events
   * @param event - Touch move event
   */
  handleTouchMove(event: TouchEvent): void {
    // No-op for now, just using passive listeners for performance
  }
  
  /**
   * Handle touch end events
   * @param event - Touch end event
   */
  handleTouchEnd(event: TouchEvent): void {
    // No-op for now, just using passive listeners for performance
  }
  
  /**
   * Optimize a specific element for performance
   * @param element - Element to optimize
   */
  optimizeElement(element: HTMLElement): void {
    if (!element) return;
    
    // Apply DOM optimizations
    DOMOptimizer.optimizeForAnimation(element);
    
    // Apply CSS optimizations via inline styles
    const optimizedStyles = CSSOptimizer.createOptimizedInlineStyles(element);
    if (optimizedStyles) {
      element.setAttribute('style', 
        (element.getAttribute('style') || '') + ' ' + optimizedStyles
      );
    }
    
    // Apply hardware acceleration if appropriate
    this.applyHardwareAccelerationToElement(element);
    
    // Also optimize children if needed
    if (element.children.length > 0) {
      // Only optimize direct children for performance
      Array.from(element.children).forEach(child => {
        // Check if child element needs optimization
        const style = window.getComputedStyle(child);
        const needsOptimization = 
          style.position === 'absolute' || 
          style.position === 'fixed' || 
          style.animation !== 'none' ||
          style.transition !== 'none';
          
        if (needsOptimization) {
          DOMOptimizer.optimizeForAnimation(child as HTMLElement);
        }
      });
    }
  }
  
  /**
   * Dispose the performance optimizer and cleanup resources
   */
  dispose(): void {
    // Stop performance monitoring
    if (this.performanceMonitor) {
      this.performanceMonitor.dispose();
    }
    
    // Dispose DOM observer
    if (this.domObserver) {
      this.domObserver.dispose();
    }
    
    // Remove global event listeners
    document.removeEventListener('touchstart', this.handleTouchStart);
    document.removeEventListener('touchmove', this.handleTouchMove);
    document.removeEventListener('touchend', this.handleTouchEnd);
    
    // Remove injected styles
    const optimizedStyles = document.getElementById('mobile-performance-optimizations');
    if (optimizedStyles) {
      optimizedStyles.remove();
    }
    
    const simplifiedAnimations = document.getElementById('simplified-animations');
    if (simplifiedAnimations) {
      simplifiedAnimations.remove();
    }
    
    console.log('Mobile Performance Optimizer disposed');
  }
}

/**
 * Create and return a singleton instance of the MobilePerformanceOptimizer
 * @param config - Configuration for the optimizer
 * @returns - Optimizer instance
 */
let optimizerInstance: MobilePerformanceOptimizer | null = null;

export function initMobilePerformanceOptimizer(config: Partial<PerformanceOptimizerConfig> = {}): MobilePerformanceOptimizer {
  if (!optimizerInstance) {
    optimizerInstance = new MobilePerformanceOptimizer(config);
  } else {
    // Update config if instance already exists
    optimizerInstance.config = {
      ...optimizerInstance.config,
      ...config,
      dom: { ...optimizerInstance.config.dom, ...(config.dom || {}) },
      css: { ...optimizerInstance.config.css, ...(config.css || {}) },
      hardware: { ...optimizerInstance.config.hardware, ...(config.hardware || {}) },
      thresholds: { ...optimizerInstance.config.thresholds, ...(config.thresholds || {}) }
    };
  }
  
  return optimizerInstance;
}

/**
 * Get the current optimizer instance
 * @returns The current optimizer instance or null
 */
export function getMobilePerformanceOptimizer(): MobilePerformanceOptimizer | null {
  return optimizerInstance;
}
