/**
 * Mobile Navigation System
 * Provides a touch-friendly navigation interface for mobile devices
 */

import { IWindowManager } from './window-manager-interface';
import { MobileWindowManager } from './mobile-window-manager';
import { AppManager } from './app-manager';
import { PlatformType, platformDetector } from './platform-detector';
import { OS } from './os';

/**
 * Interface for mobile navigation item
 */
export interface MobileNavItem {
  id: string;
  label: string;
  icon: string;
  action: () => void;
  badge?: number | string;
  isActive?: boolean;
}

/**
 * Mobile Navigation Bar for smartphone interface
 */
export class MobileNavigationBar {
  private element: HTMLElement | null = null;
  private bottomSheet: HTMLElement | null = null;
  private quickLaunchBar: HTMLElement | null = null;
  private primaryNavItems: MobileNavItem[] = [];
  private quickLaunchItems: MobileNavItem[] = [];
  private recentApps: MobileNavItem[] = [];
  private isBottomSheetExpanded: boolean = false;
  private windowManager: IWindowManager;
  private appManager: AppManager;
  private os: OS;

  /**
   * Create a new mobile navigation bar
   * @param windowManager Window manager instance
   * @param appManager App manager instance
   * @param os Operating system instance
   */
  constructor(windowManager: IWindowManager, appManager: AppManager, os: OS) {
    this.windowManager = windowManager;
    this.appManager = appManager;
    this.os = os;
  }

  /**
   * Initialize the mobile navigation bar
   */
  public init(): void {
    // Create navigation bar element if it doesn't exist
    if (!this.element) {
      this.element = document.createElement('div');
      this.element.className = 'mobile-nav-bar';
      document.body.appendChild(this.element);
    }

    // Create bottom sheet element if it doesn't exist
    if (!this.bottomSheet) {
      this.bottomSheet = document.createElement('div');
      this.bottomSheet.className = 'mobile-bottom-sheet';
      document.body.appendChild(this.bottomSheet);
    }

    // Create quick launch bar if it doesn't exist
    if (!this.quickLaunchBar) {
      this.quickLaunchBar = document.createElement('div');
      this.quickLaunchBar.className = 'mobile-quick-launch-bar';
      this.element.appendChild(this.quickLaunchBar);
    }

    // Setup default navigation items
    this.setupDefaultNavItems();

    // Setup gesture detection
    this.setupGestureDetection();

    // Render the navigation
    this.render();
  }

  /**
   * Set up default navigation items
   */
  private setupDefaultNavItems(): void {
    // Primary navigation items (shown in main nav bar)
    this.primaryNavItems = [
      {
        id: 'home',
        label: 'Home',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path><polyline points="9 22 9 12 15 12 15 22"></polyline></svg>',
        action: () => this.showHome(),
        isActive: true
      },
      {
        id: 'apps',
        label: 'Apps',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7"></rect><rect x="14" y="3" width="7" height="7"></rect><rect x="14" y="14" width="7" height="7"></rect><rect x="3" y="14" width="7" height="7"></rect></svg>',
        action: () => this.toggleBottomSheet()
      },
      {
        id: 'recent',
        label: 'Recent',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>',
        action: () => this.showRecentApps()
      },
      {
        id: 'search',
        label: 'Search',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>',
        action: () => this.showSearch()
      },
      {
        id: 'settings',
        label: 'Settings',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"></circle><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path></svg>',
        action: () => this.openSettings()
      }
    ];

    // Get installed apps for quick launch
    this.updateQuickLaunchItems();
  }

  /**
   * Update quick launch items based on installed apps
   */
  public updateQuickLaunchItems(): void {
    // Get frequently used apps from app manager
    const apps = this.appManager.getInstalledApps();
    const frequentApps = apps.slice(0, 4); // Take top 4 most frequently used apps

    this.quickLaunchItems = frequentApps.map(app => ({
      id: app.id,
      label: app.name,
      icon: app.icon || '',
      action: () => this.launchApp(app.id)
    }));

    // Update recent apps list
    this.updateRecentApps();
  }

