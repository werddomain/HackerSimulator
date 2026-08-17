import { Theme } from '../theme';

/**
 * Creates a macOS theme as a standalone complete theme object
 * This avoids circular dependencies with the DEFAULT_THEME
 */
export function createMacOSTheme(): Theme {
    const windowBorderRadius = 10;

  return {
    id: "macos",
    name: "macOS",
    description: "Clean macOS inspired theme with rounded elements",
    author: "HackerSimulator Team",
    version: "1.0.0",
    
    // Main accent colors
    accentColor: "#007aff", // Apple blue
    accentColorLight: "#3395ff",
    accentColorDark: "#0062cc",
       // Theme colors
    primaryColor: "#f5f5f7", // Light gray background
    secondaryColor: "#ffffff", // White
    tertiaryColor: "#e8e8e8", // Light gray for inputs/panels - closer to actual macOS
    
    // Text colors
    textColorPrimary: "rgba(0, 0, 0, 0.85)", // Slightly less black for better readability
    textColorSecondary: "rgba(0, 0, 0, 0.55)", // Medium gray text
    textColorDisabled: "rgba(0, 0, 0, 0.25)", // Light gray text
    
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
    windowBorderColor: "rgba(0, 0, 0, 0.08)", // Subtler border color
    windowHeaderColor: "#f0f0f0", // Slightly darker than in the current theme - matches screenshot
    desktopBgColor: "#14B2F5", // Modern macOS gradient
    dialogBgColor: "rgba(250, 250, 250, 0.98)", // More opaque - macOS dialogs aren't very transparent
    dialogBorderColor: "rgba(0, 0, 0, 0.08)", // Subtler border
    
    // Taskbar colors
    taskbarBgStart: "rgba(255, 255, 255, 0.8)",
    taskbarBgEnd: "rgba(255, 255, 255, 0.8)",
    taskbarBorderTopColor: "rgba(0, 0, 0, 0.1)",
    taskbarShadow: "0 0 10px rgba(0, 0, 0, 0.1)",
    taskbarItemBg: "transparent",
    taskbarItemHoverBg: "rgba(0, 0, 0, 0.05)",
    taskbarItemActiveBg: "rgba(0, 0, 0, 0.1)",
    taskbarItemActiveBorder: "none",
    systemTrayBg: "transparent",
    
    // Start button colors
    startButtonBgStart: "transparent",
    startButtonBgEnd: "transparent",
    startButtonHoverBgStart: "rgba(0, 0, 0, 0.05)",
    startButtonHoverBgEnd: "rgba(0, 0, 0, 0.05)",
    startButtonActiveBgStart: "rgba(0, 0, 0, 0.1)",
    startButtonActiveBgEnd: "rgba(0, 0, 0, 0.1)",
    
    // Window titlebar configuration
    titleBar: {
      buttonPlacement: 'left', // macOS has window controls on the left
      useGradient: false,
      buttonStyle: 'circle', // Circular traffic light buttons
      showIcon: false, // macOS doesn't show icons in the titlebar
      textAlignment: 'center',
      height: '28px',
      customCss: `
      .windows-container .window-header .window-title{
        color: black !important;
        margin-left: 11px !important;
        font-weight: 500;
        font-size: 13px;
      } 
        .windows-container .window {
            border-radius: ${windowBorderRadius}px;
            box-shadow: 0 5px 30px rgba(0, 0, 0, 0.2);
            border: 1px solid rgba(0, 0, 0, 0.2);
            overflow: hidden;
        }
        .windows-container .window.active {
            border: 1px solid rgba(0, 0, 0, 0.3);
            box-shadow: 0 8px 40px rgba(0, 0, 0, 0.25);
        }        .windows-container .window-header .window-controls{
            flex-direction: row;
        }
            .windows-container .window {
            border-radius: ${windowBorderRadius}px;
            border: 1px solid rgba(0, 0, 0, 0.15);
            box-shadow: 0 8px 30px rgba(0, 0, 0, 0.15);
            overflow: hidden;
            }
            .windows-container .window-header {
            border-radius: ${windowBorderRadius}px ${windowBorderRadius}px 0 0;
            padding: 0 ${windowBorderRadius-8}px;
        }
            .windows-container .window-content,
            .windows-container .window-content > div {

                border-radius: 0 0 ${windowBorderRadius}px ${windowBorderRadius}px;
}
            /* Style for code editor window content */
            .app-code-editor .windows-container .window-content {
                background-color: #1e1e1e;
                color: #f0f0f0;
            }
            /* Style for terminal window content */
            .app-terminal .windows-container .window-content {
                background-color: #1e1e1e;
                color: #f0f0f0;
            }
            /* General content style for native macOS appearance */
            .windows-container .window-content,
            .windows-container .window {
                background-color: #f5f5f7;
            }
             .windows-container .window-header .window-controls .window-control {
            border-radius: 50%;
            /*width: 12px;
            height: 12px;*/
            margin: 0 4px;
            border: 1px solid rgba(0, 0, 0, 0.1);
            box-shadow: inset 0 0 0 0.5px rgba(255, 255, 255, 0.15);
            transition: all 0.1s ease-in-out;
        }
        .windows-container .window-header .window-controls {
            margin-left: 8px;
            display: flex;
            gap: 3px;
        }
        .windows-container .window-header {
            flex-direction: row-reverse;
        }        
        .window-control.window-close {
            background-color: #ff5f57;
            order: -3;
        }
        .window-control.window-minimize {
            background-color: #ffbd2e;
            order: -2;
        }
        .window-control.window-maximize {
            background-color: #28c940;
            order: -1;
        }
        .window:not(.active) .window-control {
            opacity: 0.7;
        }
        .window-control.close:hover {
            background-color: #ff5f57;
            background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 10'><path d='M3 3L7 7M7 3L3 7' stroke='%23750b0b' stroke-width='1.2' stroke-linecap='round'/></svg>");
            background-position: center;
            background-repeat: no-repeat;
        }
        .window-control.minimize:hover {
            background-color: #ffbd2e;
            background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 10'><path d='M2.5 5H7.5' stroke='%23995700' stroke-width='1.2' stroke-linecap='round'/></svg>");
            background-position: center;
            background-repeat: no-repeat;
        }
        .window-control.maximize:hover {
            background-color: #28c940;
            background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 10'><path d='M3.5 3.5L6.5 6.5M3.5 6.5L6.5 3.5' stroke='%23006500' stroke-width='1.2' stroke-linecap='round'/></svg>");
            background-position: center;
            background-repeat: no-repeat;
        }
      `
    },
    
    // Taskbar configuration
    taskbar: {
      position: 'bottom',
      size: '60px', // macOS dock is typically larger
      transparency: 0.8,
      blur: true, // Modern macOS uses blur effects
      itemSpacing: '4px',
      customCss: `
        /* macOS Dock style */
        #taskbar-items, #start-menu-button{
            color: black !important;
        }
        .taskbar {
          border-radius: 16px;
          margin: 0 auto 10px auto;
          width: auto;
          max-width: 80%;
        }
        .taskbar-item {
          border-radius: 8px;
          transition: all 0.2s ease;
        }
        .taskbar-item:hover {
          transform: scale(1.1);
        }
        
      `
    },
    
    // UI element sizes
    uiElementSizes: {
      borderRadius: '8px', // macOS uses rounded corners
      buttonHeight: '24px',
      inputHeight: '24px'
    },
    
    // Custom fonts
    customFonts: {
      systemFont: "'SF Pro Text', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
      monospaceFont: "'SF Mono', Menlo, Monaco, 'Courier New', monospace",
      headerFont: "'SF Pro Display', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    }
  };
}
