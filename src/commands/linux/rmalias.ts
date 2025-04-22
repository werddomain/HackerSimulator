import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * Command to remove a path alias from the filesystem
 */
export class RmAliasCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'rmalias';
  }
  
  public get description(): string {
    return 'Remove a filesystem path alias';
  }
  
  public get usage(): string {
    return 'rmalias <alias>';
  }
  
  async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      const cmdArgs = args.args || [];
      
      if (cmdArgs.length !== 1) {
        context.stderr.writeLine("Error: Invalid arguments");
        context.stdout.writeLine(`Usage: ${this.usage}`);
        context.stdout.writeLine("Examples:");
        context.stdout.writeLine("  rmalias projects");
        return 1;
      }
      
      const aliasName = cmdArgs[0];
      
      // Check if the alias exists before trying to remove it
      const aliases = this.os.getFileSystem().getAliases();
      const aliasExists = aliases.some(alias => alias.alias === aliasName);
      
      if (!aliasExists) {
        context.stderr.writeLine(`Error: Alias not found: ${aliasName}`);
        return 1;
      }
      
      // Don't allow removing system aliases like ~
      if (aliasName === '~') {
        context.stderr.writeLine("Error: Cannot remove system alias '~'");
        return 1;
      }
      
      // Remove the alias
      this.os.getFileSystem().unregisterAlias(aliasName);
      context.stdout.writeLine(`Alias removed: ${aliasName}`);
      
      return 0;
    } catch (error) {
      context.stderr.writeLine(`Error removing alias: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
}
