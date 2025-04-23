import { openDB, IDBPDatabase } from 'idb';
import { FileStats } from './file-system-utils';
import { OS } from './os';
import { PathUtils } from './path-utils';

/**
 * Base class for path aliases in the filesystem
 */
export abstract class PathAlias {
  constructor(public readonly alias: string) {}
  
  /**
   * Resolves the alias to a real path
   * @param remainingPath The rest of the path after the alias
   * @returns The resolved full path
   */
  abstract resolve(remainingPath: string): string;
  
  /**
   * Checks if this alias can be used for the given path
   */
  matches(path: string): boolean {
    return path === this.alias || path.startsWith(this.alias);
  }
  
  /**
   * Extracts the part of the path after the alias
   */
  extractRemainingPath(path: string): string {
    if (path === this.alias) return '';
    // Add a slash after the alias if not already present
    return path.substring(this.alias.length);
  }
}

/**
 * A fixed alias that maps to a specific filesystem path
 */
export class FixedPathAlias extends PathAlias {
  constructor(alias: string, private readonly targetPath: string) {
    super(alias);
  }
  
  resolve(remainingPath: string): string {
    if (!remainingPath) return this.targetPath;
    // Add a slash between target path and remaining path if needed
    return this.targetPath + (remainingPath.startsWith('/') ? '' : '/') + remainingPath;
  }
}

/**
 * A dynamic alias that uses a function to resolve paths
 */
export class DynamicPathAlias extends PathAlias {
  constructor(
    alias: string, 
    private readonly resolveFunction: (remainingPath: string) => string
  ) {
    super(alias);
  }
  
  resolve(remainingPath: string): string {
    return this.resolveFunction(remainingPath);
  }
}

/**
 * A symlink alias that points to another location in the filesystem
 */
export class SymlinkAlias extends FixedPathAlias {
  // Uses the same implementation as FixedPathAlias
  // but conceptually represents a symlink in the filesystem
}

/**
 * Interface for file system entries
 */
export interface FileSystemEntry {
  name: string;
  type: 'file' | 'directory';
  content?: string;
  binaryContent?: ArrayBuffer;
  isBinary?: boolean;
  metadata: {
    created: number;
    modified: number;
    size: number;
    permissions: string;
    owner: string;
  };
  isDirectory: boolean; // Added for backward compatibility
}

/**
 * Interface for file system providers (allows for future expansion to server-based storage)
 */
export interface IFileSystemProvider {
  init(): Promise<void>;
  readDirectory(path: string): Promise<FileSystemEntry[]>;
  readFile(path: string): Promise<string>;
  writeFile(path: string, content: string): Promise<void>;
  createDirectory(path: string): Promise<void>;
  deleteEntry(path: string): Promise<void>;
  moveEntry(oldPath: string, newPath: string): Promise<void>;
  exists(path: string): Promise<boolean>;
  
  // Add these missing methods
  copy(sourcePath: string, destPath: string): Promise<void>;
  move(sourcePath: string, destPath: string): Promise<void>;
  remove(path: string): Promise<void>;
  stat(path: string): Promise<FileStats>;
  
  // Binary file methods
  readBinaryFile(path: string): Promise<ArrayBuffer>;
  writeBinaryFile(path: string, content: ArrayBuffer): Promise<void>;
  isBinaryFile(path: string): Promise<boolean>;
}

/**
 * Implementation of file system using IndexedDB
 */
export class FileSystem implements IFileSystemProvider {
  
  /**
   * Input a path and return a path with alias if an alias exist for this path start
   * @param newPath 
   * @returns 
   */
  public formatWithAlias(newPath: string): string {
  // Try to replace the path with an alias if possible
  // Sort aliases by the length of their resolved paths (longest first)
  // This ensures we match the most specific alias first
  const sortedAliases = [...this.aliases].sort((a, b) => {
    const pathA = a.resolve('');
    const pathB = b.resolve('');
    return pathB.length - pathA.length; // Sort in descending order
  });

  // Try to replace the path with an alias if possible
  for (const alias of sortedAliases) {
    const aliasRootPath = alias.resolve('');
    
    if (newPath === aliasRootPath || newPath.startsWith(aliasRootPath + '/')) {
      const relativePath = newPath.substring(aliasRootPath.length);
      return alias.alias + relativePath;
    }
  }
    return newPath; // No alias matched, return original path
  } 
  
