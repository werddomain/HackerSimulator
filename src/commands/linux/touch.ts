import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

/**
 * touch command - Change file timestamps or create empty files
 */
export class TouchCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'touch';
  }
  
  public get description(): string {
    return 'Update the access and modification times of each FILE to the current time, or create empty files';
  }
  
  public get usage(): string {
    return 'touch [options] file...';
  }
  
  /**
   * Execute the touch command
   * @param args Command arguments
   * @param context Command execution context
   * @returns Exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // Check if there are any files specified
    if (args.args.length === 0) {
      context.stderr.writeLine('touch: missing file operand');
      context.stderr.writeLine('Try \'touch --help\' for more information.');
      return 1; // Error exit code
    }

    let success = true;

    // Process each file
    for (const filePath of args.args) {
      try {        // Resolve the file path relative to the current directory
        const resolvedPath = PathUtils.resolve(context.cwd, filePath);
        
        // Check if file exists
        const fileExists = await this.os.getFileSystem().exists(resolvedPath);
        
        if (fileExists) {
          // To update the timestamp, we need to read the file and write it back
          // This will automatically update the modified timestamp
          const content = await this.os.getFileSystem().readFile(resolvedPath);
          await this.os.getFileSystem().writeFile(resolvedPath, content);
        } else {
          // Create empty file
          await this.os.getFileSystem().writeFile(resolvedPath, '');
        }
      } catch (error) {
        context.stderr.writeLine(`touch: cannot touch '${filePath}': ${error instanceof Error ? error.message : String(error)}`);
        success = false;
      }
    }

    return success ? 0 : 1; // Return 0 on success, 1 on any error
  }
}
