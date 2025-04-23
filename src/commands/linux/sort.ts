import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * sort command - Sort lines of text files
 */
export class SortCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'sort';
  }
  
  public get description(): string {
    return 'Sort lines of text files';
  }
    public get usage(): string {
    return `Usage: sort [options] [file...]

Sort lines of text files.

Options:
  -r, --reverse       reverse the result of comparisons
  -f, --ignore-case   fold lower case to upper case characters
  -n, --numeric-sort  compare according to string numerical value
  -u, --unique        output only the first of an equal run

If no file is specified, read from standard input.

Examples:
  sort file.txt                Sort lines in file.txt
  sort -r file.txt             Sort lines in reverse order
  sort -f file.txt             Sort lines ignoring case
  sort -n file.txt             Sort lines numerically
  sort -u file.txt             Output only unique lines
  echo "text" | sort           Sort text from standard input`;
  }
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Parse options
      const reverse = args.r || args.reverse || false; // -r, --reverse
      const ignoreCase = args.f || args['ignore-case'] || false; // -f, --ignore-case
      const numeric = args.n || args.numeric || false; // -n, --numeric-sort
      const unique = args.u || args.unique || false; // -u, --unique
      
      // Get files from arguments
      const files = args.args;
      
      // Content to sort
      let content = '';
      
      // If no files specified, read from stdin
      if (files.length === 0) {
        content = await context.stdin.read();
      } else {
        // Read content from each file
        for (const file of files) {
          try {
            const fileContent = await this.os.getFileSystem().readFile(file);
            content += fileContent + '\n';
          } catch (error) {
            context.stderr.writeLine(`sort: cannot read '${file}': ${error}`);
            return 1;
          }
        }
      }
      
      // Split content into lines
      let lines = content.split('\n');
      
      // Remove empty lines at the end if they exist
      while (lines.length > 0 && lines[lines.length - 1] === '') {
        lines.pop();
      }
      
      // Filter out unique lines if requested
      if (unique) {
        const uniqueLines = new Set<string>();
        lines.forEach(line => uniqueLines.add(line));
        lines = Array.from(uniqueLines);
      }
      
      // Sort lines
      lines.sort((a, b) => {
        // Prepare values for comparison
        let valueA = a;
        let valueB = b;
        
        // Case insensitive comparison
        if (ignoreCase) {
          valueA = valueA.toLowerCase();
          valueB = valueB.toLowerCase();
        }
        
        // Numeric sort
        if (numeric) {
          const numA = this.extractNumber(valueA);
          const numB = this.extractNumber(valueB);
          
          if (numA !== null && numB !== null) {
            return numA - numB;
          }
        }
        
        // Default string comparison
        return valueA.localeCompare(valueB);
      });
      
      // Reverse if requested
      if (reverse) {
        lines.reverse();
      }
      
      // Write sorted output
      context.stdout.writeLine(lines.join('\n'));
      
      return 0;
    } catch (error) {
      context.stderr.writeLine(`sort: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
  
  /**
   * Extract a number from the beginning of a string
   */
  private extractNumber(value: string): number | null {
    const match = value.match(/^(\d+)/);
    if (match) {
      return parseInt(match[1], 10);
    }
    return null;
  }
}
