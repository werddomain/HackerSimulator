/**
 * Progressive Loading System
 * Enables staged loading of application features based on priority and user interaction
 */

/**
 * Priority levels for feature loading
 */
export enum LoadPriority {
  CRITICAL = 0,   // Load immediately (core functionality)
  HIGH = 1,       // Load during startup, but can be deferred slightly
  MEDIUM = 2,     // Load after critical features, but before user interaction
  LOW = 3,        // Load after initial interaction or when idle
  ON_DEMAND = 4   // Load only when explicitly requested
}

/**
 * Loading stages for the application
 */
export enum LoadStage {
  INITIAL,        // Initial loading phase
  CORE_READY,     // Core features loaded and ready
  INTERACTIVE,    // Application is interactive
  COMPLETE,       // All high and medium priority features are loaded
  IDLE            // Application is idle, can load low priority features
}

/**
 * Feature loading status
 */
export enum LoadStatus {
  PENDING,        // Not yet loaded
  LOADING,        // Currently loading
  LOADED,         // Successfully loaded
  FAILED          // Failed to load
}

/**
 * Feature dependency type
 */
export type FeatureDependency = string | (() => boolean);

/**
 * Feature module loading definition
 */
export interface FeatureModule {
  id: string;                       // Unique identifier
  priority: LoadPriority;           // Loading priority
  dependencies?: string[];          // Features that must be loaded first
  condition?: () => boolean;        // Condition function that must return true to load
  loader: () => Promise<any>;       // Function that loads the feature
  onLoad?: (module: any) => void;   // Called when feature is loaded
  onFailure?: (error: Error) => void; // Called when feature fails to load
  timeout?: number;                 // Timeout in ms before considering failed
  retry?: boolean;                  // Whether to retry on failure
  maxRetries?: number;              // Maximum number of retry attempts
  stage?: LoadStage;                // Explicit loading stage (optional)
}

/**
 * Feature loading information
 */
interface FeatureInfo {
  module: FeatureModule;
  status: LoadStatus;
  loadPromise: Promise<any> | null;
  instance: any | null;
  retryCount: number;
  error: Error | null;
  loadStartTime: number | null;
  loadEndTime: number | null;
}

/**
 * Progressive Loading Manager
 * Coordinates the loading of application features in stages
 */
export class ProgressiveLoader {
  private features: Map<string, FeatureInfo> = new Map();
  private currentStage: LoadStage = LoadStage.INITIAL;
  private stageListeners: Map<LoadStage, Function[]> = new Map();
  private statusListeners: Map<string, ((status: LoadStatus) => void)[]> = new Map();
  private idleCallbackId: number | null = null;
  private isLoadingPaused: boolean = false;
  private loadQueue: string[] = [];
  private loadStartTime: number;
  private metricsEnabled: boolean = true;
  private metrics: {
    stageTimings: Record<string, { start: number, end: number }>;
    featureTimings: Record<string, { start: number, end: number, duration: number }>;
    totalDuration: number;
  };
  
  /**
   * Create a new progressive loader
   */
  constructor() {
    this.loadStartTime = performance.now();
    this.metrics = {
      stageTimings: {},
      featureTimings: {},
      totalDuration: 0
    };
    
    // Initialize stage metrics
    Object.values(LoadStage).forEach(stage => {
      if (typeof stage === 'string') return;
      this.metrics.stageTimings[LoadStage[stage]] = { start: 0, end: 0 };
    });
    
    // Start stage timing for initial stage
    this.metrics.stageTimings[LoadStage[LoadStage.INITIAL]] = { 
      start: this.loadStartTime, 
      end: 0 
    };
  }
  
