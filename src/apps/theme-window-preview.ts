import { OS } from '../core/os';
import { Theme } from '../core/theme';

/**
 * Theme Window Preview
 * Provides a visual representation of window styles with the current theme
 */
export class ThemeWindowPreview {
  private container: HTMLElement;
  private currentTheme: Theme;
  private os: OS;

  constructor(container: HTMLElement, theme: Theme, os: OS) {
    this.container = container;
    this.currentTheme = theme;
    this.os = os;
    this.init();
  }

  /**
   * Initialize the window preview
   */
  private init(): void {
    this.render();
  }

  /**
   * Update the preview with a new theme
   */
  public updateTheme(theme: Theme): void {
    this.currentTheme = theme;
    this.render();
  }

  /**
   * Render the window preview
   */
  private render(): void {
    if (!this.container) return;

    // Create different window style previews
    this.container.innerHTML = `
      <div class="window-preview-section">
        <h3>Window Styles</h3>
        <div class="preview-container">
          <div class="window-preview active">
            <div class="window-titlebar">
              <div class="window-title">Active Window</div>
              <div class="window-controls">
                <span class="window-control minimize">−</span>
                <span class="window-control maximize">□</span>
                <span class="window-control close">×</span>
              </div>
            </div>
            <div class="window-content">
              <p>This is how an active window will appear with the current theme settings.</p>
              <div class="window-element-sample">
                <button class="sample-button">Button</button>
                <input type="text" class="sample-input" value="Text Input" />
                <select class="sample-select">
                  <option>Dropdown Menu</option>
                </select>
              </div>
            </div>
          </div>

          <div class="window-preview inactive">
            <div class="window-titlebar">
              <div class="window-title">Inactive Window</div>
              <div class="window-controls">
                <span class="window-control minimize">−</span>
                <span class="window-control maximize">□</span>
                <span class="window-control close">×</span>
              </div>
            </div>
            <div class="window-content">
              <p>This is how an inactive window will appear with the current theme settings.</p>
            </div>
          </div>
        </div>
      </div>

      <div class="window-preview-section">
        <h3>Dialog Styles</h3>
        <div class="preview-container">
          <div class="dialog-preview">
            <div class="dialog-titlebar">
              <div class="dialog-title">Sample Dialog</div>
              <div class="dialog-controls">
                <span class="dialog-control close">×</span>
              </div>
            </div>
            <div class="dialog-content">
              <p>This is how a dialog will appear with the current theme settings.</p>
              <div class="dialog-buttons">
                <button class="dialog-button primary">OK</button>
                <button class="dialog-button">Cancel</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    `;

    // Apply theme styles to the previews
    this.applyThemeStyles();
  }

