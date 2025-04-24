import { FileSystem } from './filesystem';
import { PathUtils } from './path-utils';
import { UserSettings } from './UserSettings';
import { Theme, DEFAULT_THEME, THEME_PRESETS } from './theme';

/**
 * Event fired when theme changes
 */
export class ThemeChangedEvent extends Event {
  public readonly theme: Theme;
  
  constructor(theme: Theme) {
    super('themechanged');
    this.theme = theme;
  }
}

/**
 * ThemeManager handles loading, saving, and applying themes
 */
export class ThemeManager {
  private static instance: ThemeManager;
  private fileSystem: FileSystem;
  private userSettings: UserSettings;
  private currentTheme: Theme = DEFAULT_THEME;
  private customThemes: Map<string, Theme> = new Map();
  private themesDirectory = '/etc/themes';
  private readonly themeChangedEvent = new CustomEvent('themechanged');
  
  /**
   * Create a new ThemeManager instance
   * @param fileSystem The file system to use
   * @param userSettings The user settings to use
   */
  private constructor(fileSystem: FileSystem, userSettings: UserSettings) {
    this.fileSystem = fileSystem;
    this.userSettings = userSettings;
  }
  
  /**
   * Get the ThemeManager instance (singleton)
   * @param fileSystem The file system to use
   * @param userSettings The user settings to use
   */
  public static getInstance(fileSystem: FileSystem, userSettings: UserSettings): ThemeManager {
    if (!ThemeManager.instance) {
      ThemeManager.instance = new ThemeManager(fileSystem, userSettings);
    }
    return ThemeManager.instance;
  }
  
  /**
   * Initialize the theme manager
   * This creates the themes directory if it doesn't exist and loads the saved theme
   */
  public async initialize(): Promise<void> {
    // Make sure the themes directory exists
    await this.ensureThemesDirectory();
    
    // Load all custom themes
    await this.loadCustomThemes();
    
    // Load the saved theme
    await this.loadSavedTheme();
    
    // Apply the current theme
    this.applyTheme(this.currentTheme);
  }
  
  /**
   * Apply a theme by ID
   * @param themeId The ID of the theme to apply
   * @returns True if theme was found and applied, false otherwise
   */
  public async applyThemeById(themeId: string): Promise<boolean> {
    // Check preset themes
    if (THEME_PRESETS[themeId]) {
      await this.applyTheme(THEME_PRESETS[themeId]);
      return true;
    }
    
    // Check custom themes
    if (this.customThemes.has(themeId)) {
      await this.applyTheme(this.customThemes.get(themeId)!);
      return true;
    }
    
    return false;
  }
  
  /**
   * Apply a theme
   * @param theme The theme to apply
   */
  public async applyTheme(theme: Theme): Promise<void> {
    this.currentTheme = theme;
    
    // Save the current theme ID to user settings
    await this.userSettings.setPreference('currentThemeId', theme.id);
    
    // Apply CSS variables to document root element
    this.applyCssVariables(theme);
    
    // Dispatch theme changed event
    window.dispatchEvent(new ThemeChangedEvent(theme));
  }
  
