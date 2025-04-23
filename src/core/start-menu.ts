/**
 * Start Menu Controller
 * Handles all interactions with the start menu and its submenus
 */
import { createIcons, icons } from 'lucide';
import { OS } from './os';
import { AppInfo } from './app-manager';

// App tile configuration interface
interface AppTileConfig {
  name: string;
  icon: string;
  className: string;
  id: string;
}

// Context menu option interface
interface ContextMenuOption {
  label: string;
  icon: string;
  action: () => void;
}

export class StartMenuController {
  private startMenuButton: HTMLElement | null;
  private startMenu: HTMLElement | null = null;
  private sidebar: HTMLElement | null = null; private menuItem: HTMLElement | null = null;
  private userSubmenu: HTMLElement | null = null;
  private powerSubmenu: HTMLElement | null = null;
  private userItem: HTMLElement | null = null;
  private powerItem: HTMLElement | null = null;
  private appsItem: HTMLElement | null = null;
  private settingsItem: HTMLElement | null = null;
  private documentsItem: HTMLElement | null = null;
  private imagesItem: HTMLElement | null = null;
  private pinnedAppsView: HTMLElement | null = null;
  private allAppsView: HTMLElement | null = null;
  private appTiles: HTMLElement[] = [];
  private isStartMenuVisible = false;
  private isSidebarExpanded = false;
  private activeSubmenu: HTMLElement | null = null;
  private contextMenu: HTMLElement | null = null;
  private pinnedAppsChanged = false;
  private isReorderingEnabled = false;
  private saveButton: HTMLElement | null = null;

  // Configuration for sidebar items
  private sidebarItems = [
    { id: 'menu-item', icon: 'menu', title: 'Menu' },
    { id: 'user-item', icon: 'user-circle-2', title: 'User Account' },
    { id: 'documents-item', icon: 'folder', title: 'Documents' },
    { id: 'images-item', icon: 'image', title: 'Images' },
    { id: 'settings-item', icon: 'settings', title: 'Settings' },
    { id: 'apps-item', icon: 'layout-list', title: 'All Apps' },
    { id: 'power-item', icon: 'power', title: 'Power', className: 'power-item' }
  ];

  // Configuration for app tiles
  private appTileConfig: AppTileConfig[] = [
    { name: 'Terminal', icon: 'lucide:terminal', className: 'app-tile-terminal', id: 'terminal' },
    { name: 'Browser', icon: 'lucide:globe', className: 'app-tile-browser', id: 'browser' },
    { name: 'Code Editor', icon: 'lucide:code', className: 'app-tile-code', id: 'code-editor' },
    { name: 'File Explorer', icon: 'lucide:folder-open', className: 'app-tile-files', id: 'file-explorer' },
    { name: 'System Monitor', icon: 'lucide:activity', className: 'app-tile-monitor', id: 'system-monitor' },
    { name: 'Mail', icon: 'lucide:mail', className: 'app-tile-mail', id: 'mail' },
    { name: 'Shop', icon: 'lucide:shopping-cart', className: 'app-tile-shop', id: 'shop' },
    { name: 'Hack Tools', icon: 'lucide:terminal-square', className: 'app-tile-hack', id: 'hack-tools' }
  ];
 private appTileConfigDefault: AppTileConfig[] = this.appTileConfig;
 
  // Configuration for user submenu items
  private userSubmenuItems = [
    { icon: 'log-out', label: 'Sign out' },
    { icon: 'users', label: 'Switch user' },
    { icon: 'lock', label: 'Lock' }
  ];

  // Configuration for power submenu items
  private powerSubmenuItems = [
    { icon: 'moon', label: 'Sleep' },
    { icon: 'power-off', label: 'Shutdown' },
    { icon: 'refresh-cw', label: 'Restart' }
  ];
  pinnedAppsEl: HTMLDivElement | null = null;
  constructor(private os: OS) {
    this.startMenuButton = document.getElementById('start-menu-button');

    // Create DOM elements
    this.createStartMenuElements();

    // Initialize references to created elements
    this.initializeElementReferences();

  

    this.os.Ready(() => {
      // Load pinned apps from user settings
      this.loadPinnedApps();

        // Initialize Lucide icons
    this.initIcons();
    });


    // Populate the All Apps view with applications
    this.populateAllAppsView();

    // Set up event listeners
    this.setupEventListeners();
  }

