/**
 * Performance Monitor
 * Tracks rendering performance metrics like FPS and frame times
 * Provides analysis and optimization suggestions for mobile devices
 */

export interface PerformanceMetrics {
  fps: number;
  frameTime: number;
  averageFrameTime: number;
  minFrameTime: number;
  maxFrameTime: number;
  jank: number; // Jank score (% of frames that took significantly longer than average)
  lastUpdate: number;
}

export interface PerformanceConfig {
  sampleSize: number;
  targetFps: number;
  criticalFrameTime: number;
  smoothingFactor: number;
}

/**
 * Performance Monitor class to track and analyze rendering performance
 */
export class PerformanceMonitor {
  private metrics: PerformanceMetrics;
  private config: PerformanceConfig;
  private frameTimes: number[] = [];
  private lastFrameTimestamp: number = 0;
  private frameIndex: number = 0;
  private animationFrameId: number | null = null;
  private isMonitoring: boolean = false;
  private onUpdateCallback: ((metrics: PerformanceMetrics) => void) | null = null;

  /**
   * Creates a new Performance Monitor instance
   */
  constructor(config?: Partial<PerformanceConfig>) {
    // Default configuration
    this.config = {
      sampleSize: 60, // Number of frames to sample for averages
      targetFps: 60, // Target FPS (60 for most devices, 120 for high refresh rate)
      criticalFrameTime: 16.67, // Critical frame time in ms (corresponds to 60fps)
      smoothingFactor: 0.1, // Smoothing factor for moving averages (0-1)
      ...config
    };

    // Initialize metrics
    this.metrics = {
      fps: 0,
      frameTime: 0,
      averageFrameTime: 0,
      minFrameTime: Number.MAX_VALUE,
      maxFrameTime: 0,
      jank: 0,
      lastUpdate: 0
    };
  }

  /**
   * Start monitoring performance
   */
  public start(): void {
    if (this.isMonitoring) return;

    this.isMonitoring = true;
    this.lastFrameTimestamp = performance.now();
    this.animationFrameId = requestAnimationFrame(this.frameCallback);
    console.log('Performance monitoring started');
  }

  /**
   * Stop monitoring performance
   */
  public stop(): void {
    if (!this.isMonitoring) return;

    this.isMonitoring = false;
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }
    console.log('Performance monitoring stopped');
  }

  /**
   * Set a callback for metric updates
   */
  public onUpdate(callback: (metrics: PerformanceMetrics) => void): void {
    this.onUpdateCallback = callback;
  }

  /**
   * Get the current performance metrics
   */
  public getMetrics(): PerformanceMetrics {
    return { ...this.metrics };
  }

  /**
   * Reset all metrics
   */
  public reset(): void {
    this.frameTimes = [];
    this.frameIndex = 0;
    this.metrics = {
      fps: 0,
      frameTime: 0,
      averageFrameTime: 0,
      minFrameTime: Number.MAX_VALUE,
      maxFrameTime: 0,
      jank: 0,
      lastUpdate: 0
    };
    console.log('Performance metrics reset');
  }

  /**
   * Update configuration options
   */
  public updateConfig(config: Partial<PerformanceConfig>): void {
    this.config = { ...this.config, ...config };
  }

  /**
   * Frame callback for performance monitoring
   */
  private frameCallback = (timestamp: number): void => {
    if (!this.isMonitoring) return;

    // Calculate frame time
    const frameTime = timestamp - this.lastFrameTimestamp;
    this.lastFrameTimestamp = timestamp;

    // Store frame time in circular buffer
    if (this.frameTimes.length < this.config.sampleSize) {
      this.frameTimes.push(frameTime);
    } else {
      this.frameTimes[this.frameIndex] = frameTime;
    }

    // Update frame index
    this.frameIndex = (this.frameIndex + 1) % this.config.sampleSize;

    // Update metrics (only every N frames to avoid performance overhead)
    if (this.frameIndex % 5 === 0) {
      this.updateMetrics();
    }

    // Request next frame
    this.animationFrameId = requestAnimationFrame(this.frameCallback);
  };

  /**
   * Update metrics based on collected frame times
   */
  private updateMetrics(): void {
    if (this.frameTimes.length === 0) return;

    // Calculate metrics
    const currentFrameTime = this.frameTimes[this.frameIndex > 0 ? this.frameIndex - 1 : this.frameTimes.length - 1];

    // Calculate averages, min, max
    let sum = 0;
    let min = Number.MAX_VALUE;
    let max = 0;
    let jankFrames = 0;

    for (const time of this.frameTimes) {
      sum += time;
      min = Math.min(min, time);
      max = Math.max(max, time);
    }

    const average = sum / this.frameTimes.length;

    // Calculate jank (frames that took significantly longer than average)
    // A common threshold is 2x the average frame time
    for (const time of this.frameTimes) {
      if (time > average * 2) {
        jankFrames++;
      }
    }

    const jankScore = (jankFrames / this.frameTimes.length) * 100;

    // Update metrics with smoothing
    const alpha = this.config.smoothingFactor;
    this.metrics.frameTime = currentFrameTime;
    this.metrics.averageFrameTime = average * alpha + (this.metrics.averageFrameTime || average) * (1 - alpha);
    this.metrics.minFrameTime = min;
    this.metrics.maxFrameTime = max;
    this.metrics.fps = 1000 / this.metrics.averageFrameTime;
    this.metrics.jank = jankScore;
    this.metrics.lastUpdate = performance.now();

    // Call update callback if set
    if (this.onUpdateCallback) {
      this.onUpdateCallback(this.getMetrics());
    }
  }

  /**
   * Get optimization suggestions based on current metrics
   */
  public getOptimizationSuggestions(): string[] {
    const suggestions: string[] = [];
    const metrics = this.getMetrics();

    // FPS too low
    if (metrics.fps < this.config.targetFps * 0.9) {
      suggestions.push('Frame rate is below target. Consider reducing visual complexity or animations.');
    }

    // High jank score
    if (metrics.jank > 5) {
      suggestions.push('High jank detected. Look for expensive operations blocking the main thread.');
    }

    // Large gap between min and max frame times
    if (metrics.maxFrameTime > metrics.minFrameTime * 3) {
      suggestions.push('Large frame time variance detected. Optimize inconsistent operations.');
    }

    // Frame time above critical threshold
    if (metrics.averageFrameTime > this.config.criticalFrameTime) {
      suggestions.push(`Average frame time (${metrics.averageFrameTime.toFixed(2)}ms) exceeds target (${this.config.criticalFrameTime}ms).`);
    }

    return suggestions;
  }

  /**
   * Clean up resources
   */
  public dispose(): void {
    this.stop();
    this.onUpdateCallback = null;
  }
}
