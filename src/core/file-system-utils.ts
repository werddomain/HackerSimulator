import { FileSystemEntry, IFileSystemProvider } from './filesystem';

/**
 * File statistics interface
 */
export interface FileStats {
  isDirectory: boolean;
  size?: number;
  createdTime?: Date;
  modifiedTime?: Date;
  permissions?: string;
  owner?: string;
}

/**
 * Utility class for file system operations
 */
export class FileSystemUtils {
  /**
   * Copy a file from source to destination
   * @param fs The FileSystem instance
   * @param sourcePath Source path
   * @param destPath Destination path
   */
  public static async copy(fs: IFileSystemProvider, sourcePath: string, destPath: string): Promise<void> {
    // Check if source exists
    const exists = await fs.exists(sourcePath);
    if (!exists) {
      throw new Error(`Source path does not exist: ${sourcePath}`);
    }
    
    // Read source content
    const content = await fs.readFile(sourcePath);
    
    // Write to destination
    await fs.writeFile(destPath, content);
  }
    /**
   * Move a file from source to destination
   * @param fs The FileSystem instance
   * @param sourcePath Source path
   * @param destPath Destination path
   */
  public static async move(fs: IFileSystemProvider, sourcePath: string, destPath: string): Promise<void> {
    await fs.moveEntry(sourcePath, destPath);
  }
  
  /**
   * Remove a file or directory
   * @param fs The FileSystem instance
   * @param path Path to remove
   */
  public static async remove(fs: IFileSystemProvider, path: string): Promise<void> {
    await fs.deleteEntry(path);
  }
  
  /**
   * Get file or directory stats
   * @param fs The FileSystem instance
   * @param path Path to get stats for
   */
  public static async stat(fs: IFileSystemProvider, path: string): Promise<FileStats> {
    // Check if path exists
    const exists = await fs.exists(path);
    if (!exists) {
      throw new Error(`Path does not exist: ${path}`);
    }
      // Get entry
    const entries = await fs.readDirectory(path.substring(0, path.lastIndexOf('/')) || '/');
    const fileName = path.split('/').pop() || '';
    const entry = entries.find((e: FileSystemEntry) => e.name === fileName);
    
    if (!entry) {
      throw new Error(`Entry not found: ${path}`);
    }
    
    return {
      isDirectory: entry.type === 'directory',
      size: entry.metadata?.size,
      createdTime: entry.metadata?.created ? new Date(entry.metadata.created) : undefined,
      modifiedTime: entry.metadata?.modified ? new Date(entry.metadata.modified) : undefined,
      permissions: entry.metadata?.permissions,
      owner: entry.metadata?.owner
    };
  }
}
