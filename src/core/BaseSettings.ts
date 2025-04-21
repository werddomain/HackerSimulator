import { FileSystem, FileSystemEntry } from './filesystem';
import { PathUtils } from './path-utils';


/**
 * BaseSettings provides core functionality for storing and retrieving settings
 * in a Linux-like configuration file format with optional JSON support.
 */
export class BaseSettings {
  protected fs: FileSystem;
  protected basePath: string;
  
  /**
   * Create a new BaseSettings instance
   * @param fs The filesystem to use
   * @param basePath The base path where settings will be stored
   */
  constructor(fs: FileSystem, basePath: string) {
    this.fs = fs;
    this.basePath = basePath;
  }
  
  /**
   * Ensures the settings directory structure exists
   * @param configPath Full path to the config file
   */
protected async ensureConfigPath(configPath: string): Promise<void> {
    const dirPath = PathUtils.dirname(configPath);
    
    // Check if directory exists, create if it doesn't
    const dirExists = await this.fs.exists(dirPath);
    if (!dirExists) {
      await this.fs.createDirectory(dirPath); // Create directory
    }
  }
  
  /**
   * Get a setting value from a config file
   * @param configName Name of the config file or path relative to basePath
   * @param key The setting key to retrieve
   * @param defaultValue Default value if setting doesn't exist
   * @returns The setting value or defaultValue if not found
   */
  public async get(configName: string, key: string, defaultValue?: any): Promise<any> {
    const configPath = this.getConfigPath(configName);
    
    try {
      // Check if file exists
      const exists = await this.fs.exists(configPath);
      if (!exists) {
        return defaultValue;
      }
      
      // Read and parse config file
      const content = await this.readConfigFile(configPath);
      
      // Check for JSON format
      if (this.isJsonConfig(configPath)) {
        try {
          const jsonData = JSON.parse(content);
          return key in jsonData ? jsonData[key] : defaultValue;
        } catch (e) {
          console.error(`Error parsing JSON config: ${configPath}`, e);
          return defaultValue;
        }
      }
      
      // Parse traditional config format (key=value pairs)
      const lines = content.split('\n');
      for (const line of lines) {
        // Skip comments and empty lines
        if (line.trim().startsWith('#') || line.trim() === '') {
          continue;
        }
        
        const parts = line.split('=');
        if (parts.length >= 2 && parts[0].trim() === key) {
          // Join back parts in case value contained = characters
          const value = parts.slice(1).join('=').trim();
          
          // Convert values to appropriate types
          if (value === 'true') return true;
          if (value === 'false') return false;
          if (value === 'null' || value === 'undefined') return null;
          if (!isNaN(Number(value)) && value !== '') return Number(value);
          
          // Try to parse JSON objects/arrays in values
          if ((value.startsWith('{') && value.endsWith('}')) || 
              (value.startsWith('[') && value.endsWith(']'))) {
            try {
              return JSON.parse(value);
            } catch (e) {
              // Not valid JSON, return as string
            }
          }
          
          return value;
        }
      }
      
      return defaultValue;
    } catch (error) {
      console.error(`Error reading setting ${key} from ${configPath}:`, error);
      return defaultValue;
    }
  }
  
  /**
   * Set a setting value in a config file
   * @param configName Name of the config file or path relative to basePath
   * @param key The setting key to set
   * @param value The value to set
   */
  public async set(configName: string, key: string, value: any): Promise<void> {
    const configPath = this.getConfigPath(configName);
    
    try {
      // Ensure config directory exists
      await this.ensureConfigPath(configPath);
        // Check if this is a JSON config
      if (this.isJsonConfig(configPath)) {
        let jsonData: Record<string, any> = {};
        
        // Read existing content if file exists
        const exists = await this.fs.exists(configPath);
        if (exists) {
          const content = await this.readConfigFile(configPath);
          try {
            jsonData = JSON.parse(content);
          } catch (e) {
            // If parsing fails, start with an empty object
            console.error(`Error parsing JSON config: ${configPath}`, e);
          }
        }
        
        // Update value and write back
        jsonData[key] = value;
        await this.fs.writeFile(configPath, JSON.stringify(jsonData, null, 2));
        return;
      }
      
      // Handle traditional config format
      let lines: string[] = [];
      let found = false;
      
      // Read existing content if file exists
      const exists = await this.fs.exists(configPath);
      if (exists) {
        const content = await this.readConfigFile(configPath);
        lines = content.split('\n');
        
        // Update existing key if found
        for (let i = 0; i < lines.length; i++) {
          const line = lines[i];
          // Skip comments and empty lines
          if (line.trim().startsWith('#') || line.trim() === '') {
            continue;
          }
          
          const parts = line.split('=');
          if (parts.length >= 2 && parts[0].trim() === key) {
            // Format the value appropriately
            lines[i] = `${key}=${this.formatValue(value)}`;
            found = true;
            break;
          }
        }
      }
      
      // Add key if not found
      if (!found) {
        lines.push(`${key}=${this.formatValue(value)}`);
      }
      
      // Write back to file
      await this.fs.writeFile(configPath, lines.join('\n'));
    } catch (error) {
      console.error(`Error setting ${key}=${value} in ${configPath}:`, error);
      throw error;
    }
  }
  
