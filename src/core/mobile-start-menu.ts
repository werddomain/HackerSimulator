/**
 * Mobile Start Menu
 * Provides a touch-friendly start menu interface for mobile devices
 */

import { StartMenuController } from './start-menu';
import { AppManager } from './app-manager';
import { UserSettings } from './UserSettings';
import { OS } from './os';

export class MobileStartMenu {
  private element: HTMLElement | null = null;
  private startMenuController: StartMenuController;
  private appManager: AppManager;
  private userSettings: UserSettings;
  private os: OS;
  private isVisible: boolean = false;
  private searchResults: HTMLElement | null = null;
  private featuredApps: HTMLElement | null = null;
  private allApps: HTMLElement | null = null;
  private recentApps: HTMLElement | null = null;
  private pinnedApps: string[] = [];

  /**
   * Creates a new mobile start menu
   * @param startMenuController The original start menu controller
   * @param appManager App manager instance
   * @param userSettings User settings instance
   * @param os Operating system instance
   */
  constructor(
    startMenuController: StartMenuController,
    appManager: AppManager,
    userSettings: UserSettings,
    os: OS
  ) {
    this.startMenuController = startMenuController;
    this.appManager = appManager;
    this.userSettings = userSettings;
    this.os = os;
  }

  /**
   * Initialize the mobile start menu
   */
  public init(): void {
    // Create start menu element if it doesn't exist
    if (!this.element) {
      this.element = document.createElement('div');
      this.element.className = 'mobile-start-menu';
      document.body.appendChild(this.element);

      // Create structure
      this.createStartMenuStructure();
    }

    // Load pinned apps from user settings
    this.loadPinnedApps();

    // Set up gesture for closing
    this.setupCloseGesture();
  }

  /**
   * Create the structure of the start menu
   */
  private createStartMenuStructure(): void {
    if (!this.element) return;

    // Create header
    const header = document.createElement('div');
    header.className = 'mobile-start-header';
    
    // Add user info
    const userInfo = document.createElement('div');
    userInfo.className = 'mobile-start-user-info';
    userInfo.innerHTML = `
      <div class="user-avatar">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
          <circle cx="12" cy="7" r="4"></circle>
        </svg>
      </div>
      <div class="user-name">User</div>
    `;
    header.appendChild(userInfo);
    
    // Add search input
    const searchContainer = document.createElement('div');
    searchContainer.className = 'mobile-start-search';
    searchContainer.innerHTML = `
      <div class="search-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="11" cy="11" r="8"></circle>
          <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
        </svg>
      </div>
      <input type="text" class="search-input" placeholder="Search apps...">
    `;
    header.appendChild(searchContainer);
    
    this.element.appendChild(header);
    
    // Create content area
    const content = document.createElement('div');
    content.className = 'mobile-start-content';
    
    // Create search results area (initially hidden)
    this.searchResults = document.createElement('div');
    this.searchResults.className = 'mobile-start-search-results';
    this.searchResults.style.display = 'none';
    content.appendChild(this.searchResults);
    
    // Create sections
    
    // Featured apps
    this.featuredApps = document.createElement('div');
    this.featuredApps.className = 'mobile-start-section';
    this.featuredApps.innerHTML = '<h2>Featured</h2><div class="app-grid featured-grid"></div>';
    content.appendChild(this.featuredApps);
    
    // Recent apps
    this.recentApps = document.createElement('div');
    this.recentApps.className = 'mobile-start-section';
    this.recentApps.innerHTML = '<h2>Recent</h2><div class="app-grid recent-grid"></div>';
    content.appendChild(this.recentApps);
    
    // All apps
    this.allApps = document.createElement('div');
    this.allApps.className = 'mobile-start-section';
    this.allApps.innerHTML = '<h2>All Apps</h2><div class="app-grid all-apps-grid"></div>';
    content.appendChild(this.allApps);
    
    this.element.appendChild(content);
    
    // Set up search functionality
    const searchInput = searchContainer.querySelector('.search-input') as HTMLInputElement;
    if (searchInput) {
      searchInput.addEventListener('input', () => {
        this.handleSearch(searchInput.value);
      });
      
      // Focus the input when the start menu is shown
      searchInput.addEventListener('focus', () => {
        // Show virtual keyboard for mobile
        searchInput.setAttribute('inputmode', 'text');
      });
    }
    
    // Create pull handle for closing
    const pullHandle = document.createElement('div');
    pullHandle.className = 'mobile-start-pull-handle';
    this.element.appendChild(pullHandle);
  }

  /**
   * Load pinned apps from user settings
   */
  private loadPinnedApps(): void {
    // In a real implementation, this would load from user settings
    // For now, use a few default apps
    this.pinnedApps = ['terminal', 'browser', 'settings', 'file-explorer'];
    
    // Update the UI
    this.updateAppGrids();
  }

