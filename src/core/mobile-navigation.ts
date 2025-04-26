/**
 * Mobile Navigation
 * Provides a touch-friendly navigation interface for mobile devices
 */

import { IWindowManager } from './window-manager-interface';
import { AppManager } from './app-manager';
import { OS } from './os';
import { addTouchEventListener, getTouchCoordinates } from './touch-events';

export class MobileNavigationBar {
  private element: HTMLElement | null = null;
  private backButton: HTMLElement | null = null;
  private homeButton: HTMLElement | null = null;
  private appsButton: HTMLElement | null = null;
  private windowManager: IWindowManager;
  private appManager: AppManager;
  private os: OS;
  private isVisible: boolean = true;
  private sheetVisible: boolean = false;
  private controlSheet: HTMLElement | null = null;
  private sheetOverlay: HTMLElement | null = null;

  /**
   * Creates a new mobile navigation bar
   * @param windowManager Window manager instance
   * @param appManager App manager instance
   * @param os Operating system instance
   */
  constructor(
    windowManager: IWindowManager,
    appManager: AppManager,
    os: OS
  ) {
    this.windowManager = windowManager;
    this.appManager = appManager;
    this.os = os;
  }

  /**
   * Initialize the mobile navigation bar
   */
  public init(): void {
    // Create navigation element if it doesn't exist
    if (!this.element) {
      this.element = document.createElement('div');
      this.element.className = 'mobile-navigation-bar';
      document.body.appendChild(this.element);

      // Create buttons
      this.createNavigationButtons();
      
      // Create control sheet (initially hidden)
      this.createControlSheet();
    }
    
    // Set up gesture detection
    this.setupGestureDetection();
  }
  
  /**
   * Create the navigation buttons
   */
  private createNavigationButtons(): void {
    if (!this.element) return;
    
    // Back button
    this.backButton = document.createElement('div');
    this.backButton.className = 'nav-button back-button';
    this.backButton.innerHTML = `
      <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <line x1="19" y1="12" x2="5" y2="12"></line>
        <polyline points="12 19 5 12 12 5"></polyline>
      </svg>
    `;
    this.backButton.addEventListener('click', () => this.handleBackButtonClick());
    this.element.appendChild(this.backButton);
    
    // Home button
    this.homeButton = document.createElement('div');
    this.homeButton.className = 'nav-button home-button';
    this.homeButton.innerHTML = `
      <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
        <polyline points="9 22 9 12 15 12 15 22"></polyline>
      </svg>
    `;
    this.homeButton.addEventListener('click', () => this.handleHomeButtonClick());
    this.element.appendChild(this.homeButton);
    
    // Apps button (recent apps)
    this.appsButton = document.createElement('div');
    this.appsButton.className = 'nav-button apps-button';
    this.appsButton.innerHTML = `
      <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <rect x="4" y="4" width="6" height="6"></rect>
        <rect x="14" y="4" width="6" height="6"></rect>
        <rect x="4" y="14" width="6" height="6"></rect>
        <rect x="14" y="14" width="6" height="6"></rect>
      </svg>
    `;
    this.appsButton.addEventListener('click', () => this.handleAppsButtonClick());
    this.element.appendChild(this.appsButton);
  }

  /**
   * Create the control sheet that appears from bottom swipe
   */
  private createControlSheet(): void {
    this.controlSheet = document.createElement('div');
    this.controlSheet.className = 'mobile-control-sheet';
    
    // Create sheet handle for pulling
    const sheetHandle = document.createElement('div');
    sheetHandle.className = 'sheet-handle';
    this.controlSheet.appendChild(sheetHandle);
    
    // Create common controls
    this.createQuickControls();
    this.createVolumeControl();
    this.createBrightnessControl();
    this.createQuickApps();
    
    // Create overlay for dismissing sheet
    this.sheetOverlay = document.createElement('div');
    this.sheetOverlay.className = 'sheet-overlay';
    this.sheetOverlay.addEventListener('click', () => this.hideControlSheet());
    
    // Add to body but keep hidden initially
    document.body.appendChild(this.sheetOverlay);
    document.body.appendChild(this.controlSheet);
    
    // Set up sheet gestures
    this.setupSheetGestures();
  }
  
