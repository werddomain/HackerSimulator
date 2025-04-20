import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

/**
 * rm command - Remove files or directories
 */
export class RmCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'rm';
  }
  
  public get description(): string {
    return 'Remove files or directories';
  }
  
  public get usage(): string {
    return 'rm [options] file...';
  }
  
  /**
   * Display help information for the rm command
   */
  private displayHelp(context: CommandContext): void {
    context.stdout.writeLine('Usage: rm [OPTION]... [FILE]...');
    context.stdout.writeLine('Remove (unlink) the FILE(s).');
    context.stdout.writeLine('');
    context.stdout.writeLine('Options:');
    context.stdout.writeLine('  -f, --force           ignore nonexistent files and arguments, never prompt');
    context.stdout.writeLine('  -r, -R, --recursive   remove directories and their contents recursively');
    context.stdout.writeLine('  -v, --verbose         explain what is being done');
    context.stdout.writeLine('      --help            display this help and exit');
    context.stdout.writeLine('');
    context.stdout.writeLine('By default, rm does not remove directories. Use the --recursive (-r or -R)');
    context.stdout.writeLine('option to remove each listed directory, too, along with all of its contents.');
  }
    /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Check for help argument
      if (args.help) {
        this.displayHelp(context);
        return 0;
      }
      
      // Check if any file is specified
      if (args.args.length === 0) {
        context.stderr.writeLine('rm: missing operand');
        context.stderr.writeLine('Try \'rm --help\' for more information.');
        return 1;
      }      // Parse options
      const recursive = args.r || args.R || args.recursive || false; // -r, -R, --recursive to remove directories and their contents
      const force = args.f || args.force || false; // -f or --force to ignore nonexistent files and never prompt
      const verbose = args.v || args.verbose || false; // -v or --verbose to explain what is being done
      const promptEach = args.i || false; // -i prompt before every removal
      const promptBulk = args.I || false; // -I prompt once before removing multiple files
      const removeEmptyDir = args.d || args.dir || false; // -d, --dir remove empty directories

      // Check if bulk prompt is needed
      if (promptBulk && !force && args.args.length > 3) {
        context.stdout.writeLine(`rm: remove ${args.args.length} arguments? `);
        const answer = await context.stdin.readLine();
        if (answer.toLowerCase() !== 'y' && answer.toLowerCase() !== 'yes') {
          context.stdout.writeLine('rm: operation cancelled');
          return 0;
        }
      }

      // Process each file
      const fs = context.os.getFileSystem();
        for (const filePath of args.args) {
        // Handle filenames that start with dash (--) special case
        let resolvedPath = filePath;
        let displayPath = filePath;
        
        // Resolve path (handle both absolute and relative paths)        if (!PathUtils.isAbsolute(filePath)) {
          resolvedPath = PathUtils.join(context.cwd, filePath);
        
        
        try {
          // Check if path exists
          const exists = await fs.exists(resolvedPath);
          
          if (!exists) {
            if (!force) {
              context.stderr.writeLine(`rm: cannot remove '${displayPath}': No such file or directory`);
              return 1;
            }
            continue; // Skip if force is true
          }
          
          // Get file stats
          const stats = await fs.stat(resolvedPath);
          
          // Prompt before removal if -i flag is set
          if (promptEach && !force) {
            context.stdout.writeLine(`rm: remove '${displayPath}'? `);
            const answer = await context.stdin.readLine();
            if (answer.toLowerCase() !== 'y' && answer.toLowerCase() !== 'yes') {
              continue; // Skip this file
            }
          }
          
          // Handle directory removal
          if (stats.isDirectory) {
            // For empty directories with -d flag
            const entries = await fs.listDirectory(resolvedPath);
            
            if (entries.length === 0 && removeEmptyDir) {
              // Empty directory with -d flag
              await fs.remove(resolvedPath);
              if (verbose) {
                context.stdout.writeLine(`removed directory '${displayPath}'`);
              }
              continue;
            }
            
            // Check if recursive flag is set for non-empty directories
            if (!recursive) {
              context.stderr.writeLine(`rm: cannot remove '${displayPath}': Is a directory`);
              return 1;
            }
            
            // Prompt for recursive removal if -I flag is set and not already prompted
            if (promptBulk && !force && entries.length > 0) {
              context.stdout.writeLine(`rm: remove directory '${displayPath}' and its contents? `);
              const answer = await context.stdin.readLine();
              if (answer.toLowerCase() !== 'y' && answer.toLowerCase() !== 'yes') {
                continue; // Skip this directory
              }
            }
          }
          
          // Remove the file or directory
          await fs.remove(resolvedPath);
          
          if (verbose) {
            context.stdout.writeLine(`removed '${displayPath}'`);
          }
        } catch (error) {
          if (!force) {
            context.stderr.writeLine(`rm: cannot remove '${displayPath}': ${error instanceof Error ? error.message : String(error)}`);
            return 1;
          }
        }
      }
      
      return 0; // Success exit code
    } catch (error) {
      context.stderr.writeLine(`rm: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1; // Error exit code
    }
  }
}
