/**
 * Themes index file
 * Collects and exports all themes from the themes directory
 */
import { Theme } from '../theme';
import { createWindows98Theme } from './windows98';
import { createMacOSTheme } from './macos';
import { createUbuntuTheme } from './ubuntu';

// Export all themes
export function ADDITIONAL_THEMES(): Record<string, Theme> {
    return {
  "windows-98": createWindows98Theme(),
  "macos": createMacOSTheme(),
  "ubuntu": createUbuntuTheme(),
};
} 