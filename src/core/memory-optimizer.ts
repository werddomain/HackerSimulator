/**
 * Memory Optimizer for Mobile Devices
 * Provides utilities for optimizing memory usage on constrained devices
 */

import { platformDetector, PlatformType } from './platform-detector';
import { EventEmitter } from './event-emitter';

// Memory usage thresholds in bytes
const MEMORY_WARNING_THRESHOLD = 150 * 1024 * 1024; // 150MB
const MEMORY_CRITICAL_THRESHOLD = 300 * 1024 * 1024; // 300MB

// Memory stats interface
export interface MemoryStats {
  // Total JS heap size
  totalJSHeapSize: number;
  
  // Used JS heap size
  usedJSHeapSize: number;
  
  // JS heap size limit
  jsHeapSizeLimit: number;
  
  // Percentage of heap used
  usagePercentage: number;
  
  // Whether we're in warning state
  isWarning: boolean;
  
  // Whether we're in critical state
  isCritical: boolean;
  
  // Timestamp when measured
  timestamp: number;
}

// Memory optimization levels
export enum OptimizationLevel {
  NONE = 0,
  LOW = 1,
  MEDIUM = 2,
  HIGH = 3,
  CRITICAL = 4
}

// Memory budget for specific component types
export interface MemoryBudgets {
  // Image cache size (bytes)
  imageCache: number;
  
  // DOM element limit per component
  maxElementsPerComponent: number;
  
  // List/table virtualization threshold (items)
  listVirtualizationThreshold: number;
  
  // Maximum cached objects in memory
  maxCachedObjects: number;
  
  // Animation complexity (0-1)
  animationComplexity: number;
}

/**
 * Memory Optimizer
 * Monitors and manages memory usage for mobile applications
 */
export class MemoryOptimizer extends EventEmitter {
  private static instance: MemoryOptimizer;
  
  // Current optimization level
  private optimizationLevel: OptimizationLevel = OptimizationLevel.NONE;
  
  // Memory budget for current optimization level
  private currentBudgets: MemoryBudgets;
  
  // Interval for checking memory
  private checkInterval: number | null = null;
  
  // Cached memory stats
  private memoryStats: MemoryStats | null = null;
  
  // Memory budgets for different optimization levels
  private budgets: Record<OptimizationLevel, MemoryBudgets> = {
    [OptimizationLevel.NONE]: {
      imageCache: 50 * 1024 * 1024, // 50MB
      maxElementsPerComponent: 5000,
      listVirtualizationThreshold: 200,
      maxCachedObjects: 1000,
      animationComplexity: 1.0
    },
    [OptimizationLevel.LOW]: {
      imageCache: 30 * 1024 * 1024, // 30MB
      maxElementsPerComponent: 2000,
      listVirtualizationThreshold: 100,
      maxCachedObjects: 500,
      animationComplexity: 0.8
    },
    [OptimizationLevel.MEDIUM]: {
      imageCache: 15 * 1024 * 1024, // 15MB
      maxElementsPerComponent: 1000,
      listVirtualizationThreshold: 50,
      maxCachedObjects: 200,
      animationComplexity: 0.6
    },
    [OptimizationLevel.HIGH]: {
      imageCache: 5 * 1024 * 1024, // 5MB
      maxElementsPerComponent: 500,
      listVirtualizationThreshold: 20,
      maxCachedObjects: 100,
      animationComplexity: 0.4
    },
    [OptimizationLevel.CRITICAL]: {
      imageCache: 2 * 1024 * 1024, // 2MB
      maxElementsPerComponent: 200,
      listVirtualizationThreshold: 10,
      maxCachedObjects: 50,
      animationComplexity: 0.2
    }
  };
  
  // Resource cleanup handlers
  private cleanupHandlers: Array<() => void> = [];
  
  // Image cache
  private imageCache: Map<string, HTMLImageElement> = new Map();
  
  // Object cache
  private objectCache: Map<string, any> = new Map();
  