  /**
   * Register a feature module for loading
   * @param module Feature module definition
   */
  public registerFeature(module: FeatureModule): void {
    if (this.features.has(module.id)) {
      console.warn(`Feature ${module.id} is already registered`);
      return;
    }
    
    const featureInfo: FeatureInfo = {
      module,
      status: LoadStatus.PENDING,
      loadPromise: null,
      instance: null,
      retryCount: 0,
      error: null,
      loadStartTime: null,
      loadEndTime: null
    };
    
    this.features.set(module.id, featureInfo);
    
    // Add to load queue if it's a critical feature
    if (module.priority === LoadPriority.CRITICAL) {
      this.loadQueue.push(module.id);
    }
  }
  
  /**
   * Register multiple feature modules at once
   * @param modules Array of feature modules
   */
  public registerFeatures(modules: FeatureModule[]): void {
    modules.forEach(module => this.registerFeature(module));
  }
  
  /**
   * Start the progressive loading process
   */
  public startLoading(): void {
    if (this.currentStage !== LoadStage.INITIAL) {
      console.warn('Progressive loading has already started');
      return;
    }
    
    console.log('Starting progressive loading');
    
    // Start loading critical features immediately
    this.loadCriticalFeatures()
      .then(() => {
        // Advance to CORE_READY stage
        this.advanceToStage(LoadStage.CORE_READY);
        
        // Continue with high priority features
        return this.loadPriorityFeatures(LoadPriority.HIGH);
      })
      .then(() => {
        // Advance to INTERACTIVE stage
        this.advanceToStage(LoadStage.INTERACTIVE);
        
        // Continue with medium priority features
        return this.loadPriorityFeatures(LoadPriority.MEDIUM);
      })
      .then(() => {
        // Advance to COMPLETE stage
        this.advanceToStage(LoadStage.COMPLETE);
        
        // Schedule low priority features to load when idle
        this.scheduleIdleLoading();
      })
      .catch(error => {
        console.error('Error during progressive loading:', error);
      });
  }
  
  /**
   * Load all features with critical priority
   */
  private async loadCriticalFeatures(): Promise<void> {
    const criticalFeatures = Array.from(this.features.values())
      .filter(info => info.module.priority === LoadPriority.CRITICAL)
      .map(info => info.module.id);
    
    // Load critical features in parallel
    return this.loadFeatures(criticalFeatures);
  }
  
  /**
   * Load all features with a specific priority
   * @param priority Priority level to load
   */
  private async loadPriorityFeatures(priority: LoadPriority): Promise<void> {
    const featuresInPriority = Array.from(this.features.values())
      .filter(info => 
        info.module.priority === priority && 
        info.status === LoadStatus.PENDING
      )
      .map(info => info.module.id);
    
    return this.loadFeatures(featuresInPriority);
  }
  
  /**
   * Load multiple features, respecting dependencies
   * @param featureIds Features to load
   */
  private async loadFeatures(featureIds: string[]): Promise<void> {
    if (featureIds.length === 0) return;
    
    // Create a queue of features to load
    const queue = [...featureIds];
    const loaded = new Set<string>();
    const loading = new Set<string>();
    
    // Helper to check if dependencies are satisfied
    const areDependenciesSatisfied = (id: string): boolean => {
      const info = this.features.get(id);
      if (!info) return false;
      
      // Check dependencies
      const dependencies = info.module.dependencies || [];
      return dependencies.every(depId => loaded.has(depId) || !this.features.has(depId));
    };
    
    // Helper to check if condition is satisfied
    const isConditionSatisfied = (id: string): boolean => {
      const info = this.features.get(id);
      if (!info) return false;
      
      // Check condition
      return info.module.condition ? info.module.condition() : true;
    };
    
    // Process queue until all features are loaded or loading
    while (queue.length > 0) {
      // Find next feature that has all dependencies satisfied
      const nextIndex = queue.findIndex(id => 
        areDependenciesSatisfied(id) && isConditionSatisfied(id)
      );
      
      if (nextIndex === -1) {
        // No feature can be loaded yet, wait for some dependencies to load
        if (loading.size === 0) {
          // Circular dependency or unsatisfiable condition
          console.error('Circular dependency or unsatisfiable condition detected');
          break;
        }
        
        // Wait for a feature to finish loading
        await Promise.race(
          Array.from(loading).map(id => this.features.get(id)?.loadPromise)
        );
        
        // Update loaded set
        Array.from(loading).forEach(id => {
          const info = this.features.get(id);
          if (info && info.status === LoadStatus.LOADED) {
            loaded.add(id);
            loading.delete(id);
          } else if (info && info.status === LoadStatus.FAILED) {
            loading.delete(id);
          }
        });
        
        continue;
      }
      
      // Load the next feature
      const id = queue.splice(nextIndex, 1)[0];
      loading.add(id);
      
      // Load asynchronously
      this.loadFeature(id).then(() => {
        const info = this.features.get(id);
        if (info && info.status === LoadStatus.LOADED) {
          loaded.add(id);
        }
        loading.delete(id);
      });
    }
    
    // Wait for all loading features to complete
    if (loading.size > 0) {
      await Promise.all(
        Array.from(loading).map(id => this.features.get(id)?.loadPromise)
      );
    }
  }
  
