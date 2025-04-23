/**
 * Start Menu Controller
 * Handles all interactions with the start menu and its submenus
 */
import { createIcons, icons } from 'lucide';
import { OS } from './os';
import { AppInfo } from './app-manager';
export class StartMenuController {
  private startMenuButton: HTMLElement | null;
  private startMenu: HTMLElement | null = null;
  private sidebar: HTMLElement | null = null;
  private menuItem: HTMLElement | null = null;
  private userSubmenu: HTMLElement | null = null;
  private powerSubmenu: HTMLElement | null = null;
  private userItem: HTMLElement | null = null;
  private powerItem: HTMLElement | null = null;
  private appsItem: HTMLElement | null = null;
  private settingsItem: HTMLElement | null = null;
  private pinnedAppsView: HTMLElement | null = null;
  private allAppsView: HTMLElement | null = null;
  private appTiles: HTMLElement[] = [];
  private isStartMenuVisible = false;
  private isSidebarExpanded = false;
  private activeSubmenu: HTMLElement | null = null;

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
  private appTileConfig = [
    { name: 'Terminal', icon: 'terminal', className: 'app-tile-terminal', id: 'terminal' },
    { name: 'Browser', icon: 'globe', className: 'app-tile-browser', id: 'browser' },
    { name: 'Code Editor', icon: 'code', className: 'app-tile-code', id: 'code-editor' },
    { name: 'File Explorer', icon: 'folder-open', className: 'app-tile-files', id: 'file-explorer' },
    { name: 'System Monitor', icon: 'activity', className: 'app-tile-monitor', id: 'system-monitor' },
    { name: 'Mail', icon: 'mail', className: 'app-tile-mail', id: 'mail' },
    { name: 'Shop', icon: 'shopping-cart', className: 'app-tile-shop', id: 'shop' },
    { name: 'Hack Tools', icon: 'terminal-square', className: 'app-tile-hack', id: 'hack-tools' }
  ];

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

  constructor(private os: OS) {
    this.startMenuButton = document.getElementById('start-menu-button');

    // Create DOM elements
    this.createStartMenuElements();

    // Initialize references to created elements
    this.initializeElementReferences();

    // Initialize Lucide icons
    this.initIcons();

    // Populate the All Apps view with applications
    this.populateAllAppsView();

    // Set up event listeners
    this.setupEventListeners();
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
    this.appTileConfig.forEach(app => {
      const tile = document.createElement('div');
      tile.className = `app-tile ${app.className}`;
      tile.setAttribute('data-app-id', app.id); // Store the app ID as a data attribute

      const icon = document.createElement('i');
      icon.setAttribute('data-lucide', app.icon);

      const name = document.createElement('div');
      name.className = 'app-tile-name';
      name.textContent = app.name;

      tile.appendChild(icon);
      tile.appendChild(name);
      pinnedApps.appendChild(tile);
      this.appTiles.push(tile);
    });

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
  /**
   * Initialize references to the created DOM elements
   */
  private initializeElementReferences(): void {
    this.startMenu = document.querySelector('.start-menu');
    this.sidebar = document.querySelector('.start-menu-sidebar');
    this.menuItem = document.getElementById('menu-item');
    this.userSubmenu = document.querySelector('.user-submenu');
    this.powerSubmenu = document.querySelector('.power-submenu');
    this.userItem = document.getElementById('user-item');
    this.powerItem = document.getElementById('power-item');
    this.appsItem = document.getElementById('apps-item');
    this.settingsItem = document.getElementById('settings-item');
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
    });    // App tiles click
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
   */
  private hideStartMenu(): void {
    this.startMenu?.classList.remove('visible');
    this.isStartMenuVisible = false;
    this.startMenuButton?.classList.remove('active');
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
      
      // Add click event
      appItem.addEventListener('click', () => {
        this.launchApp(app.id);
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
   * Launch an application
   */
  private launchApp(appId: string): void {
    console.log(`Launching app: ${appId}`);
    // Here we would integrate with the OS's app manager to launch the actual app
    // For example: this.os.getAppManager().launchApp(appId);
    this.os.getAppManager().launchApp(appId);
    // Close the start menu after launching an app
    this.hideStartMenu();
  }
}