  /**
   * Update list of recently used applications
   */
  private updateRecentApps(): void {
    // In a real implementation, this would get data from a recent apps tracker
    // For now, we'll use the same apps as quick launch but in a different order
    this.recentApps = [...this.quickLaunchItems].reverse();
  }

  /**
   * Render the navigation bar
   */
  private render(): void {
    if (!this.element || !this.bottomSheet || !this.quickLaunchBar) return;

    // Clear existing content
    this.element.innerHTML = '';
    this.bottomSheet.innerHTML = '';
    
    // Re-add quick launch bar
    this.quickLaunchBar.innerHTML = '';
    this.element.appendChild(this.quickLaunchBar);

    // Create primary navigation items
    const primaryNav = document.createElement('div');
    primaryNav.className = 'mobile-primary-nav';

    this.primaryNavItems.forEach(item => {
      const navItem = document.createElement('button');
      navItem.className = `mobile-nav-item ${item.isActive ? 'active' : ''}`;
      navItem.setAttribute('data-id', item.id);
      navItem.innerHTML = `
        <div class="nav-icon">${item.icon}</div>
        <div class="nav-label">${item.label}</div>
      `;

      // Add badge if exists
      if (item.badge) {
        const badge = document.createElement('div');
        badge.className = 'nav-badge';
        badge.textContent = `${item.badge}`;
        navItem.appendChild(badge);
      }

      navItem.addEventListener('click', () => {
        this.activateNavItem(item.id);
        item.action();
      });

      primaryNav.appendChild(navItem);
    });

    this.element.appendChild(primaryNav);

    // Render quick launch items
    this.quickLaunchItems.forEach(item => {
      const quickLaunchItem = document.createElement('button');
      quickLaunchItem.className = 'quick-launch-item';
      quickLaunchItem.setAttribute('data-id', item.id);
      quickLaunchItem.innerHTML = item.icon;
      quickLaunchItem.title = item.label;
      quickLaunchItem.addEventListener('click', item.action);
      this.quickLaunchBar?.appendChild(quickLaunchItem);
    });

    // Render bottom sheet content
    this.renderBottomSheet();
  }

  /**
   * Render the bottom sheet with app grid
   */
  private renderBottomSheet(): void {
    if (!this.bottomSheet) return;

    // Create app grid section
    const appGrid = document.createElement('div');
    appGrid.className = 'mobile-app-grid';

    // Get all apps from app manager
    const apps = this.appManager.getInstalledApps();

    // Create app icons
    apps.forEach(app => {
      const appItem = document.createElement('div');
      appItem.className = 'mobile-app-item';
      appItem.setAttribute('data-app-id', app.id);
      appItem.innerHTML = `
        <div class="app-icon">${app.icon || ''}</div>
        <div class="app-label">${app.name}</div>
      `;
      appItem.addEventListener('click', () => {
        this.launchApp(app.id);
        this.hideBottomSheet();
      });
      appGrid.appendChild(appItem);
    });

    // Create search bar
    const searchBar = document.createElement('div');
    searchBar.className = 'mobile-search-bar';
    searchBar.innerHTML = `
      <div class="search-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
      </div>
      <input type="text" placeholder="Search apps..." class="search-input">
    `;

    // Add search functionality
    const searchInput = searchBar.querySelector('.search-input') as HTMLInputElement;
    if (searchInput) {
      searchInput.addEventListener('input', (e) => {
        const searchTerm = (e.target as HTMLInputElement).value.toLowerCase();
        const appItems = appGrid.querySelectorAll('.mobile-app-item');
        
        appItems.forEach(item => {
          const label = item.querySelector('.app-label')?.textContent?.toLowerCase() || '';
          if (label.includes(searchTerm)) {
            (item as HTMLElement).style.display = 'flex';
          } else {
            (item as HTMLElement).style.display = 'none';
          }
        });
      });
    }

    // Create recent apps section
    const recentAppsSection = document.createElement('div');
    recentAppsSection.className = 'mobile-recent-apps';
    recentAppsSection.innerHTML = '<h3>Recent Apps</h3>';

    const recentAppsGrid = document.createElement('div');
    recentAppsGrid.className = 'recent-apps-grid';

    this.recentApps.forEach(app => {
      const recentAppItem = document.createElement('div');
      recentAppItem.className = 'mobile-recent-app-item';
      recentAppItem.innerHTML = `
        <div class="app-icon">${app.icon}</div>
        <div class="app-label">${app.label}</div>
      `;
      recentAppItem.addEventListener('click', () => {
        app.action();
        this.hideBottomSheet();
      });
      recentAppsGrid.appendChild(recentAppItem);
    });

    recentAppsSection.appendChild(recentAppsGrid);

    // Add elements to bottom sheet
    this.bottomSheet.appendChild(searchBar);
    this.bottomSheet.appendChild(recentAppsSection);
    this.bottomSheet.appendChild(appGrid);

    // Add pull indicator and pull to close functionality
    const pullIndicator = document.createElement('div');
    pullIndicator.className = 'bottom-sheet-pull-indicator';
    this.bottomSheet.prepend(pullIndicator);

    pullIndicator.addEventListener('pointerdown', (e) => {
      this.setupPullToClose(e);
    });
  }

