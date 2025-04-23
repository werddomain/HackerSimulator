import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * grep command - Print lines that match patterns
 */
export class GrepCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'grep';
  }
  
  public get description(): string {
    return 'Print lines that match patterns';
  }
    public get usage(): string {
    return `grep [options] PATTERN [FILE...]

Options:
  -i, --ignore-case       Ignore case distinctions in patterns and data
  -v, --invert-match      Select non-matching lines
  -n, --line-number       Prefix each line of output with its line number
  -c, --count             Print only a count of matching lines
  -o, --only-matching     Show only the part of a line matching PATTERN
  -r, -R, --recursive     Read all files under each directory recursively

Examples:
  grep "error" log.txt     Display all lines containing "error" in log.txt
  grep -i "warning" *.log  Case-insensitive search for "warning" in all .log files
  grep -v "success" log    Display all lines NOT containing "success"`;
  }
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Check if pattern is provided
      if (args.args.length === 0) {
        context.stderr.writeLine('grep: no pattern specified');
        return 1;
      }
      
      // Parse options
      const ignoreCase = args.i || args['ignore-case'] || false; // -i, --ignore-case
      const invertMatch = args.v || args['invert-match'] || false; // -v, --invert-match
      const lineNumbers = args.n || args['line-number'] || false; // -n, --line-number
      const onlyCount = args.c || args.count || false; // -c, --count
      const onlyMatching = args.o || args['only-matching'] || false; // -o, --only-matching
      const recursive = args.r || args.R || args.recursive || false; // -r, -R, --recursive
      
      // Get pattern and files
      const pattern = args.args[0];
      let files = args.args.slice(1);
      
      // If no files specified but pattern is, read from stdin
      if (files.length === 0) {
        const input = await context.stdin.read();
        
        if (!input) {
          return 0; // No input, no results
        }
        
        const results = this.processContent(input, pattern, {
          ignoreCase,
          invertMatch,
          lineNumbers,
          onlyCount,
          onlyMatching
        });
        
        if (results) {
          context.stdout.writeLine(results);
        }
        
        return 0;
      }
      
      // Process each file
      let exitCode = 0;
      
      for (const file of files) {
        try {
          // Read file content
          const content = await this.os.getFileSystem().readFile(file);
          
          // Process content
          const results = this.processContent(content, pattern, {
            ignoreCase,
            invertMatch,
            lineNumbers,
            onlyCount,
            onlyMatching
          });
          
          // If multiple files, prefix each line with filename
          if (files.length > 1 && results) {
            const lines = results.split('\n');
            for (const line of lines) {
              if (line) {
                context.stdout.writeLine(`${file}:${line}`);
              }
            }
          } else if (results) {
            context.stdout.writeLine(results);
          }
        } catch (error) {
          context.stderr.writeLine(`grep: ${file}: ${error}`);
          exitCode = 1;
        }
      }
      
      return exitCode;
    } catch (error) {
      context.stderr.writeLine(`grep: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
  
  /**
   * Process content with the given pattern and options
   */
  private processContent(
    content: string,
    pattern: string,
    options: {
      ignoreCase: boolean;
      invertMatch: boolean;
      lineNumbers: boolean;
      onlyCount: boolean;
      onlyMatching: boolean;
    }
  ): string {
    // Split content into lines
    const lines = content.split('\n');
    
    // Create regex from pattern
    const flags = options.ignoreCase ? 'i' : '';
    const regex = new RegExp(pattern, flags);
    
    // Filter lines based on pattern
    const matchingLines = lines.filter(line => 
      options.invertMatch ? !regex.test(line) : regex.test(line)
    );
    
    // Return only the count if requested
    if (options.onlyCount) {
      return matchingLines.length.toString();
    }
    
    // Format output
    return matchingLines.map((line, index) => {
      let output = '';
      
      // Add line number if requested
      if (options.lineNumbers) {
        output += `${index + 1}:`;
      }
      
      // Add only the matching parts if requested
      if (options.onlyMatching) {
        const matches = [];
        let match;
        const matchRegex = new RegExp(pattern, flags + 'g');
        
        while ((match = matchRegex.exec(line)) !== null) {
          matches.push(match[0]);
        }
        
        return output + matches.join('\n');
      }
      
      // Add the entire line
      return output + line;
    }).join('\n');
  }
}