  /**
   * Load pinned apps from user settings
   */
  private async loadPinnedApps(): Promise<void> {
    try {
      const userSettings = this.os.getUserSettings();
      if (!userSettings) console.error('User settings not found');
      if (userSettings) {
        const pinnedAppIds = await userSettings.getStartMenuPinnedApps();

        // If we have saved pinned apps, update the appTileConfig
        if (pinnedAppIds.length > 0) {
          // Get all available apps
          const allApps = this.os.getAppManager().getAllApps();
          const newTileConfig: AppTileConfig[] = [];

          // Add apps in the order they were saved
          for (const appId of pinnedAppIds) {
            const appInfo = allApps.find(app => app.id === appId);
            if (appInfo) {
              // Find if there's a matching default config to get the className
              const defaultConfig = this.appTileConfig.find(config => config.id === appId);
              const className = defaultConfig ? defaultConfig.className : `app-tile-${appId}`;

              newTileConfig.push({
                name: appInfo.name,
                icon: appInfo.icon || 'app',
                className: className,
                id: appId
              });
            }
          }

          // If we have pinned apps, use them, otherwise keep the default
          if (newTileConfig.length > 0) {
            this.appTileConfig = newTileConfig;
            this.buildPinedApps();
          }
        }
      }
    } catch (error) {
      console.error('Error loading pinned apps:', error);
    }
  }

