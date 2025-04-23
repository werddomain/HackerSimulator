import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';
import { OS } from '../../core/os';
import { Process } from '../../core/process';

/**
 * ps command - Report process status
 */
export class PsCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'ps';
  }
  
  public get description(): string {
    return 'Report a snapshot of the current processes';
  }
    public get usage(): string {
    return `ps [options]
    
Options:
  -a, -e, -A    show all processes
  -f            full format listing
  -u            include user-oriented format
  
Examples:
  ps            show basic process list for current user
  ps -a         show all processes in simple format
  ps -af        show all processes with full details`;
  }
  public execute(args: CommandArgs, context: CommandContext): Promise<number>{
    return ExecuteMigrator.execute(this, args, context);
    }
  public async exec(args: CommandArgs): Promise<string> {
    // Parse options
    const all = args.a || args.e || args.A || false; // -a, -e, -A show all processes
    const full = args.f || false; // -f full format listing
    const showUser = args.u || false; // -u include user-oriented format
    
    // Get processes
    const processes = this.os.getProcessManager().getAllProcesses();
    
    // Filter processes if not showing all
    const filteredProcesses = all 
      ? processes 
      : processes.filter(p => p.user === 'user'); // Only show current user's processes
    
    if (filteredProcesses.length === 0) {
      return 'No processes found';
    }
    
    // Format the output
    if (full || showUser) {
      // Full or user-oriented format
      const headers = ['PID', 'USER', 'START', '%CPU', '%MEM', 'STAT', 'COMMAND'];
      const rows = filteredProcesses.map(process => this.formatProcessRow(process));
      
      // Calculate column widths
      const colWidths = headers.map((header, i) => {
        const maxDataWidth = Math.max(...rows.map(row => row[i].length));
        return Math.max(header.length, maxDataWidth);
      });
      
      // Format the table
      const headerRow = headers
        .map((header, i) => header.padEnd(colWidths[i]))
        .join(' ');
      
      const dataRows = rows.map(row => 
        row.map((cell, i) => cell.padEnd(colWidths[i])).join(' ')
      );
      
      return [headerRow, ...dataRows].join('\n');
    } else {
      // Simple format
      const output = filteredProcesses.map(process => 
        `${process.pid.toString().padStart(5)} ${process.status.charAt(0)} ${process.command || process.name}`
      );
      
      return '  PID S COMMAND\n' + output.join('\n');
    }
  }
  
  /**
   * Format process information into a row for display
   */
  private formatProcessRow(process: Process): string[] {
    // Convert start time to readable format
    const startTime = new Date(process.startTime).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
    
    // Convert process status to a single character
    const statusChar = process.status.charAt(0).toUpperCase();
    
    // Format CPU and memory usage
    const cpuUsage = (process.cpuUsage * 100).toFixed(1);
    const memUsage = process.memoryUsage.toFixed(1);
    
    return [
      process.pid.toString(),
      process.user,
      startTime,
      cpuUsage,
      memUsage,
      statusChar,
      process.command || process.name
    ];
  }
}
