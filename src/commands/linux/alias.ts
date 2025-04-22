import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * Command to list all path aliases in the filesystem
 */
export class AliasCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'alias';
  }
  
  public get description(): string {
    return 'List all filesystem path aliases';
  }
  
  public get usage(): string {
    return 'alias';
  }
  
  async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      const aliases = this.os.getFileSystem().getAliases();
      
      if (aliases.length === 0) {
        context.stdout.writeLine("No path aliases defined.");
        return 0;
      }
      
      context.stdout.writeLine("PATH ALIASES:");
      context.stdout.writeLine("=============");
      
      for (const alias of aliases) {
        let aliasInfo = `${alias.alias}`;
        
        // Determine the type of alias
        if (alias.constructor.name === 'FixedPathAlias') {
          const targetPath = (alias as any).targetPath; // Using any for simplicity
          aliasInfo += ` -> ${targetPath} (fixed)`;
        } else if (alias.constructor.name === 'SymlinkAlias') {
          const targetPath = (alias as any).targetPath; // Using any for simplicity
          aliasInfo += ` -> ${targetPath} (symlink)`;
        } else if (alias.constructor.name === 'DynamicPathAlias') {
          aliasInfo += ` -> [dynamic]`;
        }
        
        context.stdout.writeLine(aliasInfo);
      }
      
      return 0;
    } catch (error) {
      context.stderr.writeLine(`Error listing aliases: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
}
