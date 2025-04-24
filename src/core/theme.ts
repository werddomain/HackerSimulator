/**
 * Theme interface for HackerSimulator
 * Defines the structure of a theme with all configurable properties
 */

// Import theme creation functions
import { createWindows98Theme } from './themes/windows98';
import { createMacOSTheme } from './themes/macos';
import { createUbuntuTheme } from './themes/ubuntu';

export interface Theme {
    // Theme metadata
    id: string;
    name: string;
    description: string;
    author?: string;
    version?: string;
    thumbnailPath?: string;

    // Main accent colors
    accentColor: string;
    accentColorLight: string;
    accentColorDark: string;

    // Theme colors
    primaryColor: string;
    secondaryColor: string;
    tertiaryColor: string;

    // Text colors
    textColorPrimary: string;
    textColorSecondary: string;
    textColorDisabled: string;

    // Status colors
    successColor: string;
    warningColor: string;
    errorColor: string;
    infoColor: string;    // App tile colors
    terminalColor: string;
    browserColor: string;
    codeEditorColor: string;
    fileExplorerColor: string;
    systemMonitorColor: string;
    mailColor: string;
    shopColor: string;
    hackToolsColor: string;
    settingsColor: string;
    
    // App tile customization options
    appTileForegroundColors?: string[]; // Array of foreground colors for app tiles
    disableCustomAppTileColors?: boolean; // When true, always use theme-defined colors

    // UI component colors
    windowBorderColor: string;
    windowHeaderColor: string;
    desktopBgColor: string;
    dialogBgColor: string;
    dialogBorderColor: string;

    // Taskbar colors
    taskbarBgStart: string;
    taskbarBgEnd: string;
    taskbarBorderTopColor: string;
    taskbarShadow: string;
    taskbarItemBg: string;
    taskbarItemHoverBg: string;
    taskbarItemActiveBg: string;
    taskbarItemActiveBorder: string;
    systemTrayBg: string;

    // Start button colors
    startButtonBgStart: string;
    startButtonBgEnd: string;
    startButtonHoverBgStart: string;
    startButtonHoverBgEnd: string;
    startButtonActiveBgStart: string;
    startButtonActiveBgEnd: string;
    // Advanced theme options (optional)
    customFonts?: {
        systemFont?: string;
        monospaceFont?: string;
        headerFont?: string;
    };
    animationSpeed?: number; // 1 is normal, <1 is slower, >1 is faster
    uiElementSizes?: {
        borderRadius?: string;
        buttonHeight?: string;
        inputHeight?: string;
    };

    // Window titlebar configuration
    titleBar?: {
        buttonPlacement: 'left' | 'right'; // macOS = right, Windows = left
        useGradient: boolean; // For Windows 98 style gradient
        customCss?: string; // Allow complete CSS override if needed
        buttonStyle?: 'square' | 'circle' | 'pill'; // Different button styles
        showIcon: boolean; // Whether to show window icons
        textAlignment?: 'left' | 'center' | 'right';
        height?: string;
    };    // Taskbar additional configuration
    taskbar?: {
        position: 'top' | 'bottom' | 'left' | 'right'; // Position of taskbar
        size?: string; // Size of taskbar (height or width depending on position)
        transparency?: number; // 0-1 value for transparency
        blur?: boolean; // Apply blur effect (for modern themes)
        itemSpacing?: string;
        customCss?: string; // Allow complete CSS override if needed
    };
    
    // Start menu configuration
    startMenu?: {
        customCss?: string; // Allow complete CSS override for start menu
    };
}

/**
 * Default theme implementation
 * This represents the current "Hacker" theme
 */
export const DEFAULT_THEME: Theme = {
    id: "default-hacker",
    name: "Hacker",
    description: "The default dark hacker theme",
    author: "HackerSimulator Team",
    version: "1.0.0",

    // Main accent colors
    accentColor: "#0078d7",
    accentColorLight: "#0091ff",
    accentColorDark: "#005a9e",

    // Theme colors
    primaryColor: "#1a1a1a",
    secondaryColor: "#2c2c2c",
    tertiaryColor: "#444",

    // Text colors
    textColorPrimary: "rgba(255, 255, 255, 0.9)",
    textColorSecondary: "rgba(255, 255, 255, 0.7)",
    textColorDisabled: "rgba(255, 255, 255, 0.4)",

    // Status colors
    successColor: "#27AE60",
    warningColor: "#F39C12",
    errorColor: "#E74C3C",
    infoColor: "#3498DB",    // App tile colors
    terminalColor: "#2C3E50",
    browserColor: "#16A085",
    codeEditorColor: "#8E44AD",
    fileExplorerColor: "#2980B9",
    systemMonitorColor: "#C0392B",
    mailColor: "#D35400",
    shopColor: "#27AE60",
    hackToolsColor: "#7F8C8D",
    settingsColor: "#F39C12",
    
    // App tile customization options
    appTileForegroundColors: ["#FFFFFF", "#F0F0F0", "#E0E0E0", "#CCCCCC", "#000000", "#333333", "#666666", "#999999", "#FFFF00"],
    disableCustomAppTileColors: false,

    // UI component colors
    windowBorderColor: "#555",
    windowHeaderColor: "#444",
    desktopBgColor: "#1a1a1a",
    dialogBgColor: "rgba(40, 40, 40, 0.85)",
    dialogBorderColor: "rgba(255, 255, 255, 0.1)",

    // Taskbar colors
    taskbarBgStart: "#2c2c2c",
    taskbarBgEnd: "#1a1a1a",
    taskbarBorderTopColor: "#3a3a3a",
    taskbarShadow: "0 -2px 10px rgba(0, 0, 0, 0.4)",
    taskbarItemBg: "rgba(255, 255, 255, 0.04)",
    taskbarItemHoverBg: "rgba(255, 255, 255, 0.08)",
    taskbarItemActiveBg: "rgba(255, 255, 255, 0.15)",
    taskbarItemActiveBorder: "2px solid #0078d7",
    systemTrayBg: "rgba(0, 0, 0, 0.2)",

    // Start button colors
    startButtonBgStart: "#0078d7",
    startButtonBgEnd: "#005a9e",
    startButtonHoverBgStart: "#0091ff",
    startButtonHoverBgEnd: "#0078d7",
    startButtonActiveBgStart: "#005a9e",
    startButtonActiveBgEnd: "#005a9e"
};