  /**
   * Create quick controls like WiFi, Bluetooth, etc
   */
  private createQuickControls(): void {
    if (!this.controlSheet) return;
    
    const quickControls = document.createElement('div');
    quickControls.className = 'quick-controls';
    
    // Create toggle buttons
    const toggles = [
      { id: 'wifi', icon: 'wifi', label: 'WiFi' },
      { id: 'bluetooth', icon: 'bluetooth', label: 'Bluetooth' },
      { id: 'airplane', icon: 'plane', label: 'Airplane' },
      { id: 'darkmode', icon: 'moon', label: 'Dark Mode' },
    ];
    
    toggles.forEach(toggle => {
      const toggleButton = document.createElement('div');
      toggleButton.className = 'quick-toggle';
      toggleButton.setAttribute('data-toggle', toggle.id);
      toggleButton.innerHTML = `
        <div class="toggle-icon">${this.getToggleIcon(toggle.icon)}</div>
        <div class="toggle-label">${toggle.label}</div>
      `;
      
      toggleButton.addEventListener('click', () => {
        toggleButton.classList.toggle('active');
        this.handleToggleClick(toggle.id);
      });
      
      quickControls.appendChild(toggleButton);
    });
    
    this.controlSheet.appendChild(quickControls);
  }
  
  /**
   * Create volume slider control
   */
  private createVolumeControl(): void {
    if (!this.controlSheet) return;
    
    const volumeControl = document.createElement('div');
    volumeControl.className = 'slider-control volume-control';
    
    volumeControl.innerHTML = `
      <div class="slider-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"></polygon>
          <path d="M15.54 8.46a5 5 0 0 1 0 7.07"></path>
          <path d="M19.07 4.93a10 10 0 0 1 0 14.14"></path>
        </svg>
      </div>
      <input type="range" min="0" max="100" value="70" class="slider" id="volume-slider">
    `;
    
    const slider = volumeControl.querySelector('#volume-slider') as HTMLInputElement;
    slider.addEventListener('input', () => {
      const volume = parseInt(slider.value, 10);
      this.handleVolumeChange(volume);
    });
    
    this.controlSheet.appendChild(volumeControl);
  }
  
  /**
   * Create brightness slider control
   */
  private createBrightnessControl(): void {
    if (!this.controlSheet) return;
    
    const brightnessControl = document.createElement('div');
    brightnessControl.className = 'slider-control brightness-control';
    
    brightnessControl.innerHTML = `
      <div class="slider-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="5"></circle>
          <line x1="12" y1="1" x2="12" y2="3"></line>
          <line x1="12" y1="21" x2="12" y2="23"></line>
          <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
          <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
          <line x1="1" y1="12" x2="3" y2="12"></line>
          <line x1="21" y1="12" x2="23" y2="12"></line>
          <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
          <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
        </svg>
      </div>
      <input type="range" min="0" max="100" value="80" class="slider" id="brightness-slider">
    `;
    
    const slider = brightnessControl.querySelector('#brightness-slider') as HTMLInputElement;
    slider.addEventListener('input', () => {
      const brightness = parseInt(slider.value, 10);
      this.handleBrightnessChange(brightness);
    });
    
    this.controlSheet.appendChild(brightnessControl);
  }
  
  /**
   * Create quick launch apps
   */
  private createQuickApps(): void {
    if (!this.controlSheet) return;
    
    const quickApps = document.createElement('div');
    quickApps.className = 'quick-apps';
    
    // Get recent apps from app manager (limit to 4)
    const recentApps = this.appManager.getRecentApps().slice(0, 4);
    
    // If not enough recent apps, add some default ones
    const defaultApps = ['settings', 'terminal', 'browser', 'file-explorer'];
    const appsToShow = recentApps.length >= 4 ? recentApps : 
                       [...recentApps, ...defaultApps.filter(
                         app => !recentApps.find(rApp => rApp.id === app)
                       ).slice(0, 4 - recentApps.length)];
    
    // Add app buttons
    appsToShow.forEach(app => {
      const appInfo = typeof app === 'string' ? this.appManager.getAppInfo(app) : app;
      if (!appInfo) return;
      
      const appButton = document.createElement('div');
      appButton.className = 'quick-app';
      appButton.innerHTML = `
        <div class="app-icon">${appInfo.icon || ''}</div>
        <div class="app-name">${appInfo.name}</div>
      `;
      
      appButton.addEventListener('click', () => {
        this.hideControlSheet();
        this.appManager.launchApp(appInfo.id);
      });
      
      quickApps.appendChild(appButton);
    });
    
    this.controlSheet.appendChild(quickApps);
  }
  
