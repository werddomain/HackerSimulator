/**
 * Theme System Integration
 * This module initializes the theme system and provides integration with the rest of the application
 */
import { OS } from './os';
import { ThemeManager } from './ThemeManager';
import { Theme } from './theme';
import { AppManager } from './app-manager';


export class ThemeSystem {
  private os: OS;
  private themeManager: ThemeManager;
  
  constructor(os: OS) {
    this.os = os;
    this.themeManager = ThemeManager.getInstance(
      os.getFileSystem(),
      os.getUserSettings()
    );
  }
    /**
   * Initialize the theme system
   */
  public async initialize(): Promise<void> {
    console.log('Initializing Theme System...');
    
    // Initialize theme manager
    await this.themeManager.initialize();
    
    // Register events
    this.registerEvents();
   
    console.log('Theme System initialized successfully.');
  }
  
  /**
   * Register events for theme system
   */
  private registerEvents(): void {
    // Listen for theme changed events
    window.addEventListener('themechanged', (event: Event) => {
      if (event instanceof CustomEvent) {
        const theme = event.detail as Theme;
        console.log(`Theme changed to: ${theme.name}`);
      }
    });
    
    // Listen for settings changes
    document.addEventListener('settings-changed', (event: Event) => {
      if (event instanceof CustomEvent) {
        const { key, value } = event.detail;
        if (key === 'theme') {
          // Theme was changed from settings app
          this.themeManager.applyThemeById(value).catch(error => {
            console.error('Error applying theme:', error);
          });
        }
      }
    });
  }
  
  /**
   * Get the theme manager instance
   */
  public getThemeManager(): ThemeManager {
    return this.themeManager;
  }
}
