import { OS } from '../core/os';
import { FileEntryUtils } from '../core/file-entry-utils';
import { FileSystemUtils } from '../core/file-system-utils';
import { FileSystemEntry } from '../core/filesystem';
import { GuiApplication } from '../core/gui-application';
import JSZip from 'jszip'; // Add JSZip import

/**
 * File Explorer App for the Hacker Game
 * Provides a graphical interface to browse and manage the file system
 */
export class FileExplorerApp extends GuiApplication {
  private currentPath: string = '/home/user';
  private selectedItems: string[] = [];
  private clipboard: { action: 'copy' | 'cut'; paths: string[] } | null = null;
  private history: string[] = [];
  private historyPosition: number = -1;
  private viewMode: 'grid' | 'list' = 'list';
  private showHidden: boolean = false;

  constructor(os: OS) {
    super(os);
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'file-explorer';
  }

  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    this.render();
    this.navigateTo('/home/user');
    
    // Add history entry for initial path
    this.addToHistory('/home/user');
  }

  /**
   * Render the file explorer UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="file-explorer-app">        <div class="file-explorer-toolbar">
          <div class="navigation-buttons">
            <button class="navigation-btn back" title="Back">◀</button>
            <button class="navigation-btn forward" title="Forward">▶</button>
            <button class="navigation-btn up" title="Up">▲</button>
            <button class="navigation-btn refresh" title="Refresh">↻</button>
          </div>
          <div class="path-bar">
            <input type="text" class="path-input" value="${this.currentPath}">
          </div>
          <div class="view-options">
            <button class="view-btn ${this.viewMode === 'grid' ? 'active' : ''}" data-view="grid" title="Grid View">□□</button>
            <button class="view-btn ${this.viewMode === 'list' ? 'active' : ''}" data-view="list" title="List View">≡</button>
            <button class="view-btn show-hidden ${this.showHidden ? 'active' : ''}" title="Show Hidden Files">👁️</button>
            <button class="view-btn upload-btn" title="Upload File">📤</button>
            <button class="view-btn download-btn" title="Download File">📥</button>
          </div>
        </div>
        <div class="file-explorer-main">
          <div class="file-explorer-sidebar">
            <div class="sidebar-section">
              <div class="sidebar-header">Favorites</div>
              <div class="sidebar-item" data-path="/home/user">🏠 Home</div>
              <div class="sidebar-item" data-path="/home/user/Desktop">🖥️ Desktop</div>
              <div class="sidebar-item" data-path="/home/user/Documents">📄 Documents</div>
              <div class="sidebar-item" data-path="/home/user/Downloads">⬇️ Downloads</div>
            </div>
            <div class="sidebar-section">
              <div class="sidebar-header">Devices</div>
              <div class="sidebar-item" data-path="/">💻 System</div>
              <div class="sidebar-item" data-path="/mnt">💿 Media</div>
            </div>
            <div class="sidebar-section">
              <div class="sidebar-header">Network</div>
              <div class="sidebar-item" data-path="/net/local">🔌 Local Network</div>
              <div class="sidebar-item" data-path="/net/web">🌐 Web</div>
            </div>
          </div>
          <div class="file-explorer-content">
            <div class="file-list ${this.viewMode}">
              <div class="loading-indicator">Loading...</div>
            </div>
          </div>
        </div>
        <div class="file-explorer-statusbar">
          <div class="status-info">
            <span class="item-count">0 items</span>
            <span class="selection-count"></span>
          </div>
          <div class="disk-usage">
            Free: 18.2 GB / 50.0 GB
          </div>
        </div>
        <div class="context-menu" style="display: none;">
          <div class="context-menu-item" data-action="open">Open</div>
          <div class="context-menu-item" data-action="open-with">Open With...</div>
          <div class="context-menu-separator"></div>
          <div class="context-menu-item" data-action="cut">Cut</div>
          <div class="context-menu-item" data-action="copy">Copy</div>
          <div class="context-menu-item" data-action="paste">Paste</div>
          <div class="context-menu-separator"></div>
          <div class="context-menu-item" data-action="rename">Rename</div>
          <div class="context-menu-item" data-action="delete">Delete</div>
          <div class="context-menu-separator"></div>
          <div class="context-menu-item" data-action="new-file">New File</div>
          <div class="context-menu-item" data-action="new-folder">New Folder</div>
          <div class="context-menu-separator"></div>
          <div class="context-menu-item" data-action="properties">Properties</div>
        </div>
        <!-- Hidden file input for upload functionality -->
        <input type="file" id="file-upload-input" style="display: none;" multiple>
      </div>
      <!-- NOTE: DO NOT ADD STYLES HERE! 
     All styles for the file explorer should be added to file-explorer.less instead.
     This ensures proper scoping and prevents conflicts with other components. -->
      <style>
 <!-- NOTE: DO NOT ADD STYLES HERE! -->
      </style>
    `;
    
    this.setupEventListeners();
  }

  /**
   * Setup event listeners for UI elements
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // Navigation buttons
    const backBtn = this.container.querySelector('.navigation-btn.back');
    const forwardBtn = this.container.querySelector('.navigation-btn.forward');
    const upBtn = this.container.querySelector('.navigation-btn.up');
    const refreshBtn = this.container.querySelector('.navigation-btn.refresh');
    
    backBtn?.addEventListener('click', () => this.navigateBack());
    forwardBtn?.addEventListener('click', () => this.navigateForward());
    upBtn?.addEventListener('click', () => this.navigateUp());
    refreshBtn?.addEventListener('click', () => this.refresh());
    
    // Upload button
    const uploadBtn = this.container.querySelector('.upload-btn');
    uploadBtn?.addEventListener('click', () => this.handleUpload());
    
    // Download button
    const downloadBtn = this.container.querySelector('.download-btn');
    downloadBtn?.addEventListener('click', () => this.handleDownload());
    
    // File upload input change
    const fileInput = this.container.querySelector<HTMLInputElement>('#file-upload-input');
    fileInput?.addEventListener('change', (e) => this.processUploadedFiles(e));
    
    // Path input
    const pathInput = this.container.querySelector<HTMLInputElement>('.path-input');
    pathInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        this.navigateTo(pathInput.value);
      }
    });
    
    // View buttons
    const viewBtns = this.container.querySelectorAll('.view-btn');
    viewBtns.forEach(btn => {
      btn.addEventListener('click', (e) => {
        const target = e.currentTarget as HTMLElement;
        
        if (target.classList.contains('show-hidden')) {
          // Toggle show hidden files
          this.showHidden = !this.showHidden;
          target.classList.toggle('active', this.showHidden);
          this.refresh();
        } else {
          // Change view mode
          const view = target.getAttribute('data-view') as 'grid' | 'list';
          if (view && view !== this.viewMode) {
            this.viewMode = view;
            this.updateViewMode();
          }
        }
      });
    });
    
    // Sidebar items
    const sidebarItems = this.container.querySelectorAll('.sidebar-item');
    sidebarItems.forEach(item => {
      item.addEventListener('click', (e) => {
        const path = (e.currentTarget as HTMLElement).getAttribute('data-path');
        if (path) {
          this.navigateTo(path);
        }
      });
    });
      // Main area right-click (for context menu)
    const fileListEl = this.container.querySelector('.file-list');
    fileListEl?.addEventListener('contextmenu', (e) => {
      // Show context menu for both the file-list element and empty space in the content area
      e.preventDefault();
      
      // If the click happened on a file item, the file item's own context menu handler will take over
      // Otherwise, show the empty space context menu
      const target = e.target as HTMLElement;
      if (!target.closest('.file-item')) {
        this.showContextMenu(<MouseEvent>e, null);
      }
    });
    
    // Add right-click handler for the file-explorer-content area as well
    const contentEl = this.container.querySelector('.file-explorer-content');
    contentEl?.addEventListener('contextmenu', (e) => {
      const target = e.target as HTMLElement;
      // Only handle if not clicked on a file item and not on the file list
      if (!target.closest('.file-item') && !target.closest('.file-list')) {
        e.preventDefault();
        this.showContextMenu(<MouseEvent>e, null);
      }
    });
    
    // Document click to hide context menu
    document.addEventListener('click', () => {
      this.hideContextMenu();
    });
  }

  /**
   * Navigate to a specific path
   */
  public navigateTo(path: string): void {
    const fileList = this.container?.querySelector('.file-list');
    if (!fileList) return;
    
    // Show loading indicator
    fileList.innerHTML = '<div class="loading-indicator">Loading...</div>';
    
    // Update current path
    this.currentPath = path;
    
    // Update path input
    const pathInput = this.container?.querySelector<HTMLInputElement>('.path-input');
    if (pathInput) {
      pathInput.value = path;
    }
    
    // Update history
    this.addToHistory(path);
    
    // Clear selection
    this.selectedItems = [];
    
    // Load directory contents
    this.os.getFileSystem().readDirectory(path)
      .then(entries => {
        // Filter hidden files if needed
        if (!this.showHidden) {
          entries = entries.filter(entry => !entry.name.startsWith('.'));
        }
          // Sort entries: directories first, then files
        entries.sort((a, b) => {
          // Sort directories first
          if (FileEntryUtils.isDirectory(a) && !FileEntryUtils.isDirectory(b)) return -1;
          if (!FileEntryUtils.isDirectory(a) && FileEntryUtils.isDirectory(b)) return 1;
          
          // Then sort alphabetically
          return a.name.localeCompare(b.name);
        });
        
        // Update file list
        this.renderFileList(entries);
        
        // Update status bar
        this.updateStatusBar(entries.length);
        
        // Update sidebar active item
        this.updateSidebarActiveItem();
        
        // Update navigation buttons state
        this.updateNavigationButtonsState();
      })
      .catch(error => {
        fileList.innerHTML = `<div class="error">Error loading directory: ${error.message}</div>`;
        console.error('Error loading directory:', error);
      });
  }
  /**
   * Render file list based on entries
   */
  private renderFileList(entries: Array<any>): void {
    const fileList = this.container?.querySelector('.file-list');
    if (!fileList) return;
    
    // Clear the list
    fileList.innerHTML = '';
    
    // Add parent directory entry if not at root
    if (this.currentPath !== '/') {
      const parentItem = document.createElement('div');
      parentItem.className = 'file-item parent-dir';
      parentItem.innerHTML = `
        <span class="file-icon">📁</span>
        <span class="file-name">..</span>
      `;
      parentItem.addEventListener('click', () => this.navigateUp());
      parentItem.addEventListener('dblclick', () => this.navigateUp());
      fileList.appendChild(parentItem);
    }
    
    // Show empty folder message if no entries
    if (entries.length === 0) {
      fileList.innerHTML += '<div class="empty-folder">This folder is empty</div>';
      return;
    }
    
    // Add entries to the list
    entries.forEach(entry => {
      const fileItem = document.createElement('div');
      fileItem.className = 'file-item';
      fileItem.setAttribute('data-name', entry.name);
      fileItem.setAttribute('data-path', `${this.currentPath === '/' ? '' : this.currentPath}/${entry.name}`);
      const isDir = FileEntryUtils.isDirectory(entry);
      fileItem.setAttribute('data-type', isDir ? 'directory' : 'file');
      
      // Get appropriate icon based on file type
      const icon = FileEntryUtils.getFileIcon(entry.name, entry);
      
      // Format file size if available
      const sizeText = entry.metadata?.size !== undefined ? this.formatFileSize(entry.metadata.size) : '';
      
      // Format modified date if available
      const dateText = entry.metadata?.modified ? this.formatDate(new Date(entry.metadata.modified)) : '';
      
      fileItem.innerHTML = `
        <span class="file-icon">${icon}</span>
        <span class="file-name">${this.escapeHtml(entry.name)}</span>
        ${!isDir ? `<span class="file-size">${sizeText}</span>` : ''}
        <span class="file-date">${dateText}</span>
      `;
      
      // Add event listeners
      fileItem.addEventListener('click', (e) => this.handleFileItemClick(e, fileItem));
      fileItem.addEventListener('dblclick', () => this.handleFileItemDblClick(fileItem));
      fileItem.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        this.handleFileItemClick(e, fileItem);
        this.showContextMenu(e, fileItem);
      });
      
      fileList.appendChild(fileItem);
    });
  }

  /**
   * Handle file item click
   */
  private handleFileItemClick(e: MouseEvent, fileItem: HTMLElement): void {
    // Handle selection logic
    if (e.ctrlKey) {
      // Ctrl+Click: Toggle selection of this item
      fileItem.classList.toggle('selected');
      
      const path = fileItem.getAttribute('data-path');
      if (path) {
        const index = this.selectedItems.indexOf(path);
        if (index === -1) {
          this.selectedItems.push(path);
        } else {
          this.selectedItems.splice(index, 1);
        }
      }
    } else if (e.shiftKey && this.selectedItems.length > 0) {
      // Shift+Click: Select range
      const items = Array.from(this.container?.querySelectorAll('.file-item') || []);
      const lastSelectedIndex = items.findIndex(item => 
        item.getAttribute('data-path') === this.selectedItems[this.selectedItems.length - 1]
      );
      const currentIndex = items.indexOf(fileItem);
      
      // Clear previous selection
      items.forEach(item => item.classList.remove('selected'));
      this.selectedItems = [];
      
      // Select range
      const start = Math.min(lastSelectedIndex, currentIndex);
      const end = Math.max(lastSelectedIndex, currentIndex);
      
      for (let i = start; i <= end; i++) {
        items[i].classList.add('selected');
        const path = items[i].getAttribute('data-path');
        if (path) {
          this.selectedItems.push(path);
        }
      }
    } else {
      // Regular click: Select only this item
      this.container?.querySelectorAll('.file-item.selected').forEach(item => {
        item.classList.remove('selected');
      });
      
      fileItem.classList.add('selected');
      
      const path = fileItem.getAttribute('data-path');
      if (path) {
        this.selectedItems = [path];
      } else {
        this.selectedItems = [];
      }
    }
    
    // Update status bar with selection count
    this.updateStatusBar();
  }

  /**
   * Handle file item double click
   */
  private handleFileItemDblClick(fileItem: HTMLElement): void {
    const path = fileItem.getAttribute('data-path');
    const type = fileItem.getAttribute('data-type');
    
    if (!path) return;
    
    if (type === 'directory') {
      // Navigate to directory
      this.navigateTo(path);
    } else {
      // Open file based on type
      this.openFile(path);
    }
  }

  /**
   * Navigate to parent directory
   */
  private navigateUp(): void {
    if (this.currentPath === '/') return;
    
    const pathParts = this.currentPath.split('/');
    pathParts.pop(); // Remove last part
    
    const parentPath = pathParts.join('/') || '/';
    this.navigateTo(parentPath);
  }

  /**
   * Refresh current directory
   */
  private refresh(): void {
    this.navigateTo(this.currentPath);
  }

  /**
   * Add path to navigation history
   */
  private addToHistory(path: string): void {
    // If we're not at the end of the history, truncate the future history
    if (this.historyPosition < this.history.length - 1) {
      this.history = this.history.slice(0, this.historyPosition + 1);
    }
    
    // Only add to history if different from current
    if (this.history.length === 0 || this.history[this.history.length - 1] !== path) {
      this.history.push(path);
      this.historyPosition = this.history.length - 1;
    }
    
    // Update navigation buttons
    this.updateNavigationButtonsState();
  }

  /**
   * Navigate back in history
   */
  private navigateBack(): void {
    if (this.historyPosition <= 0) return;
    
    this.historyPosition--;
    const path = this.history[this.historyPosition];
    
    // Don't add to history when navigating through history
    this.currentPath = path;
    this.navigateTo(path);
    
    // Prevent double-adding to history
    this.history.pop();
    this.historyPosition = this.history.length - 1;
    
    this.updateNavigationButtonsState();
  }

  /**
   * Navigate forward in history
   */
  private navigateForward(): void {
    if (this.historyPosition >= this.history.length - 1) return;
    
    this.historyPosition++;
    const path = this.history[this.historyPosition];
    
    // Don't add to history when navigating through history
    this.currentPath = path;
    this.navigateTo(path);
    
    // Prevent double-adding to history
    this.history.pop();
    this.historyPosition = this.history.length - 1;
    
    this.updateNavigationButtonsState();
  }

  /**
   * Update navigation buttons state
   */
  private updateNavigationButtonsState(): void {
    const backBtn = this.container?.querySelector<HTMLButtonElement>('.navigation-btn.back');
    const forwardBtn = this.container?.querySelector<HTMLButtonElement>('.navigation-btn.forward');
    
    if (backBtn) {
      backBtn.disabled = this.historyPosition <= 0;
    }
    
    if (forwardBtn) {
      forwardBtn.disabled = this.historyPosition >= this.history.length - 1;
    }
  }

  /**
   * Update sidebar active item
   */
  private updateSidebarActiveItem(): void {
    // Remove active class from all sidebar items
    this.container?.querySelectorAll('.sidebar-item').forEach(item => {
      item.classList.remove('active');
    });
    
    // Add active class to matching sidebar item
    this.container?.querySelectorAll('.sidebar-item').forEach(item => {
      const itemPath = item.getAttribute('data-path');
      if (itemPath === this.currentPath) {
        item.classList.add('active');
      }
    });
  }

  /**
   * Update view mode (grid/list)
   */
  private updateViewMode(): void {
    const fileList = this.container?.querySelector('.file-list');
    const viewBtns = this.container?.querySelectorAll('.view-btn');
    
    if (fileList) {
      fileList.className = `file-list ${this.viewMode}`;
    }
    
    if (viewBtns) {
      viewBtns.forEach(btn => {
        const view = btn.getAttribute('data-view');
        if (view) {
          btn.classList.toggle('active', view === this.viewMode);
        }
      });
    }
  }

  /**
   * Update status bar
   */
  private updateStatusBar(totalItems?: number): void {
    const statusEl = this.container?.querySelector('.status-info');
    if (!statusEl) return;
    
    const itemCount = totalItems !== undefined ? totalItems : 
      this.container?.querySelectorAll('.file-item:not(.parent-dir)').length || 0;
    
    // Update item count
    const itemCountEl = statusEl.querySelector('.item-count');
    if (itemCountEl) {
      itemCountEl.textContent = `${itemCount} ${itemCount === 1 ? 'item' : 'items'}`;
    }
    
    // Update selection count
    const selectionCountEl = statusEl.querySelector('.selection-count');
    if (selectionCountEl) {
      if (this.selectedItems.length > 0) {
        selectionCountEl.textContent = `${this.selectedItems.length} selected`;
      } else {
        selectionCountEl.textContent = '';
      }
    }
  }  /**
   * Show context menu
   */
  private showContextMenu(event: MouseEvent, fileItem: HTMLElement | null): void {
    // First, remove any existing context menu
    this.hideContextMenu();
    
    // Get the predefined context menu from the DOM
    let contextMenu = this.container?.querySelector<HTMLElement>('.context-menu');
    
    if (!contextMenu) {
      console.error('Context menu not found in the DOM');
      return;
    }
    
    // Make it focusable for blur events
    contextMenu.tabIndex = -1;
    
    // Position the menu
    contextMenu.style.display = 'block';
    contextMenu.style.left = `${event.clientX}px`;
    contextMenu.style.top = `${event.clientY}px`;
    
    // Determine the context type
    const isOnEmptySpace = fileItem === null;
    const isOnDirectory = fileItem?.getAttribute('data-type') === 'directory';
    const isOnFile = fileItem?.getAttribute('data-type') === 'file';
    const isZipFile = isOnFile && fileItem?.getAttribute('data-path')?.endsWith('.zip');
    
    // Build menu content based on context
    let menuItems = '';
    
    if (isOnEmptySpace) {
      // In empty space or directory background
      menuItems += `
        <div class="context-menu-item" data-action="new-folder">New Folder</div>
        <div class="context-menu-item" data-action="new-file">New Text File</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item ${!this.clipboard ? 'disabled' : ''}" data-action="paste">Paste</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="refresh">Refresh</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="properties">Properties</div>
      `;
    } else if (isOnDirectory) {
      // On a directory
      menuItems += `
        <div class="context-menu-item" data-action="open">Open</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="cut">Cut</div>
        <div class="context-menu-item" data-action="copy">Copy</div>
        <div class="context-menu-item ${!this.clipboard ? 'disabled' : ''}" data-action="paste">Paste Into Folder</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="rename">Rename</div>
        <div class="context-menu-item" data-action="delete">Delete</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="compress">Compress to Zip</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="properties">Properties</div>
      `;
    } else if (isOnFile) {
      // On a file
      menuItems += `
        <div class="context-menu-item" data-action="open">Open</div>
        <div class="context-menu-item" data-action="open-with">Open With...</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="cut">Cut</div>
        <div class="context-menu-item" data-action="copy">Copy</div>
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="rename">Rename</div>
        <div class="context-menu-item" data-action="delete">Delete</div>
      `;
      
      // Add extract option if zip file
      if (isZipFile) {
        menuItems += `
          <div class="context-menu-separator"></div>
          <div class="context-menu-item" data-action="extract">Extract Here</div>
        `;
      }
      
      menuItems += `
        <div class="context-menu-separator"></div>
        <div class="context-menu-item" data-action="properties">Properties</div>
      `;
    }
    
    // Set the menu content
    contextMenu.innerHTML = menuItems;
    
    // Focus the menu to enable blur event
    contextMenu.focus();
    
    // Create a click handler to detect clicks outside the menu
    const handleOutsideClick = (e: MouseEvent) => {
      // Check if the click was outside the context menu
      if (!contextMenu?.contains(e.target as Node)) {
        this.hideContextMenu();
        // Remove this event listener
        document.removeEventListener('mousedown', handleOutsideClick);
      }
    };
    
    // Add the event listener
    document.addEventListener('mousedown', handleOutsideClick);
    
    // Prevent clicks on the menu from closing it
    contextMenu.addEventListener('click', (e) => {
      e.stopPropagation();
    });
    
    // Setup action handlers
    contextMenu.querySelectorAll('.context-menu-item').forEach(item => {
      if (item.classList.contains('disabled')) return;
      
      item.addEventListener('click', (e) => {
        e.stopPropagation();
        const action = (item as HTMLElement).getAttribute('data-action');
        if (!action) return;
        
        this.handleContextMenuAction(action, fileItem);
        this.hideContextMenu();
      });
    });
    
    // Prevent the menu from going off-screen
    setTimeout(() => {
      if (!contextMenu) return;
      
      const rect = contextMenu.getBoundingClientRect();
      const windowWidth = window.innerWidth;
      const windowHeight = window.innerHeight;
      
      if (rect.right > windowWidth) {
        contextMenu.style.left = `${windowWidth - rect.width - 10}px`;
      }
      
      if (rect.bottom > windowHeight) {
        contextMenu.style.top = `${windowHeight - rect.height - 10}px`;
      }
    }, 0);
    
    // Prevent default browser context menu
    event.preventDefault();
    event.stopPropagation();
  }  /**
   * Hide context menu
   */
  private hideContextMenu(): void {
    const contextMenu = this.container?.querySelector<HTMLElement>('.context-menu');
    if (contextMenu) {
      // Hide the menu instead of removing it
      contextMenu.style.display = 'none';
    }
  }

  /**
   * Handle context menu actions
   */
  private handleContextMenuAction(action: string, fileItem: HTMLElement | null): void {
    const selectedPath = fileItem?.getAttribute('data-path');
    
    switch (action) {
      case 'open':
        if (selectedPath) {
          const type = fileItem?.getAttribute('data-type');
          if (type === 'directory') {
            this.navigateTo(selectedPath);
          } else {
            this.openFile(selectedPath);
          }
        }
        break;
        
      case 'open-with':
        if (selectedPath) {
          this.showOpenWithDialog(selectedPath);
        }
        break;
        
      case 'cut':
        this.cutSelectedItems();
        break;
        
      case 'copy':
        this.copySelectedItems();
        break;
        
      case 'paste':
        this.pasteItems();
        break;
        
      case 'rename':
        if (selectedPath) {
          this.showRenameDialog(selectedPath);
        }
        break;
        
      case 'delete':
        this.deleteSelectedItems();
        break;
        
      case 'new-file':
        this.showNewFileDialog();
        break;
        
      case 'new-folder':
        this.showNewFolderDialog();
        break;
        
      case 'properties':
        if (selectedPath) {
          this.showPropertiesDialog(selectedPath);
        }
        break;
        
      case 'download':
        if (selectedPath) {
          this.downloadFile(selectedPath);
        }
        break;
    }
  }

  /**
   * Cut selected items
   */
  private cutSelectedItems(): void {
    if (this.selectedItems.length === 0) return;
    
    this.clipboard = {
      action: 'cut',
      paths: [...this.selectedItems]
    };
  }

  /**
   * Copy selected items
   */
  private copySelectedItems(): void {
    if (this.selectedItems.length === 0) return;
    
    this.clipboard = {
      action: 'copy',
      paths: [...this.selectedItems]
    };
  }
  /**
   * Paste items from clipboard
   */
  private pasteItems(): void {
    if (!this.clipboard || this.clipboard.paths.length === 0) return;
    
    const action = this.clipboard.action;
    const paths = this.clipboard.paths;
    const fs = this.os.getFileSystem();
    
    // Handle each path
    Promise.all(paths.map(path => {
      const fileName = path.split('/').pop() || '';
      const destPath = `${this.currentPath === '/' ? '' : this.currentPath}/${fileName}`;
      
      // Check if file already exists
      return fs.exists(destPath)
        .then(exists => {
          if (exists) {
            // Ask for confirmation to overwrite
            return new Promise<boolean>((resolve) => {
              this.showConfirmDialog(
                'Confirm Overwrite',
                `File "${fileName}" already exists. Do you want to replace it?`,
                () => resolve(true),
                () => resolve(false)
              );
            });
          }
          return true;
        })
        .then(proceed => {
          if (!proceed) return Promise.resolve();
          
          // Perform the operation using our utility functions
          if (action === 'copy') {
            return FileSystemUtils.copy(fs, path, destPath);
          } else {
            return FileSystemUtils.move(fs, path, destPath);
          }
        });
    }))
    .then(() => {
      // If this was a cut operation, clear the clipboard
      if (action === 'cut') {
        this.clipboard = null;
      }
      
      // Refresh the view
      this.refresh();
    })
    .catch((error: Error) => {
      console.error('Error pasting files:', error);
      this.showErrorDialog('Error', `Failed to paste files: ${error.message}`);
    });
  }
  /**
   * Delete selected items
   */
  private deleteSelectedItems(): void {
    if (this.selectedItems.length === 0) return;
    
    const message = this.selectedItems.length === 1 
      ? `Are you sure you want to delete "${this.selectedItems[0].split('/').pop()}"?`
      : `Are you sure you want to delete ${this.selectedItems.length} items?`;
    
    this.showConfirmDialog('Confirm Delete', message, () => {
      const fs = this.os.getFileSystem();
      
      // Perform deletion using our utility
      Promise.all(this.selectedItems.map(path => 
        FileSystemUtils.remove(fs, path)
      ))
      .then(() => {
        // Refresh view
        this.refresh();
      })
      .catch((error: Error) => {
        console.error('Error deleting files:', error);
        this.showErrorDialog('Error', `Failed to delete files: ${error.message}`);
      });
    });
  }
  /**
   * Show rename dialog
   */
  private showRenameDialog(path: string): void {
    const fileName = path.split('/').pop() || '';
    
    this.showInputDialog(
      'Rename',
      'Enter new name:',
      fileName,
      (newName) => {
        if (!newName || newName === fileName) return;
        
        const newPath = `${path.substring(0, path.lastIndexOf('/'))}/${newName}`;
        const fs = this.os.getFileSystem();
        
        FileSystemUtils.move(fs, path, newPath)
          .then(() => {
            this.refresh();
          })
          .catch((error: Error) => {
            console.error('Error renaming file:', error);
            this.showErrorDialog('Error', `Failed to rename file: ${error.message}`);
          });
      }
    );
  }

  /**
   * Show new file dialog
   */
  private showNewFileDialog(): void {
    this.showInputDialog(
      'New File',
      'Enter file name:',
      'untitled.txt',
      (fileName) => {
        if (!fileName) return;
        
        const filePath = `${this.currentPath === '/' ? '' : this.currentPath}/${fileName}`;
        
        this.os.getFileSystem().writeFile(filePath, '')
          .then(() => {
            this.refresh();
          })
          .catch(error => {
            console.error('Error creating file:', error);
            this.showErrorDialog('Error', `Failed to create file: ${error.message}`);
          });
      }
    );
  }

  /**
   * Show new folder dialog
   */
  private showNewFolderDialog(): void {
    this.showInputDialog(
      'New Folder',
      'Enter folder name:',
      'New Folder',
      (folderName) => {
        if (!folderName) return;
        
        const folderPath = `${this.currentPath === '/' ? '' : this.currentPath}/${folderName}`;
        
        this.os.getFileSystem().createDirectory(folderPath)
          .then(() => {
            this.refresh();
          })
          .catch(error => {
            console.error('Error creating folder:', error);
            this.showErrorDialog('Error', `Failed to create folder: ${error.message}`);
          });
      }
    );
  }
  /**
   * Show properties dialog
   */
  private showPropertiesDialog(path: string): void {
    const fileName = path.split('/').pop() || '';
    
    // Create dialog
    const dialogOverlay = document.createElement('div');
    dialogOverlay.className = 'dialog-overlay';
    dialogOverlay.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">Properties: ${this.escapeHtml(fileName)}</div>
        <div class="dialog-content">
          <div class="loading-properties">Loading...</div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn">OK</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialogOverlay);
    
    // Add event listeners
    const okBtn = dialogOverlay.querySelector('.dialog-btn');
    okBtn?.addEventListener('click', () => {
      dialogOverlay.remove();
    });
    
    // Load file info using our utility
    const fs = this.os.getFileSystem();
    FileSystemUtils.stat(fs, path)
      .then(stats => {
        const contentDiv = dialogOverlay.querySelector('.dialog-content');
        if (!contentDiv) return;
        
        const type = stats.isDirectory ? 'Directory' : 'File';
        const size = stats.size !== undefined ? this.formatFileSize(stats.size) : 'Unknown';
        const created = stats.createdTime ? this.formatDate(stats.createdTime) : 'Unknown';
        const modified = stats.modifiedTime ? this.formatDate(stats.modifiedTime) : 'Unknown';
        
        contentDiv.innerHTML = `
          <table class="properties-table">
            <tr>
              <td>Name:</td>
              <td>${this.escapeHtml(fileName)}</td>
            </tr>
            <tr>
              <td>Type:</td>
              <td>${type}</td>
            </tr>
            <tr>
              <td>Location:</td>
              <td>${this.escapeHtml(path.substring(0, path.lastIndexOf('/')) || '/')}</td>
            </tr>
            <tr>
              <td>Size:</td>
              <td>${size}</td>
            </tr>
            <tr>
              <td>Created:</td>
              <td>${created}</td>
            </tr>
            <tr>
              <td>Modified:</td>
              <td>${modified}</td>
            </tr>
          </table>
        `;
      })
      .catch((error: Error) => {
        console.error('Error getting file properties:', error);
        
        const contentDiv = dialogOverlay.querySelector('.dialog-content');
        if (contentDiv) {
          contentDiv.innerHTML = `<div class="error">Error loading properties: ${error.message}</div>`;
        }
      });
  }

  /**
   * Show "Open With" dialog
   */
  private showOpenWithDialog(path: string): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">Open With</div>
        <div class="dialog-content">
          <div class="app-list">
            <div class="app-item" data-app="text-editor">
              <span class="app-icon">📝</span>
              <span class="app-name">Text Editor</span>
            </div>
            <div class="app-item" data-app="code-editor">
              <span class="app-icon">💻</span>
              <span class="app-name">Code Editor</span>
            </div>
          </div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn cancel">Cancel</button>
          <button class="dialog-btn open" disabled>Open</button>
        </div>
      </div>
      <style>
        .dialog-content .app-list {
          max-height: 300px;
          overflow-y: auto;
        }
        .dialog-content .app-item {
          padding: 10px;
          cursor: pointer;
          display: flex;
          align-items: center;
          border-radius: 3px;
        }
        .dialog-content .app-item:hover {
          background-color: #2a2d2e;
        }
        .dialog-content .app-item.selected {
          background-color: #0e639c;
        }
        .dialog-content .app-icon {
          margin-right: 10px;
          font-size: 24px;
        }
      </style>
    `;
    
    this.container?.appendChild(dialog);
    
    let selectedApp = '';
    
    // App item click
    const appItems = dialog.querySelectorAll('.app-item');
    appItems.forEach(item => {
      item.addEventListener('click', () => {
        // Update selection
        appItems.forEach(i => i.classList.remove('selected'));
        item.classList.add('selected');
        
        // Enable open button
        const openBtn = dialog.querySelector<HTMLButtonElement>('.dialog-btn.open');
        if (openBtn) {
          openBtn.disabled = false;
        }
        
        selectedApp = (item as HTMLElement).getAttribute('data-app') || '';
      });
    });
    
    // Double-click to open
    appItems.forEach(item => {
      item.addEventListener('dblclick', () => {
        selectedApp = (item as HTMLElement).getAttribute('data-app') || '';
        if (selectedApp) {
          dialog.remove();
          this.openFileWithApp(path, selectedApp);
        }
      });
    });
    
    // Button click handlers
    const cancelBtn = dialog.querySelector('.dialog-btn.cancel');
    const openBtn = dialog.querySelector('.dialog-btn.open');
    
    cancelBtn?.addEventListener('click', () => {
      dialog.remove();
    });
    
    openBtn?.addEventListener('click', () => {
      if (selectedApp) {
        dialog.remove();
        this.openFileWithApp(path, selectedApp);
      }
    });
  }

  /**
   * Open file with specified app
   */
  private openFileWithApp(filePath: string, appId: string): void {
    // Create a custom event to request app launch
    const event = new CustomEvent('app-launch-request', {
      detail: {
        appId: appId,
        args: [filePath]
      }
    });
    
    document.dispatchEvent(event);
  }

  /**
   * Open file based on type
   */
  private openFile(path: string): void {
    const extension = path.split('.').pop()?.toLowerCase() || '';
    
    // Determine appropriate app based on file extension
    let appId = 'text-editor'; // Default to text editor
    
    if (['js', 'ts', 'html', 'css', 'json', 'py', 'c', 'cpp', 'h', 'php'].includes(extension)) {
      appId = 'code-editor';
    } else if (['jpg', 'jpeg', 'png', 'gif', 'bmp'].includes(extension)) {
      appId = 'image-viewer';
    } else if (['mp3', 'wav', 'ogg'].includes(extension)) {
      appId = 'audio-player';
    } else if (['mp4', 'webm', 'avi'].includes(extension)) {
      appId = 'video-player';
    } else if (['pdf'].includes(extension)) {
      appId = 'pdf-viewer';
    }
    
    // Launch the appropriate app
    this.openFileWithApp(path, appId);
  }

  /**
   * Show input dialog
   */
  private async showInputDialog(
    title: string, 
    message: string, 
    defaultValue: string,
    onConfirm: (value: string) => void
  ): Promise<void> {
    // Use the dialogManager from GuiApplication base class
    const result = await this.dialogManager.Prompt.Show(title, message, {
      defaultText: defaultValue
    });
    
    if (result !== null) {
      onConfirm(result);
    }
  }

  /**
   * Show confirm dialog
   */
  private async showConfirmDialog(
    title: string, 
    message: string, 
    onConfirm: () => void,
    onCancel?: () => void
  ): Promise<void> {
    // Use the dialogManager from GuiApplication base class
    const result = await this.dialogManager.Msgbox.Show(title, message, ['yes', 'no']);
    
    if (result === 'yes') {
      onConfirm();
    } else if (onCancel) {
      onCancel();
    }
  }

  /**
   * Show error dialog
   */
  private async showErrorDialog(title: string, message: string): Promise<void> {
    // Use the dialogManager from GuiApplication base class
    await this.dialogManager.Msgbox.Show(title, message, ['ok'], 'error');
  }

  /**
   * Get file icon based on name and type
   */
  private getFileIcon(fileName: string, isDirectory: boolean): string {
    if (isDirectory) {
      return '📁';
    }
    
    const extension = fileName.split('.').pop()?.toLowerCase();
    
    // Images
    if (['jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg'].includes(extension || '')) {
      return '🖼️';
    }
    
    // Audio
    if (['mp3', 'wav', 'ogg', 'flac'].includes(extension || '')) {
      return '🎵';
    }
    
    // Video
    if (['mp4', 'webm', 'avi', 'mov', 'wmv'].includes(extension || '')) {
      return '🎬';
    }
    
    // Code
    if (['js', 'ts', 'html', 'css', 'py', 'java', 'c', 'cpp', 'php'].includes(extension || '')) {
      return '📊';
    }
    
    // Documents
    if (['pdf'].includes(extension || '')) {
      return '📕';
    }
    if (['doc', 'docx'].includes(extension || '')) {
      return '📘';
    }
    if (['xls', 'xlsx'].includes(extension || '')) {
      return '📗';
    }
    if (['ppt', 'pptx'].includes(extension || '')) {
      return '📙';
    }
    
    // Archives
    if (['zip', 'rar', 'tar', 'gz', '7z'].includes(extension || '')) {
      return '📦';
    }
    
    // Executable
    if (['exe', 'bat', 'sh', 'app'].includes(extension || '')) {
      return '⚙️';
    }
    
    // Default
    return '📄';
  }

  /**
   * Format file size
   */
  private formatFileSize(size: number): string {
    if (size < 1024) {
      return `${size} B`;
    } else if (size < 1024 * 1024) {
      return `${(size / 1024).toFixed(1)} KB`;
    } else if (size < 1024 * 1024 * 1024) {
      return `${(size / 1024 / 1024).toFixed(1)} MB`;
    } else {
      return `${(size / 1024 / 1024 / 1024).toFixed(1)} GB`;
    }
  }

  /**
   * Format date
   */
  private formatDate(date: Date): string {
    return date.toLocaleString();
  }

  /**
   * Escape HTML
   */
  private escapeHtml(str: string): string {
    return str
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  /**
   * Handle upload button click
   */
  private handleUpload(): void {
    // Trigger the hidden file input
    const fileInput = this.container?.querySelector<HTMLInputElement>('#file-upload-input');
    if (fileInput) {
      fileInput.click();
    }
  }
  /**
   * Process the files selected for upload
   */
  private processUploadedFiles(e: Event): void {
    const fileInput = e.target as HTMLInputElement;
    const files = fileInput.files;
    
    if (!files || files.length === 0) return;
    
    // Display progress dialog
    this.showProgressDialog(
      'Uploading Files', 
      `Uploading ${files.length} file(s)...`, 
      0
    );
    
    const progressDialogElement = this.container?.querySelector('.dialog-overlay .progress-value');
    
    // Process each file
    let completed = 0;
    const totalFiles = files.length;
    
    Array.from(files).forEach(file => {
      const reader = new FileReader();
      
      reader.onload = (event) => {
        const content = event.target?.result;
        if (!content) {
          console.error(`Error: No content read from file ${file.name}`);
          this.showErrorDialog('Upload Error', `Failed to read content from ${file.name}`);
          return;
        }
        
        // Create file in the virtual filesystem
        const destPath = `${this.currentPath === '/' ? '' : this.currentPath}/${file.name}`;
        
        // Convert content to string or keep as ArrayBuffer based on file type
        let fileContent: string | ArrayBuffer;
        if (typeof content === 'string') {
          fileContent = content;
        } else if (content instanceof ArrayBuffer) {
          // For text files, convert ArrayBuffer to string
          if (this.isTextFile(file.name)) {
            fileContent = new TextDecoder().decode(new Uint8Array(content));
          } else {
            // For binary files, keep as ArrayBuffer
            fileContent = content;
          }
        } else {
          console.error(`Error: Unsupported content type for file ${file.name}`);
          this.showErrorDialog('Upload Error', `Failed to process ${file.name}: Unsupported content type`);
          return;
        }
        
        const writePromise = typeof fileContent === 'string' ? this.os.getFileSystem().writeFile(destPath, fileContent) :this.os.getFileSystem().writeBinaryFile(destPath, fileContent);

          writePromise.then(() => {
            completed++;
            
            // Update progress
            if (progressDialogElement) {
              const percent = Math.round((completed / totalFiles) * 100);
              progressDialogElement.textContent = `${percent}%`;
              (progressDialogElement.previousElementSibling as HTMLElement).style.width = `${percent}%`;
            }
            
            // If all files are processed, refresh the view and close dialog
            if (completed === totalFiles) {
              this.container?.querySelector('.dialog-overlay')?.remove();
              this.refresh();
              
              // Clear the file input for future uploads
              fileInput.value = '';
            }
          })
          .catch((error: Error) => {
            console.error(`Error uploading file ${file.name}:`, error);
            this.showErrorDialog('Upload Error', `Failed to upload ${file.name}: ${error.message}`);
            
            completed++;
            if (completed === totalFiles) {
              this.container?.querySelector('.dialog-overlay')?.remove();
              this.refresh();
              fileInput.value = '';
            }
          });
      };
      
      reader.onerror = () => {
        console.error(`Error reading file ${file.name}`);
        this.showErrorDialog('Upload Error', `Failed to read ${file.name}`);
        
        completed++;
        if (completed === totalFiles) {
          this.container?.querySelector('.dialog-overlay')?.remove();
          this.refresh();
          fileInput.value = '';
        }
      };
      
      // Use readAsText for text files and readAsArrayBuffer for binary files
      if (this.isTextFile(file.name)) {
        reader.readAsText(file);
      } else {
        reader.readAsArrayBuffer(file);
      }
    });
  }
  
  /**
   * Determine if a file is a text file based on its extension
   */
  private isTextFile(fileName: string): boolean {
    const textExtensions = [
      'txt', 'md', 'js', 'ts', 'html', 'css', 'json', 'xml', 'csv', 
      'py', 'c', 'cpp', 'h', 'java', 'rb', 'php', 'sh', 'bat', 'ps1'
    ];
    const extension = fileName.split('.').pop()?.toLowerCase() || '';
    return textExtensions.includes(extension);
  }

  /**
   * Handle download button click
   */
  private handleDownload(): void {
    if (this.selectedItems.length === 0) {
      this.showErrorDialog('Download Error', 'No file selected for download');
      return;
    }
    
    // Support downloading multiple files
    if (this.selectedItems.length > 1) {
      this.showConfirmDialog(
        'Download Multiple Files',
        `Do you want to download ${this.selectedItems.length} files?`,
        () => this.downloadMultipleFiles(this.selectedItems)
      );
      return;
    }
    
    // Download a single file
    const filePath = this.selectedItems[0];
    this.downloadFile(filePath);
  }

  /**
   * Download a single file
   */
  private downloadFile(filePath: string): void {
    const fileName = filePath.split('/').pop() || 'download';
    
    // Check if it's a directory
    this.os.getFileSystem().stat(filePath)
      .then(stats => {
        if (stats.isDirectory) {
          // If it's a directory, offer to download as zip
          this.showConfirmDialog(
            'Download Folder',
            `"${fileName}" is a folder. Do you want to download it as a zip file?`,
            () => this.downloadFolderAsZip(filePath),
            () => {}
          );
          return;
        }
        
        // Read the file
        return this.os.getFileSystem().readFile(filePath)
          .then(content => {
            // Create a blob from the content
            let blob;
            
            if (typeof content === 'string') {
              // Handle text content
              blob = new Blob([content], { type: 'application/octet-stream' });
            } else if (<any>content instanceof ArrayBuffer) {
              // Handle binary content
              blob = new Blob([content], { type: 'application/octet-stream' });
            } else {
              throw new Error('Unsupported file content type');
            }
            
            // Create download link
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            
            // Clean up
            setTimeout(() => {
              document.body.removeChild(a);
              URL.revokeObjectURL(url);
            }, 100);
          });
      })
      .catch((error: Error) => {
        console.error('Error downloading file:', error);
        this.showErrorDialog('Download Error', `Failed to download ${fileName}: ${error.message}`);
      });
  }

  /**
   * Download multiple files
   */
  private downloadMultipleFiles(filePaths: string[]): void {
    // Check if all selected items are files, or if there are directories
    let hasDirectories = false;
    let directoryCount = 0;
    
    // Show progress dialog for checking selection
    this.showProgressDialog(
      'Preparing Download',
      'Checking selected items...',
      0
    );
    
    // Use Promise.all to check all paths in parallel
    Promise.all(filePaths.map(path => 
      this.os.getFileSystem().stat(path)
        .then(stats => {
          if (stats.isDirectory) {
            hasDirectories = true;
            directoryCount++;
          }
          return { path, isDirectory: stats.isDirectory };
        })
    ))
    .then(results => {
      // Remove the progress dialog
      this.container?.querySelector('.dialog-overlay')?.remove();
      
      if (hasDirectories) {
        // If there are directories, offer to download as a single zip
        this.showConfirmDialog(
          'Download as Zip',
          `Your selection includes ${directoryCount} folder(s). Do you want to download everything as a single zip file?`,
          () => this.downloadMultipleItemsAsZip(filePaths),
          () => this.downloadFilesOnly(results.filter(item => !item.isDirectory).map(item => item.path))
        );
      } else {
        // If all are files, download them individually
        this.downloadFilesOnly(filePaths);
      }
    })
    .catch(error => {
      console.error('Error checking files:', error);
      this.container?.querySelector('.dialog-overlay')?.remove();
      this.showErrorDialog('Download Error', `Failed to prepare download: ${error.message}`);
    });
  }
  
  /**
   * Download only the files from a selection (skipping directories)
   */
  private downloadFilesOnly(filePaths: string[]): void {
    if (filePaths.length === 0) {
      this.showErrorDialog('Download Error', 'No files to download');
      return;
    }
    
    // For now, download each file separately
    this.showProgressDialog(
      'Downloading Files', 
      `Downloading ${filePaths.length} file(s)...`, 
      0
    );
    
    const progressDialogElement = this.container?.querySelector('.dialog-overlay .progress-value');
    
    // Process each file
    let completed = 0;
    const totalFiles = filePaths.length;
    
    filePaths.forEach(filePath => {
      const fileName = filePath.split('/').pop() || 'download';
      
      // Read the file
      return this.os.getFileSystem().readFile(filePath)
        .then(content => {
          // Create a blob from the content
          let blob;
          
          if (typeof content === 'string') {
            // Handle text content
            blob = new Blob([content], { type: 'application/octet-stream' });
          } else if (<any>content instanceof ArrayBuffer) {
            // Handle binary content
            blob = new Blob([content], { type: 'application/octet-stream' });
          } else {
            throw new Error('Unsupported file content type');
          }
          
          // Create download link
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = fileName;
          document.body.appendChild(a);
          a.click();
          
          // Clean up
          setTimeout(() => {
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
          }, 100);
          
          completed++;
          
          // Update progress
          if (progressDialogElement) {
            const percent = Math.round((completed / totalFiles) * 100);
            progressDialogElement.textContent = `${percent}%`;
            (progressDialogElement.previousElementSibling as HTMLElement).style.width = `${percent}%`;
          }
          
          // If all files are processed, refresh the view and close dialog
          if (completed === totalFiles) {
            this.container?.querySelector('.dialog-overlay')?.remove();
            this.refresh();
          }
        })
        .catch((error: Error) => {
          console.error(`Error downloading file ${fileName}:`, error);
          this.showErrorDialog('Download Error', `Failed to download ${fileName}: ${error.message}`);
          
          completed++;
          if (completed === totalFiles) {
            this.container?.querySelector('.dialog-overlay')?.remove();
            this.refresh();
          }
        });
    });
  }
  /* Show progress dialog
   */
  private showProgressDialog(title: string, message: string, initialProgress: number): void {
 const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">${title}</div>
        <div class="dialog-content">
          <div class="dialog-message">${message}</div>
          <div class="progress-bar">
            <div class="progress-fill" style="width: ${initialProgress}%"></div>
            <div class="progress-value">${initialProgress}%</div>
          </div>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
 }
  /**
   * Download folder as a zip file
   */
  private downloadFolderAsZip(folderPath: string): void {
    const folderName = folderPath.split('/').pop() || 'folder';
    
    // Show progress dialog
    this.showProgressDialog(
      'Creating Zip Archive',
      `Preparing ${folderName} for download...`,
      0
    );
    
    const progressElement = this.container?.querySelector('.dialog-overlay .progress-value');
    const progressFill = this.container?.querySelector('.dialog-overlay .progress-fill') as HTMLElement;
    
    // Create a new JSZip instance
    const zip = new JSZip();
    
    // First, count total files to track progress
    this.countFilesInFolder(folderPath)
      .then(totalFiles => {
        let processedFiles = 0;
        
        // Add the folder to the zip
        return this.addFolderToZip(zip, folderPath, '', () => {
          processedFiles++;
          const percent = Math.round((processedFiles / totalFiles) * 100);
          
          // Update progress
          if (progressElement) {
            progressElement.textContent = `${percent}%`;
            if (progressFill) {
              progressFill.style.width = `${percent}%`;
            }
          }
        });
      })
      .then(() => {
        // Update progress to indicate zip generation
        if (progressElement) {
          progressElement.textContent = 'Generating zip file...';
        }
        
        // Generate the zip file
        return zip.generateAsync({ type: 'blob' });
      })
      .then(blob => {
        // Download the zip file
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${folderName}.zip`;
        document.body.appendChild(a);
        a.click();
        
        // Clean up
        setTimeout(() => {
          document.body.removeChild(a);
          URL.revokeObjectURL(url);
        }, 100);
        
        // Remove progress dialog
        this.container?.querySelector('.dialog-overlay')?.remove();
      })
      .catch((error: Error) => {
        console.error('Error creating zip:', error);
        this.showErrorDialog('Download Error', `Failed to create zip archive: ${error.message}`);
        this.container?.querySelector('.dialog-overlay')?.remove();
      });
  }
  
  /**
   * Download multiple items (files and folders) as a single zip
   */
  private downloadMultipleItemsAsZip(paths: string[]): void {
    // Show progress dialog
    this.showProgressDialog(
      'Creating Zip Archive',
      `Preparing ${paths.length} items for download...`,
      0
    );
    
    const progressElement = this.container?.querySelector('.dialog-overlay .progress-value');
    const progressFill = this.container?.querySelector('.dialog-overlay .progress-fill') as HTMLElement;
    
    // Create a new JSZip instance
    const zip = new JSZip();
    
    // Count total files to track progress
    Promise.all(paths.map(path => 
      this.os.getFileSystem().stat(path)
        .then(stats => {
          if (stats.isDirectory) {
            return this.countFilesInFolder(path);
          }
          return 1; // Count a single file as 1
        })
    ))
    .then(counts => {
      const totalFiles = counts.reduce((a, b) => a + b, 0);
      let processedFiles = 0;
      
      // Process each path
      return Promise.all(paths.map(path => {
        return this.os.getFileSystem().stat(path)
          .then(stats => {
            const name = path.split('/').pop() || 'item';
            
            if (stats.isDirectory) {
              // Add directory to zip
              return this.addFolderToZip(zip, path, name, () => {
                processedFiles++;
                const percent = Math.round((processedFiles / totalFiles) * 100);
                
                // Update progress
                if (progressElement) {
                  progressElement.textContent = `${percent}%`;
                  if (progressFill) {
                    progressFill.style.width = `${percent}%`;
                  }
                }
              });
            } else {
              // Add file to zip
              return this.os.getFileSystem().readFile(path)
                .then(content => {
                  zip.file(name, content);
                  
                  processedFiles++;
                  const percent = Math.round((processedFiles / totalFiles) * 100);
                  
                  // Update progress
                  if (progressElement) {
                    progressElement.textContent = `${percent}%`;
                    if (progressFill) {
                      progressFill.style.width = `${percent}%`;
                    }
                  }
                });
            }
          });
      }));
    })
    .then(() => {
      // Update progress
      if (progressElement) {
        progressElement.textContent = 'Generating zip file...';
      }
      
      // Generate the zip file
      return zip.generateAsync({ type: 'blob' });
    })
    .then(blob => {
      // Download the zip
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'download.zip';
      document.body.appendChild(a);
      a.click();
      
      // Clean up
      setTimeout(() => {
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
      }, 100);
      
      // Remove dialog
      this.container?.querySelector('.dialog-overlay')?.remove();
    })
    .catch((error: Error) => {
      console.error('Error creating zip:', error);
      this.showErrorDialog('Download Error', `Failed to create zip archive: ${error.message}`);
      this.container?.querySelector('.dialog-overlay')?.remove();
    });
  }
  
  /**
   * Count files in a folder (recursively)
   */
  private async countFilesInFolder(folderPath: string): Promise<number> {
    let count = 0;
    
    // Read directory contents
    const entries = await this.os.getFileSystem().readDirectory(folderPath);
    
    // Count each entry
    for (const entry of entries) {
      const entryPath = `${folderPath === '/' ? '' : folderPath}/${entry.name}`;
      
      if (FileEntryUtils.isDirectory(entry)) {
        // Recursively count files in subdirectories
        count += await this.countFilesInFolder(entryPath);
      } else {
        // Count files
        count++;
      }
    }
    
    return count;
  }
  
  /**
   * Recursively add folder contents to a zip file
   */
  private async addFolderToZip(
    zip: JSZip, 
    folderPath: string, 
    zipPath: string,
    onFileProcessed?: () => void
  ): Promise<void> {
    // Read directory contents
    const entries = await this.os.getFileSystem().readDirectory(folderPath);
    
    // Process each entry
    for (const entry of entries) {
      const entryPath = `${folderPath === '/' ? '' : folderPath}/${entry.name}`;
      const entryZipPath = zipPath ? `${zipPath}/${entry.name}` : entry.name;
      
      if (FileEntryUtils.isDirectory(entry)) {
        // Create folder in zip
        zip.folder(entryZipPath);
        
        // Recursively process subdirectories
        await this.addFolderToZip(zip, entryPath, entryZipPath, onFileProcessed);
      } else {
        // Read and add file to zip
        const content = await this.os.getFileSystem().readFile(entryPath);
        zip.file(entryZipPath, content);
        
        // Call progress callback
        if (onFileProcessed) {
          onFileProcessed();
        }
      }
    }
  }
  /**
   * Extract files from a zip file
   */
  private extractZipFile(zipFilePath: string, destinationPath: string): void {
    // Show progress dialog
    this.showProgressDialog(
      'Extracting Zip File',
      `Extracting ${zipFilePath.split('/').pop()}...`,
      0
    );

    const progressElement = this.container?.querySelector('.dialog-overlay .progress-value');
    const progressFill = this.container?.querySelector('.dialog-overlay .progress-fill') as HTMLElement;

    // Read the zip file
    this.os.getFileSystem().readFile(zipFilePath)
      .then(content => {
        if (content.constructor.name !== 'ArrayBuffer') {
          throw new Error('Invalid zip file content');
        }

        // Load the zip content using JSZip
        return JSZip.loadAsync(content);
      })
      .then(zip => {
        const fileNames = Object.keys(zip.files);
        let processedFiles = 0;

        // Extract each file
        return Promise.all(fileNames.map(fileName => {
          const file = zip.files[fileName];

          if (file.dir) {
            // Create directory
            return this.os.getFileSystem().createDirectory(`${destinationPath}/${fileName}`);
          } else {
            // Extract file content
            return file.async('uint8array').then(content => {
              const textContent = new TextDecoder().decode(content);
              return this.os.getFileSystem().writeFile(`${destinationPath}/${fileName}`, textContent);
            });
          }
        }).map(promise => promise.then(() => {
          processedFiles++;
          const percent = Math.round((processedFiles / fileNames.length) * 100);

          // Update progress
          if (progressElement) {
            progressElement.textContent = `${percent}%`;
            if (progressFill) {
              progressFill.style.width = `${percent}%`;
            }
          }
        })));
      })
      .then(() => {
        // Remove progress dialog
        this.container?.querySelector('.dialog-overlay')?.remove();
        this.refresh();
      })
      .catch(error => {
        console.error('Error extracting zip file:', error);
        this.showErrorDialog('Extraction Error', `Failed to extract zip file: ${error.message}`);
        this.container?.querySelector('.dialog-overlay')?.remove();
      });
  }
}

