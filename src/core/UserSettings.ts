import { FileSystem } from './filesystem';
import { BaseSettings } from './BaseSettings';
import { PathUtils } from './path-utils';

/**
 * UserSettings handles user-specific settings stored in the user's home directory
 * similar to how Linux stores user configuration files.
 */
export class UserSettings extends BaseSettings {
  /**
   * Create a new UserSettings instance
   * @param fs The filesystem to use
   */
  constructor(fs: FileSystem) {
    // Use ~/.config as the base path for user settings (Linux convention)
    super(fs, '/home/user/.config');
  }
  
  /**
   * Get a user preference
   * @param key The preference key
   * @param defaultValue Default value if preference doesn't exist
   */
  public async getPreference(key: string, defaultValue?: any): Promise<any> {
    return this.get('user-preferences', key, defaultValue);
  }
  
  /**
   * Set a user preference
   * @param key The preference key
   * @param value The value to set
   */
  public async setPreference(key: string, value: any): Promise<void> {
    return this.set('user-preferences', key, value);
  }
  
  /**
   * Delete a user preference
   * @param key The preference key to delete
   */
  public async deletePreference(key: string): Promise<void> {
    return this.delete('user-preferences', key);
  }
  
  /**
   * Get all user preferences
   * @returns Object with all preferences
   */
  public async getAllPreferences(): Promise<Record<string, any>> {
    return this.getAll('user-preferences');
  }
  
  /**
   * Get app-specific settings for the user
   * @param appName Name of the application
   * @param configName Name of the config file (defaults to settings.conf)
   * @returns Object with all app settings
   */
  public async getAppSettings(appName: string, configName: string = 'settings.conf'): Promise<Record<string, any>> {
    const appConfigPath = PathUtils.join(appName, configName);
    return this.getAll(appConfigPath);
  }
  
  /**
   * Get a specific app setting for the user
   * @param appName Name of the application
   * @param key The setting key
   * @param defaultValue Default value if setting doesn't exist
   * @param configName Name of the config file (defaults to settings.conf)
   */
  public async getAppSetting(
    appName: string, 
    key: string, 
    defaultValue?: any, 
    configName: string = 'settings.conf'
  ): Promise<any> {
    const appConfigPath = PathUtils.join(appName, configName);
    return this.get(appConfigPath, key, defaultValue);
  }
  
  /**
   * Set a specific app setting for the user
   * @param appName Name of the application
   * @param key The setting key
   * @param value The value to set
   * @param configName Name of the config file (defaults to settings.conf)
   */
  public async setAppSetting(
    appName: string, 
    key: string, 
    value: any, 
    configName: string = 'settings.conf'
  ): Promise<void> {
    // Ensure app config directory exists
    await this.createAppSettingsDirectory(appName);
    
    const appConfigPath = PathUtils.join(appName, configName);
    return this.set(appConfigPath, key, value);
  }
  
  /**
   * Get the list of pinned apps for the Start Menu
   * @returns Array of app IDs in order
   */
  public async getStartMenuPinnedApps(): Promise<string[]> {
    const pinnedAppsStr = await this.getAppSetting("StartMenu", "PinnedApps");
    console.log("Pinned Apps String:", pinnedAppsStr); // Debugging line
    return pinnedAppsStr ? pinnedAppsStr.split(',') : [];
  }
  
  /**
   * Set the pinned apps for the Start Menu
   * @param appIds Array of app IDs in order
   */
  public async setStartMenuPinnedApps(appIds: string[]): Promise<void> {
    return this.setAppSetting("StartMenu", "PinnedApps", appIds.join(','));
   
  }
  
  /**
   * Get app tile color assignments
   * Format: "appId;colorClass" (e.g., "terminal;A,browser;B,calculator;G")
   * @returns Map of app IDs to color class letters
   */
  public async getAppTileColorAssignments(): Promise<Map<string, string>> {
    const colorAssignmentsStr = await this.getAppSetting("StartMenu", "AppTileColors");
    const result = new Map<string, string>();
    
    if (colorAssignmentsStr) {
      // Split the string by commas to get individual assignments
      const assignments = colorAssignmentsStr.split(',');
      
      for (const assignment of assignments) {
        // Split each assignment by semicolon to get appId and colorClass
        const parts = assignment.trim().split(';');
        if (parts.length === 2) {
          const [appId, colorClass] = parts;
          result.set(appId, colorClass);
        }
      }
    }
    
    return result;
  }
  
  /**
   * Set app tile color assignments
   * @param colorAssignments Map of app IDs to color class letters
   */
  public async setAppTileColorAssignments(colorAssignments: Map<string, string>): Promise<void> {
    // Convert the map to a string in the format "appId;colorClass,appId;colorClass,..."
    const assignments: string[] = [];
    
    for (const [appId, colorClass] of colorAssignments.entries()) {
      assignments.push(`${appId};${colorClass}`);
    }
    
    await this.setAppSetting("StartMenu", "AppTileColors", assignments.join(','));
  }
  
  /**
   * Set a single app tile color assignment
   * @param appId The app ID
   * @param colorClass The color class letter (A-I)
   */
  public async setAppTileColorAssignment(appId: string, colorClass: string): Promise<void> {
    const colorAssignments = await this.getAppTileColorAssignments();
    colorAssignments.set(appId, colorClass);
    await this.setAppTileColorAssignments(colorAssignments);
  }
  
  /**
   * Get a single app tile color assignment
   * @param appId The app ID
   * @returns The color class letter, or undefined if not assigned
   */
  public async getAppTileColorAssignment(appId: string): Promise<string | undefined> {
    const colorAssignments = await this.getAppTileColorAssignments();
    return colorAssignments.get(appId);
  }
  
  /**
   * Clear an app tile color assignment
   * @param appId The app ID
   */
  public async clearAppTileColorAssignment(appId: string): Promise<void> {
    const colorAssignments = await this.getAppTileColorAssignments();
    colorAssignments.delete(appId);
    await this.setAppTileColorAssignments(colorAssignments);
  }
}