/**
 * Additional theme presets
 */

// Classic Hacker theme (dark green/black with matrix style)
export const CLASSIC_HACKER_THEME: Theme = {
    ...DEFAULT_THEME,
    id: "classic-hacker",
    name: "Classic Hacker",
    description: "Dark green/black theme with matrix style",

    // Override colors to create the classic green/black hacker look
    accentColor: "#00ff00",
    accentColorLight: "#33ff33",
    accentColorDark: "#00cc00",

    primaryColor: "#000000",
    secondaryColor: "#0a1a0a",
    tertiaryColor: "#112211",

    textColorPrimary: "rgba(0, 255, 0, 0.9)",
    textColorSecondary: "rgba(0, 255, 0, 0.7)",
    textColorDisabled: "rgba(0, 255, 0, 0.4)",

    // Modified app colors to fit the green theme
    terminalColor: "#002200",
    browserColor: "#003300",
    codeEditorColor: "#004400",

    desktopBgColor: "#000000",
    windowBorderColor: "#00cc00",
    windowHeaderColor: "#001100",

    taskbarBgStart: "#0a1a0a",
    taskbarBgEnd: "#000000",
};

// Light theme
export const LIGHT_THEME: Theme = {
    ...DEFAULT_THEME,
    id: "light",
    name: "Light",
    description: "A clean light theme",

    // Override colors for light theme
    accentColor: "#0078d7",
    accentColorLight: "#0091ff",
    accentColorDark: "#005a9e",

    primaryColor: "#f0f0f0",
    secondaryColor: "#e0e0e0",
    tertiaryColor: "#d0d0d0",

    textColorPrimary: "rgba(0, 0, 0, 0.9)",
    textColorSecondary: "rgba(0, 0, 0, 0.7)",
    textColorDisabled: "rgba(0, 0, 0, 0.4)",

    desktopBgColor: "#f5f5f5",
    windowBorderColor: "#cccccc",
    windowHeaderColor: "#e0e0e0",

    taskbarBgStart: "#e0e0e0",
    taskbarBgEnd: "#d0d0d0",
    taskbarBorderTopColor: "#cccccc",
    taskbarShadow: "0 -2px 10px rgba(0, 0, 0, 0.1)",
};

// Dark Blue theme
export const DARK_BLUE_THEME: Theme = {
    ...DEFAULT_THEME,
    id: "dark-blue",
    name: "Dark Blue",
    description: "A sleek dark blue theme",

    // Override colors for dark blue theme
    accentColor: "#007acc",
    accentColorLight: "#1e8ad2",
    accentColorDark: "#005f9e",

    primaryColor: "#15202b",
    secondaryColor: "#192734",
    tertiaryColor: "#22303c",

    desktopBgColor: "#10171e",
    windowBorderColor: "#38444d",
    windowHeaderColor: "#192734",

    taskbarBgStart: "#192734",
    taskbarBgEnd: "#15202b",
};

// High Contrast theme
export const HIGH_CONTRAST_THEME: Theme = {
    ...DEFAULT_THEME,
    id: "high-contrast",
    name: "High Contrast",
    description: "High contrast theme for better visibility",

    // Override colors for high contrast theme
    accentColor: "#ffff00",
    accentColorLight: "#ffff33",
    accentColorDark: "#cccc00",

    primaryColor: "#000000",
    secondaryColor: "#222222",
    tertiaryColor: "#333333",

    textColorPrimary: "#ffffff",
    textColorSecondary: "#eeeeee",
    textColorDisabled: "#aaaaaa",

    successColor: "#00ff00",
    warningColor: "#ffff00",
    errorColor: "#ff0000",
    infoColor: "#00ffff",

    desktopBgColor: "#000000",
    windowBorderColor: "#ffffff",
    windowHeaderColor: "#000000",

    taskbarBgStart: "#000000",
    taskbarBgEnd: "#000000",
    taskbarBorderTopColor: "#ffffff",
};

// Export theme collection for easy access
export function THEME_PRESETS(): Record<string, Theme> {
    return {
        "default-hacker": DEFAULT_THEME,
        "classic-hacker": CLASSIC_HACKER_THEME,
        "light": LIGHT_THEME,
        "dark-blue": DARK_BLUE_THEME,
        "high-contrast": HIGH_CONTRAST_THEME,
        "windows-98": createWindows98Theme(),
        "macos": createMacOSTheme(),
        "ubuntu": createUbuntuTheme()
    };
} 
