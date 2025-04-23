import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';
import { OS } from '../../core/os';

/**
 * kill command - Terminate processes
 */
export class KillCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'kill';
  }
  
  public get description(): string {
    return 'Terminate processes by PID';
  }
    public get usage(): string {
    return 'kill [OPTION]... PID...\n' +
           'Terminate processes by process ID (PID).\n\n' +
           'Options:\n' +
           '  -s SIGNAL    specify the signal to send (default: TERM)\n\n' +
           'PIDs must be positive integer process IDs.\n' +
           'If no processes are found matching the specified PIDs, an error message is displayed.';
  }
  public execute(args: CommandArgs, context: CommandContext): Promise<number>{
    return ExecuteMigrator.execute(this, args, context);
    }
  public async exec(args: CommandArgs): Promise<string> {
    // Check if any PID is specified
    if (args.args.length === 0) {
      return 'kill: usage: kill [-s signal] pid...';
    }
    
    // Process each PID
    const results: string[] = [];
    
    for (const pidStr of args.args) {
      try {
        const pid = parseInt(pidStr, 10);
        
        if (isNaN(pid)) {
          results.push(`kill: ${pidStr}: arguments must be process or job IDs`);
          continue;
        }
        
        // Attempt to kill the process
        const success = this.os.getProcessManager().killProcess(pid);
        
        if (!success) {
          results.push(`kill: (${pid}) - No such process`);
        }
      } catch (error) {
        results.push(`kill: ${pidStr}: ${error}`);
      }
    }
    
    return results.length > 0 ? results.join('\n') : '';
  }
}