  /**
   * Set up gestures for the control sheet
   */
  private setupSheetGestures(): void {
    if (!this.controlSheet) return;
    
    const handle = this.controlSheet.querySelector('.sheet-handle');
    if (!handle) return;
    
    let startY = 0;
    let currentY = 0;
    const sheetHeight = this.controlSheet.offsetHeight;
    
    addTouchEventListener(handle as HTMLElement, 'touchstart', (e) => {
      const coords = getTouchCoordinates(e);
      startY = coords.clientY;
      currentY = startY;
      
      this.controlSheet!.style.transition = '';
    });
    
    addTouchEventListener(handle as HTMLElement, 'touchmove', (e) => {
      const coords = getTouchCoordinates(e);
      currentY = coords.clientY;
      const deltaY = currentY - startY;
      
      if (deltaY > 0) {
        // Allow pulling down
        this.controlSheet!.style.transform = `translateY(${deltaY}px)`;
      }
    });
    
    addTouchEventListener(handle as HTMLElement, 'touchend', () => {
      const deltaY = currentY - startY;
      
      if (deltaY > 70) {
        // Pull threshold reached, close sheet
        this.hideControlSheet();
      } else {
        // Reset to open position
        this.controlSheet!.style.transition = 'transform 0.3s ease-out';
        this.controlSheet!.style.transform = '';
      }
    });
  }

  /**
   * Set up gesture detection for navigation
   */
  private setupGestureDetection(): void {
    // Set up swipe up from bottom to show sheet
    addTouchEventListener(document, 'touchstart', (e) => {
      const coords = getTouchCoordinates(e);
      const touchY = coords.clientY;
      const threshold = window.innerHeight - 20;
      
      if (touchY > threshold) {
        this.handleBottomEdgeSwipe(e);
      }
    });

    // Setup other gestures
    this.setupLongPressGesture();
    this.setupPinchGesture();
  }

  /**
   * Handle swipe from bottom edge of screen
   */
  private handleBottomEdgeSwipe(startEvent: TouchEvent): void {
    const coords = getTouchCoordinates(startEvent);
    const initialY = coords.clientY;
    let startDistance = 0;
    let currentDistance = 0;

    const onMove = (moveEvent: TouchEvent): void => {
      const moveCoords = getTouchCoordinates(moveEvent);
      currentDistance = initialY - moveCoords.clientY;
      
      if (currentDistance > startDistance && currentDistance > 50) {
        // User has swiped up enough to show sheet
        this.showControlSheet();
        document.removeEventListener('touchmove', onMove as EventListener);
        document.removeEventListener('touchend', onEnd as EventListener);
      }
    };
    
    const onEnd = (): void => {
      document.removeEventListener('touchmove', onMove as EventListener);
      document.removeEventListener('touchend', onEnd as EventListener);
    };
    
    document.addEventListener('touchmove', onMove as EventListener);
    document.addEventListener('touchend', onEnd as EventListener);
  }
  
