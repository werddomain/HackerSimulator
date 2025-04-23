import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';
import { OS } from '../../core/os';

/**
 * cat command - Concatenate and display file contents
 */
export class CatCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Check if any file is specified
      if (args.args.length === 0) {
        context.stderr.writeLine('cat: no file specified');
        return 1;
      }
      
      // Parse options
      const numberLines = args.n || false; // -n to number lines
      const showEnds = args.E || false; // -E to show line endings
      
      // Read each file and concatenate their contents
      const results: string[] = [];
      
      for (const filePath of args.args) {
        try {
          const content = await this.os.getFileSystem().readFile(filePath);
          
          // Process content based on options
          if (numberLines || showEnds) {
            const lines = content.split('\n');
            const formattedLines = lines.map((line, i) => {
              let output = '';
              
              // Add line number if -n is specified
              if (numberLines) {
                output += `${(i + 1).toString().padStart(6, ' ')}  `;
              }
              
              // Add the line content
              output += line;
              
              // Add $ at the end of each line if -E is specified
              if (showEnds) {
                output += '$';
              }
              
              return output;
            });
            
            results.push(formattedLines.join('\n'));
          } else {
            results.push(content);
          }
        } catch (error) {
          context.stderr.writeLine(`cat: ${filePath}: ${error}`);
          return 1;
        }
      }
      
      // Write the concatenated output to stdout
      context.stdout.write(results.join('\n'));
      return 0;
    } catch (error) {
      context.stderr.writeLine(`cat: error: ${error}`);
      return 1;
    }
  }
  
  public get name(): string {
    return 'cat';
  }
  
  public get description(): string {
    return 'Concatenate and display file contents';
  }
    public get usage(): string {
    return `Usage: cat [options] file [file...]

Concatenate and display the content of files.

Options:
  -n        Number all output lines, starting with 1
  -E        Display $ at the end of each line

Examples:
  cat file.txt                # Display contents of file.txt
  cat -n file.txt             # Display file.txt with line numbers
  cat -E file.txt             # Display file.txt with $ at end of each line
  cat -n -E file.txt          # Display file.txt with line numbers and $ markers
  cat file1.txt file2.txt     # Concatenate and display multiple files`;
  }
  
  public async exec(args: CommandArgs): Promise<string> {
    try {
      // Check if any file is specified
      if (args.args.length === 0) {
        return 'cat: no file specified';
      }
      
      // Parse options
      const numberLines = args.n || false; // -n to number lines
      const showEnds = args.E || false; // -E to show line endings
      
      // Read each file and concatenate their contents
      const results: string[] = [];
      
      for (const filePath of args.args) {
        try {
          const content = await this.os.getFileSystem().readFile(filePath);
          
          // Process content based on options
          if (numberLines || showEnds) {
            const lines = content.split('\n');
            const formattedLines = lines.map((line, i) => {
              let output = '';
              
              // Add line number if -n is specified
              if (numberLines) {
                output += `${(i + 1).toString().padStart(6, ' ')}  `;
              }
              
              // Add the line content
              output += line;
              
              // Add $ at the end of each line if -E is specified
              if (showEnds) {
                output += '$';
              }
              
              return output;
            });
            
            results.push(formattedLines.join('\n'));
          } else {
            results.push(content);
          }
        } catch (error) {
          results.push(`cat: ${filePath}: ${error}`);
        }
      }
      
      return results.join('\n');
    } catch (error) {
      return `cat: error: ${error}`;
    }
  }
}