  /**
   * Load a single feature
   * @param id Feature ID
   */
  public async loadFeature(id: string): Promise<any> {
    const info = this.features.get(id);
    if (!info) {
      throw new Error(`Feature ${id} is not registered`);
    }
    
    // Skip if already loaded or loading
    if (info.status === LoadStatus.LOADED) {
      return info.instance;
    }
    
    if (info.status === LoadStatus.LOADING && info.loadPromise) {
      return info.loadPromise;
    }
    
    // Check dependencies
    const dependencies = info.module.dependencies || [];
    for (const depId of dependencies) {
      if (!this.features.has(depId)) {
        console.warn(`Dependency ${depId} is not registered`);
        continue;
      }
      
      const depInfo = this.features.get(depId)!;
      if (depInfo.status !== LoadStatus.LOADED) {
        await this.loadFeature(depId);
      }
    }
    
    // Check condition
    if (info.module.condition && !info.module.condition()) {
      return null;
    }
    
    // Start loading
    info.status = LoadStatus.LOADING;
    info.loadStartTime = performance.now();
    this.notifyStatusChange(id, LoadStatus.LOADING);
    
    // Create load promise with timeout
    const loadWithTimeout = async (): Promise<any> => {
      try {
        const timeoutPromise = info.module.timeout
          ? new Promise((_, reject) => {
              setTimeout(() => reject(new Error(`Loading ${id} timed out`)), info.module.timeout);
            })
          : Promise.resolve(null);
        
        // Race between loading and timeout
        const result = await Promise.race([
          info.module.loader(),
          timeoutPromise
        ]);
        
        // Mark as loaded
        info.status = LoadStatus.LOADED;
        info.instance = result;
        info.loadEndTime = performance.now();
        
        // Record metrics
        if (this.metricsEnabled && info.loadStartTime !== null) {
          this.metrics.featureTimings[id] = {
            start: info.loadStartTime,
            end: info.loadEndTime,
            duration: info.loadEndTime - info.loadStartTime
          };
        }
        
        // Call onLoad callback
        if (info.module.onLoad) {
          info.module.onLoad(result);
        }
        
        this.notifyStatusChange(id, LoadStatus.LOADED);
        return result;
      } catch (error) {
        // Handle loading failure
        info.status = LoadStatus.FAILED;
        info.error = error as Error;
        info.loadEndTime = performance.now();
        
        console.error(`Failed to load feature ${id}:`, error);
        
        // Call onFailure callback
        if (info.module.onFailure) {
          info.module.onFailure(error as Error);
        }
        
        // Retry if configured
        if (info.module.retry && info.retryCount < (info.module.maxRetries || 3)) {
          info.retryCount++;
          console.log(`Retrying load for ${id} (attempt ${info.retryCount})`);
          
          // Retry after a delay
          await new Promise(resolve => setTimeout(resolve, 1000 * info.retryCount));
          return this.loadFeature(id);
        }
        
        this.notifyStatusChange(id, LoadStatus.FAILED);
        throw error;
      }
    };
    
    // Store and return the loading promise
    info.loadPromise = loadWithTimeout();
    return info.loadPromise;
  }
  
