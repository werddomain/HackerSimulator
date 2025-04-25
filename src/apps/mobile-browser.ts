/**
 * Mobile Browser App for HackerSimulator
 * Provides a touch-friendly web browsing experience for mobile devices
 */

import { OS } from '../core/os';
import { BrowserApp } from './browser';
import { platformDetector, PlatformType } from '../core/platform-detector';
import { MobileInputHandler } from '../core/mobile-input-handler';
import { TouchContextMenu, ContextMenuItem } from '../core/touch-context-menu';
import { GestureType, SwipeDirection } from '../core/gesture-detector';

/**
 * Mobile-optimized Browser Application
 * Extends the base BrowserApp with mobile-specific UI and touch interactions
 */
export class MobileBrowserApp extends BrowserApp {
  // Mobile-specific properties
  private isMobile: boolean = false;
  private contentElement: HTMLElement | null = null;
  private mobileInputHandler: MobileInputHandler;
  private tabsElement: HTMLElement | null = null;
  private tabs: Array<{ url: string, title: string, id: string }> = [];
  private activeTabId: string = '';
  private isAddressBarFocused: boolean = false;
  private isFullscreen: boolean = false;
  private addressBar: HTMLElement | null = null;
  private bottomNavBar: HTMLElement | null = null;
  private isToolbarsVisible: boolean = true;
  private lastScrollY: number = 0;
  private scrollThreshold: number = 20; // pixels to scroll before hiding/showing toolbars
  private isMenuOpen: boolean = false;
  private touchStartY: number = 0;
  private refreshDistance: number = 60; // pixels to pull for refresh
  private isRefreshing: boolean = false;
  private lastUrl: string = '';

  constructor(os: OS) {
    super(os);
    this.mobileInputHandler = MobileInputHandler.getInstance();
    this.isMobile = platformDetector.getPlatformType() === PlatformType.Mobile;
  }

  /**
   * Initialize the application
   * Override to use mobile UI when on mobile devices
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    if (this.isMobile) {
      // Use mobile-specific rendering and interaction
      this.initMobileUI();
    } else {
      // Fall back to desktop version
      super.initApplication();
    }
  }

  /**
   * Initialize mobile-specific UI
   */
  private initMobileUI(): void {
    if (!this.container) return;
    
    // Render mobile UI
    this.renderMobileUI();
    
    // Create initial tab
    this.createNewTab(this.getCurrentUrl() || 'https://hackersearch.net');
    
    // Setup gesture detection
    this.setupSwipeNavigation();
    
    // Setup scroll handling for address bar auto-hide
    this.setupScrollHandling();
    
    // Setup pull-to-refresh
    this.setupPullToRefresh();
  }

