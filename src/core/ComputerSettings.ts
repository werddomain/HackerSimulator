import { FileSystem } from './filesystem';
import { BaseSettings } from './BaseSettings';
import { PathUtils } from './path-utils';

/**
 * ComputerSettings handles system-wide settings stored in /etc
 * similar to how Linux stores system configuration files.
 */
export class ComputerSettings extends BaseSettings {
  /**
   * Create a new ComputerSettings instance
   * @param fs The filesystem to use
   */
  constructor(fs: FileSystem) {
    // Use /etc as the base path for system settings (Linux convention)
    super(fs, '/etc');
  }
  
  /**
   * Get a system configuration value
   * @param key The configuration key
   * @param defaultValue Default value if configuration doesn't exist
   */
  public async getSystemConfig(key: string, defaultValue?: any): Promise<any> {
    return this.get('system.conf', key, defaultValue);
  }
  
  /**
   * Set a system configuration value
   * @param key The configuration key
   * @param value The value to set
   */
  public async setSystemConfig(key: string, value: any): Promise<void> {
    return this.set('system.conf', key, value);
  }
  
  /**
   * Delete a system configuration value
   * @param key The configuration key to delete
   */
  public async deleteSystemConfig(key: string): Promise<void> {
    return this.delete('system.conf', key);
  }
  
  /**
   * Get all system configuration values
   * @returns Object with all configuration values
   */
  public async getAllSystemConfig(): Promise<Record<string, any>> {
    return this.getAll('system.conf');
  }
  
  /**
   * Get application defaults configuration
   * @param appName Name of the application
   * @param configName Name of the config file (defaults to defaults.conf)
   * @returns Object with all default settings
   */
  public async getAppDefaults(appName: string, configName: string = 'defaults.conf'): Promise<Record<string, any>> {
    const appConfigPath = PathUtils.join('app-defaults', appName, configName);
    return this.getAll(appConfigPath);
  }
  
  /**
   * Get a specific application default configuration
   * @param appName Name of the application
   * @param key The setting key
   * @param defaultValue Default value if setting doesn't exist
   * @param configName Name of the config file (defaults to defaults.conf)
   */
  public async getAppDefault(
    appName: string, 
    key: string, 
    defaultValue?: any,
    configName: string = 'defaults.conf'
  ): Promise<any> {
    const appConfigPath = PathUtils.join('app-defaults', appName, configName);
    return this.get(appConfigPath, key, defaultValue);
  }
  
  /**
   * Set a specific application default configuration
   * This should only be used by system processes or during system setup
   * @param appName Name of the application
   * @param key The setting key
   * @param value The value to set
   * @param configName Name of the config file (defaults to defaults.conf)
   */
  public async setAppDefault(
    appName: string, 
    key: string, 
    value: any,
    configName: string = 'defaults.conf'
  ): Promise<void> {
    // Ensure app defaults directory exists
    const dirPath = PathUtils.join(this.basePath, 'app-defaults', appName);
    const dirExists = await this.fs.exists(dirPath);
    if (!dirExists) {
      await this.fs.createDirectory(dirPath); // Create with parents
    }
    
    const appConfigPath = PathUtils.join('app-defaults', appName, configName);
    return this.set(appConfigPath, key, value);
  }

  /**
   * Set network configuration parameters
   * @param key The network configuration key
   * @param value The value to set
   */
  public async setNetworkConfig(key: string, value: any): Promise<void> {
    return this.set('network/network.conf', key, value);
  }

  /**
   * Get network configuration parameters
   * @param key The network configuration key
   * @param defaultValue Default value if configuration doesn't exist
   */
  public async getNetworkConfig(key: string, defaultValue?: any): Promise<any> {
    return this.get('network/network.conf', key, defaultValue);
  }
}
