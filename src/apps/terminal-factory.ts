/**
 * Terminal Factory
 * Provides platform-specific terminal implementations
 */

import { OS } from '../core/os';
import { TerminalApp } from './terminal';
import { MobileTerminalApp } from './mobile-terminal';
import { PlatformType, platformDetector } from '../core/platform-detector';

/**
 * Terminal Factory
 * Creates the appropriate terminal implementation based on platform
 */
export class TerminalFactory {
  private static instance: TerminalFactory;
  
  /**
   * Get singleton instance
   */
  public static getInstance(): TerminalFactory {
    if (!TerminalFactory.instance) {
      TerminalFactory.instance = new TerminalFactory();
    }
    return TerminalFactory.instance;
  }
  
  /**
   * Create a terminal appropriate for the current platform
   * @param os Operating system instance
   * @returns Terminal application instance
   */
  public createTerminal(os: OS): TerminalApp {
    // Check platform type
    const platformType = platformDetector.getPlatformType();
    
    if (platformType === PlatformType.Mobile) {
      // Create mobile terminal for mobile platforms
      return new MobileTerminalApp(os);
    } else {
      // Create standard terminal for desktop platforms
      return new TerminalApp(os);
    }
  }
}