  /**
   * Update app grids with current data
   */
  private updateAppGrids(): void {
    // Update featured (pinned) apps
    this.updateFeaturedApps();
    
    // Update recent apps
    this.updateRecentApps();
    
    // Update all apps
    this.updateAllApps();
  }

  /**
   * Update featured apps section
   */
  private updateFeaturedApps(): void {
    if (!this.featuredApps) return;
    
    const gridElement = this.featuredApps.querySelector('.featured-grid');
    if (!gridElement) return;
    
    // Clear grid
    gridElement.innerHTML = '';
    
    // Add pinned apps
    this.pinnedApps.forEach(appId => {
      const appInfo = this.appManager.getAppInfo(appId);
      if (!appInfo) return;
      
      const appElement = this.createAppElement(appInfo, true);
      gridElement.appendChild(appElement);
    });
  }

  /**
   * Update recent apps section
   */
  private updateRecentApps(): void {
    if (!this.recentApps) return;
    
    const gridElement = this.recentApps.querySelector('.recent-grid');
    if (!gridElement) return;
    
    // Clear grid
    gridElement.innerHTML = '';
    
    // Get recent apps from app manager
    const recentApps = this.appManager.getRecentApps().slice(0, 8);
    
    // Add recent apps
    recentApps.forEach(app => {
      const appElement = this.createAppElement(app);
      gridElement.appendChild(appElement);
    });
    
    // If no recent apps, hide section
    if (recentApps.length === 0) {
      this.recentApps.style.display = 'none';
    } else {
      this.recentApps.style.display = 'block';
    }
  }

  /**
   * Update all apps section
   */
  private updateAllApps(): void {
    if (!this.allApps) return;
    
    const gridElement = this.allApps.querySelector('.all-apps-grid');
    if (!gridElement) return;
    
    // Clear grid
    gridElement.innerHTML = '';
    
    // Get all apps from app manager
    const allApps = this.appManager.getInstalledApps();
    
    // Sort alphabetically
    allApps.sort((a, b) => a.name.localeCompare(b.name));
    
    // Add all apps
    allApps.forEach(app => {
      const appElement = this.createAppElement(app);
      gridElement.appendChild(appElement);
    });
  }

  /**
   * Create an app element for the grid
   */
  private createAppElement(app: any, isPinned: boolean = false): HTMLElement {
    const appElement = document.createElement('div');
    appElement.className = 'mobile-start-app';
    appElement.setAttribute('data-app-id', app.id);
    
    // App icon
    const iconElement = document.createElement('div');
    iconElement.className = 'app-icon';
    iconElement.innerHTML = app.icon || '';
    appElement.appendChild(iconElement);
    
    // App name
    const nameElement = document.createElement('div');
    nameElement.className = 'app-name';
    nameElement.textContent = app.name;
    appElement.appendChild(nameElement);
    
    // Add pin indicator if app is pinned
    if (isPinned) {
      appElement.classList.add('pinned');
    }
    
    // Add click event to launch app
    appElement.addEventListener('click', () => {
      this.launchApp(app.id);
    });
    
    // Add long press for context menu
    let longPressTimer: number | null = null;
    
    appElement.addEventListener('touchstart', () => {
      longPressTimer = window.setTimeout(() => {
        this.showAppContextMenu(app, appElement);
      }, 700);
    });
    
    appElement.addEventListener('touchend', () => {
      if (longPressTimer) {
        clearTimeout(longPressTimer);
        longPressTimer = null;
      }
    });
    
    appElement.addEventListener('touchmove', () => {
      if (longPressTimer) {
        clearTimeout(longPressTimer);
        longPressTimer = null;
      }
    });
    
    return appElement;
  }

