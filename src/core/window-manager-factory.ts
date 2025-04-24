/**
 * Window Manager Factory
 * Creates the appropriate window manager based on platform detection
 */

import { WindowManager } from './window';
import { MobileWindowManager } from './mobile-window-manager';
import { IWindowManager } from './window-manager-interface';
import { PlatformType, platformDetector } from './platform-detector';

/**
 * Factory class for creating window managers
 */
export class WindowManagerFactory {
  private static instance: WindowManagerFactory;
  private desktopWindowManager: WindowManager;
  private mobileWindowManager: MobileWindowManager;
  private currentPlatform: PlatformType;
  
  /**
   * Private constructor for singleton pattern
   */
  private constructor() {
    this.desktopWindowManager = new WindowManager();
    this.mobileWindowManager = new MobileWindowManager();
    this.currentPlatform = platformDetector.getPlatformInfo().type;
    
    // Listen for platform changes
    platformDetector.addPlatformChangeListener(this.handlePlatformChange.bind(this));
  }
  
  /**
   * Get the singleton instance of WindowManagerFactory
   */
  public static getInstance(): WindowManagerFactory {
    if (!WindowManagerFactory.instance) {
      WindowManagerFactory.instance = new WindowManagerFactory();
    }
    return WindowManagerFactory.instance;
  }
  
  /**
   * Handle platform change events
   */
  private handlePlatformChange(platformInfo: any): void {
    const previousPlatform = this.currentPlatform;
    this.currentPlatform = platformInfo.type;
    
    // If platform changed, dispatch an event
    if (previousPlatform !== this.currentPlatform) {
      window.dispatchEvent(new CustomEvent('windowManagerChanged', {
        detail: {
          previous: previousPlatform,
          current: this.currentPlatform
        }
      }));
    }
  }
  
  /**
   * Initialize both window managers
   */
  public initWindowManagers(): void {
    this.desktopWindowManager.init();
    this.mobileWindowManager.init();
  }
  
  /**
   * Get the current window manager based on platform
   */
  public getCurrentWindowManager(): IWindowManager {
    return this.currentPlatform === PlatformType.MOBILE
      ? this.mobileWindowManager
      : this.desktopWindowManager;
  }
  
  /**
   * Get the desktop window manager
   */
  public getDesktopWindowManager(): WindowManager {
    return this.desktopWindowManager;
  }
  
  /**
   * Get the mobile window manager
   */
  public getMobileWindowManager(): MobileWindowManager {
    return this.mobileWindowManager;
  }
  
  /**
   * Create a window using the appropriate window manager
   * @param options Window options
   * @param forcePlatform Optional platform to force use of specific window manager
   * @returns Window ID
   */
  public createWindow(options: any, forcePlatform?: PlatformType): string {
    const platform = forcePlatform || this.currentPlatform;
    
    return platform === PlatformType.MOBILE
      ? this.mobileWindowManager.createWindow(options)
      : this.desktopWindowManager.createWindow(options);
  }
}