  private aliases: PathAlias[] = [];
  
  constructor(private readonly os:OS) {

  }

  /**
   * Register a new path alias
   * @param alias The alias to register
   */
  public registerAlias(alias: PathAlias): void {
    // Check for duplicate aliases
    const existingIndex = this.aliases.findIndex(a => a.alias === alias.alias);
    if (existingIndex >= 0) {
      this.aliases[existingIndex] = alias; // Replace existing alias
    } else {
      this.aliases.push(alias);
    }
  }
  
  /**
   * Unregister a path alias
   * @param aliasName The name of the alias to remove
   */
  public unregisterAlias(aliasName: string): void {
    this.aliases = this.aliases.filter(a => a.alias !== aliasName);
  }
  
  /**
   * Register a fixed path alias
   * @param alias The alias name (like ~ or /mnt/cdrom)
   * @param targetPath The real path this alias points to
   */
  public registerFixedAlias(alias: string, targetPath: string): void {
    this.registerAlias(new FixedPathAlias(alias, targetPath));
  }
  
  /**
   * Register a symlink
   * @param linkPath The path where the symlink appears
   * @param targetPath The real path the symlink points to
   */
  public registerSymlink(linkPath: string, targetPath: string): void {
    this.registerAlias(new SymlinkAlias(linkPath, targetPath));
  }