  /**
   * Apply CSS variables to the document root element
   * @param theme The theme to apply
   */
  private applyCssVariables(theme: Theme): void {
    const root = document.documentElement;
    
    // Main accent colors
    root.style.setProperty('--accent-color', theme.accentColor);
    root.style.setProperty('--accent-color-light', theme.accentColorLight);
    root.style.setProperty('--accent-color-dark', theme.accentColorDark);
    
    // Theme colors
    root.style.setProperty('--primary-color', theme.primaryColor);
    root.style.setProperty('--secondary-color', theme.secondaryColor);
    root.style.setProperty('--tertiary-color', theme.tertiaryColor);
    
    // Text colors
    root.style.setProperty('--text-color-primary', theme.textColorPrimary);
    root.style.setProperty('--text-color-secondary', theme.textColorSecondary);
    root.style.setProperty('--text-color-disabled', theme.textColorDisabled);
    
    // Status colors
    root.style.setProperty('--success-color', theme.successColor);
    root.style.setProperty('--warning-color', theme.warningColor);
    root.style.setProperty('--error-color', theme.errorColor);
    root.style.setProperty('--info-color', theme.infoColor);
    
    // App tile colors
    root.style.setProperty('--terminal-color', theme.terminalColor);
    root.style.setProperty('--browser-color', theme.browserColor);
    root.style.setProperty('--code-editor-color', theme.codeEditorColor);
    root.style.setProperty('--file-explorer-color', theme.fileExplorerColor);
    root.style.setProperty('--system-monitor-color', theme.systemMonitorColor);
    root.style.setProperty('--mail-color', theme.mailColor);
    root.style.setProperty('--shop-color', theme.shopColor);
    root.style.setProperty('--hack-tools-color', theme.hackToolsColor);
    root.style.setProperty('--settings-color', theme.settingsColor);
    
    // UI component colors
    root.style.setProperty('--window-border-color', theme.windowBorderColor);
    root.style.setProperty('--window-header-color', theme.windowHeaderColor);
    root.style.setProperty('--desktop-bg-color', theme.desktopBgColor);
    root.style.setProperty('--dialog-bg-color', theme.dialogBgColor);
    root.style.setProperty('--dialog-border-color', theme.dialogBorderColor);
    
    // Taskbar colors
    root.style.setProperty('--taskbar-bg-start', theme.taskbarBgStart);
    root.style.setProperty('--taskbar-bg-end', theme.taskbarBgEnd);
    root.style.setProperty('--taskbar-border-top-color', theme.taskbarBorderTopColor);
    root.style.setProperty('--taskbar-shadow', theme.taskbarShadow);
    root.style.setProperty('--taskbar-item-bg', theme.taskbarItemBg);
    root.style.setProperty('--taskbar-item-hover-bg', theme.taskbarItemHoverBg);
    root.style.setProperty('--taskbar-item-active-bg', theme.taskbarItemActiveBg);
    root.style.setProperty('--taskbar-item-active-border', theme.taskbarItemActiveBorder);
    root.style.setProperty('--system-tray-bg', theme.systemTrayBg);
    
    // Start button colors
    root.style.setProperty('--start-button-bg-start', theme.startButtonBgStart);
    root.style.setProperty('--start-button-bg-end', theme.startButtonBgEnd);
    root.style.setProperty('--start-button-hover-bg-start', theme.startButtonHoverBgStart);
    root.style.setProperty('--start-button-hover-bg-end', theme.startButtonHoverBgEnd);
    root.style.setProperty('--start-button-active-bg-start', theme.startButtonActiveBgStart);
    root.style.setProperty('--start-button-active-bg-end', theme.startButtonActiveBgEnd);
    
    // Apply advanced theme options if available
    if (theme.customFonts) {
      if (theme.customFonts.systemFont) {
        root.style.setProperty('--system-font', theme.customFonts.systemFont);
      }
      if (theme.customFonts.monospaceFont) {
        root.style.setProperty('--monospace-font', theme.customFonts.monospaceFont);
      }
      if (theme.customFonts.headerFont) {
        root.style.setProperty('--header-font', theme.customFonts.headerFont);
      }
    }
    
    if (theme.animationSpeed) {
      root.style.setProperty('--animation-speed', theme.animationSpeed.toString());
    }
    
    if (theme.uiElementSizes) {
      if (theme.uiElementSizes.borderRadius) {
        root.style.setProperty('--border-radius', theme.uiElementSizes.borderRadius);
      }
      if (theme.uiElementSizes.buttonHeight) {
        root.style.setProperty('--button-height', theme.uiElementSizes.buttonHeight);
      }
      if (theme.uiElementSizes.inputHeight) {
        root.style.setProperty('--input-height', theme.uiElementSizes.inputHeight);
      }
    }
  }
  
  /**
   * Get the current theme
   * @returns The current theme
   */
  public getCurrentTheme(): Theme {
    return this.currentTheme;
  }
  
  /**
   * Get all available themes (preset and custom)
   * @returns Map of theme IDs to themes
   */
  public getAllThemes(): Map<string, Theme> {
    const allThemes = new Map<string, Theme>();
    
    // Add preset themes
    Object.entries(THEME_PRESETS).forEach(([id, theme]) => {
      allThemes.set(id, theme);
    });
    
    // Add custom themes
    this.customThemes.forEach((theme, id) => {
      allThemes.set(id, theme);
    });
    
    return allThemes;
  }
  
  /**
   * Save a custom theme
   * @param theme The theme to save
   */
  public async saveTheme(theme: Theme): Promise<void> {
    // Make sure the theme has a unique ID
    if (!theme.id) {
      theme.id = `custom-theme-${Date.now()}`;
    }
    
    // Add to custom themes map
    this.customThemes.set(theme.id, theme);
    
    // Save to filesystem
    const themePath = PathUtils.join(this.themesDirectory, `${theme.id}.json`);
    await this.fileSystem.writeFile(themePath, JSON.stringify(theme, null, 2));
    
    // If this is the current theme, apply it
    if (this.currentTheme.id === theme.id) {
      await this.applyTheme(theme);
    }
  }
  
