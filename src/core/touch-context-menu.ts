/**
 * Mobile Context Menu System for HackerSimulator
 * Provides touch-friendly context menus for mobile interfaces
 */

/**
 * Context menu item interface
 */
export interface ContextMenuItem {
  id: string;
  label: string;
  icon?: string;
  action?: () => void;
  disabled?: boolean;
  separator?: boolean;
  submenu?: ContextMenuItem[];
}

/**
 * Context menu options
 */
export interface ContextMenuOptions {
  width?: number;
  maxHeight?: number;
  position?: 'auto' | 'top' | 'bottom' | 'left' | 'right';
  animation?: 'fade' | 'scale' | 'slide';
  closeOnAction?: boolean;
  backdrop?: boolean;
  theme?: string;
  className?: string;
}

/**
 * Default context menu options
 */
const DEFAULT_CONTEXT_MENU_OPTIONS: ContextMenuOptions = {
  width: 220,
  maxHeight: 400,
  position: 'auto',
  animation: 'scale',
  closeOnAction: true,
  backdrop: true
};

/**
 * Touch Context Menu Class
 * Creates and manages touch-friendly context menus
 */
export class TouchContextMenu {
  private element: HTMLElement | null = null;
  private backdropElement: HTMLElement | null = null;
  private items: ContextMenuItem[] = [];
  private options: ContextMenuOptions;
  private isVisible: boolean = false;
  private position: { x: number, y: number } = { x: 0, y: 0 };
  private openSubmenu: TouchContextMenu | null = null;
  private parentMenu: TouchContextMenu | null = null;
  
  // Singleton instance
  private static instance: TouchContextMenu | null = null;
  
  /**
   * Get singleton instance
   */
  public static getInstance(): TouchContextMenu {
    if (!TouchContextMenu.instance) {
      TouchContextMenu.instance = new TouchContextMenu();
    }
    return TouchContextMenu.instance;
  }
  
  /**
   * Constructor
   */
  private constructor() {
    this.options = { ...DEFAULT_CONTEXT_MENU_OPTIONS };
    
    // Create menu element
    this.createMenuElement();
    
    // Add document event listener to close menu on tap outside
    document.addEventListener('click', this.handleDocumentClick.bind(this));
    
    // Add window resize listener
    window.addEventListener('resize', this.handleWindowResize.bind(this));
  }
  
  /**
   * Create the menu element
   */
  private createMenuElement(): void {
    // Create menu container
    this.element = document.createElement('div');
    this.element.className = 'touch-context-menu';
    this.element.setAttribute('role', 'menu');
    this.element.style.display = 'none';
    
    // Add to document body
    document.body.appendChild(this.element);
    
    // Create backdrop element
    this.backdropElement = document.createElement('div');
    this.backdropElement.className = 'touch-context-menu-backdrop';
    this.backdropElement.style.display = 'none';
    
    // Add backdrop click handler
    this.backdropElement.addEventListener('click', () => {
      this.hide();
    });
    
    // Add to document body
    document.body.appendChild(this.backdropElement);
  }
  
  /**
   * Show the context menu at specified position with given items
   */
  public show(
    x: number, 
    y: number, 
    items: ContextMenuItem[], 
    options: Partial<ContextMenuOptions> = {}
  ): void {
    // Update options
    this.options = { ...DEFAULT_CONTEXT_MENU_OPTIONS, ...options };
    
    // Store items
    this.items = items;
    
    // Store position
    this.position = { x, y };
    
    // Create menu content
    this.renderMenu();
    
    // Show menu
    this.element!.style.display = 'block';
    
    // Show backdrop if enabled
    if (this.options.backdrop) {
      this.backdropElement!.style.display = 'block';
    }
    
    // Position the menu
    this.positionMenu();
    
    // Add animation class
    if (this.options.animation) {
      this.element!.classList.add(`animation-${this.options.animation}`);
      
      // Trigger animation
      requestAnimationFrame(() => {
        this.element!.classList.add('menu-visible');
      });
    }
    
    // Mark as visible
    this.isVisible = true;
    
    // Dispatch event
    window.dispatchEvent(new CustomEvent('contextmenuopen'));
  }
  