  /**
   * Delete a setting from a config file
   * @param configName Name of the config file or path relative to basePath
   * @param key The setting key to delete
   */
  public async delete(configName: string, key: string): Promise<void> {
    const configPath = this.getConfigPath(configName);
    
    try {
      // Check if file exists
      const exists = await this.fs.exists(configPath);
      if (!exists) {
        return;
      }
      
      // Handle JSON config
      if (this.isJsonConfig(configPath)) {
        const content = await this.readConfigFile(configPath);
        try {
          const jsonData = JSON.parse(content);
          if (key in jsonData) {
            delete jsonData[key];
            await this.fs.writeFile(configPath, JSON.stringify(jsonData, null, 2));
          }
        } catch (e) {
          console.error(`Error parsing JSON config: ${configPath}`, e);
        }
        return;
      }
      
      // Handle traditional config format
      const content = await this.readConfigFile(configPath);
      const lines = content.split('\n');
      const newLines = lines.filter(line => {
        // Keep comments and empty lines
        if (line.trim().startsWith('#') || line.trim() === '') {
          return true;
        }
        
        const parts = line.split('=');
        // Keep lines that don't match our key
        return parts.length < 2 || parts[0].trim() !== key;
      });
      
      // Write back to file if changed
      if (newLines.length !== lines.length) {
        await this.fs.writeFile(configPath, newLines.join('\n'));
      }
    } catch (error) {
      console.error(`Error deleting setting ${key} from ${configPath}:`, error);
      throw error;
    }
  }
  
  /**
   * Get all settings from a config file
   * @param configName Name of the config file or path relative to basePath
   * @returns Object with all settings
   */
  public async getAll(configName: string): Promise<Record<string, any>> {
    const configPath = this.getConfigPath(configName);
    const result: Record<string, any> = {};
    
    try {
      // Check if file exists
      const exists = await this.fs.exists(configPath);
      if (!exists) {
        return result;
      }
      
      const content = await this.readConfigFile(configPath);
      
      // Handle JSON config
      if (this.isJsonConfig(configPath)) {
        try {
          return JSON.parse(content);
        } catch (e) {
          console.error(`Error parsing JSON config: ${configPath}`, e);
          return result;
        }
      }
      
      // Parse traditional config format
      const lines = content.split('\n');
      for (const line of lines) {
        // Skip comments and empty lines
        if (line.trim().startsWith('#') || line.trim() === '') {
          continue;
        }
        
        const parts = line.split('=');
        if (parts.length >= 2) {
          const key = parts[0].trim();
          // Join back parts in case value contained = characters
          const value = parts.slice(1).join('=').trim();
          
          // Convert values to appropriate types
          if (value === 'true') result[key] = true;
          else if (value === 'false') result[key] = false;
          else if (value === 'null' || value === 'undefined') result[key] = null;
          else if (!isNaN(Number(value)) && value !== '') result[key] = Number(value);
          else {
            // Try to parse JSON objects/arrays
            if ((value.startsWith('{') && value.endsWith('}')) || 
                (value.startsWith('[') && value.endsWith(']'))) {
              try {
                result[key] = JSON.parse(value);
              } catch (e) {
                // Not valid JSON, store as string
                result[key] = value;
              }
            } else {
              result[key] = value;
            }
          }
        }
      }
      
      return result;
    } catch (error) {
      console.error(`Error reading all settings from ${configPath}:`, error);
      return result;
    }
  }
  
  /**
   * Creates an app settings directory and returns its path
   * @param appName Name of the application
   * @returns Path to the app settings directory
   */
public async createAppSettingsDirectory(appName: string): Promise<string> {
    const dirPath = this.getAppConfigPath(appName);
    
    // Check if directory exists, create if it doesn't
    const dirExists = await this.fs.exists(dirPath);
    if (!dirExists) {
      await this.fs.createDirectory(dirPath); // Create directory
    }
    
    return dirPath;
  }
  
  /**
   * Get the full config path for an app
   * @param appName Name of the application
   * @returns Path to the app config directory
   */
  public getAppConfigPath(appName: string): string {
    return PathUtils.join(this.basePath, appName);
  }
  
  /**
   * Get the full config path
   * @param configName Name of the config file or relative path
   * @returns Full path to the config file
   */
  protected getConfigPath(configName: string): string {
    // If configName contains slashes, treat it as a relative path
    if (configName.includes('/')) {
      return PathUtils.join(this.basePath, configName);
    }
    
    // If it doesn't end with .conf or .json, add .conf extension
    if (!configName.endsWith('.conf') && !configName.endsWith('.json')) {
      configName = `${configName}.conf`;
    }
    
    return PathUtils.join(this.basePath, configName);
  }
  
  /**
   * Format a value for storage in a config file
   * @param value The value to format
   * @returns Formatted string value
   */
  private formatValue(value: any): string {
    if (value === null || value === undefined) return 'null';
    if (typeof value === 'boolean' || typeof value === 'number') return value.toString();
    if (typeof value === 'object') return JSON.stringify(value);
    return value.toString();
  }
  
  /**
   * Check if a config file is in JSON format
   * @param configPath Path to the config file
   * @returns True if the file has a .json extension
   */
  private isJsonConfig(configPath: string): boolean {
    return configPath.toLowerCase().endsWith('.json');
  }
    /**
   * Read a config file's contents
   * @param configPath Path to the config file
   * @returns File contents as string
   */
  private async readConfigFile(configPath: string): Promise<string> {
    try {
      // Check if file exists
      const exists = await this.fs.exists(configPath);
      if (!exists) {
        throw new Error(`Config file not found: ${configPath}`);
      }
      
      // Read the file contents directly
      return await this.fs.readFile(configPath);
    } catch (error) {
      console.error(`Error reading config file: ${configPath}`, error);
      throw new Error(`Failed to read config file: ${configPath}`);
    }
  }
}
