/**
 * Component Virtualization System
 * Provides utilities for lazy loading UI components and optimizing 
 * rendering of large datasets on mobile devices
 */

import { debounce } from './dom-optimizer';
import { platformDetector, PlatformType } from './platform-detector';

/**
 * Configuration for a virtualized component
 */
export interface VirtualComponentConfig {
  // Component identifier
  id: string;
  
  // Element to attach the component to
  container: HTMLElement;
  
  // Function to create the component
  createComponent: () => Promise<HTMLElement>;
  
  // Whether to lazy load this component
  lazyLoad: boolean;
  
  // Priority (lower numbers load first)
  priority: number;
  
  // Whether to show loading indicator
  showLoadingIndicator: boolean;
  
  // Minimum loading time to prevent flashing (ms)
  minimumLoadingTime?: number;
  
  // Function to create a loading placeholder
  createPlaceholder?: () => HTMLElement;
}

/**
 * Registry to manage virtualized components
 */
export class ComponentVirtualizer {
  private static instance: ComponentVirtualizer;
  
  // Registered components configs
  private components: Map<string, VirtualComponentConfig> = new Map();
  
  // Loaded components
  private loadedComponents: Map<string, HTMLElement> = new Map();
  
  // Components currently loading
  private loadingComponents: Set<string> = new Set();
  
  // Components observers for viewport detection
  private observers: Map<string, IntersectionObserver> = new Map();
  
  // Components that should be preloaded
  private preloadQueue: string[] = [];
  
  // Whether the system is initialized
  private initialized: boolean = false;
  
  // Whether to use more aggressive optimization on mobile
  private mobileMode: boolean = false;
  
  /**
   * Get the singleton instance
   */
  public static getInstance(): ComponentVirtualizer {
    if (!ComponentVirtualizer.instance) {
      ComponentVirtualizer.instance = new ComponentVirtualizer();
    }
    return ComponentVirtualizer.instance;
  }
  
  /**
   * Private constructor to enforce singleton pattern
   */  private constructor() {
    // Check if we're on mobile
    this.mobileMode = platformDetector.getPlatformType() === PlatformType.MOBILE;
  }
  
  /**
   * Initialize the component virtualization system
   */
  public init(): void {
    if (this.initialized) return;
    
    // Setup intersection observer for viewport detection
    if ('IntersectionObserver' in window) {
      // Start checking for components in viewport
      this.checkComponentsInViewport();
      
      // Listen for scroll events to detect components entering viewport
      window.addEventListener('scroll', debounce(this.checkComponentsInViewport.bind(this), 100), { passive: true });
      window.addEventListener('resize', debounce(this.checkComponentsInViewport.bind(this), 200), { passive: true });
    } else {
      // Fallback for browsers that don't support IntersectionObserver
      // In this case, we'll load components as they're registered
      console.warn('IntersectionObserver not supported, lazy loading will be limited');
    }
    
    // Mark as initialized
    this.initialized = true;
  }
  
  /**
   * Register a component for virtualization
   * @param config Component configuration
   */
  public registerComponent(config: VirtualComponentConfig): void {
    // Ensure system is initialized
    if (!this.initialized) {
      this.init();
    }
    
    // Register component
    this.components.set(config.id, config);
    
    if (config.lazyLoad && 'IntersectionObserver' in window) {
      // Create placeholder if needed
      if (config.showLoadingIndicator && config.createPlaceholder) {
        const placeholder = config.createPlaceholder();
        placeholder.setAttribute('data-component-placeholder', config.id);
        config.container.appendChild(placeholder);
      }
      
      // Setup intersection observer for this component
      this.setupComponentObserver(config);
    } else {
      // Load immediately if not lazy loading
      this.loadComponent(config.id);
    }
  }
  
