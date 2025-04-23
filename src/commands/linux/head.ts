import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * head command - Output the first part of files
 */
export class HeadCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'head';
  }
  
  public get description(): string {
    return 'Output the first part of files';
  }
    public get usage(): string {
    return 'head [OPTION]... [FILE]...\n' +
           'Print the first 10 lines of each FILE to standard output.\n' +
           'With more than one FILE, precede each with a header giving the file name.\n\n' +
           'Options:\n' +
           '  -n N         output the first N lines (default: 10)\n' +
           '  -N           same as -n N\n\n' +
           'If no FILE is specified, or when FILE is -, read standard input.';
  }
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Parse options
      // Default is 10 lines
      let numLines = 10;
      
      // Parse -n option
      if (args.n !== undefined) {
        const n = parseInt(args.n.toString());
        if (!isNaN(n)) {
          numLines = n;
        }
      }
      
      // Parse first argument as number if no -n provided
      if (args.n === undefined && args.args.length > 0 && args.args[0].startsWith('-')) {
        const n = parseInt(args.args[0].substring(1));
        if (!isNaN(n)) {
          numLines = n;
          args.args.shift(); // Remove the number arg
        }
      }
      
      // Get files from arguments
      const files = args.args;
      
      // If no files specified, read from stdin
      if (files.length === 0) {
        const content = await context.stdin.read();
        const result = this.getHeadLines(content, numLines);
        context.stdout.writeLine(result);
        return 0;
      }
      
      // Process each file
      let exitCode = 0;
      
      for (const file of files) {
        try {
          // Read file content
          const content = await this.os.getFileSystem().readFile(file);
          
          // If multiple files, print filename header
          if (files.length > 1) {
            context.stdout.writeLine(`==> ${file} <==`);
          }
          
          // Get and display head lines
          const result = this.getHeadLines(content, numLines);
          context.stdout.writeLine(result);
          
          // Add a blank line between files
          if (files.length > 1 && file !== files[files.length - 1]) {
            context.stdout.writeLine('');
          }
        } catch (error) {
          context.stderr.writeLine(`head: cannot open '${file}' for reading: ${error}`);
          exitCode = 1;
        }
      }
      
      return exitCode;
    } catch (error) {
      context.stderr.writeLine(`head: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
  
  /**
   * Get the first n lines of content
   */
  private getHeadLines(content: string, numLines: number): string {
    const lines = content.split('\n');
    return lines.slice(0, numLines).join('\n');
  }
}