  /**
   * Hide the context menu
   */
  public hide(): void {
    if (!this.isVisible) return;
    
    // Hide any open submenu first
    if (this.openSubmenu) {
      this.openSubmenu.hide();
      this.openSubmenu = null;
    }
    
    // If this is a submenu, don't hide backdrop
    const hideBackdrop = !this.parentMenu;
    
    // Remove visible class to trigger animation
    if (this.options.animation) {
      this.element!.classList.remove('menu-visible');
      
      // Wait for animation to complete
      setTimeout(() => {
        this.element!.style.display = 'none';
        
        // Hide backdrop if enabled and this is not a submenu
        if (this.options.backdrop && hideBackdrop) {
          this.backdropElement!.style.display = 'none';
        }
      }, 300); // Animation duration
    } else {
      // Hide immediately
      this.element!.style.display = 'none';
      
      // Hide backdrop if enabled and this is not a submenu
      if (this.options.backdrop && hideBackdrop) {
        this.backdropElement!.style.display = 'none';
      }
    }
    
    // Mark as hidden
    this.isVisible = false;
    
    // If this is a submenu, don't dispatch event
    if (!this.parentMenu) {
      // Dispatch event
      window.dispatchEvent(new CustomEvent('contextmenuclose'));
    }
  }
  
  /**
   * Render the menu content
   */
  private renderMenu(): void {
    if (!this.element) return;
    
    // Clear existing content
    this.element.innerHTML = '';
    
    // Apply custom class if specified
    if (this.options.className) {
      this.element.className = `touch-context-menu ${this.options.className}`;
    } else {
      this.element.className = 'touch-context-menu';
    }
    
    // Apply custom theme if specified
    if (this.options.theme) {
      this.element.classList.add(`theme-${this.options.theme}`);
    }
    
    // Create menu items
    const menuList = document.createElement('ul');
    menuList.className = 'menu-items';
    
    // Append each item
    this.items.forEach(item => this.appendMenuItem(menuList, item));
    
    // Add to menu element
    this.element.appendChild(menuList);
    
    // Set width
    if (this.options.width) {
      this.element.style.width = `${this.options.width}px`;
    }
    
    // Set max height
    if (this.options.maxHeight) {
      menuList.style.maxHeight = `${this.options.maxHeight}px`;
    }
  }
  
  /**
   * Append a menu item to the list
   */
  private appendMenuItem(menuList: HTMLElement, item: ContextMenuItem): void {
    // If it's a separator, create a separator element
    if (item.separator) {
      const separator = document.createElement('li');
      separator.className = 'menu-separator';
      menuList.appendChild(separator);
      return;
    }
    
    // Create item element
    const menuItem = document.createElement('li');
    menuItem.className = 'menu-item';
    menuItem.setAttribute('role', 'menuitem');
    menuItem.setAttribute('data-id', item.id);
    
    // Add disabled class if specified
    if (item.disabled) {
      menuItem.classList.add('disabled');
    }
    
    // Add submenu class if it has submenu
    if (item.submenu) {
      menuItem.classList.add('has-submenu');
    }
    
    // Create item content
    const content = document.createElement('div');
    content.className = 'menu-item-content';
    
    // Add icon if specified
    if (item.icon) {
      const icon = document.createElement('span');
      icon.className = 'menu-item-icon';
      
      // Check if it's an HTML element string
      if (item.icon.startsWith('<')) {
        icon.innerHTML = item.icon;
      } else {
        // Assume it's a class name
        icon.innerHTML = `<i class="${item.icon}"></i>`;
      }
      
      content.appendChild(icon);
    }
    
    // Add label
    const label = document.createElement('span');
    label.className = 'menu-item-label';
    label.textContent = item.label;
    content.appendChild(label);
    
    // Add submenu indicator if it has submenu
    if (item.submenu) {
      const submenuIndicator = document.createElement('span');
      submenuIndicator.className = 'submenu-indicator';
      submenuIndicator.innerHTML = '›';
      content.appendChild(submenuIndicator);
    }
    
    // Add content to item
    menuItem.appendChild(content);
    
    // Add click handler
    menuItem.addEventListener('click', (e) => {
      // Don't handle disabled items
      if (item.disabled) {
        e.stopPropagation();
        return;
      }
      
      // Handle submenu
      if (item.submenu) {
        e.stopPropagation();
        this.showSubmenu(item, menuItem);
      } 
      // Handle action
      else if (item.action) {
        e.stopPropagation();
        
        // Call the action
        item.action();
        
        // Close menu if configured to do so
        if (this.options.closeOnAction) {
          // Find the root menu and close it
          let rootMenu: TouchContextMenu = this;
          while (rootMenu.parentMenu) {
            rootMenu = rootMenu.parentMenu;
          }
          rootMenu.hide();
        }
      }
    });
    
    // Add to menu list
    menuList.appendChild(menuItem);
  }
  