  /**
   * Render the mobile-optimized browser UI
   */
  private renderMobileUI(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="mobile-browser-app">
        <div class="mobile-browser-address-bar">
          <div class="navigation-buttons">
            <button class="nav-btn back-btn" aria-label="Back">
              <ion-icon name="arrow-back-outline"></ion-icon>
            </button>
            <button class="nav-btn forward-btn" aria-label="Forward">
              <ion-icon name="arrow-forward-outline"></ion-icon>
            </button>
          </div>
          <div class="url-input-container">
            <div class="url-input-wrapper">
              <ion-icon name="lock-closed-outline" class="security-icon"></ion-icon>
              <input type="text" class="url-input" placeholder="Search or enter address">
              <button class="refresh-btn" aria-label="Refresh">
                <ion-icon name="refresh-outline"></ion-icon>
              </button>
            </div>
          </div>
          <button class="tabs-btn" aria-label="Tabs">
            <span class="tabs-count">1</span>
          </button>
        </div>
        
        <div class="mobile-browser-content-container">
          <div class="pull-to-refresh">
            <div class="refresh-indicator">
              <ion-icon name="refresh-outline"></ion-icon>
            </div>
            <div class="refresh-text">Pull to refresh</div>
          </div>
          <div class="mobile-browser-content">
            <div class="loading">Loading...</div>
          </div>
        </div>

        <div class="mobile-browser-bottom-bar">
          <button class="bottom-nav-btn home-btn" aria-label="Home">
            <ion-icon name="home-outline"></ion-icon>
          </button>
          <button class="bottom-nav-btn bookmarks-btn" aria-label="Bookmarks">
            <ion-icon name="bookmark-outline"></ion-icon>
          </button>
          <button class="bottom-nav-btn share-btn" aria-label="Share">
            <ion-icon name="share-outline"></ion-icon>
          </button>
          <button class="bottom-nav-btn menu-btn" aria-label="Menu">
            <ion-icon name="menu-outline"></ion-icon>
          </button>
        </div>
        
        <div class="tabs-container" style="display: none;">
          <div class="tabs-header">
            <h3>Tabs</h3>
            <button class="close-tabs-btn">
              <ion-icon name="close-outline"></ion-icon>
            </button>
          </div>
          <div class="tabs-list"></div>
          <button class="new-tab-btn">
            <ion-icon name="add-outline"></ion-icon>
            <span>New Tab</span>
          </button>
        </div>
        
        <div class="bookmarks-container" style="display: none;">
          <div class="bookmarks-header">
            <h3>Bookmarks</h3>
            <button class="close-bookmarks-btn">
              <ion-icon name="close-outline"></ion-icon>
            </button>
          </div>
          <div class="bookmarks-list"></div>
        </div>
      </div>
    `;
    
    // Store references to important elements
    this.contentElement = this.container.querySelector('.mobile-browser-content');
    this.tabsElement = this.container.querySelector('.tabs-list');
    this.addressBar = this.container.querySelector('.mobile-browser-address-bar');
    this.bottomNavBar = this.container.querySelector('.mobile-browser-bottom-bar');
    
    // Set up event listeners
    this.setupEventListeners();
  }

  /**
   * Set up event listeners for mobile browser UI
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // Back button
    const backBtn = this.container.querySelector('.back-btn');
    if (backBtn) {
      backBtn.addEventListener('click', () => {
        this.navigateBack();
      });
    }
    
    // Forward button
    const forwardBtn = this.container.querySelector('.forward-btn');
    if (forwardBtn) {
      forwardBtn.addEventListener('click', () => {
        this.navigateForward();
      });
    }
    
    // Address input
    const urlInput = this.container.querySelector('.url-input') as HTMLInputElement;
    if (urlInput) {
      urlInput.addEventListener('focus', () => {
        this.isAddressBarFocused = true;
        urlInput.select();
      });
      
      urlInput.addEventListener('blur', () => {
        this.isAddressBarFocused = false;
      });
      
      urlInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          this.navigate(urlInput.value);
          urlInput.blur();
        }
      });
    }
    
    // Refresh button
    const refreshBtn = this.container.querySelector('.refresh-btn');
    if (refreshBtn) {
      refreshBtn.addEventListener('click', () => {
        this.refreshCurrentPage();
      });
    }
    
    // Tabs button
    const tabsBtn = this.container.querySelector('.tabs-btn');
    if (tabsBtn) {
      tabsBtn.addEventListener('click', () => {
        this.toggleTabsView();
      });
    }
    
    // Close tabs button
    const closeTabsBtn = this.container.querySelector('.close-tabs-btn');
    if (closeTabsBtn) {
      closeTabsBtn.addEventListener('click', () => {
        this.toggleTabsView();
      });
    }
    
    // New tab button
    const newTabBtn = this.container.querySelector('.new-tab-btn');
    if (newTabBtn) {
      newTabBtn.addEventListener('click', () => {
        this.createNewTab('https://hackersearch.net');
        this.toggleTabsView();
      });
    }
    
    // Bottom nav buttons
    const homeBtn = this.container.querySelector('.home-btn');
    if (homeBtn) {
      homeBtn.addEventListener('click', () => {
        this.navigate('https://hackersearch.net');
      });
    }
    
    const bookmarksBtn = this.container.querySelector('.bookmarks-btn');
    if (bookmarksBtn) {
      bookmarksBtn.addEventListener('click', () => {
        this.toggleBookmarksView();
      });
    }
    
    const closeBookmarksBtn = this.container.querySelector('.close-bookmarks-btn');
    if (closeBookmarksBtn) {
      closeBookmarksBtn.addEventListener('click', () => {
        this.toggleBookmarksView();
      });
    }
    
    const shareBtn = this.container.querySelector('.share-btn');
    if (shareBtn) {
      shareBtn.addEventListener('click', () => {
        this.showShareMenu();
      });
    }
    
    const menuBtn = this.container.querySelector('.menu-btn');
    if (menuBtn) {
      menuBtn.addEventListener('click', (e) => {
        this.showMainMenu(e as MouseEvent);
      });
    }
  }

  /**
   * Setup swipe navigation for browser
   */
  private setupSwipeNavigation(): void {
    if (!this.container) return;
    
    const contentContainer = this.container.querySelector('.mobile-browser-content-container');
    if (!contentContainer) return;
    
    // Add swipe handlers for navigation
    this.mobileInputHandler.addGestureDetection(contentContainer, {
      onGesture: (gestureEvent) => {
        // Only handle horizontal swipes that start from the edge
        if (gestureEvent.type === GestureType.Swipe) {
          const edgeThreshold = 50; // pixels from edge to consider as edge swipe
          
          if (gestureEvent.direction === SwipeDirection.Right && 
              gestureEvent.startX < edgeThreshold) {
            // Swipe right from left edge to go back
            this.navigateBack();
            return true;
          } else if (gestureEvent.direction === SwipeDirection.Left &&
                    gestureEvent.startX > window.innerWidth - edgeThreshold) {
            // Swipe left from right edge to go forward
            this.navigateForward();
            return true;
          }
        }
        
        return false;
      }
    });
  }

  /**
   * Setup scroll handling to auto-hide address bar
   */
  private setupScrollHandling(): void {
    if (!this.container) return;
    
    const contentContainer = this.container.querySelector('.mobile-browser-content-container');
    if (!contentContainer) return;
    
    contentContainer.addEventListener('scroll', (e) => {
      // Avoid hiding toolbars when address bar is focused
      if (this.isAddressBarFocused) return;
      
      // Get current scroll position
      const currentScrollY = contentContainer.scrollTop;
      
      // Check if scrolled beyond threshold
      if (Math.abs(currentScrollY - this.lastScrollY) > this.scrollThreshold) {
        // Scrolling down - hide toolbars
        if (currentScrollY > this.lastScrollY && currentScrollY > 50) {
          this.hideToolbars();
        } 
        // Scrolling up - show toolbars
        else if (currentScrollY < this.lastScrollY) {
          this.showToolbars();
        }
        
        // Update last scroll position
        this.lastScrollY = currentScrollY;
      }
    });
  }

  /**
   * Setup pull-to-refresh functionality
   */
  private setupPullToRefresh(): void {
    if (!this.container) return;
    
    const contentContainer = this.container.querySelector('.mobile-browser-content-container');
    if (!contentContainer) return;
    
    // Track touch start position
    contentContainer.addEventListener('touchstart', (e) => {
      if (contentContainer.scrollTop === 0) {
        this.touchStartY = e.touches[0].clientY;
      }
    });
    
    // Track touch move for pull distance
    contentContainer.addEventListener('touchmove', (e) => {
      if (contentContainer.scrollTop === 0 && !this.isRefreshing) {
        const currentY = e.touches[0].clientY;
        const pullDistance = currentY - this.touchStartY;
        
        if (pullDistance > 0) {
          // Prevent default scrolling behavior
          e.preventDefault();
          
          // Update refresh indicator
          const refreshIndicator = this.container!.querySelector('.pull-to-refresh');
          if (refreshIndicator) {
            refreshIndicator.style.transform = `translateY(${Math.min(pullDistance, this.refreshDistance * 1.5)}px)`;
            
            // Update text when pulled enough
            const refreshText = refreshIndicator.querySelector('.refresh-text');
            if (refreshText) {
              refreshText.textContent = pullDistance > this.refreshDistance 
                ? 'Release to refresh' 
                : 'Pull to refresh';
            }
            
            // Rotate the icon based on pull distance
            const icon = refreshIndicator.querySelector('ion-icon');
            if (icon) {
              icon.style.transform = `rotate(${pullDistance * 2}deg)`;
            }
          }
        }
      }
    });
    
    // Handle touch end to trigger refresh
    contentContainer.addEventListener('touchend', (e) => {
      if (contentContainer.scrollTop === 0 && !this.isRefreshing) {
        const refreshIndicator = this.container!.querySelector('.pull-to-refresh');
        
        if (refreshIndicator) {
          const transform = refreshIndicator.style.transform;
          const pullDistance = parseInt(transform.replace(/[^0-9]/g, '')) || 0;
          
          if (pullDistance > this.refreshDistance) {
            // Trigger refresh
            this.isRefreshing = true;
            
            // Update UI to show refreshing state
            refreshIndicator.classList.add('refreshing');
            const refreshText = refreshIndicator.querySelector('.refresh-text');
            if (refreshText) {
              refreshText.textContent = 'Refreshing...';
            }
            
            // Animate the icon
            const icon = refreshIndicator.querySelector('ion-icon');
            if (icon) {
              icon.classList.add('rotating');
            }
            
            // Actually refresh the page
            this.refreshCurrentPage().then(() => {
              // Reset refresh state
              setTimeout(() => {
                this.isRefreshing = false;
                refreshIndicator.classList.remove('refreshing');
                refreshIndicator.style.transform = 'translateY(0)';
                
                if (icon) {
                  icon.classList.remove('rotating');
                  icon.style.transform = '';
                }
              }, 500);
            });
          } else {
            // Not pulled far enough, reset
            refreshIndicator.style.transform = 'translateY(0)';
          }
        }
      }
    });
  }

  /**
   * Create a new browser tab
   */
  private createNewTab(url: string): void {
    // Generate a unique ID for the tab
    const tabId = 'tab-' + Date.now();
    
    // Add to tabs array
    this.tabs.push({
      id: tabId,
      url: url,
      title: 'Loading...'
    });
    
    // Set as active tab
    this.activeTabId = tabId;
    
    // Update tabs count
    this.updateTabsCount();
    
    // Update tabs list UI
    this.updateTabsList();
    
    // Navigate to the URL
    this.navigate(url);
  }

  /**
   * Update the tabs count display
   */
  private updateTabsCount(): void {
    if (!this.container) return;
    
    const tabsCount = this.container.querySelector('.tabs-count');
    if (tabsCount) {
      tabsCount.textContent = this.tabs.length.toString();
    }
  }

  /**
   * Update the tabs list in the UI
   */
  private updateTabsList(): void {
    if (!this.tabsElement) return;
    
    // Clear existing tabs
    this.tabsElement.innerHTML = '';
    
    // Add each tab
    this.tabs.forEach(tab => {
      const tabElement = document.createElement('div');
      tabElement.className = `tab-item ${tab.id === this.activeTabId ? 'active' : ''}`;
      tabElement.setAttribute('data-tab-id', tab.id);
      
      // Tab preview (simplified)
      const favicon = this.getFaviconForUrl(tab.url);
      
      tabElement.innerHTML = `
        <div class="tab-preview">
          <img src="${favicon}" class="tab-favicon" alt="">
          <div class="tab-info">
            <div class="tab-title">${tab.title}</div>
            <div class="tab-url">${tab.url}</div>
          </div>
          <button class="tab-close-btn">
            <ion-icon name="close-outline"></ion-icon>
          </button>
        </div>
      `;
      
      // Add event listeners
      tabElement.addEventListener('click', (e) => {
        // Check if close button was clicked
        if ((e.target as Element).closest('.tab-close-btn')) {
          this.closeTab(tab.id);
          e.stopPropagation();
        } else {
          // Switch to this tab
          this.switchToTab(tab.id);
        }
      });
      
      // Add to tabs list
      this.tabsElement!.appendChild(tabElement);
    });
  }

  /**
   * Switch to a specific tab
   */
  private switchToTab(tabId: string): void {
    // Find the tab
    const tab = this.tabs.find(t => t.id === tabId);
    if (!tab) return;
    
    // Set as active tab
    this.activeTabId = tabId;
    
    // Update tabs list UI
    this.updateTabsList();
    
    // Navigate to the tab's URL
    this.navigate(tab.url);
    
    // Hide tabs view
    this.toggleTabsView(false);
  }

  /**
   * Close a specific tab
   */
  private closeTab(tabId: string): void {
    // Check if it's the only tab
    if (this.tabs.length <= 1) {
      // Create a new tab before closing this one
      this.createNewTab('https://hackersearch.net');
    }
    
    // Check if closing the active tab
    const isActiveTab = tabId === this.activeTabId;
    
    // Find the tab index
    const tabIndex = this.tabs.findIndex(t => t.id === tabId);
    if (tabIndex === -1) return;
    
    // Remove the tab
    this.tabs.splice(tabIndex, 1);
    
    // If closing active tab, switch to another tab
    if (isActiveTab) {
      // Switch to the next tab or the previous if it was the last
      const newTabIndex = Math.min(tabIndex, this.tabs.length - 1);
      this.switchToTab(this.tabs[newTabIndex].id);
    } else {
      // Just update the UI
      this.updateTabsList();
    }
    
    // Update tabs count
    this.updateTabsCount();
  }

  /**
   * Toggle the tabs view
   */
  private toggleTabsView(show?: boolean): void {
    if (!this.container) return;
    
    const tabsContainer = this.container.querySelector('.tabs-container');
    if (!tabsContainer) return;
    
    // If show is specified, set that state, otherwise toggle
    const newDisplay = show !== undefined 
      ? (show ? 'block' : 'none')
      : (tabsContainer.style.display === 'none' ? 'block' : 'none');
    
    tabsContainer.style.display = newDisplay;
    
    // Update tabs list if showing
    if (newDisplay === 'block') {
      this.updateTabsList();
    }
  }

  /**
   * Toggle the bookmarks view
   */
  private toggleBookmarksView(show?: boolean): void {
    if (!this.container) return;
    
    const bookmarksContainer = this.container.querySelector('.bookmarks-container');
    if (!bookmarksContainer) return;
    
    // If show is specified, set that state, otherwise toggle
    const newDisplay = show !== undefined 
      ? (show ? 'block' : 'none')
      : (bookmarksContainer.style.display === 'none' ? 'block' : 'none');
    
    bookmarksContainer.style.display = newDisplay;
    
    // Update bookmarks list if showing
    if (newDisplay === 'block') {
      this.updateBookmarksList();
    }
  }

  /**
   * Update the bookmarks list
   */
  private updateBookmarksList(): void {
    if (!this.container) return;
    
    const bookmarksList = this.container.querySelector('.bookmarks-list');
    if (!bookmarksList) return;
    
    // Clear existing bookmarks
    bookmarksList.innerHTML = '';
    
    // Add each bookmark
    this.getFavoriteUrls().forEach((url, name) => {
      const bookmarkElement = document.createElement('div');
      bookmarkElement.className = 'bookmark-item';
      
      const favicon = this.getFaviconForUrl(url);
      
      bookmarkElement.innerHTML = `
        <div class="bookmark-icon">
          <img src="${favicon}" alt="" class="bookmark-favicon">
        </div>
        <div class="bookmark-info">
          <div class="bookmark-name">${name}</div>
          <div class="bookmark-url">${url}</div>
        </div>
        <button class="bookmark-menu-btn">
          <ion-icon name="ellipsis-vertical-outline"></ion-icon>
        </button>
      `;
      
      // Add click event to navigate to bookmark
      bookmarkElement.addEventListener('click', (e) => {
        // Check if menu button was clicked
        if ((e.target as Element).closest('.bookmark-menu-btn')) {
          this.showBookmarkMenu(e as MouseEvent, name, url);
          e.stopPropagation();
        } else {
          // Navigate to bookmark
          this.navigate(url);
          this.toggleBookmarksView(false);
        }
      });
      
      // Add to bookmarks list
      bookmarksList.appendChild(bookmarkElement);
    });
    
    // Add "Add bookmark" button
    const addBookmarkElement = document.createElement('div');
    addBookmarkElement.className = 'bookmark-item add-bookmark';
    addBookmarkElement.innerHTML = `
      <div class="bookmark-icon">
        <ion-icon name="add-outline"></ion-icon>
      </div>
      <div class="bookmark-name">Add Bookmark</div>
    `;
    
    // Add click event to add current page as bookmark
    addBookmarkElement.addEventListener('click', () => {
      this.addCurrentPageToBookmarks();
    });
    
    // Add to bookmarks list
    bookmarksList.appendChild(addBookmarkElement);
  }

  /**
   * Show bookmark context menu
   */
  private showBookmarkMenu(event: MouseEvent, name: string, url: string): void {
    const menuItems: ContextMenuItem[] = [
      {
        id: 'open',
        label: 'Open',
        icon: 'open-outline',
        action: () => {
          this.navigate(url);
          this.toggleBookmarksView(false);
        }
      },
      {
        id: 'new-tab',
        label: 'Open in New Tab',
        icon: 'add-outline',
        action: () => {
          this.createNewTab(url);
          this.toggleBookmarksView(false);
        }
      },
      { separator: true },
      {
        id: 'edit',
        label: 'Edit',
        icon: 'create-outline',
        action: () => {
          this.editBookmark(name, url);
        }
      },
      {
        id: 'delete',
        label: 'Delete',
        icon: 'trash-outline',
        action: () => {
          this.removeBookmark(name);
        }
      }
    ];
    
    // Show the context menu
    this.mobileInputHandler.showContextMenu(
      event.clientX,
      event.clientY,
      menuItems
    );
  }

  /**
   * Add current page to bookmarks
   */
  private addCurrentPageToBookmarks(): void {
    // Find the active tab
    const activeTab = this.tabs.find(t => t.id === this.activeTabId);
    if (!activeTab) return;
    
    // Show prompt for bookmark name
    if (this.os.getWindowManager()) {
      this.os.getWindowManager().showPrompt(
        'Add Bookmark',
        'Enter a name for this bookmark:',
        activeTab.title,
        (name) => {
          if (name && name.trim()) {
            // Add to favorites
            this.addToFavorites(name.trim(), activeTab.url);
            
            // Update bookmarks list
            this.updateBookmarksList();
            
            // Show success notification
            this.os.getWindowManager()?.showNotification({
              title: 'Bookmark Added',
              message: `Added "${name.trim()}" to bookmarks.`,
              type: 'success'
            });
          }
        }
      );
    }
  }

  /**
   * Edit a bookmark
   */
  private editBookmark(name: string, url: string): void {
    // Show prompt for new bookmark name
    if (this.os.getWindowManager()) {
      this.os.getWindowManager().showPrompt(
        'Edit Bookmark',
        'Enter a new name for this bookmark:',
        name,
        (newName) => {
          if (newName && newName.trim() && newName !== name) {
            // Remove old bookmark
            this.removeBookmark(name);
            
            // Add with new name
            this.addToFavorites(newName.trim(), url);
            
            // Update bookmarks list
            this.updateBookmarksList();
          }
        }
      );
    }
  }

  /**
   * Hide toolbars (address bar and bottom bar)
   */
  private hideToolbars(): void {
    if (!this.addressBar || !this.bottomNavBar) return;
    
    // Only if not already hidden
    if (this.isToolbarsVisible) {
      this.addressBar.style.transform = 'translateY(-100%)';
      this.bottomNavBar.style.transform = 'translateY(100%)';
      this.isToolbarsVisible = false;
    }
  }

  /**
   * Show toolbars (address bar and bottom bar)
   */
  private showToolbars(): void {
    if (!this.addressBar || !this.bottomNavBar) return;
    
    // Only if not already visible
    if (!this.isToolbarsVisible) {
      this.addressBar.style.transform = 'translateY(0)';
      this.bottomNavBar.style.transform = 'translateY(0)';
      this.isToolbarsVisible = true;
    }
  }

  /**
   * Refresh the current page
   */
  private async refreshCurrentPage(): Promise<void> {
    // Find the active tab
    const activeTab = this.tabs.find(t => t.id === this.activeTabId);
    if (!activeTab) return;
    
    // Navigate to the same URL to refresh
    return new Promise<void>((resolve) => {
      // Wait a moment to show the refresh animation
      setTimeout(() => {
        this.navigate(activeTab.url);
        resolve();
      }, 500);
    });
  }

  /**
   * Show share menu
   */
  private showShareMenu(): void {
    // Find the active tab
    const activeTab = this.tabs.find(t => t.id === this.activeTabId);
    if (!activeTab) return;
    
    // Show sharing options in a modal
    if (this.os.getWindowManager()) {
      this.os.getWindowManager().showMessage(
        'Share Page',
        `
          <div style="text-align: center;">
            <p>Share this page:</p>
            <p style="word-break: break-all;">${activeTab.url}</p>
            <div style="display: flex; justify-content: center; gap: 16px; margin-top: 16px;">
              <button class="share-option" data-type="copy">
                <ion-icon name="copy-outline" style="font-size: 24px;"></ion-icon>
                <span>Copy Link</span>
              </button>
              <button class="share-option" data-type="email">
                <ion-icon name="mail-outline" style="font-size: 24px;"></ion-icon>
                <span>Email</span>
              </button>
              <button class="share-option" data-type="message">
                <ion-icon name="chatbubble-outline" style="font-size: 24px;"></ion-icon>
                <span>Message</span>
              </button>
            </div>
          </div>
        `,
        () => {},
        ['OK']
      );
      
      // Add click handlers for share options
      setTimeout(() => {
        const shareOptions = document.querySelectorAll('.share-option');
        shareOptions.forEach(option => {
          option.addEventListener('click', () => {
            const type = option.getAttribute('data-type');
            
            if (type === 'copy') {
              // Copy link to clipboard
              navigator.clipboard.writeText(activeTab.url)
                .then(() => {
                  this.os.getWindowManager()?.showNotification({
                    title: 'Link Copied',
                    message: 'Link copied to clipboard',
                    type: 'success'
                  });
                })
                .catch(() => {
                  this.os.getWindowManager()?.showNotification({
                    title: 'Copy Failed',
                    message: 'Failed to copy link to clipboard',
                    type: 'error'
                  });
                });
            } else {
              // Simulate other sharing options
              this.os.getWindowManager()?.showNotification({
                title: 'Sharing',
                message: `Sharing via ${type}...`,
                type: 'info'
              });
            }
            
            // Close the dialog
            document.querySelector('.modal-close-btn')?.dispatchEvent(new Event('click'));
          });
        });
      }, 100);
    }
  }

  /**
   * Show main menu
   */
  private showMainMenu(event: MouseEvent): void {
    const menuItems: ContextMenuItem[] = [
      {
        id: 'new-tab',
        label: 'New Tab',
        icon: 'add-outline',
        action: () => {
          this.createNewTab('https://hackersearch.net');
        }
      },
      {
        id: 'bookmarks',
        label: 'Bookmarks',
        icon: 'bookmark-outline',
        action: () => {
          this.toggleBookmarksView(true);
        }
      },
      {
        id: 'history',
        label: 'History',
        icon: 'time-outline',
        action: () => {
          this.showHistoryView();
        }
      },
      { separator: true },
      {
        id: 'find',
        label: 'Find in Page',
        icon: 'search-outline',
        action: () => {
          this.showFindInPage();
        }
      },
      {
        id: 'desktop-site',
        label: 'Request Desktop Site',
        icon: 'desktop-outline',
        action: () => {
          this.toggleDesktopSite();
        }
      },
      {
        id: 'fullscreen',
        label: this.isFullscreen ? 'Exit Fullscreen' : 'Fullscreen',
        icon: this.isFullscreen ? 'contract-outline' : 'expand-outline',
        action: () => {
          this.toggleFullscreen();
        }
      }
    ];
    
    // Add bookmark/unbookmark option
    const activeTab = this.tabs.find(t => t.id === this.activeTabId);
    if (activeTab) {
      // Check if current page is bookmarked
      const bookmarkName = this.getBookmarkNameForUrl(activeTab.url);
      if (bookmarkName) {
        menuItems.unshift({
          id: 'unbookmark',
          label: 'Remove Bookmark',
          icon: 'bookmark-remove-outline',
          action: () => {
            this.removeBookmark(bookmarkName);
            this.os.getWindowManager()?.showNotification({
              title: 'Bookmark Removed',
              message: `Removed "${bookmarkName}" from bookmarks.`,
              type: 'success'
            });
          }
        });
      } else {
        menuItems.unshift({
          id: 'bookmark',
          label: 'Add Bookmark',
          icon: 'bookmark-outline',
          action: () => {
            this.addCurrentPageToBookmarks();
          }
        });
      }
    }
    
    // Show the context menu
    this.mobileInputHandler.showContextMenu(
      event.clientX,
      event.clientY,
      menuItems
    );
  }

  /**
   * Show history view
   */
  private showHistoryView(): void {
    // Show history in a modal
    if (this.os.getWindowManager()) {
      // Get navigation history
      const history = this.getHistory();
      
      // Create HTML for history items
      let historyHtml = '';
      if (history.length === 0) {
        historyHtml = '<p style="text-align: center;">No history yet</p>';
      } else {
        historyHtml = '<div class="history-list">';
        history.forEach(url => {
          const favicon = this.getFaviconForUrl(url);
          historyHtml += `
            <div class="history-item" data-url="${url}">
              <img src="${favicon}" class="history-favicon" alt="">
              <div class="history-url">${url}</div>
            </div>
          `;
        });
        historyHtml += '</div>';
      }
      
      this.os.getWindowManager().showMessage(
        'History',
        historyHtml,
        () => {},
        ['Close']
      );
      
      // Add click handlers for history items
      setTimeout(() => {
        const historyItems = document.querySelectorAll('.history-item');
        historyItems.forEach(item => {
          item.addEventListener('click', () => {
            const url = item.getAttribute('data-url');
            if (url) {
              this.navigate(url);
              
              // Close the dialog
              document.querySelector('.modal-close-btn')?.dispatchEvent(new Event('click'));
            }
          });
        });
      }, 100);
    }
  }

  /**
   * Show find in page dialog
   */
  private showFindInPage(): void {
    // Show find dialog
    if (this.os.getWindowManager()) {
      this.os.getWindowManager().showPrompt(
        'Find in Page',
        'Enter text to find:',
        '',
        (searchText) => {
          if (searchText && searchText.trim()) {
            // Simulate find in page
            this.os.getWindowManager()?.showNotification({
              title: 'Find in Page',
              message: `Searching for "${searchText.trim()}"`,
              type: 'info'
            });
          }
        }
      );
    }
  }

  /**
   * Toggle desktop site mode
   */
  private toggleDesktopSite(): void {
    // Find the active tab
    const activeTab = this.tabs.find(t => t.id === this.activeTabId);
    if (!activeTab) return;
    
    // Simulate toggle between mobile and desktop
    this.os.getWindowManager()?.showNotification({
      title: 'Desktop Site',
      message: 'Requested desktop version of site',
      type: 'info'
    });
    
    // Refresh the page to simulate the change
    this.refreshCurrentPage();
  }

  /**
   * Toggle fullscreen mode
   */
  private toggleFullscreen(): void {
    this.isFullscreen = !this.isFullscreen;
    
    if (this.isFullscreen) {
      // Hide toolbars in fullscreen mode
      this.hideToolbars();
      
      // Show notification about how to exit
      this.os.getWindowManager()?.showNotification({
        title: 'Fullscreen Mode',
        message: 'Swipe down from top to exit fullscreen',
        type: 'info'
      });
    } else {
      // Show toolbars when exiting fullscreen
      this.showToolbars();
    }
  }

  /**
   * Get favicon for a URL
   */
  private getFaviconForUrl(url: string): string {
    // Extract domain from URL
    let domain = url.replace(/https?:\/\//, '').split('/')[0];
    
    // Return domain-specific favicon
    switch (domain) {
      case 'hackersearch.net':
        return 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0iIzRjYWY1MCIgZD0iTTkuNSwzLkE2LjUsNi41LDAsMCwxLDE2LDkuNUMxNiwxMiwxNCwxNC4xLDEwLjczLDE0LjQ4VjE5LjVIMTYuNVYyMUg5LjVBMS41LDEuNSwwLDAsMSw4LDE5LjVWMTQuNUM4LDE0LjUsOSwxNCw5LjUsMTNTMTAsMTEuNSwxMCwxMS41QTMuNSwzLjUsMCwwLDAsNi41LDhBMy41LDMuNSwwLDAsMCwzLDExLjVBMy41LDMuNSwwLDAsMCw2LjUsMTVIMVYxMy41SDYuNUMyLjUsMTMuNSwxLDExLjUsMSwxMS41QTguNSw4LjUsMCwwLDEsOS41LDNaIiAvPjwvc3ZnPg==';
      case 'hackmail.com':
        return 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0iI2YwNTkyZCIgZD0iTTIwLDhMOSwxNVYxMC41TDIwLDMuNVY4TS0wLDhMNC41LDExTDksMTRWMTguNUwyLDE0TDAsOFYzLjVMOSwxMFYxNC41TDIsOVY4SDJMLTAsOFoiIC8+PC9zdmc+';
      case 'cryptobank.com':
        return 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0iIzRBOTBFMiIgZD0iTTEyLDFMMjEsMjFIMFpNMTIsOEEyLDIsMCwwLDAsMTAsMTJIMTRDMTQsMTAuODksMTMuMSw4LDEyLDhaIiAvPjwvc3ZnPg==';
      case 'darknet.market':
        return 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0iIzVkNGQ4MCIgZD0iTTEyLDJMMTcsMTJMMTIsMjJMNywxMlpNMTIsMTdDMTMuMSwxNywxNCwxNi4xLDE0LDE1QzE0LDEzLjksMTMuMSwxMywxMiwxM0MxMC45LDEzLDEwLDEzLjksMTAsMTVDMTAsMTYuMSwxMC45LDE3LDEyLDE3WiIgLz48L3N2Zz4=';
      case 'hackerz.forum':
        return 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0iI2U5MTA2MiIgZD0iTTEwLDlWNUw0LDlMMTAsOE05LjEsMTdIMWw4LjI2LDQuNEM5LjEsMjAuMiw5LDE5LDkuMSwxN00xNywyMVY5TDE3LDZoLS4wMzJ2OUMxNSwxNSwxNC4xLDE2LjEsMTMsMTd2NA0KSDEzLjAzMmg0LjI0MkMxNy42LDIxLjEsMTcuMywyMS4zLDE3LDIxTTI0LDdINFY5SDI0VjdaIiAvPjwvc3ZnPg==';
      default:
        return 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0iIzYwN0Q4QiIgZD0iTTEyLDJBMTAsMTAsMCwwLDEsMjIsMTJBMTAsMTAsMCwwLDEsMTIsMjJBMTAsMTAsMCwwLDEsMiwxMkExMCwxMCwwLDAsMSwxMiwyWiIgLz48L3N2Zz4=';
    }
  }

  /**
   * Get bookmark name for a URL
   */
  private getBookmarkNameForUrl(url: string): string | null {
    for (const [name, bookmarkUrl] of this.getFavoriteUrls().entries()) {
      if (bookmarkUrl === url) {
        return name;
      }
    }
    return null;
  }

  /**
   * Override navigate to update tab information
   */
  public override navigate(url: string): void {
    // Call the parent class implementation
    super.navigate(url);
    
    // Update the active tab's URL
    const activeTab = this.tabs.find(t => t.id === this.activeTabId);
    if (activeTab) {
      activeTab.url = url;
      
      // Extract a title from the URL
      const domain = url.replace(/https?:\/\//, '').split('/')[0];
      activeTab.title = domain;
    }
    
    // Update the mobile URL input
    if (this.container) {
      const urlInput = this.container.querySelector('.url-input') as HTMLInputElement;
      if (urlInput) {
        urlInput.value = url;
      }
    }
  }
}