  // Weak references to tracked objects for GC
  private trackedObjects: Map<string, WeakRef<any>> = new Map();
  
  /**
   * Get singleton instance
   */
  public static getInstance(): MemoryOptimizer {
    if (!MemoryOptimizer.instance) {
      MemoryOptimizer.instance = new MemoryOptimizer();
    }
    return MemoryOptimizer.instance;
  }
  
  /**
   * Private constructor for singleton
   */
  private constructor() {
    super();
    
    // Set initial budgets based on platform
    const isMobile = platformDetector.getPlatformType() === PlatformType.Mobile;
    this.optimizationLevel = isMobile ? OptimizationLevel.LOW : OptimizationLevel.NONE;
    this.currentBudgets = { ...this.budgets[this.optimizationLevel] };
    
    // Setup cleanup for image cache on page unload
    window.addEventListener('beforeunload', () => {
      this.clearImageCache();
    });
  }
  
  /**
   * Initialize memory monitoring
   * @param checkIntervalMs How often to check memory (ms)
   */
  public init(checkIntervalMs: number = 10000): void {
    // Start memory checks if Performance API is available
    if (performance && (performance as any).memory) {
      this.checkInterval = window.setInterval(() => {
        this.checkMemoryUsage();
      }, checkIntervalMs);
      
      // Run initial check
      this.checkMemoryUsage();
    } else {
      console.warn('Performance memory API not available, memory optimization will be limited');
      
      // Set static optimization level based on platform
      const isMobile = platformDetector.getPlatformType() === PlatformType.Mobile;
      this.setOptimizationLevel(isMobile ? OptimizationLevel.MEDIUM : OptimizationLevel.NONE);
    }
    
    // Register for low memory events on mobile devices
    // Safari iOS specific
    if ('memory' in navigator && (navigator as any).memory) {
      (navigator as any).memory.addEventListener('memorypressure', (event: any) => {
        const pressure = event.pressure || 'critical';
        
        if (pressure === 'critical') {
          this.handleLowMemory();
        }
      });
    }
  }
  
  /**
   * Stop memory monitoring
   */
  public stop(): void {
    if (this.checkInterval !== null) {
      clearInterval(this.checkInterval);
      this.checkInterval = null;
    }
  }
  
  /**
   * Add a cleanup handler to be called during low memory conditions
   * @param handler Function to call for cleanup
   * @returns Function to remove this handler
   */
  public addCleanupHandler(handler: () => void): () => void {
    this.cleanupHandlers.push(handler);
    
    return () => {
      const index = this.cleanupHandlers.indexOf(handler);
      if (index !== -1) {
        this.cleanupHandlers.splice(index, 1);
      }
    };
  }
  
  /**
   * Check current memory usage
   * @returns Memory statistics
   */
  public checkMemoryUsage(): MemoryStats | null {
    // Check if Performance API is available
    if (!(performance && (performance as any).memory)) {
      return null;
    }
    
    const memoryInfo = (performance as any).memory;
    
    const stats: MemoryStats = {
      totalJSHeapSize: memoryInfo.totalJSHeapSize,
      usedJSHeapSize: memoryInfo.usedJSHeapSize,
      jsHeapSizeLimit: memoryInfo.jsHeapSizeLimit,
      usagePercentage: (memoryInfo.usedJSHeapSize / memoryInfo.jsHeapSizeLimit) * 100,
      isWarning: memoryInfo.usedJSHeapSize > MEMORY_WARNING_THRESHOLD,
      isCritical: memoryInfo.usedJSHeapSize > MEMORY_CRITICAL_THRESHOLD,
      timestamp: Date.now()
    };
    
    this.memoryStats = stats;
    
    // Update optimization level based on memory usage
    if (stats.isCritical) {
      this.setOptimizationLevel(OptimizationLevel.CRITICAL);
      this.handleLowMemory();
    } else if (stats.isWarning) {
      this.setOptimizationLevel(OptimizationLevel.HIGH);
    } else if (stats.usagePercentage > 70) {
      this.setOptimizationLevel(OptimizationLevel.MEDIUM);
    } else if (stats.usagePercentage > 50) {
      this.setOptimizationLevel(OptimizationLevel.LOW);
    } else {
      // Keep optimization level at LOW on mobile, NONE on desktop
      const isMobile = platformDetector.getPlatformType() === PlatformType.Mobile;
      this.setOptimizationLevel(isMobile ? OptimizationLevel.LOW : OptimizationLevel.NONE);
    }
    
    // Emit memory stats for monitoring
    this.emit('memory-update', stats);
    
    return stats;
  }
  
