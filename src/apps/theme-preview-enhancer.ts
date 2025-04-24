/**
 * Theme Preview Enhancer
 * Provides advanced preview functionality for the theme editor
 */
import { Theme } from '../core/theme';

/**
 * Manages advanced preview functionality for the theme editor
 */
export class ThemePreviewEnhancer {
    private container: HTMLElement;
    private currentTheme: Theme;
    private liveUpdateEnabled: boolean = false;
    private comparisonTheme: Theme | null = null;
    private darkModeEnabled: boolean = false;
    
    /**
     * Create a new ThemePreviewEnhancer
     * @param container The container element for the preview
     * @param initialTheme The initial theme to preview
     */
    constructor(container: HTMLElement, initialTheme: Theme) {
        this.container = container;
        this.currentTheme = initialTheme;
    }
    
    /**
     * Enable or disable live update mode
     * @param enabled Whether live update should be enabled
     */
    public setLiveUpdate(enabled: boolean): void {
        this.liveUpdateEnabled = enabled;
    }
    
    /**
     * Set the theme for comparison
     * @param theme The theme to compare with
     */
    public setComparisonTheme(theme: Theme | null): void {
        this.comparisonTheme = theme;
    }
    
    /**
     * Toggle dark mode preview
     * @param enabled Whether dark mode should be enabled
     */
    public setDarkMode(enabled: boolean): void {
        this.darkModeEnabled = enabled;
        this.updatePreview();
    }
    
    /**
     * Update the preview with the current theme
     * @param theme The theme to preview (optional, uses the current theme if not provided)
     */
    public updatePreview(theme?: Theme): void {
        if (theme) {
            this.currentTheme = theme;
        }
        
        // Apply current theme to preview container
        this.renderPreview();
    }
    