  /**
   * Create all Start Menu DOM elements
   */
  private createStartMenuElements(): void {
    const desktop = document.getElementById('desktop');
    if (!desktop) {
      console.error('Desktop element not found');
      return;
    }

    // Create Start Menu
    const startMenu = document.createElement('div');
    startMenu.className = 'start-menu';

    // Create sidebar
    const sidebar = document.createElement('div');
    sidebar.className = 'start-menu-sidebar';
    // Add sidebar items
    this.sidebarItems.forEach(item => {
      const sidebarItem = document.createElement('div');
      sidebarItem.className = 'sidebar-item';
      if (item.className) {
        sidebarItem.classList.add(item.className);
      }
      sidebarItem.id = item.id;
      sidebarItem.title = item.title;

      const icon = document.createElement('i');
      icon.setAttribute('data-lucide', item.icon);

      // Add a label for each sidebar item
      const label = document.createElement('span');
      label.className = 'sidebar-item-label';
      label.textContent = item.title;

      sidebarItem.appendChild(icon);
      sidebarItem.appendChild(label);
      sidebar.appendChild(sidebarItem);
    });

    // Create content area
    const content = document.createElement('div');
    content.className = 'start-menu-content';

    // Create search bar
    const searchBar = document.createElement('div');
    searchBar.className = 'search-bar';

    const searchInput = document.createElement('input');
    searchInput.type = 'text';
    searchInput.placeholder = 'Search...';

    searchBar.appendChild(searchInput);
    content.appendChild(searchBar);

    // Create pinned apps view
    const pinnedAppsView = document.createElement('div');
    pinnedAppsView.className = 'content-view pinned-apps-view active';
    const pinnedApps = document.createElement('div');
    pinnedApps.className = 'pinned-apps';    // Add app tiles
    this.pinnedAppsEl = pinnedApps;
    this.buildPinedApps();

    pinnedAppsView.appendChild(pinnedApps);
    content.appendChild(pinnedAppsView);

    // Create all apps view
    const allAppsView = document.createElement('div');
    allAppsView.className = 'content-view all-apps-view';

    const allAppsContent = document.createElement('div');
    allAppsContent.className = 'all-apps-content';

    // We'll populate this dynamically after initialization
    // This will be populated with actual apps in populateAllAppsView method

    allAppsView.appendChild(allAppsContent);
    content.appendChild(allAppsView);

    // Add sidebar and content to start menu
    startMenu.appendChild(sidebar);
    startMenu.appendChild(content);

    // Create user submenu
    const userSubmenu = document.createElement('div');
    userSubmenu.className = 'submenu user-submenu';

    this.userSubmenuItems.forEach(item => {
      const submenuItem = document.createElement('div');
      submenuItem.className = 'submenu-item';

      const icon = document.createElement('i');
      icon.setAttribute('data-lucide', item.icon);

      const label = document.createElement('span');
      label.textContent = item.label;

      submenuItem.appendChild(icon);
      submenuItem.appendChild(label);
      userSubmenu.appendChild(submenuItem);
    });

    // Create power submenu
    const powerSubmenu = document.createElement('div');
    powerSubmenu.className = 'submenu power-submenu';

    this.powerSubmenuItems.forEach(item => {
      const submenuItem = document.createElement('div');
      submenuItem.className = 'submenu-item';

      const icon = document.createElement('i');
      icon.setAttribute('data-lucide', item.icon);

      const label = document.createElement('span');
      label.textContent = item.label;

      submenuItem.appendChild(icon);
      submenuItem.appendChild(label);
      powerSubmenu.appendChild(submenuItem);
    });
    // Add all elements to the desktop
    startMenu.appendChild(userSubmenu);
    startMenu.appendChild(powerSubmenu);
    desktop.appendChild(startMenu);
  }
  buildPinedApps() {
    if (!this.pinnedAppsEl) return;
    this.pinnedAppsEl.innerHTML = ''; // Clear existing pinned apps

    this.appTileConfig.forEach(app => {
      const tile = document.createElement('div');
      tile.className = `app-tile ${app.className}`;
      tile.setAttribute('data-app-id', app.id); // Store the app ID as a data attribute

      const icon = document.createElement('i');
      this.os.getAppManager().displayIcon(app.icon, icon);

      const name = document.createElement('div');
      name.className = 'app-tile-name';
      name.textContent = app.name;

      tile.appendChild(icon);
      tile.appendChild(name);
      if (!this.pinnedAppsEl) return;

      this.pinnedAppsEl.appendChild(tile);
      this.appTiles.push(tile);

      // Add click and context menu event listeners to the tile
      this.setupAppTileEvents(tile);
    });
  }
  /**
   * Initialize references to the created DOM elements
   */
  private initializeElementReferences(): void {
    this.startMenu = document.querySelector('.start-menu');
    this.sidebar = document.querySelector('.start-menu-sidebar');
    this.menuItem = document.getElementById('menu-item');
    this.userSubmenu = document.querySelector('.user-submenu');
    this.powerSubmenu = document.querySelector('.power-submenu'); this.userItem = document.getElementById('user-item');
    this.powerItem = document.getElementById('power-item');
    this.appsItem = document.getElementById('apps-item');
    this.settingsItem = document.getElementById('settings-item');
    this.documentsItem = document.getElementById('documents-item');
    this.imagesItem = document.getElementById('images-item');
    this.pinnedAppsView = document.querySelector('.pinned-apps-view');
    this.allAppsView = document.querySelector('.all-apps-view');
  }/**
   * Initialize Lucide icons
   */
  private initIcons(): void {
    createIcons({
      icons,
      nameAttr: 'data-lucide'
    });
  }
  /**
   * Set up all event listeners
   */
  private setupEventListeners(): void {
    // Start button click
    this.startMenuButton?.addEventListener('click', () => {
      this.toggleStartMenu();
    });

    // Menu item click - toggle sidebar expansion
    this.menuItem?.addEventListener('click', (e) => {
      e.stopPropagation();
      this.toggleSidebar();
    });

    // User account item click
    this.userItem?.addEventListener('click', (e) => {
      e.stopPropagation();
      this.toggleSubmenu(this.userSubmenu, this.userItem);
    });

    // Power item click
    this.powerItem?.addEventListener('click', (e) => {
      e.stopPropagation();
      this.toggleSubmenu(this.powerSubmenu, this.powerItem);
    });

    // Apps item click
    this.appsItem?.addEventListener('click', () => {
      this.toggleAppsView();
    });    // Settings item click
    this.settingsItem?.addEventListener('click', () => {
      this.launchApp('settings');
    });

    // Documents item click
    this.documentsItem?.addEventListener('click', () => {
      this.openFileExplorerWithPath(this.os.getFileSystem().SpecialFolders.documents);
    });

    // Images item click
    this.imagesItem?.addEventListener('click', () => {
      this.openFileExplorerWithPath(this.os.getFileSystem().SpecialFolders.pictures);
    });

    // App tiles click
    this.appTiles.forEach(tile => {
      tile.addEventListener('click', () => {
        const appId = tile.getAttribute('data-app-id');
        if (appId) {
          this.launchApp(appId);
        }
      });
    });

    // Close menu when clicking outside
    document.addEventListener('click', (e) => {
      if (this.isStartMenuVisible) {
        const target = e.target as HTMLElement;
        const isClickInsideStartMenu = this.startMenu?.contains(target) || false;
        const isClickOnStartButton = this.startMenuButton?.contains(target) || false;
        const isClickInsideUserSubmenu = this.userSubmenu?.contains(target) || false;
        const isClickOnUserItem = this.userItem?.contains(target) || false;
        const isClickInsidePowerSubmenu = this.powerSubmenu?.contains(target) || false;
        const isClickOnPowerItem = this.powerItem?.contains(target) || false;

        if (!isClickInsideStartMenu && !isClickOnStartButton &&
          !isClickInsideUserSubmenu && !isClickOnUserItem &&
          !isClickInsidePowerSubmenu && !isClickOnPowerItem) {
          this.hideStartMenu();
        }
      }
    });

    // Handle submenu items
    const submenuItems = document.querySelectorAll('.submenu-item');
    submenuItems.forEach(item => {
      item.addEventListener('click', () => {
        // Action would be implemented here (like sign out, shutdown, etc.)
        this.hideStartMenu();
      });
    });
  }
  /**
   * Toggle sidebar expanded state
   */
  private toggleSidebar(): void {
    this.isSidebarExpanded = !this.isSidebarExpanded;

    if (this.isSidebarExpanded) {
      this.sidebar?.classList.add('expanded');
      this.menuItem?.classList.add('active');
    } else {
      this.sidebar?.classList.remove('expanded');
      this.menuItem?.classList.remove('active');
    }
  }

