import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * help command - Display help for available commands
 */
export class HelpCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'help';
  }
  
  public get description(): string {
    return 'Display help for available commands';
  }
  
  public get usage(): string {
    return 'help [command]';
  }
  
  /**
   * Execute the help command
   * @param args Command arguments
   * @param context Command execution context
   * @returns Exit code (0 for success)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // If a specific command is requested, show detailed help for that command
    if (args.args.length > 0) {
      const commandName = args.args[0];
      return this.showCommandHelp(commandName, context);
    }
    
    // Otherwise, show a list of all available commands
    return this.showCommandList(context);
  }
  
  /**
   * Show detailed help for a specific command
   */
  private async showCommandHelp(commandName: string, context: CommandContext): Promise<number> {
    // Get command registry from OS
    const commandRegistry = this.os.getCommandProcessor();
    const commands = commandRegistry.getAllCommands();
    
    // Find the requested command
    const command = commands.find(cmd => cmd.name === commandName);
    
    if (!command) {
      context.stderr.writeLine(`help: command not found: ${commandName}`);
      return 1;
    }
    
    // Display command information
    context.stdout.writeLine(`Command: ${command.name}`);
    context.stdout.writeLine(`Description: ${command.description}`);
    context.stdout.writeLine(`Usage: ${command.usage}`);
    
    // For more detailed help, suggest man command
    context.stdout.writeLine('');
    context.stdout.writeLine(`For more detailed information, try 'man ${command.name}'`);
    
    return 0;
  }
  
  /**
   * Show a list of all available commands
   */
  private async showCommandList(context: CommandContext): Promise<number> {
    // Get command registry from OS
    const commandRegistry = this.os.getCommandProcessor();
    const commands = commandRegistry.getAllCommands();
    
    // Sort commands alphabetically
    const sortedCommands = [...commands].sort((a, b) => a.name.localeCompare(b.name));
    
    // Display header
    context.stdout.writeLine('Available commands:');
    context.stdout.writeLine('==================');
    
    // Find the longest command name for padding
    const longestNameLength = Math.max(...sortedCommands.map(cmd => cmd.name.length));
    
    // Display each command with its description
    for (const command of sortedCommands) {
      const paddedName = command.name.padEnd(longestNameLength + 2);
      context.stdout.writeLine(`${paddedName}${command.description}`);
    }
    
    // Display footer with usage information
    context.stdout.writeLine('');
    context.stdout.writeLine('For more information on a command, type:');
    context.stdout.writeLine('  help <command>');
    context.stdout.writeLine('  man <command>');
    
    return 0;
  }
}
