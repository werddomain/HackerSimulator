/**
 * Custom path utilities for browser environment
 * This provides similar functionality to Node.js path module
 */
export class PathUtils {
  /**
   * Check if a path is absolute
   */
  public static isAbsolute(path: string): boolean {
    return path.startsWith('/');
  }
  
  /**
   * Check if a path is a recognized alias
   * @param path The path to check
   * @param aliases Optional array of alias names to check against
   */
  public static isAlias(path: string, aliases?: string[]): boolean {
    // Check against common aliases
    if (path === '~' || path.startsWith('~/')) {
      return true;
    }
    
    // If additional aliases were provided, check against those too
    if (aliases && aliases.length > 0) {
      for (const alias of aliases) {
        if (path === alias || path.startsWith(alias + '/')) {
          return true;
        }
      }
    }
    
    return false;
  }

  /**
   * Join path segments
   */
  public static join(...paths: string[]): string {
    // Filter out empty segments
    paths = paths.filter(path => path !== '');
    
    // If no paths provided, return current directory
    if (paths.length === 0) {
      return '.';
    }
    
    // Normalize the path segments and join them
    const normalized = this.normalize(paths.join('/'));
    return normalized;
  }

  /**
   * Resolve a path (similar to path.resolve)
   */
  public static resolve(...paths: string[]): string {
    // Start with an absolute path (current directory if no absolute path provided)
    let resolvedPath = '';
    
    for (const path of paths) {
      // If the path is absolute, reset the resolved path
      if (this.isAbsolute(path)) {
        resolvedPath = path;
      } else if (path) {
        // If the path is not empty, join it to the resolved path
        resolvedPath = resolvedPath ? this.join(resolvedPath, path) : path;
      }
    }
    
    // Ensure the result is an absolute path
    if (!this.isAbsolute(resolvedPath)) {
      resolvedPath = '/' + resolvedPath;
    }
    
    return this.normalize(resolvedPath);
  }

  /**
   * Normalize a path (remove redundant slashes, handle .. and .)
   */
  public static normalize(path: string): string {
    // Ensure path starts with a slash if it's an absolute path
    const isAbsolute = path.startsWith('/');
    
    // Handle empty path
    if (!path) {
      return '.';
    }
    
    // Split the path into segments
    const segments = path.split('/').filter(segment => segment !== '');
    const result: string[] = [];
    
    // Process each segment
    for (const segment of segments) {
      if (segment === '.') {
        // Skip '.' segments
        continue;
      } else if (segment === '..') {
        // Go up one level for '..' segments, if possible
        if (result.length > 0 && result[result.length - 1] !== '..') {
          result.pop();
        } else if (!isAbsolute) {
          // For relative paths, keep '..' segments
          result.push('..');
        }
      } else {
        // Add the segment to the result
        result.push(segment);
      }
    }
    
    // Join the segments
    let normalizedPath = result.join('/');
    
    // Add leading slash for absolute paths
    if (isAbsolute) {
      normalizedPath = '/' + normalizedPath;
    }
    
    // Return '.' for empty path
    return normalizedPath || (isAbsolute ? '/' : '.');
  }
  
  /**
   * Get the directory name from a path
   */
  public static dirname(path: string): string {
    if (!path) {
      return '.';
    }
    
    // Remove trailing slash
    if (path.endsWith('/') && path.length > 1) {
      path = path.slice(0, -1);
    }
    
    const lastSlashIndex = path.lastIndexOf('/');
    
    if (lastSlashIndex === -1) {
      return '.';
    }
    
    if (lastSlashIndex === 0) {
      return '/';
    }
    
    return path.slice(0, lastSlashIndex);
  }
  
  /**
   * Get the base name from a path
   */
  public static basename(path: string): string {
    if (!path) {
      return '';
    }
    
    // Remove trailing slash
    if (path.endsWith('/') && path.length > 1) {
      path = path.slice(0, -1);
    }
    
    const lastSlashIndex = path.lastIndexOf('/');
    
    if (lastSlashIndex === -1) {
      return path;
    }
    
    return path.slice(lastSlashIndex + 1);
  }
}