  /**
   * Toggle the start menu visibility
   */
  private toggleStartMenu(): void {
    if (this.isStartMenuVisible) {
      this.hideStartMenu();
    } else {
      this.showStartMenu();
    }
  }
  /**
   * Show the start menu
   */
  private showStartMenu(): void {
    // Populate the All Apps view with the latest applications
    this.populateAllAppsView();

    this.startMenu?.classList.add('visible');
    this.isStartMenuVisible = true;
    this.startMenuButton?.classList.add('active');
  }
  /**
   * Hide the start menu and any open submenus
   */  private hideStartMenu(): void {
    this.startMenu?.classList.remove('visible');
    this.isStartMenuVisible = false;
    this.startMenuButton?.classList.remove('active');

    // If reordering is enabled, disable it when hiding the start menu
    if (this.isReorderingEnabled) {
      this.disableReorderMode();
    }

    // If pinned apps were changed, save them automatically
    if (this.pinnedAppsChanged) {
      this.savePinnedApps();
    }
    
    this.hideAllSubmenus();
  }
  /**
   * Toggle a submenu's visibility
   */
  private toggleSubmenu(submenu: HTMLElement | null, trigger: HTMLElement | null): void {
    if (!submenu || !trigger) return;

    // If this submenu is already visible, hide it
    if (submenu === this.activeSubmenu) {
      submenu.classList.remove('visible');
      trigger.classList.remove('active');
      this.activeSubmenu = null;
    }
    // Otherwise, hide the current submenu (if any) and show this one
    else {
      this.hideAllSubmenus();

      // Position the submenu before showing it
      this.positionSubmenu(submenu, trigger);

      submenu.classList.add('visible');
      trigger.classList.add('active');
      this.activeSubmenu = submenu;
    }
  }