  /**
   * Get current memory stats
   * @returns Memory statistics
   */
  public getMemoryStats(): MemoryStats | null {
    return this.memoryStats;
  }
  
  /**
   * Get current optimization level
   * @returns Current optimization level
   */
  public getOptimizationLevel(): OptimizationLevel {
    return this.optimizationLevel;
  }
  
  /**
   * Set optimization level
   * @param level Optimization level
   */
  public setOptimizationLevel(level: OptimizationLevel): void {
    if (this.optimizationLevel === level) return;
    
    const oldLevel = this.optimizationLevel;
    this.optimizationLevel = level;
    this.currentBudgets = { ...this.budgets[level] };
    
    // Emit event for level change
    this.emit('optimization-level-changed', {
      oldLevel,
      newLevel: level,
      budgets: this.currentBudgets
    });
    
    // Apply optimizations
    this.applyOptimizations();
  }
  
  /**
   * Get current memory budgets
   * @returns Current memory budgets
   */
  public getCurrentBudgets(): MemoryBudgets {
    return { ...this.currentBudgets };
  }
  
  /**
   * Handle low memory condition
   */
  private handleLowMemory(): void {
    console.warn('Low memory condition detected, cleaning up resources');
    
    // Clear all caches
    this.clearImageCache();
    this.clearObjectCache();
    
    // Run all cleanup handlers
    this.cleanupHandlers.forEach(handler => {
      try {
        handler();
      } catch (error) {
        console.error('Error in cleanup handler', error);
      }
    });
    
    // Force garbage collection hint
    this.forceGarbageCollectionHint();
    
    // Emit low memory event
    this.emit('low-memory');
  }
  
  /**
   * Apply optimizations based on current level
   */
  private applyOptimizations(): void {
    // Trim caches to match current budgets
    this.trimImageCache();
    this.trimObjectCache();
    
    // Apply animation complexity adjustments
    this.updateAnimationComplexity();
  }
  
  /**
   * Update animation complexity based on current optimization level
   */
  private updateAnimationComplexity(): void {
    // Set a CSS variable that animations can use to adjust their complexity
    document.documentElement.style.setProperty(
      '--animation-complexity',
      this.currentBudgets.animationComplexity.toString()
    );
  }
  
  /**
   * Clear image cache
   */
  public clearImageCache(): void {
    for (const [url, img] of this.imageCache.entries()) {
      // Clear image src to help browser release memory
      if (img instanceof HTMLImageElement) {
        img.src = '';
      }
    }
    
    this.imageCache.clear();
  }
  
  /**
   * Trim image cache to fit within budget
   */
  private trimImageCache(): void {
    if (this.imageCache.size === 0) return;
    
    // Check if we need to trim
    const budgetBytes = this.currentBudgets.imageCache;
    let totalSize = 0;
    
    // Create an array of [url, estimatedSize] pairs
    const imageSizes: Array<[string, number]> = [];
    
    for (const [url, img] of this.imageCache.entries()) {
      // Estimate image size in memory
      // Width * Height * 4 bytes per pixel + overhead
      let estimatedSize = 20000; // Default if we can't calculate
      
      if (img.naturalWidth && img.naturalHeight) {
        estimatedSize = img.naturalWidth * img.naturalHeight * 4 + 2000;
      }
      
      totalSize += estimatedSize;
      imageSizes.push([url, estimatedSize]);
    }
    
    // If we're over budget, remove oldest items first
    if (totalSize > budgetBytes) {
      const sortedBySize = [...imageSizes].sort((a, b) => b[1] - a[1]);
      
      let currentSize = totalSize;
      for (const [url, size] of sortedBySize) {
        if (currentSize <= budgetBytes) break;
        
        // Remove this image from cache
        const img = this.imageCache.get(url);
        if (img) {
          img.src = '';
          this.imageCache.delete(url);
          currentSize -= size;
        }
      }
    }
  }
  
