/**
 * Platform Detection System
 * Detects and manages platform information (mobile/desktop)
 * Provides utilities for responsive design and platform-specific behaviors
 */

export enum PlatformType {
  DESKTOP = 'desktop',
  MOBILE = 'mobile'
}

export interface PlatformInfo {
  type: PlatformType;
  touchCapable: boolean;
  screenWidth: number;
  screenHeight: number;
  pixelRatio: number;
  userAgent: string;
}

export class PlatformDetector {
  private static instance: PlatformDetector;
  private currentPlatform: PlatformInfo;
  private userPreference: PlatformType | null = null;
  private platformChangeListeners: ((platform: PlatformInfo) => void)[] = [];
  
  // Breakpoints for determining platform type based on screen width
  private readonly MOBILE_MAX_WIDTH = 768; // Screen widths below this are considered mobile
  
  /**
   * Private constructor for singleton pattern
   */
  private constructor() {
    this.currentPlatform = this.detectPlatform();
    
    // Listen for resize events to update platform info
    window.addEventListener('resize', this.handleResize.bind(this));
  }
  
  /**
   * Get the singleton instance of PlatformDetector
   */
  public static getInstance(): PlatformDetector {
    if (!PlatformDetector.instance) {
      PlatformDetector.instance = new PlatformDetector();
    }
    return PlatformDetector.instance;
  }
  
  /**
   * Returns the current platform information
   */
  public getPlatformInfo(): PlatformInfo {
    return { ...this.currentPlatform };
  }
  
  /**
   * Determines if the current platform is mobile
   */
  public isMobile(): boolean {
    return this.currentPlatform.type === PlatformType.MOBILE;
  }
  
  /**
   * Determines if the current platform is desktop
   */
  public isDesktop(): boolean {
    return this.currentPlatform.type === PlatformType.DESKTOP;
  }
  
  /**
   * Determines if the device has touch capabilities
   */
  public hasTouchCapability(): boolean {
    return this.currentPlatform.touchCapable;
  }
  
  /**
   * Sets a user preference for platform type, overriding automatic detection
   * @param platformType The preferred platform type (DESKTOP or MOBILE), null to use automatic detection
   */
  public setUserPreference(platformType: PlatformType | null): void {
    this.userPreference = platformType;
    this.updatePlatform();
  }
  
  /**
   * Gets the current user preference for platform type
   */
  public getUserPreference(): PlatformType | null {
    return this.userPreference;
  }
  
  /**
   * Gets the current platform mode (desktop or mobile)
   * Returns the user preference if set, otherwise returns the detected platform type
   * @returns The current platform mode as a string ('desktop' or 'mobile')
   */
  public getCurrentMode(): string {
    return this.userPreference || this.currentPlatform.type;
  }
  
  /**
   * Sets the current platform mode
   * @param mode The platform mode to set ('desktop' or 'mobile')
   */
  public setMode(mode: string): void {
    if (mode === PlatformType.DESKTOP || mode === PlatformType.MOBILE) {
      this.setUserPreference(mode as PlatformType);
    }
  }
  
  /**
   * Get the current platform type
   * @returns The current platform type (DESKTOP or MOBILE)
   */
  public getPlatformType(): PlatformType {
    // If user has set a platform preference, use that
    if (this.userPreference !== null) {
      return this.userPreference;
    }
    
    // Otherwise return the detected platform type
    return this.currentPlatform.type;
  }
  
  /**
   * Add a listener for platform change events
   * @param listener Function to call when platform changes
   */
  public addPlatformChangeListener(listener: (platform: PlatformInfo) => void): void {
    this.platformChangeListeners.push(listener);
  }
  
  /**
   * Remove a platform change listener
   * @param listener The listener to remove
   */
  public removePlatformChangeListener(listener: (platform: PlatformInfo) => void): void {
    const index = this.platformChangeListeners.indexOf(listener);
    if (index !== -1) {
      this.platformChangeListeners.splice(index, 1);
    }
  }
  
  /**
   * Handle window resize events
   */
  private handleResize(): void {
    this.updatePlatform();
  }
  
  /**
   * Updates the current platform information and notifies listeners if changed
   */
  private updatePlatform(): void {
    const newPlatform = this.detectPlatform();
    const platformChanged = newPlatform.type !== this.currentPlatform.type;
    
    this.currentPlatform = newPlatform;
    
    // Apply CSS classes to document root for platform-specific styling
    document.documentElement.classList.remove(PlatformType.DESKTOP, PlatformType.MOBILE);
    document.documentElement.classList.add(newPlatform.type);
    
    // If platform type changed, notify listeners
    if (platformChanged) {
      this.notifyPlatformChangeListeners();
    }
  }
  
  /**
   * Notify all registered listeners about platform change
   */
  private notifyPlatformChangeListeners(): void {
    for (const listener of this.platformChangeListeners) {
      listener(this.currentPlatform);
    }
    
    // Also dispatch a custom event for components that use event listeners
    const event = new CustomEvent('platformChanged', { 
      detail: this.currentPlatform
    });
    window.dispatchEvent(event);
  }
  
  /**
   * Detect the current platform based on screen size, user agent, and capabilities
   */
  private detectPlatform(): PlatformInfo {
    const touchCapable = 'ontouchstart' in window || 
      navigator.maxTouchPoints > 0 || 
      (navigator as any).msMaxTouchPoints > 0;
      
    const screenWidth = window.innerWidth;
    const screenHeight = window.innerHeight;
    const pixelRatio = window.devicePixelRatio || 1;
    const userAgent = navigator.userAgent;
    
    // Determine platform type based on user preference or auto-detection
    let platformType: PlatformType;
    
    if (this.userPreference !== null) {
      // Use user preference if set
      platformType = this.userPreference;
    } else {
      // Auto-detect based on screen width and user agent
      const isMobileBySize = screenWidth <= this.MOBILE_MAX_WIDTH;
      const isMobileByUA = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(userAgent);
      
      platformType = (isMobileBySize || isMobileByUA) ? PlatformType.MOBILE : PlatformType.DESKTOP;
    }
    
    return {
      type: platformType,
      touchCapable,
      screenWidth,
      screenHeight,
      pixelRatio,
      userAgent
    };
  }
  
  /**
   * Forces a re-evaluation of the platform
   */
  public forcePlatformUpdate(): void {
    this.updatePlatform();
  }
  
  /**
   * Dynamically switches between mobile and desktop views without requiring a page reload
   * @param targetPlatform The platform type to switch to
   */
  public switchPlatformView(targetPlatform: PlatformType): void {
    if (this.currentPlatform.type === targetPlatform) {
      console.log(`Already in ${targetPlatform} view.`);
      return;
    }
    
    // Set user preference to target platform
    this.setUserPreference(targetPlatform);
    
    // Trigger event for platform-specific layouts to be applied
    const event = new CustomEvent('platformViewSwitched', { 
      detail: { 
        previous: this.currentPlatform.type,
        current: targetPlatform
      }
    });
    window.dispatchEvent(event);
    
    console.log(`Switched to ${targetPlatform} view.`);
  }
}

// Export a global instance for easier imports
export const platformDetector = PlatformDetector.getInstance();
