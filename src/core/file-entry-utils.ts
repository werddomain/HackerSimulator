import { FileSystemEntry } from './filesystem';

/**
 * Utility class for handling file system entries
 * Provides abstraction for common file operations and properties
 */
export class FileEntryUtils {
  /**
   * Check if an entry is a directory
   * @param entry File system entry
   * @returns True if the entry is a directory
   */
  public static isDirectory(entry: FileSystemEntry | { type: string } | { isDirectory: boolean }): boolean {
    if ('isDirectory' in entry) {
      return entry.isDirectory;
    }
    
    if ('type' in entry) {
      return entry.type === 'directory';
    }
    
    return false;
  }
  
  /**
   * Get file icon based on file name and type
   * @param fileName Name of the file
   * @param entry File system entry
   * @returns Icon string representation
   */
  public static getFileIcon(fileName: string, entry: FileSystemEntry | { type: string } | { isDirectory: boolean }): string {
    if (this.isDirectory(entry)) {
      return '📁';
    }
    
    const extension = fileName.split('.').pop()?.toLowerCase();
    
    // Images
    if (['jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg'].includes(extension || '')) {
      return '🖼️';
    }
    
    // Audio
    if (['mp3', 'wav', 'ogg', 'flac'].includes(extension || '')) {
      return '🎵';
    }
    
    // Video
    if (['mp4', 'webm', 'avi', 'mov', 'wmv'].includes(extension || '')) {
      return '🎬';
    }
    
    // Code
    if (['js', 'ts', 'html', 'css', 'py', 'java', 'c', 'cpp', 'php'].includes(extension || '')) {
      return '📊';
    }
    
    // Documents
    if (['pdf'].includes(extension || '')) {
      return '📕';
    }
    if (['doc', 'docx'].includes(extension || '')) {
      return '📘';
    }
    if (['xls', 'xlsx'].includes(extension || '')) {
      return '📗';
    }
    if (['ppt', 'pptx'].includes(extension || '')) {
      return '📙';
    }
    
    // Archives
    if (['zip', 'rar', 'tar', 'gz', '7z'].includes(extension || '')) {
      return '📦';
    }
    
    // Executable
    if (['exe', 'bat', 'sh', 'app'].includes(extension || '')) {
      return '⚙️';
    }
    
    // Default
    return '📄';
  }
}
