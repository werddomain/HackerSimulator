import { OS } from './os';
import { AppInfo } from './app-manager';

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
  }

  /**
   * Create a desktop icon for an app
   */
  private createDesktopIcon(app: AppInfo): void {
    if (!this.desktopIconsContainer) return;

    const iconElement = document.createElement('div');
    iconElement.className = 'desktop-icon';
    iconElement.innerHTML = `
      <div class="desktop-icon-image">${app.icon}</div>
      <div class="desktop-icon-name">${app.name}</div>
    `;
    
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
  }

  /**
   * Refresh desktop icons
   */
  public refreshDesktopIcons(): void {
    this.clearDesktopIcons();
    this.initDesktopIcons();
  }
}