  /**
   * Set up pull to close gesture for bottom sheet
   */
  private setupPullToClose(startEvent: PointerEvent): void {
    if (!this.bottomSheet) return;

    const initialY = startEvent.clientY;
    const sheetHeight = this.bottomSheet.offsetHeight;
    let currentY = initialY;

    const onMove = (moveEvent: PointerEvent) => {
      currentY = moveEvent.clientY;
      const deltaY = currentY - initialY;
      
      if (deltaY > 0) {
        // Only allow pulling down, not up
        this.bottomSheet!.style.transform = `translateY(${deltaY}px)`;
      }
    };

    const onEnd = () => {
      const deltaY = currentY - initialY;
      
      // If pulled down more than 30% of height, close sheet
      if (deltaY > sheetHeight * 0.3) {
        this.hideBottomSheet();
      } else {
        // Otherwise snap back
        this.bottomSheet!.style.transform = 'translateY(0)';
      }
      
      // Remove event listeners
      document.removeEventListener('pointermove', onMove as EventListener);
      document.removeEventListener('pointerup', onEnd);
      document.removeEventListener('pointercancel', onEnd);
    };

    document.addEventListener('pointermove', onMove as EventListener);
    document.addEventListener('pointerup', onEnd);
    document.addEventListener('pointercancel', onEnd);
  }

