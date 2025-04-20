import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

/**
 * mv command - Move (rename) files
 */
export class MvCommand implements CommandModule {
  private os: OS;
    currentDirrectory?: string;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'mv';
  }
  
  public get description(): string {
    return 'Move (rename) files';
  }
  
  public get usage(): string {
    return 'mv [options] source... destination';
  }
  
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
      this.currentDirrectory = context.cwd;
        return ExecuteMigrator.execute(this, args, context);
  }
    public async exec(args: CommandArgs): Promise<string> {
    // Need at least source and destination
    if (args.args.length < 2) {
      return 'mv: missing file operand\nTry \'mv --help\' for more information.';
    }
    
    // Parse options
    const force = args.f || args.force || false; // -f or --force to overwrite without prompt
    const verbose = args.v || args.verbose || false; // -v or --verbose to explain what is being done
    
    // Get source and destination
    const sources = args.args.slice(0, -1);
    const destination = args.args[args.args.length - 1];
    
    // Process the move operation
    const results: string[] = [];
      // Helper function to resolve paths relative to current directory
    const resolvePath = (filePath: string): string => {
      if (PathUtils.isAbsolute(filePath)) {
        return filePath;
      } else {
        // If currentDirrectory is set, use it to resolve relative paths
        if (this.currentDirrectory) {
          return PathUtils.join(this.currentDirrectory, filePath);
        }
        return filePath;
      }
    };
      try {
      // Check if destination exists and is a directory
      let isDestDir = false;
      const resolvedDestination = resolvePath(destination);
      try {
        const destStat = await this.os.getFileSystem().stat(resolvedDestination);
        isDestDir = destStat.isDirectory;
      } catch (error) {
        // Destination doesn't exist
        // If multiple sources, destination must be a directory
        if (sources.length > 1) {
          return `mv: target '${destination}' is not a directory`;
        }
      }
        // Move each source
      for (const source of sources) {
        try {
          const resolvedSource = resolvePath(source);
          
          // Determine destination path
          let destPath = resolvedDestination;
          if (isDestDir) {            // If destination is a directory, append source basename
            const basename = PathUtils.basename(resolvedSource);
            destPath = PathUtils.join(resolvedDestination, basename);
          }
          
          // Check if destination exists
          try {
            await this.os.getFileSystem().stat(destPath);
            if (!force) {
              // In a real terminal, this would prompt for confirmation
              // For simplicity, we'll just overwrite
            }
          } catch (error) {
            // Destination doesn't exist, which is fine
          }
            // Move the file or directory
          await this.os.getFileSystem().move(resolvedSource, destPath);
          
          if (verbose) {
            results.push(`'${source}' -> '${destination}'`);
          }
        } catch (error) {
          results.push(`mv: cannot move '${source}' to '${destination}': ${error}`);
        }
      }
    } catch (error) {
      return `mv: error: ${error}`;
    }
    
    return results.length > 0 ? results.join('\n') : '';
  }
}
