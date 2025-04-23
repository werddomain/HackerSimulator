import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

/**
 * cp command - Copy files and directories
 */
export class CpCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  public execute(args: CommandArgs, context: CommandContext): Promise<number>{
      return ExecuteMigrator.execute(this, args, context);
      }
  public get name(): string {
    return 'cp';
  }
  
  public get description(): string {
    return 'Copy files and directories';
  }
    public get usage(): string {
    return `Usage: cp [options] source... destination

Options:
  -r, -R, --recursive    Copy directories recursively
  -v, --verbose          Explain what is being done

Examples:
  cp file1.txt file2.txt             # Copy file1.txt to file2.txt
  cp file.txt dir/                   # Copy file.txt into directory dir/
  cp -r dir1/ dir2/                  # Copy directory dir1/ recursively to dir2/
  cp -v *.txt backups/               # Verbosely copy all .txt files to backups/ directory
  cp file1.txt file2.txt dir/        # Copy multiple files to directory dir/`;
  }
  
  public async exec(args: CommandArgs): Promise<string> {
    // Need at least source and destination
    if (args.args.length < 2) {
      return 'cp: missing file operand\nTry \'cp --help\' for more information.';
    }
    
    // Parse options
    const recursive = args.r || args.R || args.recursive || false; // -r, -R or --recursive to copy directories recursively
    const verbose = args.v || args.verbose || false; // -v or --verbose to explain what is being done
    
    // Get source and destination
    const sources = args.args.slice(0, -1);
    const destination = args.args[args.args.length - 1];
    
    // Process the copy operation
    const results: string[] = [];
    
    try {
      // Check if destination exists and is a directory
      let isDestDir = false;
      try {
        const destStat = await this.os.getFileSystem().stat(destination);
        isDestDir = destStat.isDirectory;
      } catch (error) {
        // Destination doesn't exist
        // If multiple sources, destination must be a directory
        if (sources.length > 1) {
          return `cp: target '${destination}' is not a directory`;
        }
      }
      
      // Copy each source
      for (const source of sources) {
        try {
          const sourceStat = await this.os.getFileSystem().stat(source);
          
          if (sourceStat.isDirectory && !recursive) {
            results.push(`cp: omitting directory '${source}'`);
            continue;
          }
          
          // Determine destination path
          let destPath = destination;
          if (isDestDir) {            // If destination is a directory, append source basename
            const basename = PathUtils.basename(source);
            destPath = PathUtils.join(destination, basename);
          }
          
          if (sourceStat.isDirectory) {
            // Copy directory recursively
            await this.copyDirectoryRecursive(source, destPath);
            if (verbose) {
              results.push(`'${source}' -> '${destPath}'`);
            }
          } else {
            // Copy file
            const content = await this.os.getFileSystem().readFile(source);
            await this.os.getFileSystem().writeFile(destPath, content);
            if (verbose) {
              results.push(`'${source}' -> '${destPath}'`);
            }
          }
        } catch (error) {
          results.push(`cp: cannot copy '${source}': ${error}`);
        }
      }
    } catch (error) {
      return `cp: error: ${error}`;
    }
    
    return results.length > 0 ? results.join('\n') : '';
  }
  
  /**
   * Copy a directory recursively
   */
  private async copyDirectoryRecursive(source: string, destination: string): Promise<void> {
    // Create destination directory
    try {
      await this.os.getFileSystem().createDirectory(destination);
    } catch (error) {
      // Directory might already exist, which is fine
    }
    
    // List source directory contents
    const entries = await this.os.getFileSystem().listDirectory(source);
    
    // Copy each entry
    for (const entry of entries) {      const sourcePath = PathUtils.join(source, entry.name);
      const destPath = PathUtils.join(destination, entry.name);
      
      if (entry.isDirectory) {
        // Recursively copy subdirectory
        await this.copyDirectoryRecursive(sourcePath, destPath);
      } else {
        // Copy file
        const content = await this.os.getFileSystem().readFile(sourcePath);
        await this.os.getFileSystem().writeFile(destPath, content);
      }
    }
  }
}