  /**
   * Show a submenu for an item
   */
  private showSubmenu(item: ContextMenuItem, parentElement: HTMLElement): void {
    // Hide any existing submenu
    if (this.openSubmenu) {
      this.openSubmenu.hide();
    }
    
    // Create a new submenu instance
    const submenu = new TouchContextMenu();
    submenu.parentMenu = this;
    
    // Get position for submenu
    const rect = parentElement.getBoundingClientRect();
    const x = rect.right;
    const y = rect.top;
    
    // Show submenu
    submenu.show(x, y, item.submenu!, {
      ...this.options,
      backdrop: false // No backdrop for submenu
    });
    
    // Store reference to open submenu
    this.openSubmenu = submenu;
  }
  
  /**
   * Position the menu on screen
   */
  private positionMenu(): void {
    if (!this.element) return;
    
    // Get menu dimensions
    const menuWidth = this.element.offsetWidth;
    const menuHeight = this.element.offsetHeight;
    
    // Get viewport dimensions
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    
    // Determine position based on option or auto-calculate
    let posX = this.position.x;
    let posY = this.position.y;
    
    // Adjust position to keep menu within viewport
    if (this.options.position === 'auto' || !this.options.position) {
      // Adjust horizontal position
      if (posX + menuWidth > viewportWidth) {
        posX = viewportWidth - menuWidth - 10;
      }
      
      // Adjust vertical position
      if (posY + menuHeight > viewportHeight) {
        posY = viewportHeight - menuHeight - 10;
      }
    } 
    // Apply specific positioning
    else {
      switch (this.options.position) {
        case 'top':
          posY = this.position.y - menuHeight;
          break;
        case 'bottom':
          posY = this.position.y;
          break;
        case 'left':
          posX = this.position.x - menuWidth;
          break;
        case 'right':
          posX = this.position.x;
          break;
      }
    }
    
    // Ensure menu stays within viewport bounds
    posX = Math.max(10, Math.min(viewportWidth - menuWidth - 10, posX));
    posY = Math.max(10, Math.min(viewportHeight - menuHeight - 10, posY));
    
    // Apply position
    this.element.style.left = `${posX}px`;
    this.element.style.top = `${posY}px`;
  }
  
  /**
   * Handle document click to close menu when clicking outside
   */
  private handleDocumentClick(event: MouseEvent): void {
    if (!this.isVisible) return;
    
    // Check if click was outside the menu
    const target = event.target as Node;
    if (this.element && !this.element.contains(target)) {
      // If click was inside a submenu, don't close
      if (this.openSubmenu && this.openSubmenu.contains(target)) {
        return;
      }
      
      // If this is a submenu and click was inside parent menu, don't close
      if (this.parentMenu && this.parentMenu.contains(target)) {
        return;
      }
      
      // Close the menu
      this.hide();
    }
  }
  
