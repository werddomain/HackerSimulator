import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

/**
 * find command - Search for files in a directory hierarchy
 */
export class FindCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'find';
  }
  
  public get description(): string {
    return 'Search for files in a directory hierarchy';
  }
    public get usage(): string {
    return 'find [path...] [expression]\n' +
      '  Options:\n' +
      '    -name PATTERN      Find files whose name matches the pattern\n' +
      '    -type TYPE         Find files of type: f (regular file), d (directory)\n' +
      '    -maxdepth LEVELS   Descend at most LEVELS of directories';
  }
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Default path is current directory if none specified
      const paths = args.args.length > 0 ? args.args : [context.cwd];
      
      // Parse options
      const nameFilter = args.name as string | undefined;
      const typeFilter = args.type as string | undefined;
      const maxDepth = args.maxdepth !== undefined ? parseInt(args.maxdepth as string) : undefined;
      
      for (const path of paths) {
        const absolutePath = PathUtils.resolve(context.cwd, path);
        
        try {
          // Check if path exists
          const fileEntry = await this.os.getFileSystem().exists(absolutePath);
          if (!fileEntry) {
            context.stderr.writeLine(`find: '${path}': No such file or directory`);
            continue;
          }
          
          // Find files recursively
          const foundEntries = await this.findRecursively(
            absolutePath,
            {
              namePattern: nameFilter,
              type: typeFilter,
              maxDepth: maxDepth
            }
          );
          
          // Print results
          for (const entry of foundEntries) {
            context.stdout.writeLine(entry);
          }
        } catch (error) {
          context.stderr.writeLine(`find: '${path}': ${error instanceof Error ? error.message : String(error)}`);
        }
      }
      
      return 0;
    } catch (error) {
      context.stderr.writeLine(`find: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
  
  /**
   * Find files recursively
   */
  private async findRecursively(
    path: string,
    options: {
      namePattern?: string;
      type?: string;
      maxDepth?: number;
      currentDepth?: number;
    }
  ): Promise<string[]> {
    const currentDepth = options.currentDepth || 0;
    
    // Stop if we've reached max depth
    if (options.maxDepth !== undefined && currentDepth > options.maxDepth) {
      return [];
    }
    
    // Get entries in current directory
    let result: string[] = [];
    
    try {
      const stats = await this.os.getFileSystem().stat(path);
      
      // Check if this entry matches the type filter
      const isDirectory = stats.isDirectory;
      const entryType = isDirectory ? 'd' : 'f';
      
      const matchesType = !options.type || options.type === entryType;
      
      // Check if the name matches the pattern
      const filename = PathUtils.basename(path);
      let matchesName = true;
      
      if (options.namePattern) {
        // Convert glob pattern to regex
        const regexPattern = options.namePattern
          .replace(/\./g, '\\.')
          .replace(/\*/g, '.*')
          .replace(/\?/g, '.');
          
        const regex = new RegExp(`^${regexPattern}$`);
        matchesName = regex.test(filename);
      }
      
      // Add this path if it matches filters
      if (matchesType && matchesName) {
        result.push(path);
      }
      
      // Recurse into subdirectories
      if (isDirectory) {
        const entries = await this.os.getFileSystem().readDirectory(path);
        
        for (const entry of entries) {
          const entryPath = PathUtils.join(path, entry.name);
          
          const subResults = await this.findRecursively(entryPath, {
            ...options,
            currentDepth: currentDepth + 1
          });
          
          result = result.concat(subResults);
        }
      }
    } catch (error) {
      // Skip entries with errors
      console.error(`Error processing ${path}:`, error);
    }
    
    return result;
  }
}