  /**
   * Hide all submenus
   */
  private hideAllSubmenus(): void {
    this.userSubmenu?.classList.remove('visible');
    this.powerSubmenu?.classList.remove('visible');
    this.userItem?.classList.remove('active');
    this.powerItem?.classList.remove('active');
    this.activeSubmenu = null;
  }  /**
   * Position a submenu relative to its triggering button
   * This ensures the submenu is properly aligned with the button that opened it
   */
  private positionSubmenu(submenu: HTMLElement, trigger: HTMLElement): void {
    if (!submenu || !trigger || !this.startMenu) return;

    const buttonRect = trigger.getBoundingClientRect();
    const startMenuRect = this.startMenu.getBoundingClientRect();
    const sidebarExpanded = this.isSidebarExpanded;

    // Calculate position relative to the start menu
    const leftOffset = sidebarExpanded ? 208 : 58; // Match the CSS values

    // For user submenu - align with top of the button
    if (submenu === this.userSubmenu) {
      // Calculate relative position to start menu
      const topPosition = buttonRect.top - startMenuRect.top;
      submenu.style.top = `${topPosition}px`;
      submenu.style.left = `${leftOffset}px`;
    }
    // For power submenu - align with bottom of the button, but use fixed positioning
    else if (submenu === this.powerSubmenu) {
      // Use a fixed position from the bottom instead of trying to calculate from the top
      // This ensures the power menu is always at the same position
      submenu.style.bottom = '5px'; // Fixed position from bottom
      submenu.style.top = 'auto';    // Remove any top positioning
      submenu.style.left = `${leftOffset}px`;
    }
  }

  /**
   * Toggle between pinned apps and all apps views
   */
  private toggleAppsView(): void {
    const isPinnedViewActive = this.pinnedAppsView?.classList.contains('active') || false;

    if (isPinnedViewActive) {
      this.pinnedAppsView?.classList.remove('active');
      this.allAppsView?.classList.add('active');
      this.appsItem?.classList.add('active');
    } else {
      this.allAppsView?.classList.remove('active');
      this.pinnedAppsView?.classList.add('active');
      this.appsItem?.classList.remove('active');
    }
  }
  private getAllAppsApplications(): AppInfo[] {
    return this.os.getAppManager().getAllApps();
  }

  /**
   * Populate the All Apps view with applications from the AppManager
   */
  private populateAllAppsView(): void {
    if (!this.startMenu) return;

    const allAppsContent = this.startMenu.querySelector('.all-apps-content');
    if (!allAppsContent) return;

    // Clear existing content
    allAppsContent.innerHTML = '';

    // Get all applications
    const apps = this.getAllAppsApplications();

    // Sort apps alphabetically by name
    apps.sort((a, b) => a.name.localeCompare(b.name));

    // Create app list
    const appList = document.createElement('div');
    appList.className = 'app-list';

    // Add apps to the list
    apps.forEach(app => {
      const appItem = document.createElement('div');
      appItem.className = 'app-list-item';
      appItem.setAttribute('data-app-id', app.id);

      // Create app icon
      const iconContainer = document.createElement('div');
      iconContainer.className = 'app-icon';

      // display the icon using the OS's app manager
      this.os.getAppManager().displayIcon(app, iconContainer);

      // Create app name
      const nameElement = document.createElement('div');
      nameElement.className = 'app-name';
      nameElement.textContent = app.name;
      // Add elements to app item
      appItem.appendChild(iconContainer);
      appItem.appendChild(nameElement);

      // Set up click and context menu events
      appItem.addEventListener('click', () => {
        this.launchApp(app.id);
      });

      // Add right-click context menu
      appItem.addEventListener('contextmenu', (event) => {
        // Prevent default context menu
        event.preventDefault();

        // Create context menu options
        const isPinned = this.appTileConfig.some(config => config.id === app.id);
        const options: ContextMenuOption[] = [
          {
            label: isPinned ? 'Unpin from Start' : 'Pin to Start',
            icon: isPinned ? 'x' : 'pin',
            action: () => isPinned ? this.unpinApp(app.id) : this.pinApp(app.id)
          },
          {
            label: 'Launch',
            icon: 'play',
            action: () => this.launchApp(app.id)
          }
        ];

        // Show context menu
        this.showContextMenu(event, options, appItem);
      });

      appList.appendChild(appItem);
    });

    allAppsContent.appendChild(appList);

    // Initialize Lucide icons for the newly added elements
    createIcons({
      icons,
      nameAttr: 'data-lucide'
    });
  }
  /**
   * Launch an application by ID
   */
  private launchApp(appId: string): void {
    // Hide the start menu
    this.hideStartMenu();

    // Launch the app through the OS
    this.os.getAppManager().launchApp(appId);
  }
  /**
   * Open file explorer with a specific path
   * @param path The path to open in the file explorer
   */
  private openFileExplorerWithPath(path: string): void {
    console.log(`Opening file explorer with path: ${path}`);
    // Launch file explorer with the specified path
    this.os.getAppManager().launchApp('file-explorer', [path]);
    // Close the start menu after launching
    this.hideStartMenu();
  }
  /**
   * Save pinned apps to user settings
   */
  private async savePinnedApps(): Promise<void> {
    try {
      const userSettings = this.os.getUserSettings();
      if (userSettings) {
        // Extract app IDs from the current config in order
        const appIds = this.appTileConfig.map(app => app.id);
        await userSettings.setStartMenuPinnedApps(appIds);
        this.pinnedAppsChanged = false;
        console.log('Pinned apps saved:', appIds.join(','));
      }
    } catch (error) {
      console.error('Error saving pinned apps:', error);
    }
  }

