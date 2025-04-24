import { OS } from '../core/os';
import { Theme } from '../core/theme';
import { ThemeWindowPreview } from './theme-window-preview';

/**
 * Theme Preview Integrator
 * Integrates various theme preview components with the theme editor
 */
export class ThemePreviewIntegrator {
  private container: HTMLElement;
  private currentTheme: Theme;
  private os: OS;
  private windowPreview: ThemeWindowPreview | null = null;
  private activeTab: string = 'windows';

  constructor(container: HTMLElement, theme: Theme, os: OS) {
    this.container = container;
    this.currentTheme = theme;
    this.os = os;
    this.init();
  }

  /**
   * Initialize the theme preview integrator
   */
  private init(): void {
    this.render();
    this.setupEventListeners();
    this.showPreviewTab(this.activeTab);
  }

  /**
   * Update the preview with a new theme
   */
  public updateTheme(theme: Theme): void {
    this.currentTheme = theme;
    
    // Update window preview if it exists
    if (this.windowPreview) {
      this.windowPreview.updateTheme(theme);
    }
    
    // Update other previews as they are added
  }

  /**
   * Render the preview container with tabs
   */
  private render(): void {
    if (!this.container) return;

    this.container.innerHTML = `
      <div class="theme-preview-container">
        <div class="preview-tabs">
          <button class="preview-tab-btn active" data-tab="windows">Window Styles</button>
          <button class="preview-tab-btn" data-tab="taskbar">Taskbar</button>
          <button class="preview-tab-btn" data-tab="startmenu">Start Menu</button>
          <button class="preview-tab-btn" data-tab="animations">Animations</button>
          <button class="preview-tab-btn" data-tab="fonts">UI Fonts</button>
        </div>
        
        <div class="preview-content">
          <div class="preview-tab-content" id="preview-windows"></div>
          <div class="preview-tab-content" id="preview-taskbar" style="display:none;"></div>
          <div class="preview-tab-content" id="preview-startmenu" style="display:none;"></div>
          <div class="preview-tab-content" id="preview-animations" style="display:none;"></div>
          <div class="preview-tab-content" id="preview-fonts" style="display:none;"></div>
        </div>
      </div>
    `;
  }

  /**
   * Setup event listeners for tab switching
   */
  private setupEventListeners(): void {
    const tabButtons = this.container.querySelectorAll('.preview-tab-btn');
    
    tabButtons.forEach(button => {
      button.addEventListener('click', () => {
        const tab = button.getAttribute('data-tab');
        if (tab) {
          this.showPreviewTab(tab);
        }
      });
    });
  }

  /**
   * Show a specific preview tab
   */
  private showPreviewTab(tabId: string): void {
    // Update active tab
    this.activeTab = tabId;
    
    // Update tab button states
    const tabButtons = this.container.querySelectorAll('.preview-tab-btn');
    tabButtons.forEach(button => {
      button.classList.toggle('active', button.getAttribute('data-tab') === tabId);
    });
    
    // Update tab content visibility
    const tabContents = this.container.querySelectorAll('.preview-tab-content');
    tabContents.forEach(content => {
      (content as HTMLElement).style.display = 'none';
    });
    
    // Show selected tab
    const selectedTab = this.container.querySelector(`#preview-${tabId}`);
    if (selectedTab) {
      (selectedTab as HTMLElement).style.display = 'block';
      
      // Initialize content for the tab if needed
      this.initializeTabContent(tabId, selectedTab as HTMLElement);
    }
  }

  /**
   * Initialize content for a specific tab
   */
  private initializeTabContent(tabId: string, container: HTMLElement): void {
    switch (tabId) {
      case 'windows':
        this.initializeWindowsPreview(container);
        break;
      case 'taskbar':
        this.initializeTaskbarPreview(container);
        break;
      case 'startmenu':
        this.initializeStartMenuPreview(container);
        break;
      case 'animations':
        this.initializeAnimationsPreview(container);
        break;
      case 'fonts':
        this.initializeFontsPreview(container);
        break;
    }
  }

  /**
   * Initialize the windows preview tab
   */
  private initializeWindowsPreview(container: HTMLElement): void {
    // Only initialize if not already done
    if (!this.windowPreview) {
      this.windowPreview = new ThemeWindowPreview(container, this.currentTheme, this.os);
      this.windowPreview.addInteractivity();
    }
  }