  /**
   * Show context menu for an app
   */
  private showAppContextMenu(app: any, element: HTMLElement): void {
    // Create context menu
    const contextMenu = document.createElement('div');
    contextMenu.className = 'mobile-context-menu';
    
    // Position menu near the element
    const rect = element.getBoundingClientRect();
    contextMenu.style.left = `${rect.left}px`;
    contextMenu.style.top = `${rect.bottom + 10}px`;
    
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
      this.launchApp(app.id);
      document.body.removeChild(contextMenu);
    });
    contextMenu.appendChild(launchItem);
    
    // Pin/Unpin option
    const isPinned = this.pinnedApps.includes(app.id);
    const pinItem = document.createElement('div');
    pinItem.className = 'context-menu-item';
    pinItem.textContent = isPinned ? 'Unpin from Featured' : 'Pin to Featured';
    pinItem.addEventListener('click', () => {
      if (isPinned) {
        this.unpinApp(app.id);
      } else {
        this.pinApp(app.id);
      }
      document.body.removeChild(contextMenu);
    });
    contextMenu.appendChild(pinItem);
    
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
   * Handle search input
   */
  private handleSearch(query: string): void {
    if (!this.searchResults) return;
    
    // If empty query, hide search results and show regular content
    if (!query.trim()) {
      this.searchResults.style.display = 'none';
      if (this.featuredApps) this.featuredApps.style.display = 'block';
      if (this.recentApps) this.recentApps.style.display = 'block';
      if (this.allApps) this.allApps.style.display = 'block';
      return;
    }
    
    // Show search results and hide regular content
    this.searchResults.style.display = 'block';
    if (this.featuredApps) this.featuredApps.style.display = 'none';
    if (this.recentApps) this.recentApps.style.display = 'none';
    if (this.allApps) this.allApps.style.display = 'none';
    
    // Clear previous results
    this.searchResults.innerHTML = '<h2>Search Results</h2><div class="app-grid search-grid"></div>';
    
    // Get search grid
    const searchGrid = this.searchResults.querySelector('.search-grid');
    if (!searchGrid) return;
    
    // Get all apps
    const allApps = this.appManager.getInstalledApps();
    
    // Filter apps by query
    const filteredApps = allApps.filter(app => {
      return app.name.toLowerCase().includes(query.toLowerCase());
    });
    
    // If no results
    if (filteredApps.length === 0) {
      const noResults = document.createElement('div');
      noResults.className = 'no-results';
      noResults.textContent = `No apps found matching "${query}"`;
      this.searchResults.appendChild(noResults);
      return;
    }
    
    // Add filtered apps to search grid
    filteredApps.forEach(app => {
      const appElement = this.createAppElement(app);
      searchGrid.appendChild(appElement);
    });
  }

  /**
   * Pin an app to featured section
   */
  private pinApp(appId: string): void {
    // Check if not already pinned
    if (!this.pinnedApps.includes(appId)) {
      this.pinnedApps.push(appId);
      
      // In a real implementation, save to user settings
      
      // Update UI
      this.updateFeaturedApps();
    }
  }

  /**
   * Unpin an app from featured section
   */
  private unpinApp(appId: string): void {
    // Remove from pinned apps
    const index = this.pinnedApps.indexOf(appId);
    if (index !== -1) {
      this.pinnedApps.splice(index, 1);
      
      // In a real implementation, save to user settings
      
      // Update UI
      this.updateFeaturedApps();
    }
  }

  /**
   * Launch an app
   */
  private launchApp(appId: string): void {
    this.appManager.launchApp(appId);
    this.hide();
  }

  /**
   * Show the start menu
   */
  public show(): void {
    if (!this.element) return;
    
    // Update app grids first
    this.updateAppGrids();
    
    // Show menu with animation
    this.element.classList.add('visible');
    this.isVisible = true;
    
    // Focus the search input
    setTimeout(() => {
      const searchInput = this.element?.querySelector('.search-input') as HTMLInputElement;
      if (searchInput) {
        searchInput.focus();
      }
    }, 300);
  }

  /**
   * Hide the start menu
   */
  public hide(): void {
    if (!this.element) return;
    
    this.element.classList.remove('visible');
    this.isVisible = false;
    
    // Clear search
    const searchInput = this.element.querySelector('.search-input') as HTMLInputElement;
    if (searchInput) {
      searchInput.value = '';
      this.handleSearch('');
    }
  }

  /**
   * Toggle the visibility of the start menu
   */
  public toggle(): void {
    if (this.isVisible) {
      this.hide();
    } else {
      this.show();
    }
  }

  /**
   * Set up gesture for closing the start menu
   */
  private setupCloseGesture(): void {
    if (!this.element) return;
    
    let startY = 0;
    let currentY = 0;
    
    // Handle for pull down gesture
    const pullHandle = this.element.querySelector('.mobile-start-pull-handle');
    if (pullHandle) {
      pullHandle.addEventListener('touchstart', (e) => {
        startY = e.touches[0].clientY;
        currentY = startY;
        this.element!.style.transition = '';
      });
      
      pullHandle.addEventListener('touchmove', (e) => {
        currentY = e.touches[0].clientY;
        const deltaY = currentY - startY;
        
        if (deltaY > 0) {
          // Allow pulling down
          this.element!.style.transform = `translateY(${deltaY}px)`;
        }
      });
      
      pullHandle.addEventListener('touchend', () => {
        const deltaY = currentY - startY;
        
        if (deltaY > 70) {
          // Pull threshold reached, close menu
          this.hide();
        }
        
        // Reset transform
        this.element!.style.transition = 'transform 0.3s ease-out';
        this.element!.style.transform = '';
      });
    }
    
    // Close on backdrop click
    this.element.addEventListener('click', (e) => {
      if (e.target === this.element) {
        this.hide();
      }
    });
  }
}
