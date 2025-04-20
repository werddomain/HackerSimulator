import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * man command - Display manual pages for commands
 */
export class ManCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'man';
  }
  
  public get description(): string {
    return 'Display the manual page for a command';
  }
    public get usage(): string {
    return 'man command_name';
  }
  
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // Check if a command is specified
    if (args.args.length === 0) {
      context.stdout.writeLine('What manual page do you want?\nFor example, try \'man man\'.');
      return 1;
    }
    
    const commandName = args.args[0];
    
    // Get all available commands
    const allCommands = this.os.getCommandProcessor().getAllCommands();
    
    // Find the specified command
    const command = allCommands.find(cmd => cmd.name === commandName);
    
    if (!command) {
      context.stdout.writeLine(`No manual entry for ${commandName}`);
      return 1;
    }
    
    // Format the manual page and write to stdout
    const manPage = this.formatManPage(command);
    context.stdout.writeLine(manPage);
    
    return 0;
  }
  
  /**
   * Format a command as a manual page
   */
  private formatManPage(command: CommandModule): string {
    const header = `${command.name.toUpperCase()}(1)                User Commands                ${command.name.toUpperCase()}(1)`;
    const name = `NAME\n       ${command.name} - ${command.description}`;
    const synopsis = `SYNOPSIS\n       ${command.usage}`;
    const description = `DESCRIPTION\n       ${this.expandDescription(command)}`;
    const footer = `HackerOS                         April 2025                        ${command.name.toUpperCase()}(1)`;
    
    return [header, '', name, '', synopsis, '', description, '', footer].join('\n');
  }
  
  /**
   * Expand command description with more detailed information when available
   */
  private expandDescription(command: CommandModule): string {
    // This is where you would add more detailed descriptions for each command
    // For now, we'll just return the basic description
    
    // Special case for the 'man' command itself
    if (command.name === 'man') {
      return `The man command displays manual pages for commands available in the system.
       
       When you run 'man command_name', it displays information about that command,
       including its description, usage syntax, and available options.`;
    }
    
    return command.description;
  }
}
