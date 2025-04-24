import { Theme } from '../theme';

/**
 * Creates a Windows 98 theme as a standalone complete theme object
 * This avoids circular dependencies with the DEFAULT_THEME
 */
export function createWindows98Theme(): Theme {
    const titleBarCustomCss = `
        /* Windows 98 style titlebar gradient - left to right */
        .window-header {
          background: linear-gradient(to right, #000080, #1084d0); 
          color: white !important;
          padding: 2px;
        }
        
        /* Force white text in title bar - multiple selectors for better specificity */
        .window-header .window-title,
        .window-header span, 
        .window-header div, 
        .window .window-header .window-title,
        .application-window .window-header .window-title {
          color: white !important;
          text-shadow: none !important;
        }

        /* Windows 98 style control buttons */
        .window-controls button {
          width: 16px;
          height: 14px;
          background-color: #c0c0c0;
          margin-left: 2px;
          border-top: 1px solid #ffffff;
          border-left: 1px solid #ffffff;
          border-right: 1px solid #404040;
          border-bottom: 1px solid #404040;
          position: relative;
          display: inline-flex;
          align-items: center;
          justify-content: center;
        }

        /* Button hover effect */
        .window-controls button:hover {
          background-color: #d4d0c8;
        }

        /* Button active effect - pressed in */
        .window-controls button:active {
          border-top: 1px solid #404040;
          border-left: 1px solid #404040;
          border-right: 1px solid #ffffff;
          border-bottom: 1px solid #ffffff;
        }

        /* Minimize button icon */
        .window-controls .minimize-button::after {
          content: "";
          width: 8px;
          height: 2px;
          background-color: black;
          position: absolute;
          bottom: 3px;
        }

        /* Maximize button icon */
        .window-controls .maximize-button::after {
          content: "";
          width: 8px;
          height: 8px;
          border: 1px solid black;
          background-color: transparent;
          position: absolute;
        }

        /* Close button icon */
        .window-controls .close-button::after {
          content: "×";
          font-size: 12px;
          font-weight: bold;
          color: black;
          position: absolute;
        }
      `;
    const taskbarCustomCss = `
        /* Windows 98 style - raised bevels */
        #taskbar { color: black !important; } /* Ensure taskbar text is black */        
        #start-menu-button {
            position: relative !important;
            padding-left: 48px !important; /* Make space for the logo */
        }
        #start-menu-button::before {
            content: "" !important;
            background-image: url('data:image/png;base64,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') !important;
            background-size: 32px 32px !important; /* Make the logo smaller */
            background-repeat: no-repeat !important;
            background-position: center left 8px !important; /* Position it on the left with some padding */
            height: 100% !important;
            width: 40px !important;
            position: absolute !important;
            left: 0 !important;
            top: 0 !important;
            display: block !important;
        }
        .taskbar { 
          border-top: 1px solid #ffffff;
          border-left: 1px solid #ffffff;
          border-right: 1px solid #404040;
          border-bottom: 1px solid #404040;
          color: black; /* Ensure taskbar text is black */
        } 
        .start-button {
          font-weight: bold;
          border-top: 1px solid #ffffff;
          border-left: 1px solid #ffffff;
          border-right: 1px solid #404040;
          border-bottom: 1px solid #404040;
          color: black !important; /* Ensure start button text is black */
        }        
        .window-button {
          font-family: 'Arial', sans-serif;
          font-size: 8px;
          font-weight: bold;
          border-top: 1px solid #ffffff;
          border-left: 1px solid #ffffff;
          border-right: 1px solid #404040;
          border-bottom: 1px solid #404040;
          color: black !important; /* Force window button text in taskbar to be black */
        }
          #taskbar-items .taskbar-item { 
            font-family: 'Arial', sans-serif;
            font-weight: bold;
            border-top: 1px solid #ffffff;
            border-left: 1px solid #ffffff;
            border-right: 1px solid #404040;
            border-bottom: 1px solid #404040;
            border-radius: 0 !important; /* No rounded corners */
            box-shadow: none !important; /* No shadow */
            background-color: #d4d0c8 !important; /* Classic Windows 98 gray */

          }
      `;
    var inputTheme = `
      background-color: #d4d0c8 !important;
            color: black !important;
            border-top: 1px solid #404040 !important;
            border-left: 1px solid #404040 !important;
            border-right: 1px solid #ffffff !important;
            border-bottom: 1px solid #ffffff !important;
            border-radius: 0 !important;
            `;
    return {
        id: "windows-98",
        name: "Windows 98",
        description: "Classic Windows 98 style with gradient titlebars",
        author: "HackerSimulator Team",
        version: "1.0.0",

        // Main accent colors
        accentColor: "#000080", // Navy blue
        accentColorLight: "#1084d0",
        accentColorDark: "#000066",

        // Theme colors
        primaryColor: "#c0c0c0", // Silver background
        secondaryColor: "#d4d0c8", // Classic Windows 98 gray
        tertiaryColor: "#ece9d8", // Slightly lighter gray for panels

        // Text colors
        textColorPrimary: "rgba(0, 0, 0, 0.9)",
        textColorSecondary: "rgba(0, 0, 0, 0.7)",
        textColorDisabled: "rgba(0, 0, 0, 0.4)",

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
        settingsColor: "#F39C12",    // UI component colors
        windowBorderColor: "#808080",
        windowHeaderColor: "#000080",
        desktopBgColor: "#008080", // Classic teal
        dialogBgColor: "#d4d0c8",
        dialogBorderColor: "#808080",

        // Taskbar colors
        taskbarBgStart: "#c0c0c0",
        taskbarBgEnd: "#c0c0c0",
        taskbarBorderTopColor: "#ffffff",
        taskbarShadow: "inset -1px -1px #0a0a0a, inset 1px 1px #dfdfdf", // Classic bevel
        taskbarItemBg: "#d4d0c8",
        taskbarItemHoverBg: "#ece9d8",
        taskbarItemActiveBg: "#d4d0c8",
        taskbarItemActiveBorder: "1px solid #000080",
        systemTrayBg: "#c0c0c0",

        // Start button colors
        startButtonBgStart: "#c0c0c0",
        startButtonBgEnd: "#c0c0c0",
        startButtonHoverBgStart: "#d4d0c8",
        startButtonHoverBgEnd: "#d4d0c8",
        startButtonActiveBgStart: "#ece9d8",
        startButtonActiveBgEnd: "#ece9d8",
        // Window titlebar configuration
        titleBar: {
            buttonPlacement: 'right',
            useGradient: true, // Windows 98 gradient titlebar
            buttonStyle: 'square',
            showIcon: true,
            textAlignment: 'left',
            height: '22px',
            customCss: titleBarCustomCss
        },    // Taskbar configuration    
        taskbar: {
            position: 'bottom',
            size: '30px',
            transparency: 0, // No transparency for Win98
            blur: false,
            itemSpacing: '2px',
            customCss: taskbarCustomCss
        },

        // Start menu configuration
        startMenu: {
            customCss: `
        /* Windows 98 Start Menu Styling */
        .start-menu {
          background-color: #d4d0c8 !important;
          border-radius: 0 !important;
          box-shadow: 2px 2px 8px rgba(0, 0, 0, 0.5) !important;
          border-top: 1px solid #ffffff !important;
          border-left: 1px solid #ffffff !important;
          border-right: 1px solid #404040 !important;
          border-bottom: 1px solid #404040 !important;
          backdrop-filter: none !important;
          color: black !important;
          overflow: visible !important;
        }
        
        /* Left sidebar with Windows 98 logo */
        .start-menu-sidebar {
          background-color: #000080 !important;
          border-right: 1px solid #404040 !important;
          display: flex !important;
          flex-direction: column !important;
          justify-content: space-between !important;
        }
        
        /* Windows 98 Logo */
        .start-menu-sidebar:before {
          content: "Windows 98" !important;
          writing-mode: vertical-rl !important;
          transform: rotate(180deg) !important;
          color: white !important;
          font-weight: bold !important;
          font-size: 24px !important;
          margin-bottom: 20px !important;
          margin-top: auto !important;
          font-family: 'MS Sans Serif', Arial, sans-serif !important;
          letter-spacing: 1px !important;
        }
        
        /* Menu items */
        .start-menu-item {
          color: black !important;
          font-family: 'MS Sans Serif', Arial, sans-serif !important;
          font-size: 11px !important;
          padding: 4px 8px !important;
          display: flex !important;
          align-items: center !important;
        }
        
        /* Menu item hover */
        .start-menu-item:hover {
          background-color: #000080 !important;
          color: white !important;
        }
        
        /* Icons next to menu items */
        .start-menu-item-icon {
          width: 16px !important;
          height: 16px !important;
          margin-right: 8px !important;
        }
        
        /* Submenu indicators */
        .start-menu-item-arrow {
          margin-left: auto !important;
        }
        
        /* Bottom section with shutdown options */
        .start-menu-footer {
          border-top: 1px solid #808080 !important;
          padding: 4px !important;
        }
        
        /* Windows 98 style for app tiles */
        .app-tile {
          background-color: #c0c0c0 !important;
          border-radius: 0 !important;
          border-top: 1px solid #ffffff !important;
          border-left: 1px solid #ffffff !important;
          border-right: 1px solid #404040 !important;
          border-bottom: 1px solid #404040 !important;
          transform: none !important;
          box-shadow: none !important;
          padding: 8px !important;
          border-radius: 0 !important;
        }
        
        .app-tile:hover {
          background-color: #d4d0c8 !important;
          transform: none !important; 
        }
        
        .app-tile:active {
          border-top: 1px solid #404040 !important;
          border-left: 1px solid #404040 !important;
          border-right: 1px solid #ffffff !important;
          border-bottom: 1px solid #ffffff !important;
          background-color: #d4d0c8 !important;
        }
        
        /* App tile text color */
        .app-tile-name {
          color: black !important;
          font-family: 'MS Sans Serif', Arial, sans-serif !important;
          font-size: 11px !important;
          margin-top: 8px !important;
        }
        
        /* App tile icons */
        .app-tile svg {
          stroke: black !important;
          fill: black !important;
        }
        .start-menu .search-bar input{
            ${inputTheme}
        }
        .start-menu .search-bar input::placeholder{
            color: black !important;
            font-family: 'MS Sans Serif', Arial, sans-serif !important;
            }
      `
        },

        // UI element sizes
        uiElementSizes: {
            borderRadius: '0', // No rounded corners in Win98
            buttonHeight: '22px',
            inputHeight: '22px'
        },

        // Custom fonts
        customFonts: {
            systemFont: "'MS Sans Serif', Arial, sans-serif",
            monospaceFont: "'Courier New', monospace",
            headerFont: "'MS Sans Serif', Arial, sans-serif"
        }
    };
}