    /**
     * Create a rich preview with desktop, windows, and taskbar
     */
    private renderFullDesktopPreview(): string {
        const theme = this.currentTheme;
        
        // Apply dark mode modifications if enabled
        const baseColors = this.darkModeEnabled ? this.getDarkModeColors(theme) : theme;
        
        return `
            <div class="desktop-preview" style="background-color: ${baseColors.desktopBgColor}; position: relative; height: 300px; overflow: hidden; border-radius: 4px;">
                <!-- Window 1: Main window -->
                <div class="preview-window" style="position: absolute; top: 30px; left: 30px; width: 240px; height: 160px; background-color: ${baseColors.primaryColor}; border: 1px solid ${baseColors.windowBorderColor}; box-shadow: 0 4px 8px rgba(0,0,0,0.2); z-index: 2;">
                    <div class="preview-window-header" style="background-color: ${baseColors.windowHeaderColor}; color: ${baseColors.textColorPrimary}; display: flex; justify-content: space-between; padding: 4px 8px; ${this.getHeaderStyles(theme)}">
                        <div style="display: flex; align-items: center;">
                            ${theme.titleBar?.showIcon ? '<span class="window-icon" style="margin-right: 6px;">📑</span>' : ''}
                            <span class="window-title">Document</span>
                        </div>
                        <div class="window-controls" style="display: flex; ${theme.titleBar?.buttonPlacement === 'left' ? 'order: -1; margin-right: 8px;' : 'margin-left: 8px;'}">
                            <button class="window-button" style="margin: 0 2px; border: none; background: none; color: inherit;">−</button>
                            <button class="window-button" style="margin: 0 2px; border: none; background: none; color: inherit;">□</button>
                            <button class="window-button" style="margin: 0 2px; border: none; background: none; color: inherit;">×</button>
                        </div>
                    </div>
                    <div class="preview-window-content" style="color: ${baseColors.textColorPrimary}; padding: 8px; font-size: 12px;">
                        <p style="margin: 4px 0;">Document content here...</p>
                    </div>
                </div>
                
                <!-- Window 2: Background window -->
                <div class="preview-window" style="position: absolute; top: 70px; left: 100px; width: 200px; height: 140px; background-color: ${baseColors.primaryColor}; border: 1px solid ${baseColors.windowBorderColor}; box-shadow: 0 4px 8px rgba(0,0,0,0.1); z-index: 1; opacity: 0.9;">
                    <div class="preview-window-header" style="background-color: ${baseColors.windowHeaderColor}; color: ${baseColors.textColorPrimary}; display: flex; justify-content: space-between; padding: 4px 8px; ${this.getHeaderStyles(theme)}">
                        <div style="display: flex; align-items: center;">
                            ${theme.titleBar?.showIcon ? '<span class="window-icon" style="margin-right: 6px;">💻</span>' : ''}
                            <span class="window-title">Terminal</span>
                        </div>
                        <div class="window-controls" style="display: flex; ${theme.titleBar?.buttonPlacement === 'left' ? 'order: -1; margin-right: 8px;' : 'margin-left: 8px;'}">
                            <button class="window-button" style="margin: 0 2px; border: none; background: none; color: inherit;">−</button>
                            <button class="window-button" style="margin: 0 2px; border: none; background: none; color: inherit;">□</button>
                            <button class="window-button" style="margin: 0 2px; border: none; background: none; color: inherit;">×</button>
                        </div>
                    </div>
                    <div class="preview-window-content" style="color: ${baseColors.textColorPrimary}; padding: 8px; font-size: 12px; background-color: ${theme.tertiaryColor};">
                        <p style="margin: 2px 0; font-family: monospace;">$ echo "Hello, world!"</p>
                        <p style="margin: 2px 0; font-family: monospace;">Hello, world!</p>
                        <p style="margin: 2px 0; font-family: monospace;">$ _</p>
                    </div>
                </div>
                
                <!-- Taskbar -->
                <div class="preview-taskbar" style="position: absolute; bottom: 0; left: 0; right: 0; background: linear-gradient(to bottom, ${baseColors.taskbarBgStart}, ${baseColors.taskbarBgEnd}); border-top: 1px solid ${baseColors.taskbarBorderTopColor}; padding: 4px; display: flex; gap: 4px; ${this.getTaskbarStyles(theme)}">
                    <button class="preview-start-button" style="background: linear-gradient(to bottom, ${baseColors.startButtonBgStart}, ${baseColors.startButtonBgEnd}); color: white; border: none; padding: 2px 8px; font-size: 12px;">
                        Start
                    </button>
                    <div style="background-color: ${baseColors.taskbarItemActiveBg}; padding: 2px 8px; font-size: 12px; border-bottom: ${baseColors.taskbarItemActiveBorder};">
                        Document
                    </div>
                    <div style="background-color: ${baseColors.taskbarItemBg}; padding: 2px 8px; font-size: 12px;">
                        Terminal
                    </div>
                    
                    <!-- System tray -->
                    <div style="margin-left: auto; display: flex; gap: 8px; padding: 0 8px; background-color: ${baseColors.systemTrayBg || 'transparent'};">
                        <span style="font-size: 12px; color: ${baseColors.textColorPrimary};">CPU: 5%</span>
                        <span style="font-size: 12px; color: ${baseColors.textColorPrimary};">16:42</span>
                    </div>
                </div>
            </div>
        `;
    }
    
