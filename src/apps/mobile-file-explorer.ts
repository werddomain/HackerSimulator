/**
 * Mobile File Explorer App for HackerSimulator
 * Provides a touch-friendly interface to browse and manage files on mobile devices
 */

import { OS } from '../core/os';
import { FileExplorerApp } from './file-explorer';
import { FileEntryUtils } from '../core/file-entry-utils';
import { FileSystemEntry } from '../core/filesystem';
import { platformDetector, PlatformType } from '../core/platform-detector';
import { MobileInputHandler } from '../core/mobile-input-handler';
import { TouchContextMenu, ContextMenuItem } from '../core/touch-context-menu';
import { GestureType, SwipeDirection } from '../core/gesture-detector';

/**
 * Mobile-optimized File Explorer App
 * Extends the base FileExplorerApp with mobile-specific UI and touch interactions
 */
export class MobileFileExplorerApp extends FileExplorerApp {
  // Mobile-specific properties
  private isMobile: boolean = false;
  private contentElement: HTMLElement | null = null;
  private currentFolderName: string = '';
  private longPressTimer: number | null = null;
  private mobileInputHandler: MobileInputHandler;
  private breadcrumbsElement: HTMLElement | null = null;
  private actionBarElement: HTMLElement | null = null;
  private isMultiSelectMode: boolean = false;
  private floatingActionButton: HTMLElement | null = null;
  private lastTouchY: number = 0;
  private refreshDistance: number = 60; // pixels to pull for refresh
  private isRefreshing: boolean = false;
  // Keep track of current path for mobile implementation
  private mobilePath: string = '/home/user';
  // Track selected items for mobile implementation
  private mobileSelectedItems: string[] = [];

  constructor(os: OS) {
    super(os);
    this.mobileInputHandler = MobileInputHandler.getInstance();
    this.isMobile = platformDetector.getPlatformType() === PlatformType.MOBILE;
  }

  /**
   * Get the current directory path
   * @returns The current directory path
   */
  public getCurrentPath(): string {
    return this.mobilePath;
  }

  /**
   * Get the list of selected file/directory paths
   * @returns Array of selected item paths
   */
  public getSelectedItems(): string[] {
    return this.mobileSelectedItems;
  }

  /**
   * Add a path to the selection
   * @param path Path to add to selection
   */
  private addToSelection(path: string): void {
    if (!this.mobileSelectedItems.includes(path)) {
      this.mobileSelectedItems.push(path);
    }
  }

  /**
   * Remove a path from the selection
   * @param path Path to remove from selection
   */
  private removeFromSelection(path: string): void {
    const index = this.mobileSelectedItems.indexOf(path);
    if (index !== -1) {
      this.mobileSelectedItems.splice(index, 1);
    }
  }

  /**
   * Clear all selected items
   */
  private clearSelection(): void {
    this.mobileSelectedItems = [];
  }

  /**
   * Sets the current path in the mobile file explorer
   * This is used to keep the mobile path in sync
   * @param path The path to set as current
   */
  private setCurrentPath(path: string): void {
    this.mobilePath = path;
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
    // Initialize Ionic icons
    this.initializeIonicIcons();
    
    // Render mobile UI
    this.renderMobileUI();
    
    // Handle navigation based on command args or default path
    if (this.commandArgs.length > 0) {
      this.os.getFileSystem().stat(this.commandArgs[0])
        .then(stat => {
          if (FileEntryUtils.isDirectory(stat)) {
            // If it's a directory, navigate to it
            this.navigateTo(this.commandArgs[0]);
            // Add history entry for the path
            this.addToHistory(this.commandArgs[0]);
          } else {
            // Default path if the argument isn't a directory
            this.navigateTo('/home/user');
            this.addToHistory('/home/user');
          }
        })
        .catch(() => {
          // Default path if there's an error
          this.navigateTo('/home/user');
          this.addToHistory('/home/user');
        });
    } else {
      // No arguments provided, use default path
      this.navigateTo('/home/user');
      // Add history entry for initial path
      this.addToHistory('/home/user');
    }
    
    // Add support for swipe navigation
    this.setupSwipeNavigation();
    
    // Add pull-to-refresh functionality
    this.setupPullToRefresh();
  }