  /**
   * Initialize the taskbar preview tab
   */
  private initializeTaskbarPreview(container: HTMLElement): void {
    container.innerHTML = `
      <div class="preview-section">
        <h3>Taskbar Preview</h3>
        <div class="taskbar-preview">
          <div class="taskbar-container">
            <div class="start-button">
              <span class="start-icon">🚀</span>
              <span class="start-text">Start</span>
            </div>
            <div class="taskbar-items">
              <div class="taskbar-item active">
                <span class="taskbar-icon">🌐</span>
              </div>
              <div class="taskbar-item">
                <span class="taskbar-icon">📁</span>
              </div>
              <div class="taskbar-item">
                <span class="taskbar-icon">📝</span>
              </div>
            </div>
            <div class="taskbar-tray">
              <span class="tray-icon">🔊</span>
              <span class="tray-icon">🔋</span>
              <span class="clock">17:42</span>
            </div>
          </div>
        </div>
        <p class="preview-description">
          This preview shows how the taskbar will appear with the current theme settings.
          The taskbar background, text colors, and hover effects will reflect your theme choices.
        </p>
      </div>
    `;

    // Apply theme styles to taskbar preview
    this.applyTaskbarStyles(container);
  }

  /**
   * Initialize the start menu preview tab
   */
  private initializeStartMenuPreview(container: HTMLElement): void {
    container.innerHTML = `
      <div class="preview-section">
        <h3>Start Menu Preview</h3>
        <div class="start-menu-preview">
          <div class="start-menu-container">
            <div class="user-section">
              <div class="user-avatar">👤</div>
              <div class="user-name">User</div>
            </div>
            <div class="apps-section">
              <div class="recently-used">
                <h4>Recently Used</h4>
                <div class="app-item">
                  <span class="app-icon">🌐</span>
                  <span class="app-name">Browser</span>
                </div>
                <div class="app-item">
                  <span class="app-icon">📝</span>
                  <span class="app-name">Text Editor</span>
                </div>
                <div class="app-item">
                  <span class="app-icon">📊</span>
                  <span class="app-name">System Monitor</span>
                </div>
              </div>
              <div class="all-apps">
                <h4>All Applications</h4>
                <div class="app-grid">
                  <div class="app-item">
                    <span class="app-icon">🌐</span>
                    <span class="app-name">Browser</span>
                  </div>
                  <div class="app-item">
                    <span class="app-icon">📝</span>
                    <span class="app-name">Text Editor</span>
                  </div>
                  <div class="app-item">
                    <span class="app-icon">💻</span>
                    <span class="app-name">Terminal</span>
                  </div>
                  <div class="app-item">
                    <span class="app-icon">📊</span>
                    <span class="app-name">System Monitor</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <p class="preview-description">
          This preview shows how the start menu will appear with the current theme settings.
        </p>
      </div>
    `;

    // Apply theme styles to start menu preview
    this.applyStartMenuStyles(container);
  }

  /**
   * Initialize the animations preview tab
   */
  private initializeAnimationsPreview(container: HTMLElement): void {
    container.innerHTML = `
      <div class="preview-section">
        <h3>Animation Speed Preview</h3>
        <div class="animation-preview">
          <div class="animation-controls">
            <button class="demo-button" data-animation="window-open">Open Window</button>
            <button class="demo-button" data-animation="window-close">Close Window</button>
            <button class="demo-button" data-animation="window-minimize">Minimize Window</button>
            <button class="demo-button" data-animation="window-maximize">Maximize Window</button>
          </div>
          <div class="animation-demo-area">
            <div class="demo-window">
              <div class="demo-titlebar">
                <div class="demo-title">Demo Window</div>
                <div class="demo-controls">
                  <span class="demo-control minimize">−</span>
                  <span class="demo-control maximize">□</span>
                  <span class="demo-control close">×</span>
                </div>
              </div>
              <div class="demo-content">
                <p>This window demonstrates animation effects with the current theme's speed settings.</p>
              </div>
            </div>
          </div>
        </div>
        <p class="preview-description">
          Click the buttons above to see how animations look with the current speed settings.
          The animation speed is set to: <span class="animation-speed-value">Normal</span>
        </p>
      </div>
    `;

    // Apply theme styles to animation preview
    this.applyAnimationStyles(container);
    
    // Add event listeners for demo buttons
    const demoButtons = container.querySelectorAll('.demo-button');
    demoButtons.forEach(button => {
      button.addEventListener('click', () => {
        const animationType = button.getAttribute('data-animation');
        this.playDemoAnimation(animationType, container);
      });
    });
  }