  /**
   * Schedule loading of low priority features during idle time
   */
  private scheduleIdleLoading(): void {
    // Skip if idle loading is already scheduled
    if (this.idleCallbackId !== null) return;
    
    const loadLowPriorityFeatures = () => {
      if (this.isLoadingPaused) return;
      
      // Find low priority features to load
      const lowPriorityFeatures = Array.from(this.features.values())
        .filter(info => 
          info.module.priority === LoadPriority.LOW && 
          info.status === LoadStatus.PENDING &&
          (!info.module.condition || info.module.condition())
        )
        .map(info => info.module.id);
      
      if (lowPriorityFeatures.length === 0) {
        // All low priority features loaded, advance to IDLE stage
        this.advanceToStage(LoadStage.IDLE);
        return;
      }
      
      // Load next low priority feature
      const nextFeature = lowPriorityFeatures[0];
      this.loadFeature(nextFeature)
        .catch(error => {
          console.error(`Error loading low priority feature ${nextFeature}:`, error);
        })
        .finally(() => {
          // Schedule next idle callback
          this.scheduleIdleCallback(loadLowPriorityFeatures);
        });
    };
    
    // Start idle loading
    this.scheduleIdleCallback(loadLowPriorityFeatures);
  }
  
  /**
   * Schedule a callback to run during browser idle time
   */
  private scheduleIdleCallback(callback: Function): void {
    if (window.requestIdleCallback) {
      this.idleCallbackId = window.requestIdleCallback(() => {
        this.idleCallbackId = null;
        callback();
      });
    } else {
      // Fallback for browsers without requestIdleCallback
      this.idleCallbackId = window.setTimeout(() => {
        this.idleCallbackId = null;
        callback();
      }, 1);
    }
  }
  
  /**
   * Advance to the next loading stage
   * @param stage Stage to advance to
   */
  private advanceToStage(stage: LoadStage): void {
    if (stage < this.currentStage) {
      console.warn(`Cannot go back to previous stage (${LoadStage[stage]})`);
      return;
    }
    
    // Update stage
    const previousStage = this.currentStage;
    this.currentStage = stage;
    
    console.log(`Advanced to stage: ${LoadStage[stage]}`);
    
    // Update metrics
    if (this.metricsEnabled) {
      const now = performance.now();
      
      // End timing for previous stage
      this.metrics.stageTimings[LoadStage[previousStage]].end = now;
      
      // Start timing for new stage
      this.metrics.stageTimings[LoadStage[stage]] = {
        start: now,
        end: 0
      };
    }
    
    // Notify listeners
    this.notifyStageChange(stage);
    
    // Auto-load features for this stage
    this.loadFeaturesForStage(stage);
  }
  
  /**
   * Load features assigned to a specific stage
   * @param stage Loading stage
   */
  private loadFeaturesForStage(stage: LoadStage): void {
    const featuresForStage = Array.from(this.features.values())
      .filter(info => 
        info.module.stage === stage && 
        info.status === LoadStatus.PENDING
      )
      .map(info => info.module.id);
    
    if (featuresForStage.length > 0) {
      this.loadFeatures(featuresForStage).catch(error => {
        console.error(`Error loading features for stage ${LoadStage[stage]}:`, error);
      });
    }
  }
  
  /**
   * Register a listener for stage changes
   * @param stage Stage to listen for
   * @param listener Callback function
   */
  public onStage(stage: LoadStage, listener: Function): void {
    if (!this.stageListeners.has(stage)) {
      this.stageListeners.set(stage, []);
    }
    
    this.stageListeners.get(stage)!.push(listener);
    
    // If stage is already reached, call listener immediately
    if (this.currentStage >= stage) {
      listener();
    }
  }
  
