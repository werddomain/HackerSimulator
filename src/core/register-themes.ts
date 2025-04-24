/**
 * Import and register additional themes
 * This module imports themes from the themes directory and registers them with the theme system
 */

import { ThemeManager } from './ThemeManager';
import { FileSystem } from './filesystem';
import { UserSettings } from './UserSettings';
import { Theme, THEME_PRESETS } from './theme';

// Import the new theme creation functions
import { createWindows98Theme } from './themes/windows98';
import { createMacOSTheme } from './themes/macos';
import { createUbuntuTheme } from './themes/ubuntu';

/**
 * Register additional themes with the ThemeManager
 * @param fileSystem The file system to use
 * @param userSettings The user settings to use
 */
export async function registerAdditionalThemes(
    fileSystem: FileSystem,
    userSettings: UserSettings
): Promise<void> {
    const themeManager = ThemeManager.getInstance(fileSystem, userSettings);

    // Create theme instances using the factory functions
    const newThemes: Theme[] = [
        createWindows98Theme(),
        createMacOSTheme(),
        createUbuntuTheme()
    ];
    
    // Register each theme
    for (const theme of newThemes) {
        // Check if this is a built-in theme ID
        const isBuiltin = Object.keys(THEME_PRESETS).includes(theme.id);

        if (!isBuiltin) {
            // Add to the theme manager's custom themes using the saveTheme method
            await themeManager.saveTheme(theme);
        }
    }

    console.log('Additional themes registered:', newThemes.map(t => t.name).join(', '));
}
