/**
 * Mobile Performance Monitor
 * Monitors and reports on key performance metrics for mobile devices
 */

import { platformDetector, PlatformType } from './platform-detector';
import { EventEmitter } from './event-emitter';

export enum PerformanceMetricType {
  FPS = 'fps',
  MEMORY = 'memory',
  RENDER_TIME = 'renderTime',
  LOAD_TIME = 'loadTime',
  INTERACTION_TIME = 'interactionTime'
}

export interface PerformanceMetric {
  type: PerformanceMetricType;
  value: number;
  timestamp: n      isMobile: platformDetector.getPlatformType() === PlatformType.MOBILE,
      timespan: {
        start: Math.min(...Object.values(this.metrics).flatMap(m => m.map(x => x.timestamp))) || 0,
        end: Math.max(...Object.values(this.metrics).flatMap(m => m.map(x => x.timestamp))) || 0r;
  threshold?: {
    warning: number;
    critical: number;
  };
}

export interface PerformanceReport {
  metrics: Record<PerformanceMetricType, PerformanceMetric[]>;
  averages: Record<PerformanceMetricType, number>;
  warnings: PerformanceMetric[];
  isMobile: boolean;
  timespan: {
    start: number;
    end: number;
  };
}

export interface PerformanceMonitorConfig {
  // Whether monitoring is enabled
  enabled: boolean;
  
  // How frequently to gather metrics (ms)
  sampleInterval: number;
  
  // How many samples to keep in history
  maxSamples: number;
  
  // Whether to automatically optimize when thresholds are crossed
  autoOptimize: boolean;
  
  // Thresholds for each metric
  thresholds: {
    [key in PerformanceMetricType]?: {
      warning: number;
      critical: number;
    };
  };
  
  // Whether to log performance warnings to console
  logWarnings: boolean;
}

const DEFAULT_CONFIG: PerformanceMonitorConfig = {
  enabled: true,
  sampleInterval: 1000,
  maxSamples: 120, // 2 minutes with 1s interval
  autoOptimize: true,
  thresholds: {
    [PerformanceMetricType.FPS]: {
      warning: 30,
      critical: 15
    },
    [PerformanceMetricType.MEMORY]: {
      warning: 100 * 1024 * 1024, // 100MB
      critical: 300 * 1024 * 1024 // 300MB
    },
    [PerformanceMetricType.RENDER_TIME]: {
      warning: 16, // 16ms (60fps)
      critical: 33  // 33ms (30fps)
    },
    [PerformanceMetricType.INTERACTION_TIME]: {
      warning: 100,
      critical: 300
    }
  },
  logWarnings: true
};

/**
 * Performance Monitor for tracking application performance
 * Particularly focused on mobile device optimizations
 */
export class PerformanceMonitor {
  private static instance: PerformanceMonitor;
  private config: PerformanceMonitorConfig;
  private metrics: Record<PerformanceMetricType, PerformanceMetric[]> = {
    [PerformanceMetricType.FPS]: [],
    [PerformanceMetricType.MEMORY]: [],
    [PerformanceMetricType.RENDER_TIME]: [],
    [PerformanceMetricType.LOAD_TIME]: [],
    [PerformanceMetricType.INTERACTION_TIME]: []
  };
  
  // Collection of callbacks for metrics updates
  private updateCallbacks: Array<(metrics: any) => void> = [];
  // Event listeners map for custom event emitter implementation
  private eventListeners: Map<string, Array<(data: any) => void>> = new Map();

  private frameCounterStart: number = 0;
  private frameCount: number = 0;
  private rafId: number | null = null;
  private intervalId: number | null = null;
  private lastFrameTime: number = 0;
  
  private longTaskObserver: PerformanceObserver | null = null;
  private layoutShiftObserver: PerformanceObserver | null = null;
  
  /**
   * Get the singleton instance
   */
  public static getInstance(): PerformanceMonitor {
    if (!PerformanceMonitor.instance) {
      PerformanceMonitor.instance = new PerformanceMonitor();
    }
    return PerformanceMonitor.instance;
  }
  
  /**
   * Private constructor to enforce singleton pattern
   */
  private constructor() {
    super();
    this.config = { ...DEFAULT_CONFIG };
  }
  