  /**
   * Cache an image for reuse
   * @param url Image URL
   * @param image Image element
   */
  public cacheImage(url: string, image: HTMLImageElement): void {
    // Skip if we're in critical memory state
    if (this.optimizationLevel >= OptimizationLevel.CRITICAL) return;
    
    this.imageCache.set(url, image);
    
    // Trim cache if needed
    this.trimImageCache();
  }
  
  /**
   * Get an image from cache
   * @param url Image URL
   * @returns Cached image or null
   */
  public getCachedImage(url: string): HTMLImageElement | null {
    return this.imageCache.get(url) || null;
  }
  
  /**
   * Clear object cache
   */
  public clearObjectCache(): void {
    this.objectCache.clear();
  }
  
  /**
   * Trim object cache to fit within budget
   */
  private trimObjectCache(): void {
    if (this.objectCache.size === 0) return;
    
    // Check if we need to trim
    const maxCachedObjects = this.currentBudgets.maxCachedObjects;
    
    if (this.objectCache.size > maxCachedObjects) {
      // Find oldest entries to remove
      const entries = Array.from(this.objectCache.entries());
      const toRemove = entries.slice(0, entries.length - maxCachedObjects);
      
      for (const [key] of toRemove) {
        this.objectCache.delete(key);
      }
    }
  }
  
  /**
   * Cache an object for reuse
   * @param key Cache key
   * @param object Object to cache
   */
  public cacheObject(key: string, object: any): void {
    // Skip if we're in high memory state
    if (this.optimizationLevel >= OptimizationLevel.HIGH) return;
    
    this.objectCache.set(key, object);
    
    // Trim cache if needed
    this.trimObjectCache();
  }
  
  /**
   * Get an object from cache
   * @param key Cache key
   * @returns Cached object or null
   */
  public getCachedObject<T>(key: string): T | null {
    return (this.objectCache.get(key) as T) || null;
  }
  
  /**
   * Track an object for potential cleanup
   * @param key Tracking key
   * @param object Object to track
   */
  public trackObject(key: string, object: any): void {
    // Use WeakRef if available (to allow garbage collection)
    if (typeof WeakRef !== 'undefined') {
      this.trackedObjects.set(key, new WeakRef(object));
    }
  }
  
  /**
   * Hint to browser to run garbage collection
   * (Note: This doesn't actually force GC, just suggests it)
   */
  public forceGarbageCollectionHint(): void {
    // Clean up tracked objects map
    if (typeof WeakRef !== 'undefined' && typeof FinalizationRegistry !== 'undefined') {
      // Remove any entries whose weak references are no longer valid
      for (const [key, weakRef] of this.trackedObjects.entries()) {
        if (!weakRef.deref()) {
          this.trackedObjects.delete(key);
        }
      }
    }
    
    // Set large arrays to null to help GC
    const largeArray = new Array(10000);
    for (let i = 0; i < 10000; i++) {
      largeArray[i] = new Object();
    }
    
    // Now null it out
    for (let i = 0; i < 10000; i++) {
      largeArray[i] = null;
    }
  }
  
  /**
   * Check if virtualization should be used for a list
   * @param itemCount Number of items in the list
   * @returns Whether to use virtualization
   */
  public shouldVirtualizeList(itemCount: number): boolean {
    return itemCount > this.currentBudgets.listVirtualizationThreshold;
  }
  
  /**
   * Get max elements per component based on current budget
   * @returns Maximum number of elements
   */
  public getMaxElementsPerComponent(): number {
    return this.currentBudgets.maxElementsPerComponent;
  }
}
