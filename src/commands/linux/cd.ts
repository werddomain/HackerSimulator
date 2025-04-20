import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

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
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      const targetPath = args.args[0] || '/home/user';
        // Resolve the path (handle both absolute and relative paths)
      let resolvedPath = targetPath;
      if (!PathUtils.isAbsolute(targetPath)) {
        resolvedPath = PathUtils.join(context.cwd, targetPath);
      }
      
      // Check if the directory exists before changing to it
      try {
        const fileInfo = await this.os.getFileSystem().stat(resolvedPath);
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
      
      return 0;
    } catch (error) {
      context.stderr.writeLine(`cd: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
}