    /**
     * Get styled elements with hover states for interactive preview
     */
    private renderInteractiveElements(): string {
        const theme = this.currentTheme;
        
        // Apply dark mode modifications if enabled
        const baseColors = this.darkModeEnabled ? this.getDarkModeColors(theme) : theme;
        
        return `
            <div class="interactive-elements" style="background-color: ${baseColors.primaryColor}; padding: 15px; border-radius: 4px; color: ${baseColors.textColorPrimary};">
                <h3 style="margin-top: 0; font-size: 16px;">Interactive Elements</h3>
                <p style="margin-bottom: 10px;">Hover over elements to see their states</p>
                
                <style>
                    .interactive-button {
                        background: linear-gradient(to bottom, ${baseColors.accentColor}, ${baseColors.accentColorDark});
                        color: white;
                        border: none;
                        padding: 6px 12px;
                        margin-right: 8px;
                        margin-bottom: 12px;
                        transition: all 0.2s ease;
                        cursor: pointer;
                        ${theme.uiElementSizes?.borderRadius ? `border-radius: ${theme.uiElementSizes.borderRadius};` : ''}
                        ${theme.uiElementSizes?.buttonHeight ? `height: ${theme.uiElementSizes.buttonHeight};` : ''}
                    }
                    
                    .interactive-button:hover {
                        background: linear-gradient(to bottom, ${baseColors.accentColorLight || baseColors.accentColor}, ${baseColors.accentColor});
                        transform: translateY(-1px);
                        box-shadow: 0 2px 4px rgba(0,0,0,0.2);
                    }
                    
                    .interactive-button:active {
                        background: ${baseColors.accentColorDark};
                        transform: translateY(1px);
                        box-shadow: none;
                    }
                    
                    .interactive-input {
                        background-color: ${baseColors.tertiaryColor};
                        color: ${baseColors.textColorPrimary};
                        border: 1px solid rgba(0,0,0,0.2);
                        padding: 6px 12px;
                        transition: all 0.2s ease;
                        ${theme.uiElementSizes?.borderRadius ? `border-radius: ${theme.uiElementSizes.borderRadius};` : ''}
                        ${theme.uiElementSizes?.inputHeight ? `height: ${theme.uiElementSizes.inputHeight};` : ''}
                    }
                    
                    .interactive-input:focus {
                        border-color: ${baseColors.accentColor};
                        outline: none;
                        box-shadow: 0 0 0 2px rgba(${this.hexToRgb(baseColors.accentColor)}, 0.3);
                    }
                    
                    .taskbar-item {
                        background-color: ${baseColors.taskbarItemBg};
                        padding: 4px 10px;
                        transition: all 0.2s ease;
                    }
                    
                    .taskbar-item:hover {
                        background-color: ${baseColors.taskbarItemHoverBg || 'rgba(255,255,255,0.1)'};
                    }
                </style>
                
                <div style="display: flex; gap: 10px; flex-wrap: wrap;">
                    <button class="interactive-button">Primary Button</button>
                    <button class="interactive-button" style="background: linear-gradient(to bottom, ${baseColors.secondaryColor}, ${this.darken(baseColors.secondaryColor)});">Secondary Button</button>
                    <input type="text" class="interactive-input" placeholder="Click to focus" />
                </div>
                
                <div style="margin-top: 20px; background: linear-gradient(to bottom, ${baseColors.taskbarBgStart}, ${baseColors.taskbarBgEnd}); padding: 4px; display: flex; gap: 4px; border-radius: 2px;">
                    <div class="taskbar-item">App 1</div>
                    <div class="taskbar-item">App 2</div>
                    <div class="taskbar-item" style="background-color: ${baseColors.taskbarItemActiveBg}; border-bottom: ${baseColors.taskbarItemActiveBorder};">Active App</div>
                </div>
            </div>
        `;
    }
    
