/**
 * Component Lazy Loader
 * Provides utilities for lazy loading UI components on demand
 */

/**
 * Options for lazy loaded components
 */
export interface LazyComponentOptions {
  // Element that will contain the component
  container: HTMLElement;
  
  // Placeholder to show while loading (optional)
  placeholder?: HTMLElement | string;
  
  // Whether to load when scrolled into view (default: true)
  loadOnVisible?: boolean;
  
  // Margin around the viewport for preloading (default: "100px")
  rootMargin?: string;
  
  // Callback when component is loaded
  onLoad?: (component: any) => void;
  
  // Callback when component fails to load
  onError?: (error: Error) => void;
  
  // Threshold for visibility (0-1, default: 0.1)
  threshold?: number;
  
  // Whether to retry on failure
  retry?: boolean;
  
  // Max retries before giving up
  maxRetries?: number;
  
  // Whether to show loading indicator
  showLoadingIndicator?: boolean;
  
  // Custom loading indicator element or HTML
  loadingIndicator?: HTMLElement | string;
  
  // Data to pass to the component factory
  data?: any;
}

/**
 * Factory function interface for creating components
 */
export type ComponentFactory<T> = (container: HTMLElement, data?: any) => Promise<T>;

/**
 * Manages the lazy loading of a single component
 */
export class LazyComponent<T = any> {
  private options: Required<LazyComponentOptions>;
  private componentFactory: ComponentFactory<T>;
  private isLoaded: boolean = false;
  private isLoading: boolean = false;
  private component: T | null = null;
  private observer: IntersectionObserver | null = null;
  private retryCount: number = 0;
  private loadingIndicatorElement: HTMLElement | null = null;
  private placeholderElement: HTMLElement | null = null;
  
  /**
   * Create a new lazy component
   * @param componentFactory Function that creates the component
   * @param options Configuration options
   */
  constructor(componentFactory: ComponentFactory<T>, options: LazyComponentOptions) {
    this.componentFactory = componentFactory;
    
    // Set default options
    this.options = {
      container: options.container,
      placeholder: options.placeholder || '',
      loadOnVisible: options.loadOnVisible !== false,
      rootMargin: options.rootMargin || '100px',
      onLoad: options.onLoad || (() => {}),
      onError: options.onError || ((error) => console.error('Failed to load component:', error)),
      threshold: options.threshold || 0.1,
      retry: options.retry !== false,
      maxRetries: options.maxRetries || 3,
      showLoadingIndicator: options.showLoadingIndicator !== false,
      loadingIndicator: options.loadingIndicator || this.createDefaultLoadingIndicator(),
      data: options.data || {}
    };
    
    // Set up the component
    this.initialize();
  }
  
  /**
   * Initialize the lazy component
   */
  private initialize(): void {
    const { container, loadOnVisible, placeholder } = this.options;
    
    // Show placeholder if provided
    if (placeholder) {
      this.showPlaceholder();
    }
    
    // Set up intersection observer if loading on visibility
    if (loadOnVisible) {
      this.setupIntersectionObserver();
    }
  }
  
  /**
   * Create default loading indicator
   */
  private createDefaultLoadingIndicator(): HTMLElement {
    const indicator = document.createElement('div');
    indicator.className = 'lazy-component-loading';
    indicator.style.display = 'flex';
    indicator.style.alignItems = 'center';
    indicator.style.justifyContent = 'center';
    indicator.style.width = '100%';
    indicator.style.height = '100%';
    indicator.style.minHeight = '50px';
    
    const spinner = document.createElement('div');
    spinner.className = 'lazy-component-spinner';
    spinner.style.width = '30px';
    spinner.style.height = '30px';
    spinner.style.border = '3px solid rgba(0, 0, 0, 0.1)';
    spinner.style.borderRadius = '50%';
    spinner.style.borderTop = '3px solid #0e639c';
    spinner.style.animation = 'lazy-component-spin 1s linear infinite';
    
    // Add keyframes for spinner animation
    if (!document.getElementById('lazy-component-keyframes')) {
      const style = document.createElement('style');
      style.id = 'lazy-component-keyframes';
      style.textContent = `
        @keyframes lazy-component-spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }
      `;
      document.head.appendChild(style);
    }
    
    indicator.appendChild(spinner);
    return indicator;
  }
  
  /**
   * Show placeholder content
   */
  private showPlaceholder(): void {
    const { container, placeholder } = this.options;
    
    // Clear container
    this.clearContainer();
    
    // Show placeholder
    if (typeof placeholder === 'string') {
      container.innerHTML = placeholder;
    } else {
      this.placeholderElement = placeholder;
      container.appendChild(placeholder);
    }
  }
  
  /**
   * Show loading indicator
   */
  private showLoadingIndicator(): void {
    const { container, loadingIndicator, showLoadingIndicator } = this.options;
    
    if (!showLoadingIndicator) return;
    
    // Clear container
    this.clearContainer();
    
    // Show loading indicator
    if (typeof loadingIndicator === 'string') {
      container.innerHTML = loadingIndicator;
    } else {
      this.loadingIndicatorElement = loadingIndicator;
      container.appendChild(loadingIndicator);
    }
  }
  
