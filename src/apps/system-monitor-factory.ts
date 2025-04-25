// filepath: c:\Users\clefw\source\repos\HackerSimulator\src\apps\system-monitor-factory.ts
import { OS } from '../core/os';
import { PlatformDetector } from '../core/platform-detector';
import { SystemMonitorApp } from './system-monitor';
import { MobileSystemMonitorApp } from './mobile-system-monitor';

/**
 * Factory class for creating appropriate System Monitor app based on platform
 */
export class SystemMonitorFactory {
  /**
   * Creates the appropriate System Monitor app based on platform detection
   * @param os The OS instance
   * @returns SystemMonitorApp or MobileSystemMonitorApp
   */
  public static createSystemMonitor(os: OS): SystemMonitorApp | MobileSystemMonitorApp {
    const platformDetector = PlatformDetector.getInstance();
    
    if (platformDetector.isMobile()) {
      return new MobileSystemMonitorApp(os);
    } else {
      return new SystemMonitorApp(os);
    }
  }
}
