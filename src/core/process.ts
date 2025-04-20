/**
 * Interface for system processes
 */
export interface Process {
  pid: number;
  name: string;
  cpuUsage: number;
  memoryUsage: number;
  status: 'running' | 'sleeping' | 'stopped';
  startTime: number;
  user: string;
  windowId?: string;
  command?: string;
  parentPid?: number;
  childPids: number[];
  onKill?: () => void;
}

/**
 * Process Manager class for managing system processes
 */
export class ProcessManager {
  private processes: Map<number, Process> = new Map();
  private nextPid: number = 1;
  private simulationInterval: number | null = null;

  /**
   * Initialize the process manager
   */
  public init(): void {
    console.log('Initializing Process Manager...');
    
    // Create initial system processes
    this.createSystemProcesses();
    
    // Start simulation interval to fluctuate CPU and memory usage
    this.simulationInterval = window.setInterval(() => this.simulateProcessActivity(), 2000);
  }
  /**
   * Create initial system processes
   */
  private createSystemProcesses(): void {
    // Add some basic system processes
    this.createProcess('init', 'root', 0.1, 3, undefined, undefined, undefined, undefined, true);
    this.createProcess('systemd', 'root', 0.3, 8, undefined, undefined, undefined, undefined, true);
    this.createProcess('kworker', 'root', 0.2, 4, undefined, undefined, undefined, undefined, true);
    this.createProcess('sshd', 'root', 0.1, 5, undefined, undefined, undefined, undefined, true);
  }/**
   * Create a new process
   */
  public createProcess(
    name: string, 
    user: string = 'user', 
    initialCpuUsage: number = 0.1,
    initialMemoryUsage: number = 10,
    command?: string,
    windowId?: string,
    parentPid?: number,
    onKill?: () => void,
    isSystemProcess: boolean = false
  ): number {
    // Ensure system processes have PIDs < 10 and non-system processes have PIDs >= 10
    let pid: number;
    if (isSystemProcess) {
      // For system processes, use PIDs < 10
      pid = this.nextPid++;
      if (pid >= 10) {
        console.warn('Warning: Running out of system process IDs');
      }
    } else {
      // For non-system processes, ensure PID is >= 10
      if (this.nextPid < 10) {
        this.nextPid = 10;
      }
      pid = this.nextPid++;
    }
    
    const process: Process = {
      pid,
      name,
      cpuUsage: initialCpuUsage,
      memoryUsage: initialMemoryUsage,
      status: 'running',
      startTime: Date.now(),
      user,
      command,
      windowId,
      parentPid,
      childPids: [],
      onKill
    };
    
    this.processes.set(pid, process);
    
    // If parent process is specified, add this process as a child
    if (parentPid !== undefined) {
      const parentProcess = this.processes.get(parentPid);
      if (parentProcess) {
        parentProcess.childPids.push(pid);
      }
    }
    
    return pid;
  }  /**
   * Kill a process by its PID
   */
  public killProcess(pid: number): boolean {
    const process = this.processes.get(pid);
    if (!process) {
      return false;
    }

    // Close the window if this process has a windowId
    if (process.windowId) {
      const windowManager = (window as any).os?.getWindowManager();
      if (windowManager) {
        windowManager.closeWindow(process.windowId);
      }
    }

    // Call the onKill callback if it exists
    if (process.onKill) {
      process.onKill();
    }

    // Kill all child processes recursively
    if (process.childPids.length > 0) {
      // Create a copy of the array to avoid modification during iteration
      const childPids = [...process.childPids];
      for (const childPid of childPids) {
        this.killProcess(childPid);
      }
    }

    // If this process has a parent, remove it from the parent's children
    if (process.parentPid !== undefined) {
      const parentProcess = this.processes.get(process.parentPid);
      if (parentProcess) {
        parentProcess.childPids = parentProcess.childPids.filter(childPid => childPid !== pid);
      }
    }

    // Remove the process
    return this.processes.delete(pid);
  }

  /**
   * Kill all processes (except system ones)
   */
  public killAllProcesses(): void {
    // Filter out system processes (PIDs < 10)
    for (const pid of this.processes.keys()) {
      if (pid >= 10) {
        this.processes.delete(pid);
      }
    }
  }

  /**
   * Get all processes
   */
  public getAllProcesses(): Process[] {
    return Array.from(this.processes.values());
  }

  /**
   * Get a process by PID
   */
  public getProcess(pid: number): Process | undefined {
    return this.processes.get(pid);
  }

  /**
   * Get total CPU usage (0-100)
   */
  public getTotalCpuUsage(): number {
    let total = 0;
    for (const process of this.processes.values()) {
      total += process.cpuUsage;
    }
    // Cap at 100%
    return Math.min(total * 100, 100);
  }

  /**
   * Get total memory usage (0-100)
   */
  public getTotalMemoryUsage(): number {
    let total = 0;
    for (const process of this.processes.values()) {
      total += process.memoryUsage;
    }
    // Cap at 100%
    return Math.min(total, 100);
  }

  /**
   * Simulate process activity by randomly adjusting CPU and memory usage
   */
  private simulateProcessActivity(): void {
    for (const process of this.processes.values()) {
      // Randomly adjust CPU usage
      process.cpuUsage = Math.max(0.01, Math.min(1, process.cpuUsage + (Math.random() * 0.1 - 0.05)));
      
      // Randomly adjust memory usage
      process.memoryUsage = Math.max(1, Math.min(50, process.memoryUsage + (Math.random() * 2 - 1)));
    }
  }

  /**
   * Clean up resources
   */
  public dispose(): void {
    if (this.simulationInterval !== null) {
      clearInterval(this.simulationInterval);
      this.simulationInterval = null;
    }
  }
}
