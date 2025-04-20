import { FileSystemEntry } from './filesystem';
import { FileEntryUtils } from './file-entry-utils';

/**
 * File class that implements FileSystemEntry interface
 * with additional utility methods built-in
 */
export class File implements FileSystemEntry {
  name: string;
  type: 'file' | 'directory';
  content?: string;
  metadata: {
    created: number;
    modified: number;
    size: number;
    permissions: string;
    owner: string;
  };

  /**
   * Create a new File from a FileSystemEntry
   * @param entry The original FileSystemEntry
   */
  constructor(entry: FileSystemEntry) {
    this.name = entry.name;
    this.type = entry.type;
    this.content = entry.content;
    this.metadata = { ...entry.metadata };
  }
  /**
   * Check if this entry is a directory
   */
  get isDirectory(): boolean {
    return FileEntryUtils.isDirectory(this);
  }

  /**
   * Check if this entry is a file
   */
  get isFile(): boolean {
    return !this.isDirectory;
  }

  /**
   * Get the creation time as a Date object
   */
  get createdTime(): Date {
    return new Date(this.metadata.created);
  }

  /**
   * Get the modification time as a Date object
   */
  get modifiedTime(): Date {
    return new Date(this.metadata.modified);
  }

  /**
   * Get the appropriate icon for this file
   */
  getIcon(): string {
    return FileEntryUtils.getFileIcon(this.name, this);
  }

  /**
   * Convert to a plain FileSystemEntry object
   */
  toEntry(): FileSystemEntry {
    return {
      name: this.name,
      type: this.type,
      content: this.content,
      metadata: { ...this.metadata }, isDirectory: this.isDirectory
    };
  }

  /**
   * Create a File from a FileSystemEntry
   * @param entry The FileSystemEntry to convert
   */
  static from(entry: FileSystemEntry): File {
    return new File(entry);
  }

  /**
   * Convert an array of FileSystemEntry objects to File objects
   * @param entries The array of FileSystemEntry objects
   */
  static fromArray(entries: FileSystemEntry[]): File[] {
    return entries.map(entry => new File(entry));
  }

  /**
   * Create a new directory
   * @param name The name of the directory
   * @param owner The owner of the directory (default: 'user')
   */
  static createDirectory(name: string, owner: string = 'user'): File {
    return new File({
      name,
      type: 'directory',
      metadata: {
        created: Date.now(),
        modified: Date.now(),
        size: 0,
        permissions: 'drwxr-xr-x',
        owner
      }, isDirectory: true
    });
  }

  /**
   * Create a new file
   * @param name The name of the file
   * @param content The content of the file
   * @param owner The owner of the file (default: 'user')
   */
  static createFile(name: string, content: string = '', owner: string = 'user'): File {
    return new File({
      name,
      type: 'file',
      content,
      metadata: {
        created: Date.now(),
        modified: Date.now(),
        size: content.length,
        permissions: '-rw-r--r--',
        owner
      }, isDirectory: false
    });
  }
}