  /**
   * Show context menu at the specified position
   */
  private showContextMenu(event: MouseEvent, options: ContextMenuOption[], element: HTMLElement): void {
    // Prevent default right-click menu
    event.preventDefault();

    // Hide any existing context menu
    this.hideContextMenu();

    // Create new context menu
    this.contextMenu = document.createElement('div');
    this.contextMenu.className = 'start-menu-context-menu';

    // Add each option to the menu
    options.forEach(option => {
      const optionElement = document.createElement('div');
      optionElement.className = 'context-menu-option';

      // Create icon
      const icon = document.createElement('i');
      icon.setAttribute('data-lucide', option.icon);

      // Create label
      const label = document.createElement('span');
      label.textContent = option.label;
      // Add click handler
      optionElement.addEventListener('click', (e) => {
        // Stop propagation to prevent the document click handler from closing the start menu
        e.stopPropagation();
        option.action();
        this.hideContextMenu();
      });

      // Add elements to option
      optionElement.appendChild(icon);
      optionElement.appendChild(label);
      this.contextMenu!.appendChild(optionElement);
    });

    // Position the context menu where the click happened
    this.contextMenu.style.left = `${event.clientX}px`;
    this.contextMenu.style.top = `${event.clientY}px`;

    // Add to document body
    document.body.appendChild(this.contextMenu);

    // Initialize icons for the context menu
    createIcons({
      icons,
      nameAttr: 'data-lucide'
    });

    // Add event to hide the context menu when clicking outside
    setTimeout(() => {
      document.addEventListener('click', this.handleContextMenuClickOutside);
    }, 0);
  }

  /**
   * Handle click outside the context menu
   */
  private handleContextMenuClickOutside = (event: MouseEvent): void => {
    if (this.contextMenu && !this.contextMenu.contains(event.target as Node)) {
      this.hideContextMenu();
    }
  };

