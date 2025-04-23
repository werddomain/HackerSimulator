import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * wc command - Print newline, word, and byte counts for files
 */
export class WcCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'wc';
  }
    public get description(): string {
    return 'Print newline, word, and byte counts';
  }
  
  public get usage(): string {
    return `Usage: wc [options] [file...]

Print newline, word, and byte counts for each file.

Options:
  -l, --lines     print the newline counts
  -w, --words     print the word counts
  -c, --bytes     print the byte counts
  -m, --chars     print the character counts

With no options, print line, word, and byte counts.
With no file, read standard input.

Examples:
  wc file.txt            Print line, word, and byte counts for file.txt
  wc -l file.txt         Print only line count for file.txt
  wc -w file.txt         Print only word count for file.txt
  wc -c file.txt         Print only byte count for file.txt
  wc -m file.txt         Print only character count for file.txt
  wc file1.txt file2.txt Print counts for each file and a total
  cat file.txt | wc      Print counts for data from standard input`;
  }
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Parse options
      const countLines = args.l || args.lines || false; // -l, --lines
      const countWords = args.w || args.words || false; // -w, --words
      const countBytes = args.c || args.bytes || false; // -c, --bytes
      const countChars = args.m || args.chars || false; // -m, --chars
      
      // If no specific counts requested, count everything
      const countAll = !(countLines || countWords || countBytes || countChars);
      
      // Get files from arguments
      const files = args.args;
      let totalLines = 0;
      let totalWords = 0;
      let totalBytes = 0;
      let totalChars = 0;
      
      // Process input
      if (files.length === 0) {
        // Read from stdin
        const content = await context.stdin.read();
        const { lines, words, bytes, chars } = this.getCounts(content);
        
        // Display counts
        this.displayCounts(
          context, 
          lines, words, bytes, chars,
          countLines || countAll, 
          countWords || countAll, 
          countBytes || countAll, 
          countChars,
          ''
        );
        
        return 0;
      }
      
      // Process each file
      for (const file of files) {
        try {
          const content = await this.os.getFileSystem().readFile(file);
          const { lines, words, bytes, chars } = this.getCounts(content);
          
          // Add to totals
          totalLines += lines;
          totalWords += words;
          totalBytes += bytes;
          totalChars += chars;
          
          // Display counts for this file
          this.displayCounts(
            context, 
            lines, words, bytes, chars,
            countLines || countAll, 
            countWords || countAll, 
            countBytes || countAll, 
            countChars,
            file
          );
        } catch (error) {
          context.stderr.writeLine(`wc: ${file}: ${error}`);
          return 1;
        }
      }
      
      // If multiple files, display total
      if (files.length > 1) {
        this.displayCounts(
          context, 
          totalLines, totalWords, totalBytes, totalChars,
          countLines || countAll, 
          countWords || countAll, 
          countBytes || countAll, 
          countChars,
          'total'
        );
      }
      
      return 0;
    } catch (error) {
      context.stderr.writeLine(`wc: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
  
  /**
   * Count lines, words, bytes, and chars in content
   */
  private getCounts(content: string): { lines: number; words: number; bytes: number; chars: number } {
    const lines = content.split('\n').length;
    const words = content.trim().split(/\s+/).filter(word => word.length > 0).length;
    const bytes = Buffer.from(content).length;
    const chars = content.length;
    
    return { lines, words, bytes, chars };
  }
  
  /**
   * Display counts in the correct format
   */
  private displayCounts(
    context: CommandContext,
    lines: number,
    words: number,
    bytes: number,
    chars: number,
    showLines: boolean,
    showWords: boolean,
    showBytes: boolean,
    showChars: boolean,
    filename: string
  ): void {
    const parts: string[] = [];
    
    if (showLines) {
      parts.push(lines.toString().padStart(7));
    }
    
    if (showWords) {
      parts.push(words.toString().padStart(7));
    }
    
    if (showChars) {
      parts.push(chars.toString().padStart(7));
    } else if (showBytes) {
      parts.push(bytes.toString().padStart(7));
    }
    
    if (filename) {
      parts.push(filename);
    }
    
    context.stdout.writeLine(parts.join(' '));
  }
}