  /**
   * Initialize the performance monitor
   * @param config Configuration options
   */
  public init(config?: Partial<PerformanceMonitorConfig>): void {
    // Merge provided config with defaults
    if (config) {
      this.config = { ...this.config, ...config };
      
      // Merge nested threshold properties
      if (config.thresholds) {
        this.config.thresholds = {
          ...this.config.thresholds,
          ...config.thresholds
        };
      }
    }
      // Only enable full monitoring on mobile platforms
    const isMobile = platformDetector.getPlatformType() === PlatformType.MOBILE;
    
    if (this.config.enabled) {
      // Set up FPS counter
      this.startFpsMonitoring();
      
      // Set up memory monitoring
      this.startMemoryMonitoring();
      
      // Set up performance observers if supported
      this.setupPerformanceObservers();
      
      // Start periodic sampling
      this.startSampling();
      
      console.log(`Performance monitoring initialized (Mobile: ${isMobile})`);
    }
  }
  
  /**
   * Stop all monitoring activities
   */
  public stop(): void {
    if (this.rafId !== null) {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
    
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
    
    if (this.longTaskObserver) {
      this.longTaskObserver.disconnect();
    }
    
    if (this.layoutShiftObserver) {
      this.layoutShiftObserver.disconnect();
    }
  }
  
  /**
   * Start monitoring FPS (frames per second)
   */
  private startFpsMonitoring(): void {
    this.frameCounterStart = performance.now();
    this.frameCount = 0;
    this.lastFrameTime = performance.now();
    
    const countFrame = () => {
      const now = performance.now();
      const renderTime = now - this.lastFrameTime;
      this.lastFrameTime = now;
      
      // Record render time for this frame
      this.recordMetric({
        type: PerformanceMetricType.RENDER_TIME,
        value: renderTime,
        timestamp: now,
        threshold: this.config.thresholds[PerformanceMetricType.RENDER_TIME]
      });
      
      this.frameCount++;
      this.rafId = requestAnimationFrame(countFrame);
    };
    
    this.rafId = requestAnimationFrame(countFrame);
  }
  
  /**
   * Start monitoring memory usage
   */
  private startMemoryMonitoring(): void {
    // Check if memory info is available
    if (performance && (performance as any).memory) {
      // Record initial memory usage
      this.recordMemoryMetric();
    }
  }
  
  /**
   * Record current memory usage
   */
  private recordMemoryMetric(): void {
    if ((performance as any).memory) {
      const memory = (performance as any).memory;
      const value = memory.usedJSHeapSize;
      
      this.recordMetric({
        type: PerformanceMetricType.MEMORY,
        value,
        timestamp: performance.now(),
        threshold: this.config.thresholds[PerformanceMetricType.MEMORY]
      });
    }
  }
  
  /**
   * Set up performance observers for additional metrics
   */
  private setupPerformanceObservers(): void {
    // Observe long tasks
    if (typeof PerformanceObserver !== 'undefined') {
      try {
        // Track long tasks (tasks that block the main thread)
        if (PerformanceObserver.supportedEntryTypes.includes('longtask')) {
          this.longTaskObserver = new PerformanceObserver((list) => {
            for (const entry of list.getEntries()) {
              this.recordMetric({
                type: PerformanceMetricType.INTERACTION_TIME,
                value: entry.duration,
                timestamp: performance.now(),
                threshold: this.config.thresholds[PerformanceMetricType.INTERACTION_TIME]
              });
              
              if (entry.duration > (this.config.thresholds[PerformanceMetricType.INTERACTION_TIME]?.warning || 100)) {
                console.warn(`Long task detected: ${Math.round(entry.duration)}ms`);
              }
            }
          });
          
          this.longTaskObserver.observe({ entryTypes: ['longtask'] });
        }
        
        // Track layout shifts
        if (PerformanceObserver.supportedEntryTypes.includes('layout-shift')) {
          this.layoutShiftObserver = new PerformanceObserver((list) => {
            for (const entry of list.getEntries()) {
              // Only log significant layout shifts
              if ((entry as any).value > 0.01) {
                console.debug(`Layout shift: ${(entry as any).value.toFixed(4)}`);
              }
            }
          });
          
          this.layoutShiftObserver.observe({ entryTypes: ['layout-shift'] });
        }
      } catch (error) {
        console.warn('Performance observers not fully supported', error);
      }
    }
  }
  
  /**
   * Start periodic sampling of performance metrics
   */
  private startSampling(): void {
    this.intervalId = window.setInterval(() => {
      // Calculate current FPS
      const now = performance.now();
      const elapsed = now - this.frameCounterStart;
      
      if (elapsed > 0) {
        const fps = (this.frameCount / elapsed) * 1000;
        
        // Record FPS metric
        this.recordMetric({
          type: PerformanceMetricType.FPS,
          value: fps,
          timestamp: now,
          threshold: this.config.thresholds[PerformanceMetricType.FPS]
        });
        
        // Reset FPS counter
        this.frameCounterStart = now;
        this.frameCount = 0;
      }
      
      // Record memory usage
      this.recordMemoryMetric();
      
      // Generate and emit report
      this.emitReport();
      
    }, this.config.sampleInterval);
  }
  
  /**
   * Record a performance metric
   */
  private recordMetric(metric: PerformanceMetric): void {
    // Add to metrics history
    this.metrics[metric.type].push(metric);
    
    // Keep history under max size
    while (this.metrics[metric.type].length > this.config.maxSamples) {
      this.metrics[metric.type].shift();
    }
    
    // Check for warning/critical thresholds
    if (metric.threshold) {
      const isCritical = this.isMetricCritical(metric);
      const isWarning = this.isMetricWarning(metric);
      
      if (isCritical) {
        this.emit('critical', metric);
        
        if (this.config.logWarnings) {
          console.error(`Performance critical: ${metric.type} = ${metric.value.toFixed(2)}`);
        }
        
        if (this.config.autoOptimize) {
          this.triggerOptimization(metric);
        }
      } else if (isWarning) {
        this.emit('warning', metric);
        
        if (this.config.logWarnings) {
          console.warn(`Performance warning: ${metric.type} = ${metric.value.toFixed(2)}`);
        }
      }
    }
    
    // Emit metric event
    this.emit('metric', metric);
  }
  
  /**
   * Check if a metric is at warning level
   */
  private isMetricWarning(metric: PerformanceMetric): boolean {
    if (!metric.threshold) return false;
    
    switch (metric.type) {
      case PerformanceMetricType.FPS:
        return metric.value < metric.threshold.warning;
      case PerformanceMetricType.MEMORY:
      case PerformanceMetricType.RENDER_TIME:
      case PerformanceMetricType.INTERACTION_TIME:
        return metric.value > metric.threshold.warning;
      default:
        return false;
    }
  }
  
  /**
   * Check if a metric is at critical level
   */
  private isMetricCritical(metric: PerformanceMetric): boolean {
    if (!metric.threshold) return false;
    
    switch (metric.type) {
      case PerformanceMetricType.FPS:
        return metric.value < metric.threshold.critical;
      case PerformanceMetricType.MEMORY:
      case PerformanceMetricType.RENDER_TIME:
      case PerformanceMetricType.INTERACTION_TIME:
        return metric.value > metric.threshold.critical;
      default:
        return false;
    }
  }
  
  /**
   * Trigger automatic optimizations based on performance issues
   */
  private triggerOptimization(metric: PerformanceMetric): void {
    // Dynamic optimizations based on metric type
    switch (metric.type) {
      case PerformanceMetricType.FPS:
      case PerformanceMetricType.RENDER_TIME:
        // Reduce animation complexity
        this.emit('optimize', {
          type: 'animation',
          severity: 'critical',
          metric
        });
        break;
        
      case PerformanceMetricType.MEMORY:
        // Reduce memory usage
        this.emit('optimize', {
          type: 'memory',
          severity: 'critical',
          metric
        });
        break;
        
      case PerformanceMetricType.INTERACTION_TIME:
        // Optimize blocking operations
        this.emit('optimize', {
          type: 'interaction',
          severity: 'critical',
          metric
        });
        break;
    }
  }
  
  /**
   * Generate and emit a performance report
   */
  private emitReport(): void {
    // Calculate averages
    const averages: Record<PerformanceMetricType, number> = {
      [PerformanceMetricType.FPS]: this.calculateAverage(PerformanceMetricType.FPS),
      [PerformanceMetricType.MEMORY]: this.calculateAverage(PerformanceMetricType.MEMORY),
      [PerformanceMetricType.RENDER_TIME]: this.calculateAverage(PerformanceMetricType.RENDER_TIME),
      [PerformanceMetricType.LOAD_TIME]: this.calculateAverage(PerformanceMetricType.LOAD_TIME),
      [PerformanceMetricType.INTERACTION_TIME]: this.calculateAverage(PerformanceMetricType.INTERACTION_TIME)
    };
    
    // Find warnings
    const warnings: PerformanceMetric[] = [];
    Object.values(this.metrics).forEach(metricList => {
      metricList.forEach(metric => {
        if (this.isMetricWarning(metric) || this.isMetricCritical(metric)) {
          warnings.push(metric);
        }
      });
    });
    
    // Create report
    const report: PerformanceReport = {      metrics: { ...this.metrics },
      averages,
      warnings,
      isMobile: platformDetector.getPlatformType() === PlatformType.MOBILE,
      timespan: {
        start: Math.min(...Object.values(this.metrics).flatMap(m => m.map(x => x.timestamp))),
        end: Math.max(...Object.values(this.metrics).flatMap(m => m.map(x => x.timestamp)))
      }
    };
    
    this.emit('report', report);
  }
  
  /**
   * Calculate average for a metric type
   */
  private calculateAverage(type: PerformanceMetricType): number {
    const metrics = this.metrics[type];
    if (metrics.length === 0) return 0;
    
    const sum = metrics.reduce((acc, metric) => acc + metric.value, 0);
    return sum / metrics.length;
  }
  
  /**
   * Get the most recent performance report
   */
  public getReport(): PerformanceReport {
    // Generate a fresh report
    const averages: Record<PerformanceMetricType, number> = {
      [PerformanceMetricType.FPS]: this.calculateAverage(PerformanceMetricType.FPS),
      [PerformanceMetricType.MEMORY]: this.calculateAverage(PerformanceMetricType.MEMORY),
      [PerformanceMetricType.RENDER_TIME]: this.calculateAverage(PerformanceMetricType.RENDER_TIME),
      [PerformanceMetricType.LOAD_TIME]: this.calculateAverage(PerformanceMetricType.LOAD_TIME),
      [PerformanceMetricType.INTERACTION_TIME]: this.calculateAverage(PerformanceMetricType.INTERACTION_TIME)
    };
    
    // Find warnings
    const warnings: PerformanceMetric[] = [];
    Object.values(this.metrics).forEach(metricList => {
      metricList.forEach(metric => {
        if (this.isMetricWarning(metric) || this.isMetricCritical(metric)) {
          warnings.push(metric);
        }
      });
    });
      return {
      metrics: { ...this.metrics },
      averages,
      warnings,
      isMobile: platformDetector.getPlatformType() === PlatformType.MOBILE,
      timespan: {
        start: Math.min(...Object.values(this.metrics).flatMap(m => m.map(x => x.timestamp))) || 0,
        end: Math.max(...Object.values(this.metrics).flatMap(m => m.map(x => x.timestamp))) || 0
      }
    };
  }
  
  /**
   * Start performance monitoring
   */
  public start(): void {
    if (!this.config.enabled) {
      this.config.enabled = true;
      this.init();
    }
  }
    /**
   * Register an update callback to receive performance metrics updates
   * @param callback Function to call when metrics are updated
   */
  public onUpdate(callback: (metrics: any) => void): void {
    // Store callback for metrics updates
    this.updateCallbacks.push(callback);
  }
  
  /**
   * Get the current performance metrics
   * @returns Object containing the current performance metrics
   */
  public getMetrics(): any {
    // Calculate average values for each metric type
    const fps = this.calculateAverageFps();
    const averageFrameTime = this.calculateAverageFrameTime();
    const minFrameTime = this.getMinFrameTime();
    const maxFrameTime = this.getMaxFrameTime();
    const jank = this.calculateJankScore();
    
    return {
      fps,
      frameTime: averageFrameTime,
      averageFrameTime,
      minFrameTime,
      maxFrameTime,
      jank,
      lastUpdate: Date.now()
    };
  }
  
  /**
   * Calculate the average FPS from recent frame data
   */
  private calculateAverageFps(): number {
    const renderTimeMetrics = this.metrics[PerformanceMetricType.RENDER_TIME];
    if (renderTimeMetrics.length === 0) return 0;
    
    // Use the most recent metrics up to the sample size
    const recentMetrics = renderTimeMetrics.slice(-this.config.maxSamples);
    const totalFrameTime = recentMetrics.reduce((sum, metric) => sum + metric.value, 0);
    const averageFrameTime = totalFrameTime / recentMetrics.length;
    
    // Calculate FPS from average frame time
    return averageFrameTime > 0 ? 1000 / averageFrameTime : 0;
  }
  
  /**
   * Calculate the average frame time from recent data
   */
  private calculateAverageFrameTime(): number {
    const renderTimeMetrics = this.metrics[PerformanceMetricType.RENDER_TIME];
    if (renderTimeMetrics.length === 0) return 0;
    
    // Use the most recent metrics up to the sample size
    const recentMetrics = renderTimeMetrics.slice(-this.config.maxSamples);
    const totalFrameTime = recentMetrics.reduce((sum, metric) => sum + metric.value, 0);
    
    return totalFrameTime / recentMetrics.length;
  }
  
  /**
   * Get the minimum frame time from recent data
   */
  private getMinFrameTime(): number {
    const renderTimeMetrics = this.metrics[PerformanceMetricType.RENDER_TIME];
    if (renderTimeMetrics.length === 0) return 0;
    
    // Use the most recent metrics up to the sample size
    const recentMetrics = renderTimeMetrics.slice(-this.config.maxSamples);
    return Math.min(...recentMetrics.map(metric => metric.value));
  }
  
  /**
   * Get the maximum frame time from recent data
   */
  private getMaxFrameTime(): number {
    const renderTimeMetrics = this.metrics[PerformanceMetricType.RENDER_TIME];
    if (renderTimeMetrics.length === 0) return 0;
    
    // Use the most recent metrics up to the sample size
    const recentMetrics = renderTimeMetrics.slice(-this.config.maxSamples);
    return Math.max(...recentMetrics.map(metric => metric.value));
  }
  
  /**
   * Calculate jank score (percentage of frames that took significantly longer than average)
   */
  private calculateJankScore(): number {
    const renderTimeMetrics = this.metrics[PerformanceMetricType.RENDER_TIME];
    if (renderTimeMetrics.length < 10) return 0; // Need sufficient samples
    
    // Use the most recent metrics up to the sample size
    const recentMetrics = renderTimeMetrics.slice(-this.config.maxSamples);
    const averageFrameTime = this.calculateAverageFrameTime();
    
    // Count frames that took significantly longer than average (1.5x or more)
    const jankThreshold = averageFrameTime * 1.5;
    const jankFrames = recentMetrics.filter(metric => metric.value > jankThreshold);
    
    // Calculate percentage of janky frames
    return (jankFrames.length / recentMetrics.length) * 100;
  }
  
  /**
   * Get optimization suggestions based on current performance metrics
   * @returns Array of suggestion strings
   */
  public getOptimizationSuggestions(): string[] {
    const suggestions: string[] = [];
    const metrics = this.getMetrics();
    
    // FPS suggestions
    if (metrics.fps < 30) {
      suggestions.push("FPS is significantly below target (60). Consider reducing visual complexity.");
    } else if (metrics.fps < 45) {
      suggestions.push("FPS is below optimal levels. Minor optimizations may improve smoothness.");
    }
    
    // Frame time suggestions
    if (metrics.averageFrameTime > 33.33) {
      suggestions.push("Frame time exceeds 33ms. UI will appear janky to users.");
    } else if (metrics.averageFrameTime > 16.67) {
      suggestions.push("Frame time exceeds 16.7ms. Consider performance optimizations to achieve 60fps.");
    }
    
    // Jank suggestions
    if (metrics.jank > 10) {
      suggestions.push("High jank detected. Frame timing is inconsistent and will cause noticeable stutters.");
    } else if (metrics.jank > 5) {
      suggestions.push("Moderate jank detected. Some UI interactions may appear inconsistent.");
    }
    
    // Frame time variance suggestions
    const variance = metrics.maxFrameTime - metrics.minFrameTime;
    if (variance > 50) {
      suggestions.push("Large frame time variance detected. Some operations are causing significant slowdowns.");
    }
    
    return suggestions;
  }
  
  /**
   * Dispose of the performance monitor and clean up resources
   */
  public dispose(): void {
    // Stop all monitoring
    this.stop();
    
    // Clear all event listeners
    this.removeAllListeners();
    
    // Clear all update callbacks
    this.updateCallbacks = [];
  }
  
  /**
   * Emit an event to all registered listeners
   * @param eventName The name of the event to emit
   * @param data The data to pass to the listeners
   */
  private emit(eventName: string, data: any): void {
    const listeners = this.eventListeners.get(eventName);
    if (listeners) {
      for (const listener of listeners) {
        try {
          listener(data);
        } catch (error) {
          console.error(`Error in event listener for ${eventName}:`, error);
        }
      }
    }
    
    // Also call update callbacks for 'metric' events
    if (eventName === 'metric') {
      for (const callback of this.updateCallbacks) {
        try {
          callback(this.getMetrics());
        } catch (error) {
          console.error('Error in update callback:', error);
        }
      }
    }
  }
  
  /**
   * Remove all event listeners
   */
  private removeAllListeners(): void {
    this.eventListeners.clear();
  }
}