    /**
     * Create a side-by-side comparison view
     */
    private renderComparisonView(): string {
        if (!this.comparisonTheme) {
            return '<div class="comparison-notice" style="padding: 10px; background-color: #f8f8f8; border-radius: 4px;">To enable comparison view, select a theme to compare with.</div>';
        }
        
        const theme1 = this.currentTheme;
        const theme2 = this.comparisonTheme;
        
        return `
            <div class="comparison-view" style="display: flex; gap: 15px;">
                <div style="flex: 1;">
                    <h4 style="text-align: center; margin-top: 0;">${theme1.name}</h4>
                    <div style="background-color: ${theme1.primaryColor}; border: 1px solid ${theme1.windowBorderColor}; border-radius: 4px; overflow: hidden;">
                        <div style="background-color: ${theme1.windowHeaderColor}; color: ${theme1.textColorPrimary}; padding: 8px;">Window Title</div>
                        <div style="padding: 10px; color: ${theme1.textColorPrimary};">
                            <p style="color: ${theme1.textColorPrimary};">Primary Text</p>
                            <p style="color: ${theme1.textColorSecondary};">Secondary Text</p>
                            <button style="background: linear-gradient(to bottom, ${theme1.accentColor}, ${theme1.accentColorDark}); color: white; border: none; padding: 5px 10px; border-radius: 4px;">Button</button>
                        </div>
                    </div>
                </div>
                
                <div style="flex: 1;">
                    <h4 style="text-align: center; margin-top: 0;">${theme2.name}</h4>
                    <div style="background-color: ${theme2.primaryColor}; border: 1px solid ${theme2.windowBorderColor}; border-radius: 4px; overflow: hidden;">
                        <div style="background-color: ${theme2.windowHeaderColor}; color: ${theme2.textColorPrimary}; padding: 8px;">Window Title</div>
                        <div style="padding: 10px; color: ${theme2.textColorPrimary};">
                            <p style="color: ${theme2.textColorPrimary};">Primary Text</p>
                            <p style="color: ${theme2.textColorSecondary};">Secondary Text</p>
                            <button style="background: linear-gradient(to bottom, ${theme2.accentColor}, ${theme2.accentColorDark}); color: white; border: none; padding: 5px 10px; border-radius: 4px;">Button</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }
    
    /**
     * Render the CSS viewer that shows generated CSS
     */
    private renderCssViewer(): string {
        const theme = this.currentTheme;
        
        // Create a simplified CSS representation
        const cssText = `
/* Theme: ${theme.name} */

:root {
  --accent-color: ${theme.accentColor};
  --accent-color-light: ${theme.accentColorLight};
  --accent-color-dark: ${theme.accentColorDark};
  --primary-color: ${theme.primaryColor};
  --secondary-color: ${theme.secondaryColor};
  --tertiary-color: ${theme.tertiaryColor};
  
  --text-color-primary: ${theme.textColorPrimary};
  --text-color-secondary: ${theme.textColorSecondary};
  --text-color-disabled: ${theme.textColorDisabled};
  
  --window-border-color: ${theme.windowBorderColor};
  --window-header-color: ${theme.windowHeaderColor};
  
  --system-font: ${theme.customFonts?.systemFont || 'inherit'};
  --monospace-font: ${theme.customFonts?.monospaceFont || 'monospace'};
  --header-font: ${theme.customFonts?.headerFont || 'inherit'};
  
  --border-radius: ${theme.uiElementSizes?.borderRadius || '4px'};
  --button-height: ${theme.uiElementSizes?.buttonHeight || 'auto'};
  --input-height: ${theme.uiElementSizes?.inputHeight || 'auto'};
  
  --animation-speed: ${theme.animationSpeed || '1'}s;
}

/* Window styles */
.window {
  background-color: var(--primary-color);
  border: 1px solid var(--window-border-color);
}

.window-header {
  background-color: var(--window-header-color);
  color: var(--text-color-primary);
}

/* Button styles */
.button {
  background: linear-gradient(to bottom, var(--accent-color), var(--accent-color-dark));
  color: white;
  border-radius: var(--border-radius);
  height: var(--button-height);
  transition: all var(--animation-speed);
}

.button:hover {
  background: linear-gradient(to bottom, var(--accent-color-light), var(--accent-color));
}

/* Input styles */
input, textarea, select {
  background-color: var(--tertiary-color);
  color: var(--text-color-primary);
  border-radius: var(--border-radius);
  height: var(--input-height);
}

/* Taskbar styles */
.taskbar {
  background: linear-gradient(to bottom, ${theme.taskbarBgStart}, ${theme.taskbarBgEnd});
  border-top: 1px solid ${theme.taskbarBorderTopColor};
}

.taskbar-item {
  background-color: ${theme.taskbarItemBg};
}

.taskbar-item.active {
  background-color: ${theme.taskbarItemActiveBg};
  border-bottom: ${theme.taskbarItemActiveBorder};
}
`;

        return `
            <div class="css-viewer" style="background-color: ${theme.tertiaryColor}; border-radius: 4px; overflow: hidden;">
                <div style="background-color: ${theme.secondaryColor}; padding: 8px; color: ${theme.textColorPrimary}; font-weight: bold;">Generated CSS</div>
                <pre style="margin: 0; padding: 12px; color: ${theme.textColorPrimary}; font-family: monospace; white-space: pre-wrap; max-height: 300px; overflow: auto;">${this.escapeHtml(cssText)}</pre>
            </div>
        `;
    }
    
    /**
     * Render the preview with all sections
     */
    private renderPreview(): void {
        // Create the tabbed preview interface
        const previewHTML = `
            <div class="preview-controls" style="margin-bottom: 15px; display: flex; justify-content: space-between; align-items: center;">
                <div class="preview-tabs">
                    <button class="preview-tab active" data-tab="desktop">Full Desktop</button>
                    <button class="preview-tab" data-tab="interactive">Interactive</button>
                    <button class="preview-tab" data-tab="comparison">Comparison</button>
                    <button class="preview-tab" data-tab="css">CSS View</button>
                </div>
                
                <div class="preview-options">
                    <label style="margin-right: 10px;">
                        <input type="checkbox" id="preview-dark-mode" ${this.darkModeEnabled ? 'checked' : ''}>
                        Dark Mode
                    </label>
                    <label>
                        <input type="checkbox" id="preview-live-update" ${this.liveUpdateEnabled ? 'checked' : ''}>
                        Live Update
                    </label>
                </div>
            </div>
            
            <div class="preview-content">
                <div class="preview-pane active" data-tab="desktop">
                    ${this.renderFullDesktopPreview()}
                </div>
                
                <div class="preview-pane" data-tab="interactive">
                    ${this.renderInteractiveElements()}
                </div>
                
                <div class="preview-pane" data-tab="comparison">
                    ${this.renderComparisonView()}
                </div>
                
                <div class="preview-pane" data-tab="css">
                    ${this.renderCssViewer()}
                </div>
            </div>
        `;
        
        // Update the container
        this.container.innerHTML = previewHTML;
        
        // Add tab switching functionality
        const tabs = this.container.querySelectorAll('.preview-tab');
        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                // Update active tab
                tabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
                
                // Update active pane
                const tabId = tab.getAttribute('data-tab');
                const panes = this.container.querySelectorAll('.preview-pane');
                panes.forEach(pane => {
                    pane.classList.remove('active');
                    if (pane.getAttribute('data-tab') === tabId) {
                        pane.classList.add('active');
                    }
                });
            });
        });
        
        // Add event listeners for controls
        const darkModeCheckbox = this.container.querySelector('#preview-dark-mode') as HTMLInputElement;
        if (darkModeCheckbox) {
            darkModeCheckbox.addEventListener('change', () => {
                this.setDarkMode(darkModeCheckbox.checked);
            });
        }
        
        const liveUpdateCheckbox = this.container.querySelector('#preview-live-update') as HTMLInputElement;
        if (liveUpdateCheckbox) {
            liveUpdateCheckbox.addEventListener('change', () => {
                this.setLiveUpdate(liveUpdateCheckbox.checked);
            });
        }
    }
    
    /**
     * Get header styles based on theme configuration
     */
    private getHeaderStyles(theme: Theme): string {
        let styles = '';
        
        if (theme.titleBar) {
            if (theme.titleBar.useGradient) {
                styles += `background: linear-gradient(to bottom, ${theme.windowHeaderColor}, ${this.darken(theme.windowHeaderColor)});`;
            }
            
            if (theme.titleBar.textAlignment) {
                styles += `text-align: ${theme.titleBar.textAlignment};`;
            }
            
            if (theme.titleBar.height) {
                styles += `height: ${theme.titleBar.height};`;
            }
            
            if (theme.titleBar.buttonStyle === 'circle') {
                styles += `
                .window-button {
                    border-radius: 50% !important;
                    width: 12px;
                    height: 12px;
                    font-size: 0;
                    margin: 0 2px !important;
                }
                `;
            } else if (theme.titleBar.buttonStyle === 'pill') {
                styles += `
                .window-button {
                    border-radius: 8px !important;
                    margin: 0 2px !important;
                }
                `;
            }
        }
        
        return styles;
    }
    
    /**
     * Get taskbar styles based on theme configuration
     */
    private getTaskbarStyles(theme: Theme): string {
        let styles = '';
        
        if (theme.taskbar) {
            if (theme.taskbar.position === 'top') {
                styles += 'top: 0; bottom: auto;';
            } else if (theme.taskbar.position === 'left') {
                styles += 'top: 0; bottom: 0; width: 40px; height: 100%; flex-direction: column;';
            } else if (theme.taskbar.position === 'right') {
                styles += 'top: 0; right: 0; left: auto; bottom: 0; width: 40px; height: 100%; flex-direction: column;';
            }
            
            if (theme.taskbar.size) {
                if (theme.taskbar.position === 'top' || theme.taskbar.position === 'bottom') {
                    styles += `height: ${theme.taskbar.size};`;
                } else {
                    styles += `width: ${theme.taskbar.size};`;
                }
            }
            
            if (theme.taskbar.transparency) {
                styles += `opacity: ${theme.taskbar.transparency};`;
            }
            
            if (theme.taskbar.blur) {
                styles += 'backdrop-filter: blur(5px); -webkit-backdrop-filter: blur(5px);';
            }
            
            if (theme.taskbar.itemSpacing) {
                styles += `gap: ${theme.taskbar.itemSpacing};`;
            }
        }
        
        return styles;
    }
    
    /**
     * Convert hex color to RGB values
     */
    private hexToRgb(hex: string): string {
        // Remove # if present
        hex = hex.replace('#', '');
        
        // Parse the hex values
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        
        // Return RGB values as string
        return `${r}, ${g}, ${b}`;
    }
    
    /**
     * Darken a color by 20%
     */
    private darken(color: string): string {
        // Remove # if present
        const hex = color.replace('#', '');
        
        // Parse the hex values
        let r = parseInt(hex.substring(0, 2), 16);
        let g = parseInt(hex.substring(2, 4), 16);
        let b = parseInt(hex.substring(4, 6), 16);
        
        // Darken by 20%
        r = Math.max(0, Math.floor(r * 0.8));
        g = Math.max(0, Math.floor(g * 0.8));
        b = Math.max(0, Math.floor(b * 0.8));
        
        // Convert back to hex
        return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
    }
    
    /**
     * Apply dark mode to a theme
     */
    private getDarkModeColors(theme: Theme): Theme {
        // Create a copy to avoid modifying the original
        const darkTheme = { ...theme };
        
        // Invert or darken colors
        if (this.isLightColor(theme.primaryColor)) {
            darkTheme.primaryColor = this.invertOrDarken(theme.primaryColor);
        }
        
        if (this.isLightColor(theme.secondaryColor)) {
            darkTheme.secondaryColor = this.invertOrDarken(theme.secondaryColor);
        }
        
        if (this.isLightColor(theme.tertiaryColor)) {
            darkTheme.tertiaryColor = this.invertOrDarken(theme.tertiaryColor);
        }
        
        // Invert text colors if needed
        if (this.isLightColor(theme.textColorPrimary)) {
            darkTheme.textColorPrimary = this.invertOrDarken(theme.textColorPrimary);
        }
        
        if (this.isLightColor(theme.textColorSecondary)) {
            darkTheme.textColorSecondary = this.invertOrDarken(theme.textColorSecondary);
        }
        
        return darkTheme;
    }
    
    /**
     * Check if a color is light
     */
    private isLightColor(color: string): boolean {
        // Remove # if present
        const hex = color.replace('#', '');
        
        // Parse the hex values
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        
        // Calculate luminance (perceived brightness)
        const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        
        // Return true if light, false if dark
        return luminance > 0.5;
    }
    
    /**
     * Invert or darken a color depending on its brightness
     */
    private invertOrDarken(color: string): string {
        // Remove # if present
        const hex = color.replace('#', '');
        
        // Parse the hex values
        let r = parseInt(hex.substring(0, 2), 16);
        let g = parseInt(hex.substring(2, 4), 16);
        let b = parseInt(hex.substring(4, 6), 16);
        
        // Calculate luminance (perceived brightness)
        const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        
        if (luminance > 0.8) {
            // Very light color, invert it
            r = 255 - r;
            g = 255 - g;
            b = 255 - b;
        } else {
            // Darken by 40%
            r = Math.max(0, Math.floor(r * 0.6));
            g = Math.max(0, Math.floor(g * 0.6));
            b = Math.max(0, Math.floor(b * 0.6));
        }
        
        // Convert back to hex
        return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
    }
    
    /**
     * Escape HTML special characters
     */
    private escapeHtml(text: string): string {
        return text
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }
}
