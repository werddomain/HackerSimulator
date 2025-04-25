/**
 * File Explorer Factory
 * Creates the appropriate file explorer version based on platform
 */

import { OS } from '../core/os';
import { FileExplorerApp } from './file-explorer';
import { MobileFileExplorerApp } from './mobile-file-explorer';
import { platformDetector, PlatformType } from '../core/platform-detector';

/**
 * Factory function to create the appropriate file explorer instance
 * based on current platform (mobile or desktop)
 */
export function createFileExplorer(os: OS): FileExplorerApp {
  // Determine if we're on mobile
  const isMobile = platformDetector.getPlatformType() === PlatformType.Mobile;
  
  // Create the appropriate file explorer instance
  if (isMobile) {
    return new MobileFileExplorerApp(os);
  } else {
    return new FileExplorerApp(os);
  }
}
