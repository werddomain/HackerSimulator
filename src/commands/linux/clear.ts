import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';

/**
 * clear command - Clear the terminal screen
 */
export class ClearCommand implements CommandModule {
  public get name(): string {
    return 'clear';
  }
  
  public get description(): string {
    return 'Clear the terminal screen';
  }
  
  public get usage(): string {
    return 'clear';
  }
  
  public async exec(_args: CommandArgs): Promise<string> {
    // Special return value that terminal will interpret as clear screen
    return '\x1B[2J\x1B[0f';
  }
    /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    return ExecuteMigrator.execute(this, args, context);
  }
}
