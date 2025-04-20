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
    public execute(args: CommandArgs, context: CommandContext): Promise<number>{
    return ExecuteMigrator.execute(this, args, context);
    }
  
  public get name(): string {
    return 'cat';
  }
  
  public get description(): string {
    return 'Concatenate and display file contents';
  }
  
  public get usage(): string {
    return 'cat [options] file [file...]';
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