  /**
   * Render the mobile-optimized file explorer UI
   */
  private renderMobileUI(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="mobile-file-explorer-app">
        <div class="mobile-file-explorer-header">
          <div class="header-navigation">
            <button class="nav-btn back-btn">
              <ion-icon name="arrow-back-outline"></ion-icon>
            </button>
            <div class="breadcrumbs-container">
              <div class="breadcrumbs"></div>
            </div>
            <button class="nav-btn options-btn">
              <ion-icon name="ellipsis-vertical-outline"></ion-icon>
            </button>
          </div>
        </div>
        
        <div class="mobile-file-explorer-content">
          <div class="pull-to-refresh">
            <div class="refresh-indicator">
              <ion-icon name="refresh-outline"></ion-icon>
            </div>
            <div class="refresh-text">Pull to refresh</div>
          </div>
          <div class="mobile-file-list grid"></div>
        </div>
        
        <div class="mobile-file-explorer-action-bar">
          <button class="action-btn cancel-btn" style="display: none;">
            <ion-icon name="close-outline"></ion-icon>
            <span>Cancel</span>
          </button>
          <div class="selection-count" style="display: none;">0 selected</div>
          <div class="action-buttons" style="display: none;">
            <button class="action-btn copy-btn">
              <ion-icon name="copy-outline"></ion-icon>
            </button>
            <button class="action-btn cut-btn">
              <ion-icon name="cut-outline"></ion-icon>
            </button>
            <button class="action-btn delete-btn">
              <ion-icon name="trash-outline"></ion-icon>
            </button>
          </div>
        </div>
        
        <div class="floating-action-button">
          <ion-icon name="add-outline"></ion-icon>
        </div>
      </div>
    `;
    
    // Store references to important elements
    this.contentElement = this.container.querySelector('.mobile-file-list');
    this.breadcrumbsElement = this.container.querySelector('.breadcrumbs');
    this.actionBarElement = this.container.querySelector('.mobile-file-explorer-action-bar');
    this.floatingActionButton = this.container.querySelector('.floating-action-button');
    
    // Setup event listeners
    this.setupEventListeners();
  }

  /**
   * Setup event listeners for mobile UI elements
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
    
    // Options button
    const optionsBtn = this.container.querySelector('.options-btn');
    if (optionsBtn) {
      optionsBtn.addEventListener('click', (e) => {
        this.showOptionsMenu(e as MouseEvent);
      });
    }
    
    // Cancel multi-select mode
    const cancelBtn = this.container.querySelector('.cancel-btn');
    if (cancelBtn) {
      cancelBtn.addEventListener('click', () => {
        this.exitMultiSelectMode();
      });
    }
    
    // Action buttons
    const copyBtn = this.container.querySelector('.copy-btn');
    if (copyBtn) {
      copyBtn.addEventListener('click', () => {
        this.copySelectedItems();
      });
    }
    
    const cutBtn = this.container.querySelector('.cut-btn');
    if (cutBtn) {
      cutBtn.addEventListener('click', () => {
        this.cutSelectedItems();
      });
    }
    
    const deleteBtn = this.container.querySelector('.delete-btn');
    if (deleteBtn) {
      deleteBtn.addEventListener('click', () => {
        this.deleteSelectedItems();
      });
    }
    
    // Floating action button
    if (this.floatingActionButton) {
      this.floatingActionButton.addEventListener('click', (e) => {
        this.showCreateMenu(e as MouseEvent);
      });
    }
  }

  /**
   * Update breadcrumbs navigation based on current path
   */
  private updateBreadcrumbs(): void {
    if (!this.breadcrumbsElement) return;
    
    // Split path into components
    const pathParts = this.getCurrentPath().split('/').filter(part => part);
    
    // Create simplified breadcrumbs for mobile
    if (pathParts.length === 0) {
      // Root directory
      this.breadcrumbsElement.innerHTML = `
        <span class="breadcrumb-item current">Root</span>
      `;
      this.currentFolderName = 'Root';
    } else {
      // Show current folder name with parent indicator
      this.currentFolderName = pathParts[pathParts.length - 1];
      
      if (pathParts.length === 1) {
        // Top-level directory
        this.breadcrumbsElement.innerHTML = `
          <span class="breadcrumb-item current">${this.currentFolderName}</span>
        `;
      } else {
        // Nested directory, show parent and current
        const parentName = pathParts[pathParts.length - 2] || 'Root';
        this.breadcrumbsElement.innerHTML = `
          <span class="breadcrumb-item parent">${parentName}</span>
          <span class="breadcrumb-separator">/</span>
          <span class="breadcrumb-item current">${this.currentFolderName}</span>
        `;
      }
    }
  }

  /**
   * Setup swipe navigation for browsing folders
   */
  private setupSwipeNavigation(): void {
    if (!this.container) return;
    
    // Add swipe handlers for navigation
    this.mobileInputHandler.enableSwipeNavigation(this.container, {
      onSwipeRight: () => {
        // Swipe right to go back (like browser back)
        this.navigateBack();
      },
      onSwipeLeft: () => {
        // Swipe left to go forward (like browser forward)
        this.navigateForward();
      }
    });
  }

  /**
   * Setup pull-to-refresh functionality
   */
  private setupPullToRefresh(): void {
    if (!this.container) return;
    
    const contentContainer = this.container.querySelector('.mobile-file-explorer-content');
    if (!contentContainer) return;
    
    // Track touch start position
    contentContainer.addEventListener('touchstart', (e) => {
      if (this.contentElement && this.contentElement.scrollTop === 0) {
        this.lastTouchY = e.touches[0].clientY;
      }
    });
    
    // Track touch move for pull distance
    contentContainer.addEventListener('touchmove', (e) => {
      if (this.contentElement && this.contentElement.scrollTop === 0 && !this.isRefreshing) {
        const currentY = e.touches[0].clientY;
        const pullDistance = currentY - this.lastTouchY;
        
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
      if (this.contentElement && this.contentElement.scrollTop === 0 && !this.isRefreshing) {
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
            
            // Actually refresh the contents
            this.refreshCurrentFolder().then(() => {
              // Reset refresh state
              this.isRefreshing = false;
              refreshIndicator.classList.remove('refreshing');
              refreshIndicator.style.transform = 'translateY(0)';
              
              if (icon) {
                icon.classList.remove('rotating');
                icon.style.transform = '';
              }
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
   * Refresh the current folder contents
   */
  private async refreshCurrentFolder(): Promise<void> {
    // Wait a moment to show the refresh animation
    await new Promise(resolve => setTimeout(resolve, 500));
    
    // Refresh the current path
    return this.loadFolder(this.getCurrentPath());
  }

  /**
   * Override loadFolder to use mobile-optimized rendering
   */
  protected async loadFolder(path: string): Promise<void> {
    // Update current path
    this.setCurrentPath(path);
    
    // Update breadcrumbs
    this.updateBreadcrumbs();
    
    if (!this.contentElement) return;
    
    // Show loading indicator
    this.contentElement.innerHTML = `
      <div class="mobile-loading-indicator">
        <ion-icon name="refresh-outline" class="rotating"></ion-icon>
        <span>Loading...</span>
      </div>
    `;
    
    try {
      // Get files and directories in the current path
      const entries = await this.os.getFileSystem().readdir(path);
      
      // Filter hidden files unless showing hidden is enabled
      let filteredEntries = entries;
      if (!this.isShowHidden()) {
        filteredEntries = entries.filter(entry => !entry.name.startsWith('.'));
      }
      
      // Sort entries: directories first, then files
      filteredEntries.sort((a, b) => {
        const aIsDir = FileEntryUtils.isDirectory(a);
        const bIsDir = FileEntryUtils.isDirectory(b);
        
        if (aIsDir && !bIsDir) return -1;
        if (!aIsDir && bIsDir) return 1;
        
        return a.name.localeCompare(b.name);
      });
      
      // Render the items
      this.renderFileItems(filteredEntries);
      
      // Update status in action bar
      const countText = `${filteredEntries.length} item${filteredEntries.length !== 1 ? 's' : ''}`;
      const statusInfo = this.container?.querySelector('.selection-count');
      if (statusInfo) {
        statusInfo.textContent = this.getSelectedItems().length > 0 
          ? `${this.getSelectedItems().length} selected` 
          : '';
      }
      
    } catch (error) {
      // Handle errors
      this.contentElement.innerHTML = `
        <div class="mobile-error-message">
          <ion-icon name="alert-circle-outline"></ion-icon>
          <span>Error loading folder: ${error}</span>
        </div>
      `;
    }
  }

  /**
   * Render file items in mobile-optimized grid
   */
  private renderFileItems(entries: FileSystemEntry[]): void {
    if (!this.contentElement) return;
    
    if (entries.length === 0) {
      // Empty folder
      this.contentElement.innerHTML = `
        <div class="mobile-empty-folder">
          <ion-icon name="folder-open-outline"></ion-icon>
          <span>This folder is empty</span>
        </div>
      `;
      return;
    }
    
    // Clear existing content
    this.contentElement.innerHTML = '';
    
    // Create grid container
    const gridContainer = document.createElement('div');
    gridContainer.className = 'mobile-file-grid';
    
    // Add each file/folder as an item
    entries.forEach(entry => {
      const isDirectory = FileEntryUtils.isDirectory(entry);
      const itemPath = `${this.getCurrentPath()}/${entry.name}`.replace(/\/+/g, '/');
      
      // Create item element
      const itemElement = document.createElement('div');
      itemElement.className = `mobile-file-item ${isDirectory ? 'directory' : 'file'}`;
      itemElement.setAttribute('data-path', itemPath);
      itemElement.setAttribute('data-name', entry.name);
      
      // Check if this item is selected
      if (this.getSelectedItems().includes(itemPath)) {
        itemElement.classList.add('selected');
      }
      
      // Determine icon based on file type
      let iconName = 'document-outline';
      if (isDirectory) {
        iconName = 'folder-outline';
      } else {
        // Get extension
        const extension = entry.name.split('.').pop()?.toLowerCase() || '';
        
        // Assign icon based on file type
        switch (extension) {
          case 'jpg':
          case 'jpeg':
          case 'png':
          case 'gif':
          case 'svg':
            iconName = 'image-outline';
            break;
          case 'mp3':
          case 'wav':
          case 'ogg':
            iconName = 'musical-note-outline';
            break;
          case 'mp4':
          case 'avi':
          case 'mov':
            iconName = 'film-outline';
            break;
          case 'js':
            iconName = 'logo-javascript';
            break;
          case 'html':
            iconName = 'logo-html5';
            break;
          case 'css':
            iconName = 'logo-css3';
            break;
          case 'ts':
          case 'py':
          case 'java':
          case 'c':
          case 'cpp':
            iconName = 'code-outline';
            break;
          case 'txt':
          case 'md':
          case 'json':
            iconName = 'document-text-outline';
            break;
          case 'zip':
          case 'tar':
          case 'gz':
          case 'rar':
            iconName = 'archive-outline';
            break;
          default:
            iconName = 'document-outline';
        }
      }
      
      // Create item content
      itemElement.innerHTML = `
        <div class="item-icon${this.isMultiSelectMode ? ' with-checkbox' : ''}">
          ${this.isMultiSelectMode ? '<div class="item-checkbox"></div>' : ''}
          <ion-icon name="${iconName}"></ion-icon>
        </div>
        <div class="item-name">${entry.name}</div>
      `;
      
      // Add tap handler
      itemElement.addEventListener('click', (e) => {
        // In multi-select mode, tap toggles selection
        if (this.isMultiSelectMode) {
          this.toggleItemSelection(itemPath);
          this.updateItemSelectionUI(itemElement, this.getSelectedItems().includes(itemPath));
          return;
        }
        
        // Normal mode: navigate into directories, open files
        if (isDirectory) {
          this.navigateTo(itemPath);
          this.addToHistory(itemPath);
        } else {
          this.openFile(entry, itemPath);
        }
      });
      
      // Add long-press handler for context menu
      this.mobileInputHandler.addLongPressMenu(
        itemElement,
        () => this.getItemContextMenu(entry, itemPath),
        { position: 'bottom' }
      );
      
      // Add to grid
      gridContainer.appendChild(itemElement);
    });
    
    // Add grid to content area
    this.contentElement.appendChild(gridContainer);
  }

  /**
   * Show options menu
   */
  private showOptionsMenu(event: MouseEvent): void {
    const items: ContextMenuItem[] = [
      {
        id: 'view',
        label: 'View Options',
        icon: 'eye-outline',
        submenu: [
          {
            id: 'toggle-hidden',
            label: this.isShowHidden() ? 'Hide Hidden Files' : 'Show Hidden Files',
            action: () => {
              this.toggleShowHidden();
              this.loadFolder(this.getCurrentPath());
            }
          },
          {
            id: 'toggle-view',
            label: this.getViewMode() === 'grid' ? 'List View' : 'Grid View',
            action: () => {
              this.toggleViewMode();
              this.loadFolder(this.getCurrentPath());
            }
          }
        ]
      },
      {
        id: 'navigate',
        label: 'Navigate',
        icon: 'compass-outline',
        submenu: [
          {
            id: 'home',
            label: 'Home',
            action: () => {
              this.navigateTo('/home/user');
              this.addToHistory('/home/user');
            }
          },
          {
            id: 'desktop',
            label: 'Desktop',
            action: () => {
              this.navigateTo('/home/user/Desktop');
              this.addToHistory('/home/user/Desktop');
            }
          },
          {
            id: 'documents',
            label: 'Documents',
            action: () => {
              this.navigateTo('/home/user/Documents');
              this.addToHistory('/home/user/Documents');
            }
          },
          {
            id: 'downloads',
            label: 'Downloads',
            action: () => {
              this.navigateTo('/home/user/Downloads');
              this.addToHistory('/home/user/Downloads');
            }
          }
        ]
      },
      {
        id: 'select',
        label: 'Select Items',
        icon: 'checkbox-outline',
        action: () => {
          this.enterMultiSelectMode();
        }
      },
      {
        id: 'paste',
        label: 'Paste',
        icon: 'clipboard-outline',
        disabled: !this.hasClipboardItems(),
        action: () => {
          this.pasteItems();
        }
      },
      { separator: true },
      {
        id: 'refresh',
        label: 'Refresh',
        icon: 'refresh-outline',
        action: () => {
          this.loadFolder(this.getCurrentPath());
        }
      }
    ];
    
    // Show the context menu
    this.mobileInputHandler.showContextMenu(
      event.clientX,
      event.clientY,
      items
    );
  }

  /**
   * Show create menu (floating action button)
   */
  private showCreateMenu(event: MouseEvent): void {
    const items: ContextMenuItem[] = [
      {
        id: 'new-folder',
        label: 'New Folder',
        icon: 'folder-outline',
        action: () => {
          this.createNewFolder();
        }
      },
      {
        id: 'new-file',
        label: 'New Text File',
        icon: 'document-text-outline',
        action: () => {
          this.createNewFile();
        }
      },
      {
        id: 'upload',
        label: 'Upload',
        icon: 'cloud-upload-outline',
        action: () => {
          this.uploadFile();
        }
      }
    ];
    
    // Show the context menu
    this.mobileInputHandler.showContextMenu(
      event.clientX,
      event.clientY,
      items,
      { position: 'top' }
    );
  }

  /**
   * Get context menu items for a file or directory
   */
  private getItemContextMenu(entry: FileSystemEntry, path: string): ContextMenuItem[] {
    const isDirectory = FileEntryUtils.isDirectory(entry);
    
    const items: ContextMenuItem[] = [
      {
        id: 'open',
        label: isDirectory ? 'Open' : 'Open with...',
        icon: isDirectory ? 'folder-open-outline' : 'open-outline',
        action: () => {
          if (isDirectory) {
            this.navigateTo(path);
            this.addToHistory(path);
          } else {
            this.openFile(entry, path);
          }
        }
      }
    ];
    
    // Add operations common to files and folders
    items.push(
      {
        id: 'rename',
        label: 'Rename',
        icon: 'create-outline',
        action: () => {
          this.renameItem(path);
        }
      },
      {
        id: 'copy',
        label: 'Copy',
        icon: 'copy-outline',
        action: () => {
          this.setClipboard('copy', [path]);
        }
      },
      {
        id: 'cut',
        label: 'Cut',
        icon: 'cut-outline',
        action: () => {
          this.setClipboard('cut', [path]);
        }
      },
      {
        id: 'delete',
        label: 'Delete',
        icon: 'trash-outline',
        action: () => {
          this.deleteItem(path);
        }
      }
    );
    
    // Add file-specific operations
    if (!isDirectory) {
      items.push(
        {
          id: 'download',
          label: 'Download',
          icon: 'cloud-download-outline',
          action: () => {
            this.downloadFile(path);
          }
        }
      );
    }
    
    return items;
  }

  /**
   * Enter multi-select mode
   */
  private enterMultiSelectMode(): void {
    this.isMultiSelectMode = true;
    
    // Update UI for multi-select mode
    if (this.container) {
      // Show action bar with selection controls
      const actionBar = this.container.querySelector('.mobile-file-explorer-action-bar');
      if (actionBar) {
        const cancelBtn = actionBar.querySelector('.cancel-btn');
        const selectionCount = actionBar.querySelector('.selection-count');
        const actionButtons = actionBar.querySelector('.action-buttons');
        
        if (cancelBtn) cancelBtn.style.display = 'flex';
        if (selectionCount) selectionCount.style.display = 'flex';
        if (actionButtons) actionButtons.style.display = 'flex';
      }
      
      // Hide floating action button
      if (this.floatingActionButton) {
        this.floatingActionButton.style.display = 'none';
      }
      
      // Update file items to show checkboxes
      const fileItems = this.container.querySelectorAll('.mobile-file-item');
      fileItems.forEach(item => {
        const iconContainer = item.querySelector('.item-icon');
        if (iconContainer) {
          iconContainer.classList.add('with-checkbox');
          
          // Add checkbox if not present
          if (!iconContainer.querySelector('.item-checkbox')) {
            const checkbox = document.createElement('div');
            checkbox.className = 'item-checkbox';
            iconContainer.prepend(checkbox);
          }
        }
      });
    }
  }

  /**
   * Exit multi-select mode
   */
  private exitMultiSelectMode(): void {
    this.isMultiSelectMode = false;
    
    // Clear selection
    this.clearSelection();
    
    // Update UI to exit multi-select mode
    if (this.container) {
      // Hide action bar controls
      const actionBar = this.container.querySelector('.mobile-file-explorer-action-bar');
      if (actionBar) {
        const cancelBtn = actionBar.querySelector('.cancel-btn');
        const selectionCount = actionBar.querySelector('.selection-count');
        const actionButtons = actionBar.querySelector('.action-buttons');
        
        if (cancelBtn) cancelBtn.style.display = 'none';
        if (selectionCount) selectionCount.style.display = 'none';
        if (actionButtons) actionButtons.style.display = 'none';
      }
      
      // Show floating action button
      if (this.floatingActionButton) {
        this.floatingActionButton.style.display = 'flex';
      }
      
      // Update file items to hide checkboxes
      const fileItems = this.container.querySelectorAll('.mobile-file-item');
      fileItems.forEach(item => {
        item.classList.remove('selected');
        const iconContainer = item.querySelector('.item-icon');
        if (iconContainer) {
          iconContainer.classList.remove('with-checkbox');
          
          // Remove checkbox
          const checkbox = iconContainer.querySelector('.item-checkbox');
          if (checkbox) {
            checkbox.remove();
          }
        }
      });
    }
  }

  /**
   * Toggle selection of an item
   */
  private toggleItemSelection(path: string): void {
    const selectedItems = this.getSelectedItems();
    
    if (selectedItems.includes(path)) {
      // Remove from selection
      this.removeFromSelection(path);
    } else {
      // Add to selection
      this.addToSelection(path);
    }
    
    // Update selection count
    if (this.container) {
      const selectionCount = this.container.querySelector('.selection-count');
      if (selectionCount) {
        const count = this.getSelectedItems().length;
        selectionCount.textContent = count > 0 ? `${count} selected` : '';
      }
    }
  }

  /**
   * Update UI to reflect item selection state
   */
  private updateItemSelectionUI(itemElement: HTMLElement, isSelected: boolean): void {
    if (isSelected) {
      itemElement.classList.add('selected');
    } else {
      itemElement.classList.remove('selected');
    }
  }

  /**
   * Create a new folder in the current directory
   */
  private createNewFolder(): void {
    // Show prompt for folder name
    if (this.os.getWindowManager()) {
      this.os.getWindowManager().showPrompt(
        'Create Folder',
        'Enter name for new folder:',
        'New Folder',
        async (folderName) => {
          if (folderName && folderName.trim()) {
            try {
              const path = `${this.getCurrentPath()}/${folderName.trim()}`.replace(/\/+/g, '/');
              await this.os.getFileSystem().mkdir(path);
              
              // Refresh the folder contents
              this.loadFolder(this.getCurrentPath());
              
              // Show success notification
              this.os.getWindowManager()?.showNotification({
                title: 'Folder Created',
                message: `Created folder: ${folderName.trim()}`,
                type: 'success'
              });
            } catch (error) {
              // Show error notification
              this.os.getWindowManager()?.showNotification({
                title: 'Error',
                message: `Failed to create folder: ${error}`,
                type: 'error'
              });
            }
          }
        }
      );
    }
  }

  /**
   * Create a new empty text file in the current directory
   */
  private createNewFile(): void {
    // Show prompt for file name
    if (this.os.getWindowManager()) {
      this.os.getWindowManager().showPrompt(
        'Create File',
        'Enter name for new file:',
        'New File.txt',
        async (fileName) => {
          if (fileName && fileName.trim()) {
            try {
              const path = `${this.getCurrentPath()}/${fileName.trim()}`.replace(/\/+/g, '/');
              await this.os.getFileSystem().writeFile(path, '');
              
              // Refresh the folder contents
              this.loadFolder(this.getCurrentPath());
              
              // Show success notification
              this.os.getWindowManager()?.showNotification({
                title: 'File Created',
                message: `Created file: ${fileName.trim()}`,
                type: 'success'
              });
            } catch (error) {
              // Show error notification
              this.os.getWindowManager()?.showNotification({
                title: 'Error',
                message: `Failed to create file: ${error}`,
                type: 'error'
              });
            }
          }
        }
      );
    }
  }

  /**
   * Upload a file to the current directory
   */
  private uploadFile(): void {
    // Create file input element
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.multiple = true;
    fileInput.style.display = 'none';
    document.body.appendChild(fileInput);
    
    // Handle file selection
    fileInput.addEventListener('change', async () => {
      if (fileInput.files && fileInput.files.length > 0) {
        // Show loading indicator
        this.os.getWindowManager()?.showNotification({
          title: 'Uploading',
          message: `Uploading ${fileInput.files.length} file(s)...`,
          type: 'info'
        });
        
        let successCount = 0;
        
        // Upload each selected file
        for (let i = 0; i < fileInput.files.length; i++) {
          const file = fileInput.files[i];
          try {
            // Read file content
            const content = await this.readFileAsArrayBuffer(file);
            
            // Write to filesystem
            const path = `${this.getCurrentPath()}/${file.name}`.replace(/\/+/g, '/');
            await this.os.getFileSystem().writeFile(path, content);
            successCount++;
          } catch (error) {
            console.error(`Failed to upload file ${file.name}:`, error);
          }
        }
        
        // Refresh folder contents
        this.loadFolder(this.getCurrentPath());
        
        // Show result notification
        if (successCount > 0) {
          this.os.getWindowManager()?.showNotification({
            title: 'Upload Complete',
            message: `Successfully uploaded ${successCount} of ${fileInput.files.length} file(s)`,
            type: 'success'
          });
        } else {
          this.os.getWindowManager()?.showNotification({
            title: 'Upload Failed',
            message: 'Failed to upload files',
            type: 'error'
          });
        }
      }
      
      // Clean up
      document.body.removeChild(fileInput);
    });
    
    // Trigger file picker
    fileInput.click();
  }

  /**
   * Read file as ArrayBuffer
   */
  private readFileAsArrayBuffer(file: File): Promise<ArrayBuffer> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      
      reader.onload = () => {
        resolve(reader.result as ArrayBuffer);
      };
      
      reader.onerror = () => {
        reject(reader.error);
      };
      
      reader.readAsArrayBuffer(file);
    });
  }

  /**
   * Rename an item (file or directory)
   */
  private renameItem(path: string): void {
    const name = path.split('/').pop() || '';
    
    // Show prompt for new name
    if (this.os.getWindowManager()) {      this.os.getWindowManager().showPrompt(
        'Rename',
        'Enter new name:',
        name,
        async (newName: string) => {
          if (newName && newName.trim() && newName !== name) {
            try {
              const parentPath = path.substring(0, path.lastIndexOf('/'));
              const newPath = `${parentPath}/${newName}`.replace(/\/+/g, '/');
              
              await this.os.getFileSystem().rename(path, newPath);
              
              // Refresh the folder contents
              this.loadFolder(this.getCurrentPath());
              
              // Show success notification
              this.os.getWindowManager()?.showNotification({
                title: 'Renamed',
                message: `Renamed to: ${newName}`,
                type: 'success'
              });
            } catch (error) {
              // Show error notification
              this.os.getWindowManager()?.showNotification({
                title: 'Error',
                message: `Failed to rename: ${error}`,
                type: 'error'
              });
            }
          }
        }
      );
    }
  }

  /**
   * Delete an item (file or directory)
   */
  private deleteItem(path: string): void {
    const name = path.split('/').pop() || '';
    
    // Show confirmation dialog
    if (this.os.getWindowManager()) {      this.os.getWindowManager().showConfirm(
        'Delete',
        `Are you sure you want to delete "${name}"?`,
        async (confirmed: boolean) => {
          if (confirmed) {
            try {
              // Check if it's a directory or file
              const stat = await this.os.getFileSystem().stat(path);
              
              if (FileEntryUtils.isDirectory(stat)) {
                // Delete directory recursively
                await this.os.getFileSystem().rmdir(path, true);
              } else {
                // Delete file
                await this.os.getFileSystem().unlink(path);
              }
              
              // Refresh the folder contents
              this.loadFolder(this.getCurrentPath());
              
              // Show success notification
              this.os.getWindowManager()?.showNotification({
                title: 'Deleted',
                message: `Deleted: ${name}`,
                type: 'success'
              });
            } catch (error) {
              // Show error notification
              this.os.getWindowManager()?.showNotification({
                title: 'Error',
                message: `Failed to delete: ${error}`,
                type: 'error'
              });
            }
          }
        }
      );
    }
  }

  /**
   * Download a file
   */
  private downloadFile(path: string): void {
    const fileName = path.split('/').pop() || 'file';
    
    // Read the file content
    this.os.getFileSystem().readFile(path)
      .then(content => {
        // Create blob with content
        const blob = new Blob([content], { type: 'application/octet-stream' });
        
        // Create download link
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        
        // Trigger download
        a.click();
        
        // Clean up
        setTimeout(() => {
          document.body.removeChild(a);
          URL.revokeObjectURL(url);
        }, 100);
      })
      .catch(error => {
        // Show error notification
        this.os.getWindowManager()?.showNotification({
          title: 'Download Failed',
          message: `Failed to download file: ${error}`,
          type: 'error'
        });
      });
  }

  /**
   * Delete selected items
   */
  private deleteSelectedItems(): void {
    const selectedItems = this.getSelectedItems();
    
    if (selectedItems.length === 0) {
      return;
    }
    
    // Show confirmation dialog
    if (this.os.getWindowManager()) {      this.os.getWindowManager().showConfirm(
        'Delete',
        `Are you sure you want to delete ${selectedItems.length} selected item(s)?`,
        async (confirmed: boolean) => {
          if (confirmed) {
            let successCount = 0;
            
            // Delete each selected item
            for (const path of selectedItems) {
              try {
                // Check if it's a directory or file
                const stat = await this.os.getFileSystem().stat(path);
                
                if (FileEntryUtils.isDirectory(stat)) {
                  // Delete directory recursively
                  await this.os.getFileSystem().rmdir(path, true);
                } else {
                  // Delete file
                  await this.os.getFileSystem().unlink(path);
                }
                
                successCount++;
              } catch (error) {
                console.error(`Failed to delete ${path}:`, error);
              }
            }
            
            // Exit multi-select mode
            this.exitMultiSelectMode();
            
            // Refresh the folder contents
            this.loadFolder(this.getCurrentPath());
            
            // Show result notification
            if (successCount > 0) {
              this.os.getWindowManager()?.showNotification({
                title: 'Deleted',
                message: `Successfully deleted ${successCount} of ${selectedItems.length} item(s)`,
                type: 'success'
              });
            } else {
              this.os.getWindowManager()?.showNotification({
                title: 'Delete Failed',
                message: 'Failed to delete selected items',
                type: 'error'
              });
            }
          }
        }
      );
    }
  }
}
