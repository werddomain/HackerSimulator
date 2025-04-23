import { CommandModule, CommandArgs } from '../command-processor';
import { EnhancedCommandModule, CommandContext } from '../enhanced-command-processor';
import { OS } from '../../core/os';
import { FileEntryUtils } from '../../core/file-entry-utils';

/**
 * ls command - List directory contents
 */
export class LsCommand implements CommandModule, EnhancedCommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'ls';
  }
  
  public get description(): string {
    return 'List directory contents';
  }
    public get usage(): string {
    return 'ls [OPTION]... [FILE]...\n' +
           'List information about the FILEs (the current directory by default).\n\n' +
           'Options:\n' +
           '  -a, --all       do not ignore entries starting with .\n' +
           '  -l              use a long listing format\n' +
           '  -h              with -l, print sizes in human readable format (e.g., 1K 234M 2G)\n\n' +
           'If no path is specified, the contents of the current working directory are displayed.';
  }
   
  /**
   * Enhanced execute method with context and streams
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Parse options
      const showAll = args.a || args.all || false; // -a or --all to show hidden files
      const longFormat = args.l || false; // -l for long listing format
      const humanReadable = args.h || false; // -h for human readable sizes
      
      // Get target path (default to current working directory)
      const path = args.args[0] || context.cwd;
      
      // Get directory contents
      const entries = await context.os.getFileSystem().listDirectory(path);
      
      // Filter hidden files (those starting with .) if -a not specified
      const filteredEntries = showAll 
        ? entries 
        : entries.filter(entry => !entry.name.startsWith('.'));
      
      if (filteredEntries.length === 0) {
        return 0; // Success with no output
      }
      
      // Format the output
      if (longFormat) {
        // Long format (-l)
        for (const entry of filteredEntries) {
          const isDir = FileEntryUtils.isDirectory(entry);
          const perms = isDir ? 'drwxr-xr-x' : '-rw-r--r--';
          const size = entry.metadata?.size !== undefined 
            ? (humanReadable ? this.formatSize(entry.metadata.size) : entry.metadata.size.toString())
            : '0';
          const date = entry.metadata?.modified 
            ? new Date(entry.metadata.modified).toLocaleString('en-US', { 
                month: 'short', 
                day: '2-digit', 
                hour: '2-digit', 
                minute: '2-digit' 
              }) 
            : 'Jan 01 00:00';
          const owner = 'user';
          const group = 'user';
          
          // Write directly to stdout stream
          context.stdout.writeLine(
            `${perms} 1 ${owner} ${group} ${size.padStart(8)} ${date} ${entry.name}${isDir ? '/' : ''}`
          );
        }
      } else {
        // Simple format - collect all entries and write at once
        const formattedEntries = filteredEntries.map(entry => {
          const isDir = FileEntryUtils.isDirectory(entry);
          return `${entry.name}${isDir ? '/' : ''}`;
        });
        
        // Write directly to stdout stream
        context.stdout.writeLine(formattedEntries.join('  '));
      }
      
      return 0; // Success exit code
    } catch (error) {
      // Write error to stderr
      context.stderr.writeLine(
        `ls: cannot access '${args.args[0] || context.cwd}': ${error}`
      );
      return 1; // Error exit code
    }
  }
  
  /**
   * Format file size in human readable format
   */
  private formatSize(bytes: number): string {
    const units = ['B', 'K', 'M', 'G', 'T'];
    let size = bytes;
    let unitIndex = 0;
    
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    
    return `${size.toFixed(unitIndex > 0 ? 1 : 0)}${units[unitIndex]}`;
  }
}
