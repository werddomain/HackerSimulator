import { OS } from '../core/os';
import { FileEntryUtils } from '../core/file-entry-utils';
import { FileSystemUtils } from '../core/file-system-utils';
import { FileSystemEntry } from '../core/filesystem';
import { GuiApplication } from '../core/gui-application';

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
      </div>      <style>
        .file-explorer-app {
          display: flex;
          flex-direction: column;
          height: 100%;
          width: 100%;
          background-color: #1e1e1e;
          color: #d4d4d4;
          font-family: 'Segoe UI', 'Arial', sans-serif;
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          overflow: hidden;
        }
        
        .file-explorer-toolbar {
          display: flex;
          padding: 5px;
          background-color: #252526;
          border-bottom: 1px solid #3c3c3c;
        }
        
        .navigation-buttons {
          display: flex;
          margin-right: 5px;
        }
        
        .navigation-btn {
          background: none;
          border: none;
          color: #d4d4d4;
          padding: 5px 10px;
          cursor: pointer;
          border-radius: 3px;
          font-size: 14px;
        }
        
        .navigation-btn:hover {
          background-color: #3c3c3c;
        }
        
        .navigation-btn:disabled {
          color: #666;
          cursor: not-allowed;
        }
        
        .path-bar {
          flex: 1;
          padding: 0 5px;
        }
        
        .path-input {
          width: 100%;
          background-color: #3c3c3c;
          border: 1px solid #555;
          color: #d4d4d4;
          padding: 5px 10px;
          border-radius: 3px;
        }
        
        .view-options {
          display: flex;
          margin-left: 5px;
        }
        
        .view-btn {
          background: none;
          border: none;
          color: #d4d4d4;
          padding: 5px 8px;
          cursor: pointer;
          border-radius: 3px;
        }
        
        .view-btn:hover {
          background-color: #3c3c3c;
        }
        
        .view-btn.active {
          background-color: #0e639c;
        }
        
        .file-explorer-main {
          display: flex;
          flex: 1;
          overflow: hidden;
        }
        
        .file-explorer-sidebar {
          width: 200px;
          background-color: #252526;
          border-right: 1px solid #3c3c3c;
          padding: 5px;
          overflow-y: auto;
        }
        
        .sidebar-section {
          margin-bottom: 15px;
        }
        
        .sidebar-header {
          font-weight: bold;
          padding: 5px;
          color: #e0e0e0;
          font-size: 14px;
        }
        
        .sidebar-item {
          padding: 5px 5px 5px 15px;
          cursor: pointer;
          border-radius: 3px;
          white-space: nowrap;
          overflow: hidden;
          text-overflow: ellipsis;
        }
        
        .sidebar-item:hover {
          background-color: #2a2d2e;
        }
        
        .sidebar-item.active {
          background-color: #37373d;
        }
        
        .file-explorer-content {
          flex: 1;
          overflow: auto;
          padding: 5px;
        }
        
        .file-list {
          display: flex;
          flex-direction: column;
        }
        
        .file-list.grid {
          display: flex;
          flex-wrap: wrap;
          align-content: flex-start;
        }
        
        .file-item {
          padding: 5px;
          margin: 2px;
          cursor: pointer;
          border-radius: 2px;
          white-space: nowrap;
          overflow: hidden;
          text-overflow: ellipsis;
        }
        
        .file-item:hover {
          background-color: #2a2d2e;
        }
        
        .file-item.selected {
          background-color: #094771;
        }
        
        .file-list.list .file-item {
          display: flex;
          align-items: center;
          width: calc(100% - 4px);
        }
        
        .file-list.grid .file-item {
          display: flex;
          flex-direction: column;
          align-items: center;
          text-align: center;
          width: 100px;
          height: 100px;
          margin: 5px;
          padding: 10px 5px;
        }
        
        .file-icon {
          margin-right: 5px;
          font-size: 16px;
        }
        
        .file-list.grid .file-icon {
          font-size: 32px;
          margin-right: 0;
          margin-bottom: 5px;
        }
        
        .file-name {
          flex: 1;
        }
        
        .file-list.grid .file-name {
          width: 100%;
          overflow: hidden;
          text-overflow: ellipsis;
        }
        
        .file-size {
          color: #a0a0a0;
          margin-left: 10px;
          font-size: 0.9em;
        }
        
        .file-date {
          color: #a0a0a0;
          margin-left: 20px;
          font-size: 0.9em;
        }
        
        .file-list.grid .file-size,
        .file-list.grid .file-date {
          display: none;
        }
        
        .loading-indicator {
          padding: 20px;
          text-align: center;
          color: #a0a0a0;
        }
        
        .empty-folder {
          padding: 30px;
          text-align: center;
          color: #a0a0a0;
          font-style: italic;
        }
        
        .file-explorer-statusbar {
          display: flex;
          justify-content: space-between;
          padding: 5px 10px;
          background-color: #007acc;
          color: white;
          font-size: 12px;
        }
        
        .selection-count {
          margin-left: 10px;
        }
        
        .context-menu {
          position: absolute;
          background-color: #252526;
          border: 1px solid #3c3c3c;
          box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5);
          padding: 5px 0;
          z-index: 1000;
        }
        
        .context-menu-item {
          padding: 5px 20px;
          cursor: pointer;
        }
        
        .context-menu-item:hover {
          background-color: #2a2d2e;
        }
        
        .context-menu-separator {
          height: 1px;
          background-color: #3c3c3c;
          margin: 5px 0;
        }
        
        .file-rename-input {
          background-color: #3c3c3c;
          border: 1px solid #0e639c;
          color: #d4d4d4;
          padding: 2px 5px;
          margin: 0;
          width: calc(100% - 10px);
          box-sizing: border-box;
        }
        
        /* Dialog styles */
        .dialog-overlay {
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background-color: rgba(0, 0, 0, 0.5);
          display: flex;
          justify-content: center;
          align-items: center;
          z-index: 1000;
        }
        
        .dialog {
          background-color: #252526;
          border: 1px solid #3c3c3c;
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
          width: 400px;
          border-radius: 4px;
          overflow: hidden;
        }
        
        .dialog-header {
          background-color: #333;
          padding: 10px 15px;
          font-weight: bold;
          border-bottom: 1px solid #3c3c3c;
        }
        
        .dialog-content {
          padding: 15px;
        }
        
        .dialog-footer {
          padding: 10px 15px;
          text-align: right;
          border-top: 1px solid #3c3c3c;
        }
        
        .dialog-btn {
          padding: 5px 15px;
          margin-left: 10px;
          background-color: #0e639c;
          color: white;
          border: none;
          border-radius: 3px;
          cursor: pointer;
        }
        
        .dialog-btn.cancel {
          background-color: #3c3c3c;
        }
        
        .dialog-btn:hover {
          background-color: #1177bb;
        }
        
        .dialog-btn.cancel:hover {
          background-color: #515151;
        }
        
        .dialog-input {
          width: 100%;
          padding: 5px 10px;
          background-color: #3c3c3c;
          border: 1px solid #555;
          color: #d4d4d4;
          box-sizing: border-box;
          margin: 5px 0;
        }
        
        .dialog-message {
          margin-bottom: 15px;
        }
        
        /* Properties dialog */
        .properties-table {
          width: 100%;
          border-collapse: collapse;
        }
        
        .properties-table td {
          padding: 5px;
          vertical-align: top;
        }
        
        .properties-table td:first-child {
          font-weight: bold;
          width: 100px;
        }
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
      if ((e.target as HTMLElement).classList.contains('file-list')) {
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
  }

  /**
   * Show context menu
   */
  private showContextMenu(event: MouseEvent, fileItem: HTMLElement | null): void {
    const contextMenu = this.container?.querySelector<HTMLElement>('.context-menu');
    if (!contextMenu) return;
    
    // Position the menu
    contextMenu.style.left = `${event.clientX}px`;
    contextMenu.style.top = `${event.clientY}px`;
    contextMenu.style.display = 'block';
    
    // Configure menu items based on context
    const pasteItem = contextMenu.querySelector('[data-action="paste"]');
    if (pasteItem) {
      pasteItem.classList.toggle('disabled', !this.clipboard);
    }
    
    // Setup action handlers
    contextMenu.querySelectorAll('.context-menu-item').forEach(item => {
      item.addEventListener('click', (e) => {
        e.stopPropagation();
        
        const action = (item as HTMLElement).getAttribute('data-action');
        if (!action) return;
        
        this.handleContextMenuAction(action, fileItem);
        this.hideContextMenu();
      });
    });
    
    // Prevent the menu from going off-screen
    const rect = contextMenu.getBoundingClientRect();
    const windowWidth = window.innerWidth;
    const windowHeight = window.innerHeight;
    
    if (rect.right > windowWidth) {
      contextMenu.style.left = `${windowWidth - rect.width - 10}px`;
    }
    
    if (rect.bottom > windowHeight) {
      contextMenu.style.top = `${windowHeight - rect.height - 10}px`;
    }
  }

  /**
   * Hide context menu
   */
  private hideContextMenu(): void {
    const contextMenu = this.container?.querySelector<HTMLElement>('.context-menu');
    if (contextMenu) {
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
        .app-list {
          max-height: 300px;
          overflow-y: auto;
        }
        .app-item {
          padding: 10px;
          cursor: pointer;
          display: flex;
          align-items: center;
          border-radius: 3px;
        }
        .app-item:hover {
          background-color: #2a2d2e;
        }
        .app-item.selected {
          background-color: #0e639c;
        }
        .app-icon {
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
  private showInputDialog(
    title: string, 
    message: string, 
    defaultValue: string,
    onConfirm: (value: string) => void
  ): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">${title}</div>
        <div class="dialog-content">
          <div class="dialog-message">${message}</div>
          <input type="text" class="dialog-input" value="${this.escapeHtml(defaultValue)}">
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn cancel">Cancel</button>
          <button class="dialog-btn confirm">OK</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    // Focus input
    const input = dialog.querySelector<HTMLInputElement>('.dialog-input');
    input?.focus();
    input?.select();
    
    // Button click handlers
    const cancelBtn = dialog.querySelector('.dialog-btn.cancel');
    const confirmBtn = dialog.querySelector('.dialog-btn.confirm');
    
    cancelBtn?.addEventListener('click', () => {
      dialog.remove();
    });
    
    confirmBtn?.addEventListener('click', () => {
      const value = input?.value || '';
      dialog.remove();
      onConfirm(value);
    });
    
    // Handle Enter key
    input?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        const value = input.value || '';
        dialog.remove();
        onConfirm(value);
      }
    });
  }

  /**
   * Show confirm dialog
   */
  private showConfirmDialog(
    title: string, 
    message: string, 
    onConfirm: () => void,
    onCancel?: () => void
  ): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">${title}</div>
        <div class="dialog-content">
          <div class="dialog-message">${message}</div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn cancel">Cancel</button>
          <button class="dialog-btn confirm">OK</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    // Button click handlers
    const cancelBtn = dialog.querySelector('.dialog-btn.cancel');
    const confirmBtn = dialog.querySelector('.dialog-btn.confirm');
    
    cancelBtn?.addEventListener('click', () => {
      dialog.remove();
      if (onCancel) onCancel();
    });
    
    confirmBtn?.addEventListener('click', () => {
      dialog.remove();
      onConfirm();
    });
  }

  /**
   * Show error dialog
   */
  private showErrorDialog(title: string, message: string): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">${title}</div>
        <div class="dialog-content">
          <div class="dialog-message" style="color: #ff6b6b;">${message}</div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn">OK</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    // Button click handler
    const okBtn = dialog.querySelector('.dialog-btn');
    okBtn?.addEventListener('click', () => {
      dialog.remove();
    });
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
}