  /**
   * Initialize the UI fonts preview tab
   */
  private initializeFontsPreview(container: HTMLElement): void {
    container.innerHTML = `
      <div class="preview-section">
        <h3>UI Fonts Preview</h3>
        <div class="fonts-preview">
          <div class="font-sample title-font">
            <h4>Title Font</h4>
            <p class="font-preview">The quick brown fox jumps over the lazy dog</p>
            <p class="font-info">Font: <span class="font-name">Default Title Font</span>, Size: <span class="font-size">14px</span></p>
          </div>
          <div class="font-sample ui-font">
            <h4>UI Font</h4>
            <p class="font-preview">The quick brown fox jumps over the lazy dog</p>
            <p class="font-info">Font: <span class="font-name">Default UI Font</span>, Size: <span class="font-size">12px</span></p>
          </div>
          <div class="font-sample monospace-font">
            <h4>Monospace Font</h4>
            <p class="font-preview">The quick brown fox jumps over the lazy dog</p>
            <p class="font-info">Font: <span class="font-name">Default Monospace Font</span>, Size: <span class="font-size">13px</span></p>
          </div>
          <div class="ui-elements-sample">
            <h4>UI Elements with Applied Fonts</h4>
            <div class="elements-container">
              <button class="sample-button">Button</button>
              <input type="text" class="sample-input" value="Input Field" />
              <select class="sample-select">
                <option>Dropdown Menu</option>
              </select>
              <div class="sample-checkbox">
                <input type="checkbox" id="sample-check" checked />
                <label for="sample-check">Checkbox Label</label>
              </div>
            </div>
          </div>
        </div>
        <p class="preview-description">
          This preview shows how different UI fonts will appear with the current theme settings.
        </p>
      </div>
    `;

    // Apply theme font styles
    this.applyFontStyles(container);
  }

  /**
   * Apply taskbar theme styles
   */
  private applyTaskbarStyles(container: HTMLElement): void {
    const taskbar = container.querySelector('.taskbar-container');
    const startButton = container.querySelector('.start-button');
    const taskbarItems = container.querySelectorAll('.taskbar-item');
    
    if (taskbar) {
      (taskbar as HTMLElement).style.backgroundColor = this.currentTheme.taskbarBgStart || '#333';
      (taskbar as HTMLElement).style.color = this.currentTheme.textColorPrimary || '#fff';
    }
    
    if (startButton) {
      (startButton as HTMLElement).style.backgroundColor = this.currentTheme.startButtonBgStart || '#444';
      (startButton as HTMLElement).style.color = this.currentTheme.textColorPrimary || '#fff';
    }
    
    if (taskbarItems.length) {
      taskbarItems.forEach(item => {
        if (item.classList.contains('active')) {
          (item as HTMLElement).style.backgroundColor = this.currentTheme.taskbarItemActiveBg || '#555';
        } else {
          (item as HTMLElement).style.backgroundColor = 'transparent';
        }
      });
    }
  }

  /**
   * Apply start menu theme styles
   */
  private applyStartMenuStyles(container: HTMLElement): void {
    const startMenu = container.querySelector('.start-menu-container');
    const appItems = container.querySelectorAll('.app-item');
    
    if (startMenu) {
      (startMenu as HTMLElement).style.backgroundColor = this.currentTheme.secondaryColor || '#333';
      (startMenu as HTMLElement).style.color = this.currentTheme.textColorPrimary || '#fff';
    }
    
    if (appItems.length) {
      appItems.forEach(item => {
        item.addEventListener('mouseenter', () => {
          (item as HTMLElement).style.backgroundColor = this.currentTheme.accentColor || '#555';
        });
        
        item.addEventListener('mouseleave', () => {
          (item as HTMLElement).style.backgroundColor = 'transparent';
        });
      });
    }
  }

  /**
   * Apply animation theme styles
   */
  private applyAnimationStyles(container: HTMLElement): void {
    const demoWindow = container.querySelector('.demo-window');
    const speedValue = container.querySelector('.animation-speed-value');
    
    if (demoWindow && this.currentTheme.window) {
      // Apply window styles
      const titlebar = demoWindow.querySelector('.demo-titlebar');
      if (titlebar && this.currentTheme.window.titleBar) {
        (titlebar as HTMLElement).style.backgroundColor = this.currentTheme.window.titleBar.activeBackground || '';
        (titlebar as HTMLElement).style.color = this.currentTheme.window.titleBar.activeForeground || '';
      }
      
      const content = demoWindow.querySelector('.demo-content');
      if (content && this.currentTheme.window) {
        (content as HTMLElement).style.backgroundColor = this.currentTheme.window.background || '';
        (content as HTMLElement).style.color = this.currentTheme.window.foreground || '';
      }
    }
    
    // Set animation speed value text
    if (speedValue && this.currentTheme.animationSpeed !== undefined) {
      let speedText = 'Normal';
      
      if (this.currentTheme.animationSpeed === 0) {
        speedText = 'None';
      } else if (this.currentTheme.animationSpeed < 1) {
        speedText = 'Fast';
      } else if (this.currentTheme.animationSpeed > 1) {
        speedText = 'Slow';
      }
      
      speedValue.textContent = speedText;
      
      // Update CSS variables for animation speed
      document.documentElement.style.setProperty(
        '--theme-animation-speed', 
        `${this.currentTheme.animationSpeed || 1}s`
      );
    }
  }