  /**
   * Delete a custom theme
   * @param themeId The ID of the theme to delete
   * @returns True if theme was found and deleted, false otherwise
   */
  public async deleteTheme(themeId: string): Promise<boolean> {
    // Can't delete preset themes
    if (THEME_PRESETS[themeId]) {
      return false;
    }
    
    // Check if theme exists
    if (!this.customThemes.has(themeId)) {
      return false;
    }
    
    // If this is the current theme, switch to default
    if (this.currentTheme.id === themeId) {
      await this.applyTheme(DEFAULT_THEME);
    }
    
    // Remove from custom themes map
    this.customThemes.delete(themeId);
    
    // Delete from filesystem
    const themePath = PathUtils.join(this.themesDirectory, `${themeId}.json`);
    await this.fileSystem.deleteFile(themePath);
    
    return true;
  }
  
  /**
   * Export a theme as JSON text
   * @param themeId The ID of the theme to export
   * @returns The theme as JSON text, or null if not found
   */
  public exportTheme(themeId: string): string | null {
    let theme: Theme | undefined;
    
    // Check preset themes
    if (THEME_PRESETS[themeId]) {
      theme = THEME_PRESETS[themeId];
    } else if (this.customThemes.has(themeId)) {
      theme = this.customThemes.get(themeId);
    }
    
    if (!theme) {
      return null;
    }
    
    return JSON.stringify(theme, null, 2);
  }
  
  /**
   * Import a theme from JSON text
   * @param themeJson The theme as JSON text
   * @returns The imported theme
   */
  public async importTheme(themeJson: string): Promise<Theme> {
    try {
      // Parse theme JSON
      const theme = JSON.parse(themeJson) as Theme;
      
      // Validate theme
      this.validateTheme(theme);
      
      // Make sure the theme has a unique ID
      if (!theme.id) {
        theme.id = `imported-theme-${Date.now()}`;
      } else if (THEME_PRESETS[theme.id] || this.customThemes.has(theme.id)) {
        // If ID already exists, generate a new one
        theme.id = `${theme.id}-${Date.now()}`;
      }
      
      // Save theme
      await this.saveTheme(theme);
      
      return theme;
    } catch (error) {
      console.error('Error importing theme:', error);
      throw new Error('Invalid theme format');
    }
  }
  
  /**
   * Validate a theme
   * @param theme The theme to validate
   * @throws Error if theme is invalid
   */
  private validateTheme(theme: Partial<Theme>): void {
    // Required fields
    const requiredFields: Array<keyof Theme> = [
      'name',
      'accentColor',
      'primaryColor',
      'secondaryColor',
      'textColorPrimary'
    ];
    
    // Check required fields
    for (const field of requiredFields) {
      if (!theme[field]) {
        throw new Error(`Missing required field: ${field}`);
      }
    }
  }
  
  /**
   * Make sure the themes directory exists
   */
  private async ensureThemesDirectory(): Promise<void> {
    try {
      // Check if directory exists
      await this.fileSystem.stat(this.themesDirectory);
    } catch (error) {
      // Directory doesn't exist, create it
      await this.fileSystem.createDirectory(this.themesDirectory, null, true);

    }
  }
  
  /**
   * Load all custom themes from the themes directory
   */
  private async loadCustomThemes(): Promise<void> {
    try {
      // Get all theme files
      const files = await this.fileSystem.listDirectory(this.themesDirectory);
      
      // Load each theme file
      for (const file of files) {
        if (file.name.endsWith('.json')) {
          try {
            const themePath = PathUtils.join(this.themesDirectory, file.name);
            const themeJson = await this.fileSystem.readFile(themePath);
            const theme = JSON.parse(themeJson) as Theme;
            
            // Add to custom themes map
            this.customThemes.set(theme.id, theme);
          } catch (error) {
            console.error(`Error loading theme ${file}:`, error);
          }
        }
      }
    } catch (error) {
      console.error('Error loading custom themes:', error);
    }
  }
  
  /**
   * Load the saved theme from user settings
   */
  private async loadSavedTheme(): Promise<void> {
    try {
      // Get saved theme ID
      const savedThemeId = await this.userSettings.getPreference('currentThemeId', DEFAULT_THEME.id);
      
      // Apply theme by ID
      const success = await this.applyThemeById(savedThemeId);
      
      // If theme not found, use default
      if (!success) {
        await this.applyTheme(DEFAULT_THEME);
      }
    } catch (error) {
      console.error('Error loading saved theme:', error);
      await this.applyTheme(DEFAULT_THEME);
    }
  }
}