  /**
   * Delete a file (not directories)
   * @param path Path to the file to delete
   * @throws Error if path doesn't exist or is a directory
   */
  public async deleteFile(path: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      
      // Check if path exists
      const exists = await this.exists(normPath);
      if (!exists) {
        throw new Error(`File does not exist: ${path}`);
      }
      
      // Get entry to check if it's a file
      const entry = await this.db.get('fs-entries', normPath);
      
      // Ensure we're only deleting files, not directories
      if (entry.entry.type !== 'file') {
        throw new Error(`Not a file: ${path}. Use deleteEntry for directories.`);
      }
      
      // Delete the file
      await this.db.delete('fs-entries', normPath);
    } catch (error) {
      console.error(`Error deleting file: ${path}`, error);
      throw new Error(`Failed to delete file: ${path}`);
    }
  }
  private db: IDBPDatabase | null = null;
  private readonly DB_NAME = 'hacker-os-fs';
  private readonly DB_VERSION = 1;
  private readonly STORE_NAME = 'fs-entries';
  public async copy(sourcePath: string, destPath: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normSourcePath = this.normalizePath(sourcePath);
      const normDestPath = this.normalizePath(destPath);
      
      // Check if source exists
      const sourceExists = await this.exists(normSourcePath);
      if (!sourceExists) {
        throw new Error(`Source path does not exist: ${sourcePath}`);
      }
      
      // Check if destination already exists
      const destExists = await this.exists(normDestPath);
      if (destExists) {
        throw new Error(`Destination path already exists: ${destPath}`);
      }
      
      // Get the source entry
      const sourceEntry = await this.db.get('fs-entries', normSourcePath);
      
      // Create new entry with same content but at destination path
      const destName = normDestPath.split('/').pop() || '';
      const destParent = normDestPath.substring(0, normDestPath.lastIndexOf('/')) || '/';
      
      // Check if parent directory exists
      const parentExists = await this.exists(destParent);
      if (!parentExists) {
        throw new Error(`Parent directory does not exist: ${destParent}`);
      }
      
      // Create a copy with updated metadata
      const newEntry = {
        path: normDestPath,
        parent: destParent,
        entry: {
          ...sourceEntry.entry,
          name: destName,
          metadata: {
            ...sourceEntry.entry.metadata,
            created: Date.now(),
            modified: Date.now()
          }
        }
      };
      
      // Add the new entry
      await this.db.put('fs-entries', newEntry);
      
    } catch (error) {
      console.error(`Error copying entry: ${sourcePath} -> ${destPath}`, error);
      throw new Error(`Failed to copy entry: ${sourcePath} -> ${destPath}`);
    }
  }

  public async move(sourcePath: string, destPath: string): Promise<void> {
    // This is essentially the same as moveEntry, so we'll reuse that
    return this.moveEntry(sourcePath, destPath);
  }

  public async remove(path: string): Promise<void> {
    // This is essentially the same as deleteEntry, so we'll reuse that
    return this.deleteEntry(path);
  }

  public async stat(path: string): Promise<FileStats> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      
      // Check if path exists
      const exists = await this.exists(normPath);
      if (!exists) {
        throw new Error(`Path does not exist: ${path}`);
      }
      
      // Get the entry
      const entry = await this.db.get('fs-entries', normPath);
      
      return {
        isDirectory: entry.entry.type === 'directory',
        size: entry.entry.metadata.size,
        createdTime: new Date(entry.entry.metadata.created),
        modifiedTime: new Date(entry.entry.metadata.modified),
        permissions: entry.entry.metadata.permissions,
        owner: entry.entry.metadata.owner
      };
    } catch (error) {
      console.error(`Error getting stats for: ${path}`, error);
      throw new Error(`Failed to get stats for: ${path}`);
    }
  }
  /**
   * Initialize the file system
   */
  public async init(): Promise<void> {
    try {
      this.db = await openDB(this.DB_NAME, this.DB_VERSION, {
        upgrade(db) {
          // Create object store for file system entries
          if (!db.objectStoreNames.contains('fs-entries')) {
            const store = db.createObjectStore('fs-entries', { keyPath: 'path' });
            store.createIndex('parent', 'parent', { unique: false });
          }
        }
      });

      // Check if root directory exists, if not create the basic Linux directory structure
      const rootExists = await this.exists('/');
      if (!rootExists) {
        await this.setupInitialFileSystem();
      }
      await this.ensureUserDirectoryExists();
      
      // Setup default aliases
      this.setupDefaultAliases();

    } catch (error) {
      console.error('Failed to initialize filesystem:', error);
      throw new Error('Failed to initialize filesystem');
    }
  }
  
  /**
   * Setup default filesystem aliases
   */
  private setupDefaultAliases(): void {
    // Home directory alias
    this.registerFixedAlias('~', this.UserFolder);
    
    // Mount points can be dynamically managed
    this.registerAlias(new DynamicPathAlias('/mnt', (path: string) => {
      // Here you could check registered mount points and map accordingly
      // For now we'll just create a basic implementation
      if (path.startsWith('/cdrom')) {
        return '/media/cdrom' + path.substring(6);
      }
      return '/mnt' + path; // Default to just passing through
    }));
    
    // Convenience aliases for common paths
    this.registerFixedAlias('/tmp', '/tmp');
    this.registerSymlink('/home/current', this.UserFolder);
  }
  
  public readonly userDirectories:string[] = ['Desktop', 'Documents', 'Downloads', 'Music', 'Pictures', 'Videos', '.config', '.local', '.cache'];
  public get UserFolder():string {
    return `/home/${this.os.currentUserName}`;
  }
  private async ensureUserDirectoryExists(): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    if (!this.os) throw new Error('OS not initialized');

    const userDir = this.UserFolder;
    const exists = await this.exists(userDir);
    
    if (!exists) {
      // Create user directory if it doesn't exist
      // TODO: add a security to prevent this folder to be deleted. Mark them as special folder;
      await this.createDirectory(userDir);
      for (const dir of this.userDirectories) {
        await this.createDirectory(dir, userDir, true);
      }
    }
  }
  /**
   * Set up the initial file system with basic Linux directory structure
   */
  private async setupInitialFileSystem(): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    // Create root directory
    await this.db.put('fs-entries', {
      path: '/',
      parent: '',
      entry: {
        name: '/',
        type: 'directory',
        metadata: {
          created: Date.now(),
          modified: Date.now(),
          size: 0,
          permissions: 'drwxr-xr-x',
          owner: 'root'
        }
      }
    });

    // Create basic Linux directory structure
    const dirs = [
      '/bin',
      '/etc',
      '/home',
      '/home/user',
      '/tmp',
      '/var',
      '/var/log'
    ];

    for (const dir of dirs) {
      const name = dir.split('/').pop();
      const parent = dir.substring(0, dir.lastIndexOf('/')) || '/';
      
      await this.db.put('fs-entries', {
        path: dir,
        parent,
        entry: {
          name: name || '/',
          type: 'directory',
          metadata: {
            created: Date.now(),
            modified: Date.now(),
            size: 0,
            permissions: dir.startsWith('/home/user') ? 'drwxr-xr-x' : 'drwxr-xr-x',
            owner: dir.startsWith('/home/user') ? 'user' : 'root'
          }
        }
      });
    }

    // Create some basic files
    const files = [
      { 
        path: '/etc/hostname', 
        content: 'hacker-machine',
        owner: 'root'
      },
      { 
        path: '/etc/passwd', 
        content: 'root:x:0:0:root:/root:/bin/bash\nuser:x:1000:1000:HackerOS User:/home/user:/bin/bash',
        owner: 'root'
      },
      { 
        path: '/home/user/README.txt', 
        content: 'Welcome to HackerOS!\n\nThis system is designed to simulate a hacking environment. Use the terminal to navigate and execute commands. Launch applications from the desktop or start menu.',
        owner: 'user'
      }
    ];

    for (const file of files) {
      const name = file.path.split('/').pop() || '';
      const parent = file.path.substring(0, file.path.lastIndexOf('/'));
      
      await this.db.put('fs-entries', {
        path: file.path,
        parent,
        entry: {
          name,
          type: 'file',
          content: file.content,
          metadata: {
            created: Date.now(),
            modified: Date.now(),
            size: file.content.length,
            permissions: '-rw-r--r--',
            owner: file.owner
          }
        }
      });
    }
  }

  /**
   * Check if a path exists in the file system
   */
  public async exists(path: string): Promise<boolean> {
    if (!this.db) throw new Error('Database not initialized');
    try {
      const entry = await this.db.get('fs-entries', this.normalizePath(path));
      return !!entry;
    } catch (error) {
      console.error(`Error checking if path exists: ${path}`, error);
      return false;
    }
  }
  /**
   * Read directory contents
   */
  public async readDirectory(path: string): Promise<FileSystemEntry[]> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      const entries = await this.db.getAllFromIndex('fs-entries', 'parent', normPath);
      return entries.map(item => {
        // Create a new object with the entry properties
        const entry = {...item.entry};
        // Set isDirectory property for backward compatibility
        entry.isDirectory = entry.type === 'directory';
        return entry;
      });
    } catch (error) {
      console.error(`Error reading directory: ${path}`, error);
      throw new Error(`Failed to read directory: ${path}`);
    }
  }

  /**
   * List directory contents (alias for readDirectory for backward compatibility)
   */
  public async listDirectory(path: string): Promise<FileSystemEntry[]> {
    return this.readDirectory(path);
  }
  /**
   * Read file contents
   */
  public async readFile(path: string): Promise<string> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      const entry = await this.db.get('fs-entries', normPath);
      
      if (!entry || entry.entry.type !== 'file') {
        throw new Error(`Not a file: ${path}`);
      }
      
      // If the file is binary, convert ArrayBuffer to base64 string
      if (entry.entry.isBinary && entry.entry.binaryContent) {
        // Convert ArrayBuffer to string (base64)
        const binary = new Uint8Array(entry.entry.binaryContent);
        let binaryString = '';
        for (let i = 0; i < binary.byteLength; i++) {
          binaryString += String.fromCharCode(binary[i]);
        }
        return btoa(binaryString);
      }
      
      return entry.entry.content || '';
    } catch (error) {
      console.error(`Error reading file: ${path}`, error);
      throw new Error(`Failed to read file: ${path}`);
    }
  }
  /**
   * Write content to a file
   * 
   * @param path File path to write to
   * @param content String content to write (can be raw text or base64-encoded binary)
   * @param isBinary Optional flag to force treating content as base64-encoded binary
   */
  public async writeFile(path: string, content: string, isBinary?: boolean): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      const name = normPath.split('/').pop() || '';
      const parent = normPath.substring(0, normPath.lastIndexOf('/')) || '/';
      
      // Check if parent directory exists
      const parentExists = await this.exists(parent);
      if (!parentExists) {
        throw new Error(`Parent directory does not exist: ${parent}`);
      }
      
      // Determine if content should be treated as binary
      let binaryContent: ArrayBuffer | undefined = undefined;
      let finalContent = content;
      let isContentBinary = isBinary || false;
      
      // If isBinary is true or we detect a base64 string, convert to ArrayBuffer
      if (isBinary || (content.length > 0 && this.isBase64(content))) {
        try {
          const binary = atob(content);
          const bytes = new Uint8Array(binary.length);
          for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
          }
          binaryContent = bytes.buffer;
          finalContent = ''; // Clear text content
          isContentBinary = true;
        } catch (e) {
          console.warn('Failed to convert to binary, treating as text', e);
          isContentBinary = false;
        }
      }
      
      // Check if file already exists
      const exists = await this.exists(normPath);
      if (exists) {
        // Update existing file
        const existingEntry = await this.db.get('fs-entries', normPath);
        const updatedEntry = {
          ...existingEntry,
          entry: {
            ...existingEntry.entry,
            content: isContentBinary ? undefined : finalContent,
            binaryContent: isContentBinary ? binaryContent : undefined,
            isBinary: isContentBinary,
            metadata: {
              ...existingEntry.entry.metadata,
              modified: Date.now(),
              size: isContentBinary ? (binaryContent?.byteLength || 0) : finalContent.length
            }
          }
        };
        
        await this.db.put('fs-entries', updatedEntry);
      } else {
        // Create new file
        await this.db.put('fs-entries', {
          path: normPath,
          parent,
          entry: {
            name,
            type: 'file',
            content: isContentBinary ? undefined : finalContent,
            binaryContent: isContentBinary ? binaryContent : undefined,
            isBinary: isContentBinary,
            metadata: {
              created: Date.now(),
              modified: Date.now(),
              size: isContentBinary ? (binaryContent?.byteLength || 0) : finalContent.length,
              permissions: '-rw-r--r--',
              owner: 'user'
            }
          }
        });
      }
    } catch (error) {
      console.error(`Error writing file: ${path}`, error);
      throw new Error(`Failed to write file: ${path}`);
    }
  }
  
  /**
   * Helper method to check if a string is likely base64 encoded
   */
  private isBase64(str: string): boolean {
    // Quick check for base64 pattern
    if (str.length % 4 !== 0) return false;
    const regex = /^[A-Za-z0-9+/]+={0,2}$/;
    return regex.test(str) && str.length > 24; // Reasonable length for binary data
  }

  /**
   * Read binary file contents
   */
  public async readBinaryFile(path: string): Promise<ArrayBuffer> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      const entry = await this.db.get('fs-entries', normPath);
      
      if (!entry || entry.entry.type !== 'file') {
        throw new Error(`Not a file: ${path}`);
      }
      
      if (!entry.entry.isBinary || !entry.entry.binaryContent) {
        throw new Error(`Not a binary file or no binary content: ${path}`);
      }
      
      return entry.entry.binaryContent;
    } catch (error) {
      console.error(`Error reading binary file: ${path}`, error);
      throw new Error(`Failed to read binary file: ${path}`);
    }
  }

  /**
   * Write binary content to a file
   */
  public async writeBinaryFile(path: string, content: ArrayBuffer): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      const name = normPath.split('/').pop() || '';
      const parent = normPath.substring(0, normPath.lastIndexOf('/')) || '/';
      
      // Check if parent directory exists
      const parentExists = await this.exists(parent);
      if (!parentExists) {
        throw new Error(`Parent directory does not exist: ${parent}`);
      }
      
      // Check if file already exists
      const exists = await this.exists(normPath);
      if (exists) {
        // Update existing file
        const existingEntry = await this.db.get('fs-entries', normPath);
        const updatedEntry = {
          ...existingEntry,
          entry: {
            ...existingEntry.entry,
            content: undefined, // Clear text content
            binaryContent: content,
            isBinary: true,
            metadata: {
              ...existingEntry.entry.metadata,
              modified: Date.now(),
              size: content.byteLength
            }
          }
        };
        
        await this.db.put('fs-entries', updatedEntry);
      } else {
        // Create new file
        await this.db.put('fs-entries', {
          path: normPath,
          parent,
          entry: {
            name,
            type: 'file',
            binaryContent: content,
            isBinary: true,
            metadata: {
              created: Date.now(),
              modified: Date.now(),
              size: content.byteLength,
              permissions: '-rw-r--r--',
              owner: 'user'
            }
          }
        });
      }
    } catch (error) {
      console.error(`Error writing binary file: ${path}`, error);
      throw new Error(`Failed to write binary file: ${path}`);
    }
  }

  /**
   * Check if a file is binary
   */
  public async isBinaryFile(path: string): Promise<boolean> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      const entry = await this.db.get('fs-entries', normPath);
      
      if (!entry || entry.entry.type !== 'file') {
        throw new Error(`Not a file: ${path}`);
      }
      
      return !!entry.entry.isBinary;
    } catch (error) {
      console.error(`Error checking if file is binary: ${path}`, error);
      throw new Error(`Failed to check if file is binary: ${path}`);
    }
  }
  /**
   * Create a directory
   * @param path Path to create directory. If path doesn't start with '/', it's treated as relative to current directory
   * @param currentDirectory Optional current directory for resolving relative paths
   */
  public async createDirectory(path: string, currentDirectory?: string|null|undefined, createParrentsDirrectories?: boolean): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      // Handle relative paths (those not starting with /)
      let resolvedPath = path;
      if (!path.startsWith('/') && currentDirectory) {
        // Join the current directory with the relative path
        resolvedPath = currentDirectory.endsWith('/') 
          ? currentDirectory + path 
          : currentDirectory + '/' + path;
      }
      
      const normPath = this.normalizePath(resolvedPath);
      const name = normPath.split('/').pop() || '';
      const parent = normPath.substring(0, normPath.lastIndexOf('/')) || '/';
      
      // Check if parent directory exists
      const parentExists = await this.exists(parent);
      if (!parentExists) {
        if (createParrentsDirrectories){
          // Create parent directories recursively
          const parentDir = parent.split('/').slice(0, -1).join('/');
          await this.createDirectory(parentDir, null, createParrentsDirrectories);
        }
        else
          throw new Error(`Parent directory does not exist: ${parent}`);
      }
      
      // Check if directory already exists
      const exists = await this.exists(normPath);
      if (exists) {
        throw new Error(`Path already exists: ${path}`);
      }
      
      // Create directory
      await this.db.put('fs-entries', {
        path: normPath,
        parent,
        entry: {
          name,
          type: 'directory',
          metadata: {
            created: Date.now(),
            modified: Date.now(),
            size: 0,
            permissions: 'drwxr-xr-x',
            owner: 'user'
          }
        }
      });
    } catch (error) {
      console.error(`Error creating directory: ${path}`, error);
      throw new Error(`Failed to create directory: ${path}`);
    }
  }

  /**
   * Delete a file or directory
   */
  public async deleteEntry(path: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normPath = this.normalizePath(path);
      
      // Check if path exists
      const exists = await this.exists(normPath);
      if (!exists) {
        throw new Error(`Path does not exist: ${path}`);
      }
      
      // Get entry to check if it's a directory
      const entry = await this.db.get('fs-entries', normPath);
      
      if (entry.entry.type === 'directory') {
        // Check if directory is empty
        const children = await this.readDirectory(normPath);
        if (children.length > 0) {
          throw new Error(`Directory is not empty: ${path}`);
        }
      }
      
      // Delete the entry
      await this.db.delete('fs-entries', normPath);
    } catch (error) {
      console.error(`Error deleting entry: ${path}`, error);
      throw new Error(`Failed to delete entry: ${path}`);
    }
  }

  /**
   * Move a file or directory to a new location
   */
  public async moveEntry(oldPath: string, newPath: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');
    
    try {
      const normOldPath = this.normalizePath(oldPath);
      const normNewPath = this.normalizePath(newPath);
      
      // Check if source exists
      const sourceExists = await this.exists(normOldPath);
      if (!sourceExists) {
        throw new Error(`Source path does not exist: ${oldPath}`);
      }
      
      // Check if destination already exists
      const destExists = await this.exists(normNewPath);
      if (destExists) {
        throw new Error(`Destination path already exists: ${newPath}`);
      }
      
      // Get the entry
      const entry = await this.db.get('fs-entries', normOldPath);
      
      // Create new entry
      const newName = normNewPath.split('/').pop() || '';
      const newParent = normNewPath.substring(0, normNewPath.lastIndexOf('/')) || '/';
      
      // Check if parent directory exists
      const parentExists = await this.exists(newParent);
      if (!parentExists) {
        throw new Error(`Parent directory does not exist: ${newParent}`);
      }
      
      // Update the entry
      const updatedEntry = {
        path: normNewPath,
        parent: newParent,
        entry: {
          ...entry.entry,
          name: newName,
          metadata: {
            ...entry.entry.metadata,
            modified: Date.now()
          }
        }
      };
      
      // Add new entry and delete old one in a transaction
      const tx = this.db.transaction('fs-entries', 'readwrite');
      await tx.store.put(updatedEntry);
      await tx.store.delete(normOldPath);
      await tx.done;
    } catch (error) {
      console.error(`Error moving entry: ${oldPath} -> ${newPath}`, error);
      throw new Error(`Failed to move entry: ${oldPath} -> ${newPath}`);
    }
  }  /**
   * Normalize a path (remove trailing slashes, handle ../, etc.)
   * Now also resolves aliases like ~ and symlinks
   */
  private normalizePath(path: string): string {
    // Special handling for the tilde (~) alias
    if (path === '~' || path.startsWith('~/')) {
      const homeAlias = this.aliases.find(a => a.alias === '~');
      if (homeAlias) {
        const remainingPath = path === '~' ? '' : path.substring(1);
        path = homeAlias.resolve(remainingPath);
      }
    } else {
      // Handle other aliases 
      for (const alias of this.aliases) {
        if (alias.matches(path)) {
          const remainingPath = alias.extractRemainingPath(path);
          path = alias.resolve(remainingPath);
          break; // Only apply the first matching alias
        }
      }
    }
    
    // Ensure path starts with a slash
    if (!path.startsWith('/')) {
      path = '/' + path;
    }
    
    // Remove trailing slash unless it's the root
    if (path.length > 1 && path.endsWith('/')) {
      path = path.slice(0, -1);
    }
      // Handle .. and .
    const parts = path.split('/');
    const stack: string[] = [];
    
    for (const part of parts) {
      if (part === '' || part === '.') continue;
      if (part === '..') {
        if (stack.length > 0) {
          stack.pop();
        }
        // Don't go beyond root directory
      } else {
        stack.push(part);
      }
    }
    
    return '/' + stack.join('/');
  }
  /**
   * Get all currently registered path aliases
   * @returns Array of all registered path aliases
   */
  public getAliases(): PathAlias[] {
    // Return a copy of the aliases array to prevent external modification
    return [...this.aliases];
  }

  /**
   * Parse a path and resolve any aliases
   * This is a public method that can be used to transform any path with aliases into a normalized path
   * @param path The path to parse, which may contain aliases
   * @param currentDirectory Optional current directory for resolving relative paths
   * @returns The normalized path with all aliases resolved
   */
  public parsePath(path: string, currentDirectory?: string): string {

    // Handle relative paths if a current directory is provided
    if (!PathUtils.isAbsolute(path) && !PathUtils.isAlias(path, this.aliases.map(a => a.alias)) && currentDirectory) {
      path = PathUtils.join(currentDirectory, path);
    }
    
    return this.normalizePath(path);
  }
}
