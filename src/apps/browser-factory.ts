/**
 * Browser Factory
 * Creates the appropriate browser version based on platform
 */

import { OS } from '../core/os';
import { BrowserApp } from './browser';
import { MobileBrowserApp } from './mobile-browser';
import { platformDetector, PlatformType } from '../core/platform-detector';

/**
 * Factory function to create the appropriate browser instance
 * based on current platform (mobile or desktop)
 */
export function createBrowser(os: OS): BrowserApp {  // Determine if we're on mobile
  const isMobile = platformDetector.getPlatformType() === PlatformType.MOBILE;
  
  // Create the appropriate browser instance
  if (isMobile) {
    return new MobileBrowserApp(os);
  } else {
    return new BrowserApp(os);
  }
}
