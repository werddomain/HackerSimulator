import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';
import { files } from 'jszip';

/**
 * cd command - Change the current working directory
 */
export class CdCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'cd';
  }
  
  public get description(): string {
    return 'Change the current working directory';
  }
  
  public get usage(): string {
    return 'cd [directory]';
  }
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      const fileSystem = this.os.getFileSystem();
      const targetPath = args.args[0] || fileSystem.UserFolder; // Default to home directory if no argument is provided
      
      // Special handling for parent directory navigation
      let resolvedPath;
      
      if (targetPath === '..') {
        // Get the normalized full path of the current directory
        const normalizedCwd = fileSystem.parsePath(context.cwd);
        
        // Don't go up beyond the root directory
        if (normalizedCwd === '/') {
          resolvedPath = '/';
        } else {
          // Extract the parent directory from the normalized path
          resolvedPath = normalizedCwd.substring(0, normalizedCwd.lastIndexOf('/'));
          if (resolvedPath === '') resolvedPath = '/'; // In case we end up with empty string
        }
      } else {
        // For all other paths, use the parsePath method
        resolvedPath = fileSystem.parsePath(targetPath, context.cwd);
      }
      
      // Check if the directory exists before changing to it
      try {
        const fileInfo = await fileSystem.stat(resolvedPath);
        if (!fileInfo.isDirectory) {
          context.stderr.writeLine(`cd: ${targetPath}: Not a directory`);
          return 1;
        }
      } catch (error) {
        context.stderr.writeLine(`cd: ${targetPath}: No such file or directory`);
        return 1;
      }
      
      // Update the working directory in the context
      context.cwd = resolvedPath;
      
      
      
      // If needed, update environment variables
      if (context.env) {
        context.env.PWD = resolvedPath;
      }
      context.stdout.writeLine(`cd: Current dirrectory is now: ${resolvedPath}`);
      return 0;
    } catch (error) {
      context.stderr.writeLine(`cd: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
}
