/**
 * View-Model System for Cross-Platform UI
 * Provides interfaces and base implementations for the MVVM architecture
 * that supports both desktop and mobile interfaces
 */

import { PlatformType } from './platform-detector';

/**
 * Base interface for all view models
 */
export interface ViewModel {
  /** 
   * Initialize the view model
   */
  initialize(): Promise<void> | void;
  
  /**
   * Clean up resources when the view model is no longer needed
   */
  dispose(): void;
}

/**
 * Base interface for all views
 */
export interface View {
  /**
   * Get the DOM element representing this view
   */
  getElement(): HTMLElement;
  
  /**
   * Initialize the view
   */
  initialize(): Promise<void> | void;
  
  /**
   * Render the view with current data
   */
  render(): void;
  
  /**
   * Clean up resources when the view is no longer needed
   */
  dispose(): void;
  
  /**
   * Get the associated view model
   */
  getViewModel(): ViewModel;
}

/**
 * Platform-specific view factory interface
 */
export interface ViewFactory {
  /**
   * Create a view for the specified component
   * @param componentName The name of the component to create a view for
   * @param viewModel The view model to associate with the view
   * @param options Additional options for view creation
   */
  createView(componentName: string, viewModel: ViewModel, options?: any): View;
  
  /**
   * Get the platform type this factory creates views for
   */
  getPlatformType(): PlatformType;
}

/**
 * Base class for implementing view models
 */
export abstract class BaseViewModel implements ViewModel {
  private disposed = false;
  protected eventListeners: Map<string, Set<(...args: any[]) => void>> = new Map();
  
  /**
   * Initialize the view model
   */
  initialize(): Promise<void> | void {
    // Default implementation does nothing
  }
  
  /**
   * Clean up resources when the view model is no longer needed
   */
  dispose(): void {
    if (this.disposed) return;
    
    // Clear all event listeners
    this.eventListeners.clear();
    this.disposed = true;
  }
  
  /**
   * Add an event listener
   * @param eventName The name of the event to listen for
   * @param callback The function to call when the event is triggered
   */
  protected addEventListener(eventName: string, callback: (...args: any[]) => void): void {
    if (!this.eventListeners.has(eventName)) {
      this.eventListeners.set(eventName, new Set());
    }
    this.eventListeners.get(eventName)!.add(callback);
  }
  
  /**
   * Remove an event listener
   * @param eventName The name of the event
   * @param callback The callback to remove
   */
  protected removeEventListener(eventName: string, callback: (...args: any[]) => void): void {
    const listeners = this.eventListeners.get(eventName);
    if (listeners) {
      listeners.delete(callback);
      if (listeners.size === 0) {
        this.eventListeners.delete(eventName);
      }
    }
  }
  
  /**
   * Trigger an event
   * @param eventName The name of the event to trigger
   * @param args Arguments to pass to the event listeners
   */
  protected triggerEvent(eventName: string, ...args: any[]): void {
    const listeners = this.eventListeners.get(eventName);
    if (listeners) {
      listeners.forEach(callback => {
        try {
          callback(...args);
        } catch (error) {
          console.error(`Error in event listener for ${eventName}:`, error);
        }
      });
    }
  }
}

/**
 * Base class for implementing views
 */
export abstract class BaseView implements View {
  protected element: HTMLElement;
  protected viewModel: ViewModel;
  protected disposed = false;
  
  /**
   * Create a new base view
   * @param viewModel The view model to associate with this view
   * @param elementType The type of HTML element to create (default: div)
   */
  constructor(viewModel: ViewModel, elementType: string = 'div') {
    this.viewModel = viewModel;
    this.element = document.createElement(elementType);
  }
  
  /**
   * Get the DOM element representing this view
   */
  getElement(): HTMLElement {
    return this.element;
  }
  
  /**
   * Initialize the view
   */
  initialize(): Promise<void> | void {
    // Default implementation does nothing
  }
  
  /**
   * Render the view with current data
   */
  abstract render(): void;
  
  /**
   * Clean up resources when the view is no longer needed
   */
  dispose(): void {
    if (this.disposed) return;
    
    // Remove element from DOM if it's attached
    if (this.element.parentNode) {
      this.element.parentNode.removeChild(this.element);
    }
    
    // Clear element contents
    this.element.innerHTML = '';
    
    this.disposed = true;
  }
  
  /**
   * Get the associated view model
   */
  getViewModel(): ViewModel {
    return this.viewModel;
  }
}

/**
 * View factory registry that maintains available view factories
 * and creates the appropriate view based on platform
 */
export class ViewFactoryRegistry {
  private static instance: ViewFactoryRegistry;
  private factories: Map<PlatformType, ViewFactory> = new Map();
  
  /**
   * Private constructor for singleton pattern
   */
  private constructor() {}
  
  /**
   * Get the singleton instance of ViewFactoryRegistry
   */
  public static getInstance(): ViewFactoryRegistry {
    if (!ViewFactoryRegistry.instance) {
      ViewFactoryRegistry.instance = new ViewFactoryRegistry();
    }
    return ViewFactoryRegistry.instance;
  }
  
  /**
   * Register a view factory for a specific platform
   * @param factory The view factory to register
   */
  public registerFactory(factory: ViewFactory): void {
    this.factories.set(factory.getPlatformType(), factory);
  }
  
  /**
   * Create a view for the specified component using the appropriate factory for the current platform
   * @param componentName The name of the component to create a view for
   * @param viewModel The view model to associate with the view
   * @param platformType The platform type to create a view for (if not specified, uses the current platform)
   * @param options Additional options for view creation
   */
  public createView(
    componentName: string, 
    viewModel: ViewModel, 
    platformType?: PlatformType,
    options?: any
  ): View | null {
    // If platform not specified, use the current platform from the platform detector
    if (!platformType) {
      const { platformDetector } = require('./platform-detector');
      platformType = platformDetector.getPlatformInfo().type;
    }
    
    // Get the factory for the specified platform
    const factory = this.factories.get(platformType);
    if (!factory) {
      console.error(`No view factory registered for platform type: ${platformType}`);
      
      // Try to fall back to desktop if available
      const desktopFactory = this.factories.get(PlatformType.DESKTOP);
      if (desktopFactory) {
        console.log(`Falling back to desktop view for component: ${componentName}`);
        return desktopFactory.createView(componentName, viewModel, options);
      }
      
      return null;
    }
    
    // Create the view using the factory
    return factory.createView(componentName, viewModel, options);
  }
}