  /**
   * Set up gesture detection for navigation
   */
  private setupGestureDetection(): void {
    // Set up swipe up from bottom to show sheet
    document.addEventListener('touchstart', (e) => {
      const touchY = e.touches[0].clientY;
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
    const initialY = startEvent.touches[0].clientY;
    let startDistance = 0;
    let currentDistance = 0;

    const onMove = (moveEvent: TouchEvent) => {
      currentDistance = initialY - moveEvent.touches[0].clientY;
      
      if (currentDistance > startDistance && currentDistance > 50) {
        // Swipe up detected, expand bottom sheet
        moveEvent.preventDefault();
        this.showBottomSheet();
        
        // Remove event listeners
        document.removeEventListener('touchmove', onMove);
        document.removeEventListener('touchend', onEnd);
      }
    };

    const onEnd = () => {
      document.removeEventListener('touchmove', onMove);
      document.removeEventListener('touchend', onEnd);
    };

    document.addEventListener('touchmove', onMove);
    document.addEventListener('touchend', onEnd);
  }

  /**
   * Set up long press gesture
   */
  private setupLongPressGesture(): void {
    let longPressTimer: number | null = null;
    const longPressDuration = 700; // ms

    document.addEventListener('touchstart', (e) => {
      if (longPressTimer) {
        clearTimeout(longPressTimer);
      }

      const touch = e.touches[0];
      const target = touch.target as HTMLElement;
      
      // Check if target is an app icon or similar
      if (target.closest('.mobile-app-item') || target.closest('.quick-launch-item')) {
        longPressTimer = window.setTimeout(() => {
          this.handleLongPress(target);
        }, longPressDuration);
      }
    });

    document.addEventListener('touchend', () => {
      if (longPressTimer) {
        clearTimeout(longPressTimer);
        longPressTimer = null;
      }
    });

    document.addEventListener('touchmove', () => {
      if (longPressTimer) {
        clearTimeout(longPressTimer);
        longPressTimer = null;
      }
    });
  }

  /**
   * Handle long press on an element
   */
  private handleLongPress(target: HTMLElement): void {
    const appItem = target.closest('[data-app-id]') as HTMLElement;
    if (!appItem) return;

    const appId = appItem.getAttribute('data-app-id');
    if (!appId) return;

    // Show context menu with options
    this.showAppContextMenu(appId, appItem);
  }

  /**
   * Show context menu for an app
   */
  private showAppContextMenu(appId: string, element: HTMLElement): void {
    // Create context menu
    const contextMenu = document.createElement('div');
    contextMenu.className = 'mobile-context-menu';
    
    // Position menu near the element
    const rect = element.getBoundingClientRect();
    contextMenu.style.left = `${rect.left}px`;
    contextMenu.style.top = `${rect.bottom + 10}px`;
    
    // Add menu items
    const app = this.appManager.getAppInfo(appId);
    if (!app) return;

    // App name
    const nameItem = document.createElement('div');
    nameItem.className = 'context-menu-header';
    nameItem.textContent = app.name;
    contextMenu.appendChild(nameItem);

    // Launch option
    const launchItem = document.createElement('div');
    launchItem.className = 'context-menu-item';
    launchItem.textContent = 'Launch';
    launchItem.addEventListener('click', () => {
      this.launchApp(appId);
      document.body.removeChild(contextMenu);
    });
    contextMenu.appendChild(launchItem);

    // Add to home option
    const addToHomeItem = document.createElement('div');
    addToHomeItem.className = 'context-menu-item';
    addToHomeItem.textContent = 'Add to Quick Launch';
    addToHomeItem.addEventListener('click', () => {
      this.addToQuickLaunch(appId);
      document.body.removeChild(contextMenu);
    });
    contextMenu.appendChild(addToHomeItem);

    // Add to document and set up dismiss logic
    document.body.appendChild(contextMenu);
    
    // Dismiss on outside click
    const dismissHandler = (e: MouseEvent) => {
      if (!contextMenu.contains(e.target as Node)) {
        document.body.removeChild(contextMenu);
        document.removeEventListener('mousedown', dismissHandler);
      }
    };
    
    // Set a timeout to start listening for clicks
    setTimeout(() => {
      document.addEventListener('mousedown', dismissHandler);
    }, 10);
  }

  /**
   * Set up pinch gesture for app management
   */
  private setupPinchGesture(): void {
    let initialDistance = 0;
    let initialScale = 1;
    
    document.addEventListener('touchstart', (e) => {
      if (e.touches.length === 2) {
        // Calculate initial distance between two fingers
        initialDistance = Math.hypot(
          e.touches[0].clientX - e.touches[1].clientX,
          e.touches[0].clientY - e.touches[1].clientY
        );
      }
    });
    
    document.addEventListener('touchmove', (e) => {
      if (e.touches.length === 2) {
        // Calculate new distance
        const currentDistance = Math.hypot(
          e.touches[0].clientX - e.touches[1].clientX,
          e.touches[0].clientY - e.touches[1].clientY
        );
        
        // Calculate scale factor
        const scaleFactor = currentDistance / initialDistance;
        
        // Pinch in (scale < 1) = close app
        if (scaleFactor < 0.7) {
          this.handlePinchClose();
          initialDistance = currentDistance; // Reset to prevent multiple triggers
        } 
        // Pinch out (scale > 1) = app switcher
        else if (scaleFactor > 1.3) {
          this.handlePinchOpen();
          initialDistance = currentDistance; // Reset to prevent multiple triggers
        }
      }
    });
  }

  /**
   * Handle pinch close gesture
   */
  private handlePinchClose(): void {
    // Get current active window from window manager
    const activeWindowId = this.windowManager.getActiveWindowId();
    if (activeWindowId) {
      this.windowManager.minimizeWindow(activeWindowId);
    }
  }

  /**
   * Handle pinch open gesture
   */
  private handlePinchOpen(): void {
    // Show app switcher
    if (this.windowManager instanceof MobileWindowManager) {
      (this.windowManager as MobileWindowManager)['showAppSwitcher']();
    }
  }

  /**
   * Show bottom sheet
   */
  private showBottomSheet(): void {
    if (!this.bottomSheet) return;
    
    this.bottomSheet.classList.add('expanded');
    this.isBottomSheetExpanded = true;
    
    // Reset any transform
    this.bottomSheet.style.transform = '';
    
    // Set initial focus on search input
    setTimeout(() => {
      const searchInput = this.bottomSheet?.querySelector('.search-input') as HTMLInputElement;
      if (searchInput) {
        searchInput.focus();
      }
    }, 300);
  }

  /**
   * Hide bottom sheet
   */
  private hideBottomSheet(): void {
    if (!this.bottomSheet) return;
    
    this.bottomSheet.classList.remove('expanded');
    this.isBottomSheetExpanded = false;
    
    // Reset any transform
    this.bottomSheet.style.transform = '';
  }

  /**
   * Toggle bottom sheet visibility
   */
  private toggleBottomSheet(): void {
    if (this.isBottomSheetExpanded) {
      this.hideBottomSheet();
    } else {
      this.showBottomSheet();
    }
  }

  /**
   * Activate a navigation item
   */
  private activateNavItem(itemId: string): void {
    this.primaryNavItems.forEach(item => {
      item.isActive = item.id === itemId;
    });
    
    // Update UI
    if (this.element) {
      const navItems = this.element.querySelectorAll('.mobile-nav-item');
      navItems.forEach(item => {
        if (item.getAttribute('data-id') === itemId) {
          item.classList.add('active');
        } else {
          item.classList.remove('active');
        }
      });
    }
  }

  /**
   * Show home screen
   */
  private showHome(): void {
    // Minimize all windows
    const windowManager = this.windowManager as MobileWindowManager;
    windowManager.closeAllWindows();
    
    // Show desktop/home screen
    this.os.Ready(() => {
      this.os.getDesktop().showDesktop();
    });
  }

  /**
   * Show recent apps screen
   */
  private showRecentApps(): void {
    if (this.windowManager instanceof MobileWindowManager) {
      (this.windowManager as MobileWindowManager)['showAppSwitcher']();
    }
  }

  /**
   * Show search interface
   */
  private showSearch(): void {
    this.showBottomSheet();
    
    // Focus on search input
    setTimeout(() => {
      const searchInput = this.bottomSheet?.querySelector('.search-input') as HTMLInputElement;
      if (searchInput) {
        searchInput.focus();
      }
    }, 300);
  }

  /**
   * Launch an application
   */
  private launchApp(appId: string): void {
    this.appManager.launchApp(appId);
  }

  /**
   * Open settings app
   */
  private openSettings(): void {
    this.appManager.launchApp('settings');
  }

  /**
   * Add an app to quick launch bar
   */
  private addToQuickLaunch(appId: string): void {
    const app = this.appManager.getAppInfo(appId);
    if (!app) return;
    
    // Check if app already in quick launch
    const existingIndex = this.quickLaunchItems.findIndex(item => item.id === appId);
    if (existingIndex >= 0) return;
    
    // Remove last item if we already have 4
    if (this.quickLaunchItems.length >= 4) {
      this.quickLaunchItems.pop();
    }
    
    // Add new app at the beginning
    this.quickLaunchItems.unshift({
      id: app.id,
      label: app.name,
      icon: app.icon || '',
      action: () => this.launchApp(app.id)
    });
    
    // Update UI
    this.render();
  }
}