  /**
   * Clear the container
   */
  private clearContainer(): void {
    const { container } = this.options;
    
    // Remove placeholder if it exists
    if (this.placeholderElement && this.placeholderElement.parentElement === container) {
      container.removeChild(this.placeholderElement);
      this.placeholderElement = null;
    }
    
    // Remove loading indicator if it exists
    if (this.loadingIndicatorElement && this.loadingIndicatorElement.parentElement === container) {
      container.removeChild(this.loadingIndicatorElement);
      this.loadingIndicatorElement = null;
    }
    
    // Clear container HTML if needed
    if (container.innerHTML !== '') {
      container.innerHTML = '';
    }
  }
  
  /**
   * Set up intersection observer to detect when component is visible
   */
  private setupIntersectionObserver(): void {
    const { container, rootMargin, threshold } = this.options;
    
    this.observer = new IntersectionObserver(
      entries => {
        const entry = entries[0];
        if (entry.isIntersecting && !this.isLoaded && !this.isLoading) {
          this.load();
        }
      },
      { rootMargin, threshold }
    );
    
    this.observer.observe(container);
  }
  
  /**
   * Load the component
   */
  public async load(): Promise<T | null> {
    if (this.isLoaded) return this.component;
    if (this.isLoading) return null;
    
    this.isLoading = true;
    this.showLoadingIndicator();
    
    try {
      const { container, data, onLoad } = this.options;
      
      // Load the component
      this.component = await this.componentFactory(container, data);
      this.isLoaded = true;
      this.isLoading = false;
      
      // Clear loading indicator
      this.clearContainer();
      
      // Disconnect observer if it exists
      if (this.observer) {
        this.observer.disconnect();
        this.observer = null;
      }
      
      // Call onLoad callback
      onLoad(this.component);
      
      return this.component;
    } catch (error) {
      this.isLoading = false;
      
      // Handle error
      if (this.options.retry && this.retryCount < this.options.maxRetries) {
        this.retryCount++;
        
        // Retry after a delay
        setTimeout(() => {
          this.load();
        }, 1000 * this.retryCount); // Exponential backoff
        
        return null;
      } else {
        // Call onError callback
        this.options.onError(error as Error);
        
        // Show placeholder again
        this.showPlaceholder();
        
        return null;
      }
    }
  }
  
  /**
   * Check if component is loaded
   */
  public isComponentLoaded(): boolean {
    return this.isLoaded;
  }
  
  /**
   * Get the loaded component
   */
  public getComponent(): T | null {
    return this.component;
  }
  
  /**
   * Unload the component
   */
  public unload(): void {
    if (!this.isLoaded) return;
    
    this.clearContainer();
    this.isLoaded = false;
    this.component = null;
    
    // Show placeholder again
    this.showPlaceholder();
    
    // Set up intersection observer again
    if (this.options.loadOnVisible) {
      this.setupIntersectionObserver();
    }
  }
  
  /**
   * Destroy the lazy component
   */
  public destroy(): void {
    // Disconnect observer if it exists
    if (this.observer) {
      this.observer.disconnect();
      this.observer = null;
    }
    
    // Clear container
    this.clearContainer();
    
    // Reset state
    this.isLoaded = false;
    this.isLoading = false;
    this.component = null;
  }
}

/**
 * Lazy component registry for managing multiple lazy components
 */
export class LazyComponentRegistry {
  private components: Map<string, LazyComponent<any>> = new Map();
  
  /**
   * Register a new lazy component
   * @param id Unique identifier for the component
   * @param factory Component factory function
   * @param options Component options
   * @returns The lazy component instance
   */
  public register<T>(
    id: string,
    factory: ComponentFactory<T>,
    options: LazyComponentOptions
  ): LazyComponent<T> {
    // Create the lazy component
    const component = new LazyComponent<T>(factory, options);
    
    // Register it
    this.components.set(id, component);
    
    return component;
  }
  
  /**
   * Get a registered component by ID
   * @param id Component ID
   * @returns The lazy component or null if not found
   */
  public get<T>(id: string): LazyComponent<T> | null {
    return this.components.get(id) as LazyComponent<T> || null;
  }
  
  /**
   * Check if a component is registered
   * @param id Component ID
   * @returns Whether the component is registered
   */
  public has(id: string): boolean {
    return this.components.has(id);
  }
  
  /**
   * Load a component by ID
   * @param id Component ID
   * @returns The loaded component or null if loading failed
   */
  public async load<T>(id: string): Promise<T | null> {
    const component = this.get<T>(id);
    if (!component) return null;
    
    return component.load();
  }
  
  /**
   * Unload a component by ID
   * @param id Component ID
   */
  public unload(id: string): void {
    const component = this.get(id);
    if (component) {
      component.unload();
    }
  }
  
  /**
   * Remove a component from the registry
   * @param id Component ID
   */
  public unregister(id: string): void {
    const component = this.get(id);
    if (component) {
      component.destroy();
      this.components.delete(id);
    }
  }
  
  /**
   * Unload all components
   */
  public unloadAll(): void {
    for (const [id, component] of this.components.entries()) {
      component.unload();
    }
  }
  
  /**
   * Destroy all components and clear the registry
   */
  public destroy(): void {
    for (const [id, component] of this.components.entries()) {
      component.destroy();
    }
    
    this.components.clear();
  }
}

/**
 * Create and return a singleton instance of the LazyComponentRegistry
 */
let registryInstance: LazyComponentRegistry | null = null;

export function getLazyComponentRegistry(): LazyComponentRegistry {
  if (!registryInstance) {
    registryInstance = new LazyComponentRegistry();
  }
  
  return registryInstance;
}