  /**
   * Setup intersection observer for a component
   * @param config Component configuration
   */
  private setupComponentObserver(config: VirtualComponentConfig): void {
    if (!('IntersectionObserver' in window)) return;
    
    // Create observer
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          // Component is in viewport, load it
          this.loadComponent(config.id);
          
          // Stop observing once loaded
          observer.disconnect();
          this.observers.delete(config.id);
        }
      });
    }, {
      rootMargin: '100px', // Start loading when within 100px of viewport
      threshold: 0.1 // Trigger when at least 10% visible
    });
    
    // Start observing container
    observer.observe(config.container);
    
    // Store observer
    this.observers.set(config.id, observer);
  }
  
  /**
   * Check which components are in viewport and load them
   */
  private checkComponentsInViewport(): void {
    // Skip if no IntersectionObserver support
    if (!('IntersectionObserver' in window)) return;
    
    // Process preload queue first (ordered by priority)
    while (this.preloadQueue.length > 0) {
      const id = this.preloadQueue.shift();
      if (id && !this.loadedComponents.has(id) && !this.loadingComponents.has(id)) {
        this.loadComponent(id);
        break; // Only load one per cycle to prevent blocking
      }
    }
  }
  
  /**
   * Load a component by ID
   * @param id Component ID
   * @returns Promise that resolves when component is loaded
   */
  public async loadComponent(id: string): Promise<HTMLElement | null> {
    // Check if already loaded
    if (this.loadedComponents.has(id)) {
      return this.loadedComponents.get(id) || null;
    }
    
    // Check if already loading
    if (this.loadingComponents.has(id)) {
      // Wait for it to complete
      return new Promise((resolve) => {
        const checkInterval = setInterval(() => {
          if (this.loadedComponents.has(id)) {
            clearInterval(checkInterval);
            resolve(this.loadedComponents.get(id) || null);
          }
        }, 50);
      });
    }
    
    // Get component config
    const config = this.components.get(id);
    if (!config) {
      console.error(`Component not found: ${id}`);
      return null;
    }
    
    // Mark as loading
    this.loadingComponents.add(id);
    
    // Record start time for minimum loading time
    const startTime = performance.now();
    
    try {
      // Create component
      const component = await config.createComponent();
      
      // Apply any minimum loading time if specified
      if (config.minimumLoadingTime && config.minimumLoadingTime > 0) {
        const elapsed = performance.now() - startTime;
        const remainingTime = config.minimumLoadingTime - elapsed;
        
        if (remainingTime > 0) {
          await new Promise(resolve => setTimeout(resolve, remainingTime));
        }
      }
      
      // Remove placeholder if exists
      const placeholder = config.container.querySelector(`[data-component-placeholder="${id}"]`);
      if (placeholder) {
        placeholder.remove();
      }
      
      // Append component to container
      config.container.appendChild(component);
      
      // Mark as loaded
      this.loadedComponents.set(id, component);
      this.loadingComponents.delete(id);
      
      // If observer exists, disconnect it
      if (this.observers.has(id)) {
        this.observers.get(id)?.disconnect();
        this.observers.delete(id);
      }
      
      return component;
    } catch (error) {
      console.error(`Failed to load component: ${id}`, error);
      this.loadingComponents.delete(id);
      return null;
    }
  }
  
  /**
   * Preload a component (add to preload queue)
   * @param id Component ID
   * @param immediate Whether to load immediately
   */
  public preloadComponent(id: string, immediate: boolean = false): void {
    // Check if already loaded or loading
    if (this.loadedComponents.has(id) || this.loadingComponents.has(id)) {
      return;
    }
    
    // Get component config
    const config = this.components.get(id);
    if (!config) {
      console.error(`Component not found: ${id}`);
      return;
    }
    
    if (immediate) {
      // Load immediately
      this.loadComponent(id);
    } else {
      // Add to preload queue if not already there
      if (!this.preloadQueue.includes(id)) {
        this.preloadQueue.push(id);
        
        // Sort queue by priority
        this.preloadQueue.sort((a, b) => {
          const configA = this.components.get(a);
          const configB = this.components.get(b);
          return (configA?.priority || 0) - (configB?.priority || 0);
        });
      }
    }
  }
  
  /**
   * Create a skeleton placeholder for a component
   * @param height Height of the skeleton
   * @param className Optional CSS class to add
   * @returns Skeleton placeholder element
   */
  public createSkeletonPlaceholder(height: string, className?: string): HTMLElement {
    const skeleton = document.createElement('div');
    skeleton.className = 'skeleton-placeholder';
    if (className) {
      skeleton.classList.add(className);
    }
    
    skeleton.style.height = height;
    skeleton.style.width = '100%';
    skeleton.style.backgroundColor = 'rgba(0, 0, 0, 0.1)';
    skeleton.style.borderRadius = '4px';
    skeleton.style.animation = 'skeleton-pulse 1.5s ease-in-out infinite';
    
    // Add animation if not already defined
    if (!document.querySelector('#skeleton-animation-style')) {
      const style = document.createElement('style');
      style.id = 'skeleton-animation-style';
      style.textContent = `
        @keyframes skeleton-pulse {
          0% { opacity: 0.6; }
          50% { opacity: 0.8; }
          100% { opacity: 0.6; }
        }
      `;
      document.head.appendChild(style);
    }
    
    return skeleton;
  }
  
  /**
   * Create a more complex skeleton UI with multiple elements
   * @param config Skeleton configuration
   * @returns Skeleton container element
   */
  public createComplexSkeletonPlaceholder(config: {
    height: string;
    elements: Array<{
      type: 'line' | 'circle' | 'rectangle';
      width?: string;
      height?: string;
      marginBottom?: string;
    }>;
  }): HTMLElement {
    const container = document.createElement('div');
    container.className = 'skeleton-container';
    container.style.height = config.height;
    container.style.width = '100%';
    container.style.padding = '12px';
    
    config.elements.forEach(element => {
      const el = document.createElement('div');
      el.className = 'skeleton-element';
      el.style.backgroundColor = 'rgba(0, 0, 0, 0.1)';
      el.style.animation = 'skeleton-pulse 1.5s ease-in-out infinite';
      
      if (element.marginBottom) {
        el.style.marginBottom = element.marginBottom;
      }
      
      switch (element.type) {
        case 'line':
          el.style.height = element.height || '12px';
          el.style.width = element.width || '100%';
          el.style.borderRadius = '3px';
          break;
        case 'circle':
          el.style.height = element.height || '40px';
          el.style.width = element.width || '40px';
          el.style.borderRadius = '50%';
          break;
        case 'rectangle':
          el.style.height = element.height || '80px';
          el.style.width = element.width || '80px';
          el.style.borderRadius = '4px';
          break;
      }
      
      container.appendChild(el);
    });
    
    return container;
  }
  
  /**
   * Get a loaded component by ID
   * @param id Component ID
   * @returns Component element or null if not loaded
   */
  public getComponent(id: string): HTMLElement | null {
    return this.loadedComponents.get(id) || null;
  }
  
  /**
   * Check if a component is loaded
   * @param id Component ID
   * @returns Whether component is loaded
   */
  public isLoaded(id: string): boolean {
    return this.loadedComponents.has(id);
  }
  
  /**
   * Unload a component to free memory
   * @param id Component ID
   */
  public unloadComponent(id: string): void {
    // Get component
    const component = this.getComponent(id);
    if (!component) return;
    
    // Remove from DOM
    component.remove();
    
    // Remove from maps
    this.loadedComponents.delete(id);
    
    // Clean up any observers
    if (this.observers.has(id)) {
      this.observers.get(id)?.disconnect();
      this.observers.delete(id);
    }
  }
}
