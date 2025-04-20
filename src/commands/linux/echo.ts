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
  public execute(args: CommandArgs, context: CommandContext): Promise<number>{
    return ExecuteMigrator.execute(this, args, context);
    }
  public async exec(args: CommandArgs): Promise<string> {
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
    
    // Remove trailing newline if -n is set
    if (noNewline) {
      return output;
    } else {
      return output;
    }
  }
}