  /**
   * Set up long press for context menus
   */
  private setupLongPressGesture(): void {
    // Use touch events library for this
    addTouchEventListener(document, 'touchstart', (e: TouchEvent) => {
      // Don't handle events on the navigation bar itself
      if (this.element?.contains(e.target as Node)) {
        return;
      }
      
      const touch = e.touches[0];
      const target = touch.target as HTMLElement;
      
      // Don't trigger long press on certain elements
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || 
          target.classList.contains('ignore-long-press')) {
        return;
      }
      
      // Set up long press timer
      const timer = setTimeout(() => {
        // Check if supported by item
        const supportContextMenu = target.getAttribute('data-context-menu') === 'true';
        
        if (supportContextMenu) {
          this.showContextMenu(target, touch.clientX, touch.clientY);
        }
      }, 500);
      
      const clearTimer = () => {
        clearTimeout(timer);
      };
      
      addTouchEventListener(document, 'touchend', () => {
        clearTimer();
        document.removeEventListener('touchend', clearTimer as EventListener);
        document.removeEventListener('touchmove', clearTimer as EventListener);
      });
      
      addTouchEventListener(document, 'touchmove', clearTimer);
    });
  }
  
  /**
   * Set up pinch to zoom gesture
   */
  private setupPinchGesture(): void {
    let initialDistance = 0;
    let currentDistance = 0;
    let scaling = false;
    
    addTouchEventListener(document, 'touchstart', (e: TouchEvent) => {
      // Need at least 2 touch points for pinch
      if (e.touches.length < 2) return;
      
      // Calculate initial distance
      const touch1 = e.touches[0];
      const touch2 = e.touches[1];
      
      initialDistance = Math.hypot(
        touch1.clientX - touch2.clientX,
        touch1.clientY - touch2.clientY
      );
      
      scaling = true;
    });
    
    addTouchEventListener(document, 'touchmove', (e: TouchEvent) => {
      if (!scaling || e.touches.length < 2) return;
      
      const touch1 = e.touches[0];
      const touch2 = e.touches[1];
      
      currentDistance = Math.hypot(
        touch1.clientX - touch2.clientX,
        touch1.clientY - touch2.clientY
      );
      
      // Calculate scale factor
      const scale = currentDistance / initialDistance;
      
      // Handle scaling with some thresholds
      if (scale > 1.3) {
        // Zoom in
        this.handleZoomIn();
      } else if (scale < 0.7) {
        // Zoom out
        this.handleZoomOut();
      }
    });
    
    addTouchEventListener(document, 'touchend', () => {
      scaling = false;
    });
  }

  /**
   * Show system context menu
   */
  private showContextMenu(element: HTMLElement, x: number, y: number): void {
    // Here we would show the context menu
    // Implementation would depend on the context menu component
    console.log(`Show context menu at ${x},${y} for`, element);
  }
  
  /**
   * Show the control sheet
   */
  public showControlSheet(): void {
    if (!this.controlSheet || !this.sheetOverlay || this.sheetVisible) return;
    
    this.sheetOverlay.classList.add('visible');
    this.controlSheet.classList.add('visible');
    this.sheetVisible = true;
  }
  
  /**
   * Hide the control sheet
   */
  public hideControlSheet(): void {
    if (!this.controlSheet || !this.sheetOverlay || !this.sheetVisible) return;
    
    this.sheetOverlay.classList.remove('visible');
    this.controlSheet.classList.remove('visible');
    this.sheetVisible = false;
  }

  /**
   * Handle back button click
   */
  private handleBackButtonClick(): void {
    // Use window manager to go back in active window
    // This is a cast just to access the mobile-specific methods
    // In a real implementation, this should be properly typed
    const mobileWindowManager = this.windowManager as unknown as { goBack: () => void };
    if (typeof mobileWindowManager.goBack === 'function') {
      mobileWindowManager.goBack();
    }
  }

  /**
   * Handle home button click
   */
  private handleHomeButtonClick(): void {
    // Get Desktop API through OS to show desktop
    const desktop = this.os['getDesktop'] ? this.os['getDesktop']() : null;
    if (desktop && typeof desktop.showDesktop === 'function') {
      desktop.showDesktop();
    }
  }

  /**
   * Handle apps button click (show recent apps)
   */
  private handleAppsButtonClick(): void {
    // Show recent apps view
    // In a real implementation, this would show a UI for switching apps
    console.log('Show recent apps');
  }
  
  /**
   * Handle toggle click (WiFi, Bluetooth, etc)
   */
  private handleToggleClick(toggleId: string): void {
    // In a real implementation, this would manage system settings
    console.log(`Toggle ${toggleId}`);
  }
  
  /**
   * Handle volume change
   */
  private handleVolumeChange(volume: number): void {
    // In a real implementation, this would change system volume
    console.log(`Set volume to ${volume}`);
  }
  
  /**
   * Handle brightness change
   */
  private handleBrightnessChange(brightness: number): void {
    // In a real implementation, this would change screen brightness
    console.log(`Set brightness to ${brightness}`);
  }
  
  /**
   * Handle zoom in gesture
   */
  private handleZoomIn(): void {
    // In a real implementation, this would zoom the content
    console.log('Zoom in');
  }
  
  /**
   * Handle zoom out gesture
   */
  private handleZoomOut(): void {
    // In a real implementation, this would zoom out the content
    console.log('Zoom out');
  }
  
  /**
   * Get icon SVG for toggle buttons
   */
  private getToggleIcon(iconName: string): string {
    // Simple function to return SVG icons
    const icons: Record<string, string> = {
      wifi: `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M5 12.55a11 11 0 0 1 14.08 0"></path>
              <path d="M1.42 9a16 16 0 0 1 21.16 0"></path>
              <path d="M8.53 16.11a6 6 0 0 1 6.95 0"></path>
              <line x1="12" y1="20" x2="12.01" y2="20"></line>
            </svg>`,
      bluetooth: `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                   <polyline points="6 7 12 12 6 17"></polyline>
                   <polyline points="12 12 18 7 18 17 12 12"></polyline>
                 </svg>`,
      moon: `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
            </svg>`,
      plane: `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
               <path d="M22 2L11 13"></path>
               <path d="M22 2l-7 20-4-9-9-4 20-7z"></path>
             </svg>`
    };
    
    return icons[iconName] || '';
  }

  /**
   * Show the navigation bar
   */
  public show(): void {
    if (!this.element || this.isVisible) return;
    
    this.element.classList.add('visible');
    this.isVisible = true;
  }
  
  /**
   * Hide the navigation bar
   */
  public hide(): void {
    if (!this.element || !this.isVisible) return;
    
    this.element.classList.remove('visible');
    this.isVisible = false;
  }
}
