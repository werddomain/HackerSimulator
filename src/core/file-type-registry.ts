/**
 * File Type Registry
 * 
 * This file defines the mapping between file extensions and their metadata,
 * such as mime type, description, default app, and icons.
 */

import { AppManager } from './app-manager';

export interface FileTypeInfo {
  mime: string;
  description: string;
  defaultAppId: string;
  icon?: string;
  extractable?: boolean;
  compressible?: boolean;
}

/**
 * Singleton class to manage file type associations
 */
export class FileTypeRegistry {
  private static instance: FileTypeRegistry;
  private fileTypes: Map<string, FileTypeInfo>;
  private defaultInfo: FileTypeInfo;

  private constructor() {
    this.fileTypes = new Map();
    this.defaultInfo = {
      mime: 'application/octet-stream',
      description: 'Unknown File Type',
      defaultAppId: 'text-editor',
      icon: '📄'
    };

    this.registerDefaultTypes();
  }

  /**
   * Get the singleton instance
   */
  public static getInstance(): FileTypeRegistry {
    if (!FileTypeRegistry.instance) {
      FileTypeRegistry.instance = new FileTypeRegistry();
    }
    return FileTypeRegistry.instance;
  }

  /**
   * Register a new file type
   */
  public register(extension: string, info: FileTypeInfo): void {
    this.fileTypes.set(extension.toLowerCase(), info);
  }

  /**
   * Get info for a specific file extension
   */
  public getInfo(filename: string): FileTypeInfo {
    const extension = this.getExtension(filename);
    return this.fileTypes.get(extension) || this.defaultInfo;
  }

  /**
   * Get the mime type for a file
   */
  public getMimeType(filename: string): string {
    return this.getInfo(filename).mime;
  }

  /**
   * Get the default app ID for a file
   */
  public getDefaultAppId(filename: string): string {
    return this.getInfo(filename).defaultAppId;
  }

  /**
   * Get the file icon for a file
   */
  public getIcon(filename: string): string {
    const info = this.getInfo(filename);
    return info.icon || this.defaultInfo.icon || '📄';
  }

  /**
   * Check if a file is extractable (like zip, tar, etc.)
   */
  public isExtractable(filename: string): boolean {
    const info = this.getInfo(filename);
    return info.extractable || false;
  }

  /**
   * Get file extension from name
   */
  private getExtension(filename: string): string {
    const parts = filename.split('.');
    if (parts.length < 2) return '';
    return parts[parts.length - 1].toLowerCase();
  }

  /**
   * Register all default file types
   */
  private registerDefaultTypes(): void {
    // Text files
    this.register('txt', {
      mime: 'text/plain',
      description: 'Text Document',
      defaultAppId: 'text-editor',
      icon: '📝'
    });
    
    this.register('md', {
      mime: 'text/markdown',
      description: 'Markdown Document',
      defaultAppId: 'text-editor',
      icon: '📝'
    });

    // Code files
    this.register('js', {
      mime: 'application/javascript',
      description: 'JavaScript File',
      defaultAppId: 'code-editor',
      icon: '📜'
    });

    this.register('ts', {
      mime: 'application/typescript',
      description: 'TypeScript File',
      defaultAppId: 'code-editor',
      icon: '📜'
    });

    this.register('json', {
      mime: 'application/json',
      description: 'JSON File',
      defaultAppId: 'code-editor',
      icon: '📊'
    });

    this.register('html', {
      mime: 'text/html',
      description: 'HTML File',
      defaultAppId: 'code-editor',
      icon: '🌐'
    });

    this.register('css', {
      mime: 'text/css',
      description: 'CSS File',
      defaultAppId: 'code-editor',
      icon: '🎨'
    });

    this.register('less', {
      mime: 'text/less',
      description: 'LESS File',
      defaultAppId: 'code-editor',
      icon: '🎨'
    });

    this.register('py', {
      mime: 'text/x-python',
      description: 'Python File',
      defaultAppId: 'code-editor',
      icon: '🐍'
    });

    // Image files
    this.register('jpg', {
      mime: 'image/jpeg',
      description: 'JPEG Image',
      defaultAppId: 'image-viewer',
      icon: '🖼️'
    });

    this.register('jpeg', {
      mime: 'image/jpeg',
      description: 'JPEG Image',
      defaultAppId: 'image-viewer',
      icon: '🖼️'
    });

    this.register('png', {
      mime: 'image/png',
      description: 'PNG Image',
      defaultAppId: 'image-viewer',
      icon: '🖼️'
    });

    this.register('gif', {
      mime: 'image/gif',
      description: 'GIF Image',
      defaultAppId: 'image-viewer',
      icon: '🖼️'
    });

    this.register('svg', {
      mime: 'image/svg+xml',
      description: 'SVG Image',
      defaultAppId: 'image-viewer',
      icon: '🖼️'
    });

    // Audio files
    this.register('mp3', {
      mime: 'audio/mpeg',
      description: 'MP3 Audio',
      defaultAppId: 'media-player',
      icon: '🎵'
    });

    this.register('wav', {
      mime: 'audio/wav',
      description: 'WAV Audio',
      defaultAppId: 'media-player',
      icon: '🎵'
    });

    // Video files
    this.register('mp4', {
      mime: 'video/mp4',
      description: 'MP4 Video',
      defaultAppId: 'media-player',
      icon: '🎬'
    });

    this.register('webm', {
      mime: 'video/webm',
      description: 'WebM Video',
      defaultAppId: 'media-player',
      icon: '🎬'
    });

    // Archive files
    this.register('zip', {
      mime: 'application/zip',
      description: 'ZIP Archive',
      defaultAppId: 'file-explorer',
      icon: '📦',
      extractable: true
    });

    this.register('tar', {
      mime: 'application/x-tar',
      description: 'TAR Archive',
      defaultAppId: 'file-explorer',
      icon: '📦',
      extractable: true
    });

    this.register('gz', {
      mime: 'application/gzip',
      description: 'GZIP Archive',
      defaultAppId: 'file-explorer',
      icon: '📦',
      extractable: true
    });

    // Document files
    this.register('pdf', {
      mime: 'application/pdf',
      description: 'PDF Document',
      defaultAppId: 'pdf-viewer',
      icon: '📑'
    });

    this.register('doc', {
      mime: 'application/msword',
      description: 'Word Document',
      defaultAppId: 'text-editor',
      icon: '📄'
    });

    this.register('docx', {
      mime: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      description: 'Word Document',
      defaultAppId: 'text-editor',
      icon: '📄'
    });

    // Executable files
    this.register('exe', {
      mime: 'application/x-msdownload',
      description: 'Windows Executable',
      defaultAppId: 'wine',
      icon: '⚙️'
    });

    this.register('sh', {
      mime: 'application/x-sh',
      description: 'Shell Script',
      defaultAppId: 'terminal',
      icon: '⚙️'
    });
  }
}
