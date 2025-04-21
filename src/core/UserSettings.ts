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
}
