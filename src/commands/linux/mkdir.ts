import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * mkdir command - Create directories
 */
export class MkdirCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'mkdir';
  }
  
  public get description(): string {
    return 'Create directories';
  }
    public get usage(): string {
    return 'mkdir [options] directory...';
  }
  
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // Check if any directory is specified
    if (args.args.length === 0) {
      context.stderr.writeLine('mkdir: missing operand\nTry \'mkdir --help\' for more information.');
      return 1;
    }
    
    // Parse options
    const createParents = args.p || args.parents || false; // -p or --parents to create parent directories
    
    // Process each directory
    const errors: string[] = [];
    
    for (const dirPath of args.args) {      try {
        if (createParents) {
          // Create parent directories as needed
          await this.createParentDirectories(dirPath, context.cwd);
        } else {
          // Just create the specified directory
          await this.os.getFileSystem().createDirectory(dirPath, context.cwd);
        }
      } catch (error) {
        errors.push(`mkdir: cannot create directory '${dirPath}': ${error}`);
      }
    }
    
    if (errors.length > 0) {
      context.stderr.writeLine(errors.join('\n'));
      return 1;
    }
    
    return 0;
  }
  /**
   * Create a directory and its parent directories if they don't exist
   * @param path Path to create directory
   * @param currentDirectory Current working directory for resolving relative paths
   */
  private async createParentDirectories(path: string, currentDirectory: string): Promise<void> {
    // Split the path into components
    const components = path.split('/').filter(component => component.length > 0);
    
    if (path.startsWith('/')) {
      // Start from root if path is absolute
      let currentPath = '/';
      
      for (const component of components) {
        currentPath += (currentPath.endsWith('/') ? '' : '/') + component;
        
        try {
          // Check if the directory exists
          const stat = await this.os.getFileSystem().stat(currentPath);
          
          // If it exists but is not a directory, throw an error
          if (stat && !stat.isDirectory) {
            throw new Error(`Not a directory`);
          }
        } catch (error) {
          // Directory doesn't exist, create it
          await this.os.getFileSystem().createDirectory(currentPath);
        }
      }
    } else {
      // For relative paths, start with the current working directory
      let startPath = currentDirectory;
      
      // Make sure startPath doesn't have a trailing slash unless it's root
      if (startPath !== '/' && startPath.endsWith('/')) {
        startPath = startPath.slice(0, -1);
      }
      
      // Create each component of the path
      let currentPath = startPath;
      
      for (const component of components) {
        currentPath += (currentPath.endsWith('/') ? '' : '/') + component;
        
        try {
          // Check if the directory exists
          const stat = await this.os.getFileSystem().stat(currentPath);
          
          // If it exists but is not a directory, throw an error
          if (stat && !stat.isDirectory) {
            throw new Error(`Not a directory`);
          }
        } catch (error) {
          // Directory doesn't exist, create it
          await this.os.getFileSystem().createDirectory(currentPath);
        }
      }
    }
  }
}