  /**
   * Apply the current theme styles to the preview elements
   */
  private applyThemeStyles(): void {
    // Apply window styles
    const activeWindow = this.container.querySelector('.window-preview.active');
    const inactiveWindow = this.container.querySelector('.window-preview.inactive');
    const dialog = this.container.querySelector('.dialog-preview');
    
    if (activeWindow) {
      // Apply active window styles
      const titlebar = <HTMLElement>activeWindow.querySelector('.window-titlebar');
      if (titlebar && this.currentTheme.window?.titleBar) {
        titlebar.style.backgroundColor = this.currentTheme.window.titleBar.activeBackground || '';
        titlebar.style.color = this.currentTheme.window.titleBar.activeForeground || '';
        titlebar.style.borderBottom = this.currentTheme.window.titleBar.border ? 
          `1px solid ${this.currentTheme.window.titleBar.activeBorderColor || '#454545'}` : 'none';
      }

      // Apply window content styles
      const content = <HTMLElement>activeWindow.querySelector('.window-content');
      if (content && this.currentTheme.window) {
        content.style.backgroundColor = this.currentTheme.window.background || '';
        content.style.color = this.currentTheme.window.foreground || '';
        content.style.border = this.currentTheme.window.border ? 
          `1px solid ${this.currentTheme.window.borderColor || '#454545'}` : 'none';
      }      // Apply control button styles
      const controls = activeWindow.querySelectorAll('.window-control');
      if (controls.length && this.currentTheme.window?.titleBar) {
        controls.forEach(control => {
            const ctrl = control as HTMLElement;
          // Add null checks when accessing nested properties
          ctrl.style.backgroundColor = this.currentTheme.window?.titleBar?.buttonBackground || '';
          ctrl.style.color = this.currentTheme.window?.titleBar?.buttonForeground || '';
        });
      }
    }

    if (inactiveWindow) {
      // Apply inactive window styles
      const titlebar = <HTMLElement>inactiveWindow.querySelector('.window-titlebar');
      if (titlebar && this.currentTheme.window?.titleBar) {
        titlebar.style.backgroundColor = this.currentTheme.window.titleBar.inactiveBackground || '';
        titlebar.style.color = this.currentTheme.window.titleBar.inactiveForeground || '';
        titlebar.style.borderBottom = this.currentTheme.window.titleBar.border ? 
          `1px solid ${this.currentTheme.window.titleBar.inactiveBorderColor || '#333333'}` : 'none';
      }

      // Apply window content styles for inactive window (slightly dimmed)
      const content = <HTMLElement>inactiveWindow.querySelector('.window-content');
      if (content && this.currentTheme.window) {
        content.style.backgroundColor = this.currentTheme.window.background || '';
        content.style.color = this.currentTheme.window.foreground || '';
        content.style.opacity = '0.8'; // Make inactive window appear dimmed
        content.style.border = this.currentTheme.window.border ? 
          `1px solid ${this.currentTheme.window.borderColor || '#454545'}` : 'none';
      }
    }

    if (dialog) {
      // Apply dialog styles
      const dialogTitlebar = <HTMLElement>dialog.querySelector('.dialog-titlebar');
      if (dialogTitlebar && this.currentTheme.dialog) {
        dialogTitlebar.style.backgroundColor = this.currentTheme.dialog.titleBarBackground || '';
        dialogTitlebar.style.color = this.currentTheme.dialog.titleBarForeground || '';
      }

      const dialogContent = <HTMLElement>dialog.querySelector('.dialog-content');
      if (dialogContent && this.currentTheme.dialog) {
        dialogContent.style.backgroundColor = this.currentTheme.dialog.background || '';
        dialogContent.style.color = this.currentTheme.dialog.foreground || '';
      }      // Apply button styles
      const buttons = dialog.querySelectorAll('.dialog-button');
      if (buttons.length && this.currentTheme.dialog) {
        buttons.forEach(button => {
            const ctrl = button as HTMLElement;

          ctrl.style.backgroundColor = this.currentTheme.dialog?.buttonBackground || '';
          ctrl.style.color = this.currentTheme.dialog?.buttonForeground || '';
        });
      }

      // Apply primary button styles
      const primaryButton = <HTMLElement>dialog.querySelector('.dialog-button.primary');
      if (primaryButton && this.currentTheme.dialog) {
        primaryButton.style.backgroundColor = this.currentTheme.dialog.primaryButtonBackground || '';
        primaryButton.style.color = this.currentTheme.dialog.primaryButtonForeground || '';
      }
    }

    // Apply UI element size adjustments
    this.applyUiElementSizes();
  }

  /**
   * Apply UI element size adjustments based on theme settings
   */  private applyUiElementSizes(): void {
    if (!this.currentTheme.uiElementSizes) return;

    const { uiElementSizes } = this.currentTheme;
    
    // Apply button sizes
    if (uiElementSizes.buttonHeight) {
      // Calculate appropriate padding based on the button height
      const buttonHeight = parseInt(uiElementSizes.buttonHeight);
      const buttonPadding = `${Math.max(4, buttonHeight / 4)}px ${Math.max(8, buttonHeight / 2)}px`;
      
      this.container.querySelectorAll('.sample-button, .dialog-button').forEach(button => {
        (button as HTMLElement).style.padding = buttonPadding;
      });
    }
    
    // Apply input field sizes
    if (uiElementSizes.inputHeight) {
      const inputHeight = uiElementSizes.inputHeight;
      
      this.container.querySelectorAll('.sample-input, .sample-select').forEach(input => {
        (input as HTMLElement).style.height = inputHeight;
      });
    }
  }

  /**
   * Add interactive demo capabilities to the preview
   */
  public addInteractivity(): void {
    // Add click events to window controls
    const minimizeButtons = this.container.querySelectorAll('.window-control.minimize');
    const maximizeButtons = this.container.querySelectorAll('.window-control.maximize');
    const closeButtons = this.container.querySelectorAll('.window-control.close');
    
    minimizeButtons.forEach(button => {
      button.addEventListener('click', () => {
        // Simply demonstrate the animation
        const window = (button as HTMLElement).closest('.window-preview');
        if (window) {
          window.classList.add('minimizing');
          setTimeout(() => {
            window.classList.remove('minimizing');
          }, 500);
        }
      });
    });
    
    maximizeButtons.forEach(button => {
      button.addEventListener('click', () => {
        const window = (button as HTMLElement).closest('.window-preview');
        if (window) {
          window.classList.toggle('maximized');
          const maxButton = window.querySelector('.window-control.maximize');
          if (maxButton) {
            maxButton.textContent = window.classList.contains('maximized') ? '⧉' : '□';
          }
        }
      });
    });
    
    closeButtons.forEach(button => {
      button.addEventListener('click', () => {
        const window = (button as HTMLElement).closest('.window-preview, .dialog-preview');
        if (window) {
          window.classList.add('closing');
          setTimeout(() => {
            window.classList.remove('closing');
            // Reset the window after animation
            setTimeout(() => {
              window.classList.remove('maximized');
              const maxButton = window.querySelector('.window-control.maximize');
              if (maxButton) maxButton.textContent = '□';
            }, 300);
          }, 300);
        }
      });
    });
  }
}
