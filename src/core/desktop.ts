import { OS } from './os';
import { AppInfo } from './app-manager';
import { createIcons, icons } from 'lucide';

/**
 * Desktop class for managing the desktop UI and icons
 */
export class Desktop {
  private os: OS;
  private desktopIconsContainer: HTMLElement | null = null;

  constructor(os: OS) {
    this.os = os;
  }

/**
   * Initialize Lucide icons
   */
  private initIcons(): void {
    createIcons({
      icons,
      nameAttr: 'data-lucide'
    });
  }
  /**
   * Initialize the desktop
   */
  public init(): void {
    console.log('Initializing Desktop...');
    
    // Get the desktop icons container
    this.desktopIconsContainer = document.getElementById('desktop-icons');
    if (!this.desktopIconsContainer) {
      console.error('Desktop icons container not found');
      return;
    }
    
    // Initialize desktop icons
    this.initDesktopIcons();
  }

  /**
   * Initialize desktop icons
   */
  private initDesktopIcons(): void {
    if (!this.desktopIconsContainer) return;

    // Get default apps from app manager
    const defaultApps = this.os.getAppManager().getDefaultApps();
    
    // Create icons for each app
    defaultApps.forEach(app => {
      this.createDesktopIcon(app);
    });
    this.initIcons();
  }

  /**
   * Create a desktop icon for an app
   */
  private createDesktopIcon(app: AppInfo): void {
    if (!this.desktopIconsContainer) return;

    const iconElement = document.createElement('div');
    iconElement.className = 'desktop-icon';
    
    const image = document.createElement('div');
    image.className = 'desktop-icon-image';
    
    const i = document.createElement('i');
    this.os.getAppManager().displayIcon(app, i);
    image.appendChild(i);
   
    const name = document.createElement('div');
    name.className = 'desktop-icon-name';
    name.textContent = app.name;

    iconElement.appendChild(image);
    iconElement.appendChild(name);

    // Add click event to launch the app
    iconElement.addEventListener('click', () => {
      this.os.getAppManager().launchApp(app.id);
    });
    
    this.desktopIconsContainer.appendChild(iconElement);
  }

  /**
   * Add a new icon to the desktop
   */
  public addDesktopIcon(app: AppInfo): void {
    this.createDesktopIcon(app);
  }

  /**
   * Clear all desktop icons
   */
  public clearDesktopIcons(): void {
    if (!this.desktopIconsContainer) return;
    this.desktopIconsContainer.innerHTML = '';
  }  /**
   * Show the desktop by minimizing all open windows
   */
  public showDesktop(): void {
    // Get the window manager
    const windowManager = this.os.getWindowManager();
    
    // Minimize all open windows to reveal the desktop
    if (windowManager) {
      // Get all window IDs from the window elements map
      // We need to access the private windowElements map via a cast to any
      const windowElements = (windowManager as any).windowElements as Map<string, HTMLElement>;
      
      // Minimize each window
      if (windowElements) {
        for (const [id] of windowElements) {
          windowManager.minimizeWindow(id);
        }
      }
    }
  }

  /**
   * Refresh desktop icons
   */
  public refreshDesktopIcons(): void {
    this.clearDesktopIcons();
    this.initDesktopIcons();
  }
}
