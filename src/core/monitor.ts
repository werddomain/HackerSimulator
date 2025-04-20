import { ProcessManager } from './process';

/**
 * System Monitor class for displaying system resources
 */
export class SystemMonitor {
  private processManager: ProcessManager;
  private updateInterval: number | null = null;
  private cpuIndicator: HTMLElement | null = null;
  private ramIndicator: HTMLElement | null = null;

  constructor(processManager: ProcessManager) {
    this.processManager = processManager;
  }

  /**
   * Initialize the system monitor
   */
  public init(): void {
    console.log('Initializing System Monitor...');
    
    // Get element references
    this.cpuIndicator = document.getElementById('cpu-indicator');
    this.ramIndicator = document.getElementById('ram-indicator');
    
    if (!this.cpuIndicator || !this.ramIndicator) {
      console.error('System indicator elements not found');
      return;
    }
    
    // Start update interval
    this.updateInterval = window.setInterval(() => this.updateIndicators(), 1000);
    
    // Initial update
    this.updateIndicators();
  }

  /**
   * Update system resource indicators
   */
  private updateIndicators(): void {
    if (!this.cpuIndicator || !this.ramIndicator) return;
    
    // Get CPU and RAM usage from process manager
    const cpuUsage = this.processManager.getTotalCpuUsage();
    const ramUsage = this.processManager.getTotalMemoryUsage();
    
    // Update indicators
    this.cpuIndicator.textContent = `CPU: ${Math.round(cpuUsage)}%`;
    this.ramIndicator.textContent = `RAM: ${Math.round(ramUsage)}%`;
    
    // Update indicator colors based on usage
    this.updateIndicatorColor(this.cpuIndicator, cpuUsage);
    this.updateIndicatorColor(this.ramIndicator, ramUsage);
  }

  /**
   * Update indicator color based on usage
   */
  private updateIndicatorColor(element: HTMLElement, usage: number): void {
    if (usage < 60) {
      element.style.color = 'white';
    } else if (usage < 80) {
      element.style.color = 'yellow';
    } else {
      element.style.color = 'red';
    }
  }

  /**
   * Clean up resources
   */
  public dispose(): void {
    if (this.updateInterval !== null) {
      clearInterval(this.updateInterval);
      this.updateInterval = null;
    }
  }
}
