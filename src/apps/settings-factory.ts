// filepath: c:\Users\clefw\source\repos\HackerSimulator\src\apps\settings-factory.ts
import { OS } from '../core/os';
import { PlatformDetector } from '../core/platform-detector';
import { SettingsApp } from './settings';
import { MobileSettingsApp } from './mobile-settings';

/**
 * Factory class for creating appropriate Settings app based on platform
 */
export class SettingsFactory {
  /**
   * Creates the appropriate Settings app based on platform detection
   * @param os The OS instance
   * @returns SettingsApp or MobileSettingsApp
   */
  public static createSettings(os: OS): SettingsApp | MobileSettingsApp {
    const platformDetector = PlatformDetector.getInstance();
    
    if (platformDetector.isMobile()) {
      return new MobileSettingsApp(os);
    } else {
      return new SettingsApp(os);
    }
  }
}