  /**
   * Hide the context menu
   */
  private hideContextMenu(): void {
    if (this.contextMenu) {
      document.body.removeChild(this.contextMenu);
      this.contextMenu = null;
      document.removeEventListener('click', this.handleContextMenuClickOutside);
    }
  }
  /**
   * Enable reordering mode for pinned apps
   */
  private enableReorderMode(): void {
    if (this.pinnedAppsView) {
      this.isReorderingEnabled = true;
      this.pinnedAppsView.classList.add('reorder-mode');

      // Show the save button when entering reorder mode
      this.pinnedAppsChanged = true;
      this.updateSaveButton();

      // Add drag and drop functionality
      const tiles = this.pinnedAppsView.querySelectorAll('.app-tile');
      tiles.forEach(tile => {
        tile.setAttribute('draggable', 'true');
        tile.addEventListener('dragstart', this.handleDragStart);
        tile.addEventListener('dragover', this.handleDragOver);
        tile.addEventListener('drop', this.handleDrop);
        tile.addEventListener('dragend', this.handleDragEnd);
      });
    }
  }
  /**
   * Disable reordering mode for pinned apps
   */
  private disableReorderMode(): void {
    if (this.pinnedAppsView) {
      this.isReorderingEnabled = false;
      this.pinnedAppsView.classList.remove('reorder-mode');

      // Hide the save button when exiting reorder mode
      if (this.saveButton) {
        this.saveButton.classList.remove('visible');
      }

      // Remove drag and drop functionality
      const tiles = this.pinnedAppsView.querySelectorAll('.app-tile');
      tiles.forEach(tile => {
        tile.removeAttribute('draggable');
        tile.removeEventListener('dragstart', this.handleDragStart);
        tile.removeEventListener('dragover', this.handleDragOver);
        tile.removeEventListener('drop', this.handleDrop);
        tile.removeEventListener('dragend', this.handleDragEnd);
      });
    }
  }
  /**
 * Refresh the pinned apps view with current configuration
 */
  private refreshPinnedAppsView(): void {
    // Clear existing pinned apps
    if (!this.pinnedAppsView) return;

    const pinnedApps = this.pinnedAppsView.querySelector('.pinned-apps');
    if (!pinnedApps) return;

    // Clear existing tiles
    pinnedApps.innerHTML = '';
    this.appTiles = [];    // Add app tiles based on current configuration
    this.appTileConfig.forEach(app => {
      const tile = document.createElement('div');
      tile.className = `app-tile ${app.className}`;
      tile.setAttribute('data-app-id', app.id);

      const icon = document.createElement('i');
      this.os.getAppManager().displayIcon(app.icon, icon);

      const name = document.createElement('div');
      name.className = 'app-tile-name';
      name.textContent = app.name;

      tile.appendChild(icon);
      tile.appendChild(name);
      pinnedApps.appendChild(tile);
      this.appTiles.push(tile);

      // Add click and contextmenu event listeners
      this.setupAppTileEvents(tile);
    });

    // Re-initialize Lucide icons
    createIcons({
      icons,
      nameAttr: 'data-lucide'
    });

    // Re-enable reordering if it was enabled
    if (this.isReorderingEnabled) {
      this.enableReorderMode();
    }

    // Show save button if changes were made
    this.updateSaveButton();
  }

  /**
   * Update save button visibility based on whether changes were made
   */
  private updateSaveButton(): void {
    if (!this.saveButton) {
      this.createSaveButton();
    }

    if (this.pinnedAppsChanged) {
      this.saveButton?.classList.add('visible');
    } else {
      this.saveButton?.classList.remove('visible');
    }
  }

  /**
   * Create the save button for pinned apps
   */
  private createSaveButton(): void {
    if (this.saveButton) return;

    this.saveButton = document.createElement('button');
    this.saveButton.className = 'save-pinned-apps-button';
    this.saveButton.title = 'Save pinned apps';

    const icon = document.createElement('i');
    icon.setAttribute('data-lucide', 'save');
    this.saveButton.appendChild(icon); this.saveButton.addEventListener('click', async (e) => {
      // Stop propagation to prevent the document click handler from closing the start menu
      e.stopPropagation();

      // Explicitly set pinnedAppsChanged to false immediately
      this.pinnedAppsChanged = false;

      // Update the save button visibility first
      this.updateSaveButton();

      // Disable reorder mode
      this.disableReorderMode();

      // Then save pinned apps (async operation)
      await this.savePinnedApps();
    });

    // Add to pinned apps view
    this.pinnedAppsView?.appendChild(this.saveButton);

    // Initialize icon
    createIcons({
      icons,
      nameAttr: 'data-lucide'
    });
  }

  // Drag and drop handlers
  private handleDragStart = (e: Event): void => {
    const event = e as DragEvent;
    if (event.dataTransfer && event.target instanceof HTMLElement) {
      event.dataTransfer.setData('text/plain', event.target.getAttribute('data-app-id') || '');
      event.dataTransfer.effectAllowed = 'move';
      event.target.classList.add('dragging');
    }
  };

  private handleDragOver = (e: Event): void => {
    const event = e as DragEvent;
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  };

