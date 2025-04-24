/**
 * Theme Editor Help Documentation
 * Contains comprehensive documentation for theme properties
 */

export interface PropertyHelp {
  name: string;
  description: string;
  examples?: string[];
  cssProperty?: string;
  cssExample?: string;
  relatedProperties?: string[];
}

export interface CategoryHelp {
  name: string;
  description: string;
  properties: PropertyHelp[];
}

/**
 * Comprehensive help documentation for all theme properties
 */
export const themeHelpDocs: CategoryHelp[] = [
  {
    name: "General",
    description: "Core theme properties that affect the overall look and feel of the system.",
    properties: [
      {
        name: "name",
        description: "The name of the theme. This is displayed in the theme selector."
      },
      {
        name: "author",
        description: "The creator of the theme."
      },
      {
        name: "description",
        description: "A brief description of the theme's style and appearance."
      },
      {
        name: "version",
        description: "The version number of the theme."
      },
      {
        name: "animationSpeed",
        description: "Controls the speed of all animations in the system. Values less than 1 make animations faster, while values greater than 1 make them slower. Set to 0 to disable animations.",
        examples: ["0", "0.5", "1", "1.5", "2"],
        cssProperty: "--animation-speed",
        cssExample: `.my-animation { transition-duration: calc(var(--animation-speed) * 300ms); }`
      }
    ]
  },
  {
    name: "Colors",
    description: "Color properties that define the color scheme of the system.",
    properties: [
      {
        name: "primaryColor",
        description: "The main accent color used throughout the interface.",
        examples: ["#0078d7", "#6200ee", "#f44336"],
        cssProperty: "--primary-color",
        cssExample: `.button { background-color: var(--primary-color); }`
      },
      {
        name: "secondaryColor",
        description: "A secondary accent color used for contrasting elements.",
        examples: ["#0069c0", "#3700b3", "#ba000d"],
        cssProperty: "--secondary-color",
        cssExample: `.secondary-button { background-color: var(--secondary-color); }`
      },
      {
        name: "backgroundColor",
        description: "The main background color of the desktop and application surfaces.",
        examples: ["#121212", "#ffffff", "#f5f5f5"],
        cssProperty: "--background-color",
        cssExample: `.app-container { background-color: var(--background-color); }`
      },
      {
        name: "foregroundColor",
        description: "The main text color used throughout the interface.",
        examples: ["#ffffff", "#000000", "#333333"],
        cssProperty: "--foreground-color",
        cssExample: `.text { color: var(--foreground-color); }`
      },
      {
        name: "borderColor",
        description: "The color used for borders and dividers in the interface.",
        examples: ["#454545", "#e0e0e0", "#cccccc"],
        cssProperty: "--border-color",
        cssExample: `.card { border: 1px solid var(--border-color); }`
      }
    ]
  },
  {
    name: "Fonts",
    description: "Font properties that define the typography of the system.",
    properties: [
      {
        name: "customFonts.uiFont",
        description: "The main font used for user interface elements like labels, buttons, and menus.",
        examples: ["'Segoe UI', sans-serif", "'Roboto', Arial, sans-serif", "'SF Pro Text', -apple-system, BlinkMacSystemFont, sans-serif"],
        cssProperty: "--ui-font",
        cssExample: `.ui-element { font-family: var(--ui-font); }`
      },
      {
        name: "customFonts.titleFont",
        description: "The font used for window titles, headings, and other prominent text.",
        examples: ["'Segoe UI Semibold', sans-serif", "'Roboto Medium', Arial, sans-serif", "'SF Pro Display', -apple-system, BlinkMacSystemFont, sans-serif"],
        cssProperty: "--title-font",
        cssExample: `h1, h2, .window-title { font-family: var(--title-font); }`
      },
      {
        name: "customFonts.monoFont",
        description: "The monospace font used for code, terminal text, and other fixed-width text.",
        examples: ["'Cascadia Code', 'Consolas', monospace", "'Roboto Mono', monospace", "'SF Mono', 'Menlo', monospace"],
        cssProperty: "--mono-font",
        cssExample: `pre, code, .terminal { font-family: var(--mono-font); }`
      }
    ]
  },
  {
    name: "UI Element Sizes",
    description: "Size properties that define the dimensions of various UI elements.",
    properties: [
      {
        name: "uiElementSizes.fontSize",
        description: "The base font size for standard UI text.",
        examples: ["12", "14", "16"],
        cssProperty: "--base-font-size",
        cssExample: `body { font-size: var(--base-font-size); }`
      },
      {
        name: "uiElementSizes.titleFontSize",
        description: "The font size for window titles and headings.",
        examples: ["14", "16", "18"],
        cssProperty: "--title-font-size",
        cssExample: `.window-title { font-size: var(--title-font-size); }`
      },
      {
        name: "uiElementSizes.monoFontSize",
        description: "The font size for monospace text in terminals and code editors.",
        examples: ["12", "13", "14"],
        cssProperty: "--mono-font-size",
        cssExample: `.terminal, .code-editor { font-size: var(--mono-font-size); }`
      },
      {
        name: "uiElementSizes.iconSize",
        description: "The size of standard icons in the interface.",
        examples: ["16", "20", "24"],
        cssProperty: "--icon-size",
        cssExample: `.icon { width: var(--icon-size); height: var(--icon-size); }`
      },
      {
        name: "uiElementSizes.buttonSize",
        description: "The base size for buttons, affecting their height and padding.",
        examples: ["24", "28", "32"],
        cssProperty: "--button-size",
        cssExample: `.button { height: var(--button-size); padding: calc(var(--button-size)/6) calc(var(--button-size)/3); }`
      },
      {
        name: "uiElementSizes.inputFieldHeight",
        description: "The height of text input fields and form controls.",
        examples: ["24", "28", "32"],
        cssProperty: "--input-height",
        cssExample: `input, select { height: var(--input-height); }`
      }
    ]
  },
  {
    name: "Windows",
    description: "Properties that define the appearance of application windows.",
    properties: [
      {
        name: "window.background",
        description: "The background color of window content areas.",
        examples: ["#1e1e1e", "#f0f0f0", "#ffffff"],
        cssProperty: "--window-background",
        cssExample: `.window-content { background-color: var(--window-background); }`
      },
      {
        name: "window.foreground",
        description: "The text color used within window content areas.",
        examples: ["#cccccc", "#333333", "#000000"],
        cssProperty: "--window-foreground",
        cssExample: `.window-content { color: var(--window-foreground); }`
      },
      {
        name: "window.border",
        description: "Whether windows should have a visible border. Set to true to show borders, false to hide them.",
        examples: ["true", "false"],
        cssProperty: "--window-border",
        cssExample: `.window { border: var(--window-border, 1px) solid var(--window-border-color, transparent); }`
      },
      {
        name: "window.borderColor",
        description: "The color of window borders when borders are enabled.",
        examples: ["#454545", "#d1d1d1", "#cccccc"],
        cssProperty: "--window-border-color",
        cssExample: `.window { border-color: var(--window-border-color); }`
      },
      {
        name: "window.borderRadius",
        description: "The radius of window corners, controlling how rounded they appear.",
        examples: ["0", "4px", "8px"],
        cssProperty: "--window-border-radius",
        cssExample: `.window { border-radius: var(--window-border-radius); }`
      },
      {
        name: "window.boxShadow",
        description: "The shadow effect applied to windows to create depth.",
        examples: ["none", "0 2px 10px rgba(0,0,0,0.2)", "0 4px 20px rgba(0,0,0,0.3)"],
        cssProperty: "--window-box-shadow",
        cssExample: `.window { box-shadow: var(--window-box-shadow); }`
      }
    ]
  },
  {
    name: "Title Bar",
    description: "Properties that define the appearance of window title bars.",
    properties: [
      {
        name: "window.titleBar.activeBackground",
        description: "The background color of the title bar for the active (focused) window.",
        examples: ["#3c3c3c", "#0078d7", "#e0e0e0"],
        cssProperty: "--titlebar-active-bg",
        cssExample: `.window.active .window-titlebar { background-color: var(--titlebar-active-bg); }`
      },
      {
        name: "window.titleBar.activeForeground",
        description: "The text color of the title bar for the active (focused) window.",
        examples: ["#ffffff", "#000000", "#333333"],
        cssProperty: "--titlebar-active-fg",
        cssExample: `.window.active .window-titlebar { color: var(--titlebar-active-fg); }`
      },
      {
        name: "window.titleBar.inactiveBackground",
        description: "The background color of the title bar for inactive (unfocused) windows.",
        examples: ["#2d2d2d", "#f0f0f0", "#cccccc"],
        cssProperty: "--titlebar-inactive-bg",
        cssExample: `.window:not(.active) .window-titlebar { background-color: var(--titlebar-inactive-bg); }`
      },
      {
        name: "window.titleBar.inactiveForeground",
        description: "The text color of the title bar for inactive (unfocused) windows.",
        examples: ["#aaaaaa", "#777777", "#999999"],
        cssProperty: "--titlebar-inactive-fg",
        cssExample: `.window:not(.active) .window-titlebar { color: var(--titlebar-inactive-fg); }`
      },
      {
        name: "window.titleBar.border",
        description: "Whether the title bar should have a bottom border. Set to true to show a border, false to hide it.",
        examples: ["true", "false"],
        cssProperty: "--titlebar-border",
        cssExample: `.window-titlebar { border-bottom: var(--titlebar-border, 1px) solid var(--titlebar-border-color, transparent); }`
      },
      {
        name: "window.titleBar.height",
        description: "The height of window title bars in pixels.",
        examples: ["28", "32", "36"],
        cssProperty: "--titlebar-height",
        cssExample: `.window-titlebar { height: var(--titlebar-height); }`
      },
      {
        name: "window.titleBar.buttonBackground",
        description: "The background color of title bar buttons (minimize, maximize, close).",
        examples: ["transparent", "#505050", "#e0e0e0"],
        cssProperty: "--titlebar-button-bg",
        cssExample: `.titlebar-button { background-color: var(--titlebar-button-bg); }`
      },
      {
        name: "window.titleBar.buttonForeground",
        description: "The icon/symbol color of title bar buttons.",
        examples: ["#ffffff", "#000000", "#666666"],
        cssProperty: "--titlebar-button-fg",
        cssExample: `.titlebar-button { color: var(--titlebar-button-fg); }`
      }
    ]
  },
  {
    name: "Taskbar",
    description: "Properties that define the appearance of the taskbar.",
    properties: [
      {
        name: "taskbar.background",
        description: "The background color of the taskbar.",
        examples: ["#2d2d2d", "#f0f0f0", "#000000"],
        cssProperty: "--taskbar-bg",
        cssExample: `.taskbar { background-color: var(--taskbar-bg); }`
      },
      {
        name: "taskbar.foreground",
        description: "The text color used in the taskbar.",
        examples: ["#ffffff", "#000000", "#333333"],
        cssProperty: "--taskbar-fg",
        cssExample: `.taskbar { color: var(--taskbar-fg); }`
      },
      {
        name: "taskbar.height",
        description: "The height of the taskbar in pixels.",
        examples: ["40", "48", "56"],
        cssProperty: "--taskbar-height",
        cssExample: `.taskbar { height: var(--taskbar-height); }`
      },
      {
        name: "taskbar.border",
        description: "Whether the taskbar should have a top border. Set to true to show a border, false to hide it.",
        examples: ["true", "false"],
        cssProperty: "--taskbar-border",
        cssExample: `.taskbar { border-top: var(--taskbar-border, 1px) solid var(--taskbar-border-color, transparent); }`
      },
      {
        name: "taskbar.borderColor",
        description: "The color of the taskbar's top border when borders are enabled.",
        examples: ["#454545", "#d1d1d1", "#cccccc"],
        cssProperty: "--taskbar-border-color",
        cssExample: `.taskbar { border-color: var(--taskbar-border-color); }`
      },
      {
        name: "taskbar.activeItemBackground",
        description: "The background color for active taskbar items (running applications).",
        examples: ["#505050", "#0078d7", "#e0e0e0"],
        cssProperty: "--taskbar-active-item-bg",
        cssExample: `.taskbar-item.active { background-color: var(--taskbar-active-item-bg); }`
      },
      {
        name: "taskbar.activeItemForeground",
        description: "The icon/text color for active taskbar items.",
        examples: ["#ffffff", "#000000", "#333333"],
        cssProperty: "--taskbar-active-item-fg",
        cssExample: `.taskbar-item.active { color: var(--taskbar-active-item-fg); }`
      },
      {
        name: "taskbar.hoverItemBackground",
        description: "The background color for taskbar items when hovered.",
        examples: ["#404040", "#e5f1fb", "#e0e0e0"],
        cssProperty: "--taskbar-hover-item-bg",
        cssExample: `.taskbar-item:hover { background-color: var(--taskbar-hover-item-bg); }`
      }
    ]
  },
  {
    name: "Start Menu",
    description: "Properties that define the appearance of the start menu.",
    properties: [
      {
        name: "startMenu.background",
        description: "The background color of the start menu.",
        examples: ["#1f1f1f", "#f8f8f8", "#ffffff"],
        cssProperty: "--startmenu-bg",
        cssExample: `.start-menu { background-color: var(--startmenu-bg); }`
      },
      {
        name: "startMenu.foreground",
        description: "The text color used in the start menu.",
        examples: ["#ffffff", "#000000", "#333333"],
        cssProperty: "--startmenu-fg",
        cssExample: `.start-menu { color: var(--startmenu-fg); }`
      },
      {
        name: "startMenu.borderColor",
        description: "The color of the start menu's border.",
        examples: ["#454545", "#d1d1d1", "#cccccc"],
        cssProperty: "--startmenu-border-color",
        cssExample: `.start-menu { border-color: var(--startmenu-border-color); }`
      },
      {
        name: "startMenu.buttonBackground",
        description: "The background color of the start button in the taskbar.",
        examples: ["#2d2d2d", "#0078d7", "#e0e0e0"],
        cssProperty: "--startmenu-button-bg",
        cssExample: `.start-button { background-color: var(--startmenu-button-bg); }`
      },
      {
        name: "startMenu.buttonForeground",
        description: "The icon/text color of the start button in the taskbar.",
        examples: ["#ffffff", "#000000", "#333333"],
        cssProperty: "--startmenu-button-fg",
        cssExample: `.start-button { color: var(--startmenu-button-fg); }`
      },
      {
        name: "startMenu.hoverItemBackground",
        description: "The background color for start menu items when hovered.",
        examples: ["#383838", "#e5f1fb", "#e0e0e0"],
        cssProperty: "--startmenu-hover-item-bg",
        cssExample: `.start-menu-item:hover { background-color: var(--startmenu-hover-item-bg); }`
      },
      {
        name: "startMenu.selectedItemBackground",
        description: "The background color for selected start menu items.",
        examples: ["#0078d7", "#0e639c", "#d1d1d1"],
        cssProperty: "--startmenu-selected-item-bg",
        cssExample: `.start-menu-item.selected { background-color: var(--startmenu-selected-item-bg); }`
      }
    ]
  },
  {
    name: "Dialog",
    description: "Properties that define the appearance of dialog boxes.",
    properties: [
      {
        name: "dialog.background",
        description: "The background color of dialog content areas.",
        examples: ["#252526", "#f8f8f8", "#ffffff"],
        cssProperty: "--dialog-bg",
        cssExample: `.dialog-content { background-color: var(--dialog-bg); }`
      },
      {
        name: "dialog.foreground",
        description: "The text color used within dialog content areas.",
        examples: ["#cccccc", "#333333", "#000000"],
        cssProperty: "--dialog-fg",
        cssExample: `.dialog-content { color: var(--dialog-fg); }`
      },
      {
        name: "dialog.border",
        description: "Whether dialogs should have a visible border. Set to true to show borders, false to hide them.",
        examples: ["true", "false"],
        cssProperty: "--dialog-border",
        cssExample: `.dialog { border: var(--dialog-border, 1px) solid var(--dialog-border-color, transparent); }`
      },
      {
        name: "dialog.borderColor",
        description: "The color of dialog borders when borders are enabled.",
        examples: ["#454545", "#d1d1d1", "#cccccc"],
        cssProperty: "--dialog-border-color",
        cssExample: `.dialog { border-color: var(--dialog-border-color); }`
      },
      {
        name: "dialog.titleBarBackground",
        description: "The background color of dialog title bars.",
        examples: ["#3c3c3c", "#0078d7", "#e0e0e0"],
        cssProperty: "--dialog-titlebar-bg",
        cssExample: `.dialog-titlebar { background-color: var(--dialog-titlebar-bg); }`
      },
      {
        name: "dialog.titleBarForeground",
        description: "The text color of dialog title bars.",
        examples: ["#ffffff", "#000000", "#333333"],
        cssProperty: "--dialog-titlebar-fg",
        cssExample: `.dialog-titlebar { color: var(--dialog-titlebar-fg); }`
      },
      {
        name: "dialog.buttonBackground",
        description: "The background color of standard dialog buttons.",
        examples: ["#3c3c3c", "#f0f0f0", "#e0e0e0"],
        cssProperty: "--dialog-button-bg",
        cssExample: `.dialog-button { background-color: var(--dialog-button-bg); }`
      },
      {
        name: "dialog.buttonForeground",
        description: "The text color of standard dialog buttons.",
        examples: ["#cccccc", "#333333", "#000000"],
        cssProperty: "--dialog-button-fg",
        cssExample: `.dialog-button { color: var(--dialog-button-fg); }`
      },
      {
        name: "dialog.primaryButtonBackground",
        description: "The background color of primary (accent) dialog buttons.",
        examples: ["#0078d7", "#0e639c", "#007acc"],
        cssProperty: "--dialog-primary-button-bg",
        cssExample: `.dialog-button.primary { background-color: var(--dialog-primary-button-bg); }`
      },
      {
        name: "dialog.primaryButtonForeground",
        description: "The text color of primary (accent) dialog buttons.",
        examples: ["#ffffff", "#f0f0f0", "#f8f8f8"],
        cssProperty: "--dialog-primary-button-fg",
        cssExample: `.dialog-button.primary { color: var(--dialog-primary-button-fg); }`
      }
    ]
  },
  {
    name: "Custom CSS",
    description: "Advanced properties for custom CSS styling.",
    properties: [
      {
        name: "customCss.desktop",
        description: "Custom CSS for styling the desktop background and desktop elements.",
        cssExample: `/* Example desktop customization */
.desktop {
  background: linear-gradient(135deg, #2c3e50, #4ca1af);
}
.desktop-icon {
  background-color: rgba(255, 255, 255, 0.1);
  border-radius: 5px;
}`
      },
      {
        name: "customCss.window",
        description: "Custom CSS for styling application windows and their components.",
        cssExample: `/* Example window customization */
.window {
  backdrop-filter: blur(10px);
  background-color: rgba(30, 30, 30, 0.8);
}
.window-titlebar {
  background: linear-gradient(90deg, var(--primary-color), #2c3e50);
}`
      },
      {
        name: "customCss.taskbar",
        description: "Custom CSS for styling the taskbar and its elements.",
        cssExample: `/* Example taskbar customization */
.taskbar {
  backdrop-filter: blur(10px);
  background-color: rgba(30, 30, 30, 0.8);
}
.taskbar-item {
  margin: 0 2px;
  border-radius: 4px;
}`
      },
      {
        name: "customCss.startMenu",
        description: "Custom CSS for styling the start menu and its elements.",
        cssExample: `/* Example start menu customization */
.start-menu {
  backdrop-filter: blur(15px);
  background-color: rgba(30, 30, 30, 0.85);
  border-radius: 10px;
}
.start-menu-section {
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}`
      },
      {
        name: "customCss.dialog",
        description: "Custom CSS for styling dialog boxes and their components.",
        cssExample: `/* Example dialog customization */
.dialog {
  backdrop-filter: blur(15px);
  background-color: rgba(40, 40, 40, 0.9);
  border-radius: 10px;
}
.dialog-button.primary {
  background: linear-gradient(90deg, var(--primary-color), #2980b9);
}`
      }
    ]
  }
];

