import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';

/**
 * echo command - Display a line of text
 */
export class EchoCommand implements CommandModule {
  public get name(): string {
    return 'echo';
  }
  
  public get description(): string {
    return 'Display a line of text';
  }
    public get usage(): string {
    return 'echo [options] [string...]';
  }
  
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // Parse options
    const noNewline = args.n || false; // -n do not output trailing newline
    const escapeBackslash = args.e || false; // -e enable interpretation of backslash escapes
    
    // Combine all arguments into one string
    let output = args.args.join(' ');
    
    // Handle escape sequences if -e is set
    if (escapeBackslash) {
      output = output
        .replace(/\\n/g, '\n')   // newline
        .replace(/\\t/g, '\t')   // tab
        .replace(/\\r/g, '\r')   // carriage return
        .replace(/\\\\/, '\\');  // backslash
    }
    
    // Write output to stdout without adding a newline if -n is set
    if (noNewline) {
      context.stdout.write(output);
    } else {
      context.stdout.writeLine(output);
    }
    
    return 0; // Return success exit code
  }
}