  private handleDrop = (e: Event): void => {
    const event = e as DragEvent;
    event.preventDefault();
    if (event.dataTransfer && event.target instanceof HTMLElement) {
      const draggedAppId = event.dataTransfer.getData('text/plain');
      const targetTile = event.target.closest('.app-tile');
      const targetAppId = targetTile?.getAttribute('data-app-id') || '';

      if (draggedAppId && targetAppId && draggedAppId !== targetAppId) {
        // Get the indices of the dragged and target apps
        const draggedIndex = this.appTileConfig.findIndex(app => app.id === draggedAppId);
        const targetIndex = this.appTileConfig.findIndex(app => app.id === targetAppId);

        if (draggedIndex !== -1 && targetIndex !== -1) {
          // Reorder the app tile config
          const [draggedApp] = this.appTileConfig.splice(draggedIndex, 1);
          this.appTileConfig.splice(targetIndex, 0, draggedApp);

          // Mark as changed
          this.pinnedAppsChanged = true;

          // Refresh the pinned apps view
          this.refreshPinnedAppsView();
        }
      }
    }
  };

  private handleDragEnd = (e: Event): void => {
    const event = e as DragEvent;
    if (event.target instanceof HTMLElement) {
      event.target.classList.remove('dragging');
    }
  };
  /**
   * Set up events for app tiles (click and context menu)
   */
  private setupAppTileEvents(tile: HTMLElement): void {
    // Add click event to launch the app
    tile.addEventListener('click', () => {
      const appId = tile.getAttribute('data-app-id');
      if (appId) {
        this.launchApp(appId);
      }
    });

    // Add right-click event for context menu
    tile.addEventListener('contextmenu', (event) => {
      const appId = tile.getAttribute('data-app-id');
      if (!appId) return;

      // Create context menu options based on whether we're in pinned or all apps view
      const isPinnedApp = tile.closest('.pinned-apps-view') !== null;
      const options: ContextMenuOption[] = [];

      if (isPinnedApp) {
        // Options for pinned apps
        options.push({
          label: 'Unpin from Start',
          icon: 'x',
          action: () => this.unpinApp(appId)
        });

        options.push({
          label: this.isReorderingEnabled ? 'Disable reordering' : 'Enable reordering',
          icon: this.isReorderingEnabled ? 'lock' : 'move',
          action: () => this.toggleReorderMode()
        });
      } else {
        // Options for all apps
        options.push({
          label: 'Pin to Start',
          icon: 'pin',
          action: () => this.pinApp(appId)
        });
      }

      // Add launch option for all apps
      options.push({
        label: 'Launch',
        icon: 'play',
        action: () => this.launchApp(appId)
      });

      // Show the context menu
      this.showContextMenu(event, options, tile);
    });
  }

  /**
   * Toggle reordering mode
   */
  private toggleReorderMode(): void {
    if (this.isReorderingEnabled) {
      this.disableReorderMode();
    } else {
      this.enableReorderMode();
    }
  }  /**
 * Pin an app to the start menu
 */
  private pinApp(appId: string): void {
    // Check if the app is already pinned
    const isPinned = this.appTileConfig.some(app => app.id === appId);
    if (isPinned) return;

    // Get the app info from the OS
    const allApps = this.os.getAppManager().getAllApps();
    const appInfo = allApps.find(app => app.id === appId);
    if (!appInfo) return;

    // Create a new app tile config
    const className = `app-tile-${appId}`;
    const newAppTile: AppTileConfig = {
      name: appInfo.name,
      icon: appInfo.icon || 'app',
      className: className,
      id: appId
    };

    // Add to pinned apps
    this.appTileConfig.push(newAppTile);
    
    // Save changes immediately instead of showing the save button
    this.savePinnedApps();

    // Refresh the view
    this.refreshPinnedAppsView();
  }
  /**
   * Unpin an app from the start menu
   */
  private unpinApp(appId: string): void {
    const index = this.appTileConfig.findIndex(app => app.id === appId);
    if (index === -1) return;

    // Remove from pinned apps
    this.appTileConfig.splice(index, 1);
    
    // Save changes immediately instead of showing the save button
    this.savePinnedApps();

    // Refresh the view
    this.refreshPinnedAppsView();
  }
}
