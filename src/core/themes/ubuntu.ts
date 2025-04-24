import { Theme } from '../theme';

/**
 * Creates an Ubuntu theme as a standalone complete theme object
 * This avoids circular dependencies with the DEFAULT_THEME
 */
export function createUbuntuTheme(): Theme {
  return {
    id: "ubuntu",
    name: "Ubuntu",
    description: "Ubuntu-inspired theme with orange and purple accents",
    author: "HackerSimulator Team",
    version: "1.0.0",
    
    // Main accent colors
    accentColor: "#e95420", // Ubuntu orange
    accentColorLight: "#f08763",
    accentColorDark: "#c7411b",
     
    // Theme colors
    primaryColor: "#300a24", // Ubuntu terminal purple
    secondaryColor: "#2c001e",
    tertiaryColor: "#333333",
    
    // Text colors
    textColorPrimary: "rgba(255, 255, 255, 0.9)",
    textColorSecondary: "rgba(255, 255, 255, 0.7)",
    textColorDisabled: "rgba(255, 255, 255, 0.5)",
    
    // Status colors
    successColor: "#27AE60",
    warningColor: "#F39C12",
    errorColor: "#E74C3C",
    infoColor: "#3498DB",
    
    // App tile colors
    terminalColor: "#2C3E50",
    browserColor: "#16A085",
    codeEditorColor: "#8E44AD",
    fileExplorerColor: "#2980B9",
    systemMonitorColor: "#C0392B",
    mailColor: "#D35400",
    shopColor: "#27AE60",
    hackToolsColor: "#7F8C8D",
    settingsColor: "#F39C12",
    
    // UI component colors
    windowBorderColor: "#2c001e",
    windowHeaderColor: "#3b1640",
    desktopBgColor: "#2c001e",
    dialogBgColor: "rgba(44, 0, 30, 0.9)",
    dialogBorderColor: "#e95420",
    
    // Taskbar colors
    taskbarBgStart: "#2c001e",
    taskbarBgEnd: "#300a24",
    taskbarBorderTopColor: "#3b1640",
    taskbarShadow: "0 2px 6px rgba(0, 0, 0, 0.4)",
    taskbarItemBg: "rgba(255, 255, 255, 0.05)",
    taskbarItemHoverBg: "rgba(255, 255, 255, 0.1)",
    taskbarItemActiveBg: "rgba(233, 84, 32, 0.5)",
    taskbarItemActiveBorder: "1px solid #e95420",
    systemTrayBg: "rgba(0, 0, 0, 0.2)",
    
    // Start button colors
    startButtonBgStart: "#e95420",
    startButtonBgEnd: "#c7411b",
    startButtonHoverBgStart: "#f08763",
    startButtonHoverBgEnd: "#e95420",
    startButtonActiveBgStart: "#c7411b",
    startButtonActiveBgEnd: "#c7411b",
    
    // Window titlebar configuration
    titleBar: {
      buttonPlacement: 'right', // Ubuntu has window controls on the right by default
      useGradient: false,
      buttonStyle: 'pill', // Ubuntu uses pill-shaped buttons
      showIcon: true,
      textAlignment: 'center',
      height: '32px'
    },
    
    // Taskbar configuration
    taskbar: {
      position: 'left', // Ubuntu default taskbar (launcher) is on the left
      size: '48px',
      transparency: 0.9,
      blur: true,
      itemSpacing: '4px',
      customCss: `
        /* Ubuntu launcher style */
        .taskbar {
          background-color: rgba(44, 0, 30, 0.8);
          border-right: 1px solid #3b1640;
        }
        .taskbar-item {
          border-radius: 4px;
          margin: 4px;
        }
        .window-button.close {
          background-color: #e95420;
          border-radius: 16px;
        }
        .window-button.minimize,
        .window-button.maximize {
          border-radius: 16px;
          border: 1px solid #e95420;
        }
      `
    },
    
    // UI element sizes
    uiElementSizes: {
      borderRadius: '4px',
      buttonHeight: '32px',
      inputHeight: '32px'
    },
    
    // Custom fonts
    customFonts: {
      systemFont: "'Ubuntu', 'Segoe UI', Arial, sans-serif",
      monospaceFont: "'Ubuntu Mono', 'Courier New', monospace",
      headerFont: "'Ubuntu', 'Segoe UI', Arial, sans-serif"
    }
  };
}