/**
 * Get help documentation for a specific property
 */
export function getPropertyHelp(propertyPath: string): PropertyHelp | null {
  for (const category of themeHelpDocs) {
    const property = category.properties.find(p => p.name === propertyPath);
    if (property) {
      return property;
    }
  }
  return null;
}

/**
 * Get help documentation for a category
 */
export function getCategoryHelp(categoryName: string): CategoryHelp | null {
  return themeHelpDocs.find(c => c.name === categoryName) || null;
}

/**
 * Get relevant tooltip text for a property
 */
export function getTooltip(propertyPath: string): string {
  const property = getPropertyHelp(propertyPath);
  
  if (!property) {
    return "No documentation available for this property.";
  }
  
  let tooltip = property.description;
  
  if (property.examples && property.examples.length > 0) {
    tooltip += `\n\nExamples: ${property.examples.join(', ')}`;
  }
  
  if (property.cssProperty) {
    tooltip += `\n\nCSS Variable: ${property.cssProperty}`;
  }
  
  return tooltip;
}

/**
 * Get a quick reference guide for all properties
 */
export function getPropertyQuickReference(): string {
  let reference = "# Theme Property Quick Reference\n\n";
  
  themeHelpDocs.forEach(category => {
    reference += `## ${category.name}\n${category.description}\n\n`;
    
    category.properties.forEach(property => {
      reference += `### ${property.name}\n${property.description}\n\n`;
      
      if (property.examples && property.examples.length > 0) {
        reference += `Examples: ${property.examples.join(', ')}\n\n`;
      }
      
      if (property.cssProperty) {
        reference += `CSS Variable: \`${property.cssProperty}\`\n\n`;
      }
      
      if (property.cssExample) {
        reference += "```css\n" + property.cssExample + "\n```\n\n";
      }
    });
  });
  
  return reference;
}