  /**
   * Apply font theme styles
   */
  private applyFontStyles(container: HTMLElement): void {
    const titleFont = container.querySelector('.title-font .font-preview');
    const uiFont = container.querySelector('.ui-font .font-preview');
    const monoFont = container.querySelector('.monospace-font .font-preview');
    
    const titleFontInfo = container.querySelector('.title-font .font-name');
    const uiFontInfo = container.querySelector('.ui-font .font-name');
    const monoFontInfo = container.querySelector('.monospace-font .font-name');
    
    const titleFontSize = container.querySelector('.title-font .font-size');
    const uiFontSize = container.querySelector('.ui-font .font-size');
    const monoFontSize = container.querySelector('.monospace-font .font-size');
    
    // Apply title font (using headerFont from theme)
    if (titleFont && this.currentTheme.customFonts?.headerFont) {
      (titleFont as HTMLElement).style.fontFamily = this.currentTheme.customFonts.headerFont;
      if (titleFontInfo) titleFontInfo.textContent = this.currentTheme.customFonts.headerFont;
    }
    
    // Apply UI font (using systemFont from theme)
    if (uiFont && this.currentTheme.customFonts?.systemFont) {
      (uiFont as HTMLElement).style.fontFamily = this.currentTheme.customFonts.systemFont;
      if (uiFontInfo) uiFontInfo.textContent = this.currentTheme.customFonts.systemFont;
    }
    
    // Apply monospace font
    if (monoFont && this.currentTheme.customFonts?.monospaceFont) {
      (monoFont as HTMLElement).style.fontFamily = this.currentTheme.customFonts.monospaceFont;
      if (monoFontInfo) monoFontInfo.textContent = this.currentTheme.customFonts.monospaceFont;
    }
    
    // Apply font sizes - these aren't in the Theme interface, so using default values
    if (titleFontSize) {
      titleFontSize.textContent = "14px";
    }
    
    if (uiFontSize) {
      uiFontSize.textContent = "12px";
    }
    
    if (monoFontSize) {
      monoFontSize.textContent = "13px";
    }
    
    // Apply to UI elements sample
    const uiElements = container.querySelectorAll('.ui-elements-sample button, .ui-elements-sample input, .ui-elements-sample select, .ui-elements-sample label');
    if (uiElements.length && this.currentTheme.customFonts?.systemFont) {
      uiElements.forEach(element => {
        (element as HTMLElement).style.fontFamily = this.currentTheme.customFonts?.systemFont || '';
        (element as HTMLElement).style.fontSize = '12px';
      });
    }
  }

  /**
   * Play a demo animation
   */
  private playDemoAnimation(animationType: string | null, container: HTMLElement): void {
    if (!animationType) return;
    
    const demoWindow = container.querySelector('.demo-window');
    if (!demoWindow) return;
    
    // Calculate animation duration based on theme settings
    const duration = (this.currentTheme.animationSpeed || 1) * 1000;
    
    // Reset any existing animations
    demoWindow.classList.remove('opening', 'closing', 'minimizing', 'maximizing');
    
    // Apply new animation
    switch (animationType) {
      case 'window-open':
        demoWindow.classList.add('opening');
        break;
      case 'window-close':
        demoWindow.classList.add('closing');
        break;
      case 'window-minimize':
        demoWindow.classList.add('minimizing');
        break;
      case 'window-maximize':
        demoWindow.classList.toggle('maximized');
        if (demoWindow.classList.contains('maximized')) {
          demoWindow.classList.add('maximizing');
        } else {
          demoWindow.classList.add('restoring');
        }
        break;
    }
    
    // Remove animation class after it completes
    setTimeout(() => {
      demoWindow.classList.remove('opening', 'closing', 'minimizing', 'maximizing', 'restoring');
      
      // Reset window if it was closed
      if (animationType === 'window-close') {
        setTimeout(() => {
          demoWindow.classList.remove('maximized');
          demoWindow.classList.add('opening');
          
          // Remove the opening class after animation completes
          setTimeout(() => {
            demoWindow.classList.remove('opening');
          }, duration);
        }, 500);
      }
    }, duration);
  }
}