  /**
   * Register a listener for feature status changes
   * @param id Feature ID
   * @param listener Callback function
   */
  public onFeatureStatus(id: string, listener: (status: LoadStatus) => void): void {
    if (!this.statusListeners.has(id)) {
      this.statusListeners.set(id, []);
    }
    
    this.statusListeners.get(id)!.push(listener);
    
    // Call with current status
    const info = this.features.get(id);
    if (info) {
      listener(info.status);
    }
  }
  
  /**
   * Notify stage change listeners
   * @param stage Stage that changed
   */
  private notifyStageChange(stage: LoadStage): void {
    const listeners = this.stageListeners.get(stage);
    if (listeners) {
      listeners.forEach(listener => listener());
    }
  }
  
  /**
   * Notify feature status change listeners
   * @param id Feature ID
   * @param status New status
   */
  private notifyStatusChange(id: string, status: LoadStatus): void {
    const listeners = this.statusListeners.get(id);
    if (listeners) {
      listeners.forEach(listener => listener(status));
    }
  }
  
  /**
   * Pause loading of non-critical features
   */
  public pauseLoading(): void {
    this.isLoadingPaused = true;
    
    // Cancel idle callback
    if (this.idleCallbackId !== null) {
      if (window.cancelIdleCallback) {
        window.cancelIdleCallback(this.idleCallbackId);
      } else {
        clearTimeout(this.idleCallbackId);
      }
      this.idleCallbackId = null;
    }
  }
  
  /**
   * Resume loading of features
   */
  public resumeLoading(): void {
    this.isLoadingPaused = false;
    
    // Resume idle loading
    if (this.currentStage === LoadStage.COMPLETE || this.currentStage === LoadStage.IDLE) {
      this.scheduleIdleLoading();
    }
  }
  
  /**
   * Get the loading status of a feature
   * @param id Feature ID
   */
  public getFeatureStatus(id: string): LoadStatus | null {
    const info = this.features.get(id);
    return info ? info.status : null;
  }
  
  /**
   * Get the loaded instance of a feature
   * @param id Feature ID
   */
  public getFeatureInstance(id: string): any | null {
    const info = this.features.get(id);
    return info && info.status === LoadStatus.LOADED ? info.instance : null;
  }
  
  /**
   * Check if a feature is loaded
   * @param id Feature ID
   */
  public isFeatureLoaded(id: string): boolean {
    const info = this.features.get(id);
    return info ? info.status === LoadStatus.LOADED : false;
  }
  
  /**
   * Get the current loading stage
   */
  public getCurrentStage(): LoadStage {
    return this.currentStage;
  }
  
  /**
   * Get loading metrics
   */
  public getMetrics(): any {
    // Calculate total duration if not done already
    if (this.metrics.totalDuration === 0) {
      this.metrics.totalDuration = performance.now() - this.loadStartTime;
    }
    
    return {
      ...this.metrics,
      currentStage: LoadStage[this.currentStage],
      featuresByStatus: {
        pending: this.countFeaturesByStatus(LoadStatus.PENDING),
        loading: this.countFeaturesByStatus(LoadStatus.LOADING),
        loaded: this.countFeaturesByStatus(LoadStatus.LOADED),
        failed: this.countFeaturesByStatus(LoadStatus.FAILED),
      },
      totalFeatures: this.features.size
    };
  }
  
  /**
   * Count features by status
   */
  private countFeaturesByStatus(status: LoadStatus): number {
    return Array.from(this.features.values())
      .filter(info => info.status === status)
      .length;
  }
}

/**
 * Create a singleton instance of the Progressive Loader
 */
let loaderInstance: ProgressiveLoader | null = null;

export function getProgressiveLoader(): ProgressiveLoader {
  if (!loaderInstance) {
    loaderInstance = new ProgressiveLoader();
  }
  
  return loaderInstance;
}