  /**
   * Check if an element is inside this menu
   */
  private contains(element: Node): boolean {
    return this.element ? this.element.contains(element) : false;
  }
  
  /**
   * Handle window resize to reposition menu
   */
  private handleWindowResize(): void {
    if (this.isVisible) {
      this.positionMenu();
    }
  }
  
  /**
   * Check if menu is currently visible
   */
  public isMenuVisible(): boolean {
    return this.isVisible;
  }
  
  /**
   * Create and show a context menu for a target element
   * Convenience method that handles positioning
   */
  public static showFor(
    element: HTMLElement,
    items: ContextMenuItem[],
    options: Partial<ContextMenuOptions> = {}
  ): TouchContextMenu {
    const menu = TouchContextMenu.getInstance();
    
    // Get element position
    const rect = element.getBoundingClientRect();
    
    // Position in the center of the element by default
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    
    // Show the menu
    menu.show(x, y, items, options);
    
    return menu;
  }
  
  /**
   * Shortcut to hide the current menu
   */
  public static hideMenu(): void {
    const menu = TouchContextMenu.getInstance();
    menu.hide();
  }
  
  /**
   * Create a long-press context menu handler for an element
   */
  public static createLongPressHandler(
    element: HTMLElement,
    items: ContextMenuItem[] | (() => ContextMenuItem[]),
    options: Partial<ContextMenuOptions> = {}
  ): void {
    let pressTimer: number | null = null;
    let pressPosition = { x: 0, y: 0 };
    const longPressThreshold = 500; // ms
    
    // Touch start handler
    const handleTouchStart = (e: TouchEvent) => {
      if (e.touches.length !== 1) return;
      
      // Store position
      pressPosition = {
        x: e.touches[0].clientX,
        y: e.touches[0].clientY
      };
      
      // Start timer
      pressTimer = window.setTimeout(() => {
        pressTimer = null;
        
        // Prevent default context menu
        e.preventDefault();
        
        // Get items (could be function or direct array)
        const menuItems = typeof items === 'function' ? items() : items;
        
        // Show menu at touch position
        TouchContextMenu.getInstance().show(
          pressPosition.x,
          pressPosition.y,
          menuItems,
          options
        );
        
        // Play haptic feedback if available
        if (navigator.vibrate) {
          navigator.vibrate(20);
        }
      }, longPressThreshold);
    };
    
    // Touch end/cancel handler
    const handleTouchEnd = () => {
      // Clear timer
      if (pressTimer) {
        clearTimeout(pressTimer);
        pressTimer = null;
      }
    };
    
    // Touch move handler - cancel if moved too much
    const handleTouchMove = (e: TouchEvent) => {
      if (!pressTimer) return;
      
      // Calculate distance moved
      const dx = e.touches[0].clientX - pressPosition.x;
      const dy = e.touches[0].clientY - pressPosition.y;
      const distance = Math.sqrt(dx * dx + dy * dy);
      
      // If moved more than threshold, cancel long press
      if (distance > 10) {
        clearTimeout(pressTimer);
        pressTimer = null;
      }
    };
    
    // Add handlers
    element.addEventListener('touchstart', handleTouchStart, { passive: false });
    element.addEventListener('touchend', handleTouchEnd);
    element.addEventListener('touchcancel', handleTouchEnd);
    element.addEventListener('touchmove', handleTouchMove);
    
    // Prevent default context menu
    element.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      
      // Get items (could be function or direct array)
      const menuItems = typeof items === 'function' ? items() : items;
      
      // Show menu at mouse position
      TouchContextMenu.getInstance().show(
        e.clientX,
        e.clientY,
        menuItems,
        options
      );
    });
  }
}
