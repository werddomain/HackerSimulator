import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * pwd command - Print working directory
 */
export class PwdCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'pwd';
  }
  
  public get description(): string {
    return 'Print the current working directory';
  }
  
  public get usage(): string {
    return 'pwd';
  }
  
  /**
   * Execute the pwd command
   * @param args Command arguments
   * @param context Command execution context
   * @returns Exit code (0 for success)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // Print the current working directory to stdout
    context.stdout.writeLine(context.cwd);
    return 0; // Success
  }
}
