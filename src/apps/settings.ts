import { OS } from '../core/os';
import { GuiApplication } from '../core/gui-application';
import { BaseSettings } from '../core/BaseSettings';
import { UserSettings } from '../core/UserSettings';
import { ErrorHandler } from '../core/error-handler';
import { ThemeManager } from '../core/ThemeManager';
import { Theme } from '../core/theme';
import { AppManager } from '../core/app-manager';

/**
 * Settings App for the Hacker Game
 * Provides a graphical interface to customize user preferences and system appearance
 */
export class SettingsApp extends GuiApplication {  private userSettings: UserSettings;
  private themeManager: ThemeManager;
  private activeSection: string = 'appearance';
  private searchQuery: string = '';
  private settingsIndex: Map<string, { section: string, key: string, title: string, description: string }> = new Map();
  
  // Accent color options
  private accentColors = [
    { id: 'blue', name: 'Blue', color: '#0078d7' },
    { id: 'green', name: 'Green', color: '#107c10' },
    { id: 'red', name: 'Red', color: '#e81123' },
    { id: 'orange', name: 'Orange', color: '#ff8c00' },
    { id: 'purple', name: 'Purple', color: '#5c2d91' }
  ];
  
  // Desktop background options
  private backgroundOptions = [
    { id: 'solid', name: 'Solid Color' },
    { id: 'gradient', name: 'Gradient' },
    { id: 'image', name: 'Image' }
  ];
  
  constructor(os: OS) {
    super(os);
    this.userSettings = os.getUserSettings();
    this.themeManager = ThemeManager.getInstance(os.getFileSystem(), this.userSettings);
    this.buildSettingsIndex();
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'settings';
  }

  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    this.render();
    this.setupEventListeners();
    this.loadCurrentSettings();
  }

  /**
   * Build the search index for settings
   */
  private buildSettingsIndex(): void {
    // Appearance settings
    this.settingsIndex.set('theme', {
      section: 'appearance',
      key: 'theme',
      title: 'Theme',
      description: 'Change the overall color theme of the system'
    });
    
    this.settingsIndex.set('accent-color', {
      section: 'appearance',
      key: 'accentColor',
      title: 'Accent Color',
      description: 'Set the highlight color for buttons and interactive elements'
    });
    
    this.settingsIndex.set('background', {
      section: 'appearance',
      key: 'background',
      title: 'Desktop Background',
      description: 'Change your desktop wallpaper or background color'
    });
    
    this.settingsIndex.set('font-size', {
      section: 'appearance',
      key: 'fontSize',
      title: 'Font Size',
      description: 'Adjust the size of text throughout the system'
    });
    
    this.settingsIndex.set('animation', {
      section: 'appearance',
      key: 'animationsEnabled',
      title: 'Animations',
      description: 'Toggle system animations on or off'
    });
    
    // Display settings
    this.settingsIndex.set('resolution', {
      section: 'display',
      key: 'resolution',
      title: 'Resolution',
      description: 'Change the display resolution'
    });
    
    this.settingsIndex.set('scale', {
      section: 'display',
      key: 'displayScale',
      title: 'Display Scale',
      description: 'Scale UI elements for better visibility'
    });
    
    // Sound settings
    this.settingsIndex.set('volume', {
      section: 'sound',
      key: 'masterVolume',
      title: 'Master Volume',
      description: 'Adjust the overall system volume'
    });
    
    this.settingsIndex.set('sound-effects', {
      section: 'sound',
      key: 'soundEffectsEnabled',
      title: 'Sound Effects',
      description: 'Toggle system sound effects'
    });
    
    // Privacy settings
    this.settingsIndex.set('activity', {
      section: 'privacy',
      key: 'activityTracking',
      title: 'Activity Tracking',
      description: 'Control whether your system activities are tracked'
    });
    
    this.settingsIndex.set('error-reporting', {
      section: 'privacy',
      key: 'errorReporting.enabled',
      title: 'Error Reporting',
      description: 'Collect and store error information to help diagnose problems'
    });
    
    this.settingsIndex.set('error-telemetry', {
      section: 'privacy',
      key: 'errorReporting.telemetry',
      title: 'Error Telemetry',
      description: 'Send anonymous error information to help improve the application'
    });
    
    this.settingsIndex.set('clear-history', {
      section: 'privacy',
      key: 'clearHistory',
      title: 'Clear History',
      description: 'Clear your browsing and command history'
    });
    
    // Personalization
    this.settingsIndex.set('start-menu', {
      section: 'personalization',
      key: 'startMenuLayout',
      title: 'Start Menu Layout',
      description: 'Customize your start menu'
    });
    
    this.settingsIndex.set('taskbar', {
      section: 'personalization',
      key: 'taskbarPosition',
      title: 'Taskbar Position',
      description: 'Change the position of the taskbar'
    });
  }

  /**
   * Render the settings app UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="settings-app">
        <div class="settings-header">
          <h1>Settings</h1>
          <div class="settings-search">
            <span class="search-icon">🔍</span>
            <input type="text" class="search-input" placeholder="Find a setting...">
            <button class="clear-search" style="display: none;">×</button>
          </div>
        </div>
        
        <div class="settings-main">
          <div class="settings-sidebar">
            <div class="settings-nav">
              <div class="nav-item ${this.activeSection === 'appearance' ? 'active' : ''}" data-section="appearance">
                <span class="nav-icon">🎨</span>
                <span class="nav-text">Appearance</span>
              </div>
              <div class="nav-item ${this.activeSection === 'display' ? 'active' : ''}" data-section="display">
                <span class="nav-icon">🖥️</span>
                <span class="nav-text">Display</span>
              </div>
              <div class="nav-item ${this.activeSection === 'sound' ? 'active' : ''}" data-section="sound">
                <span class="nav-icon">🔊</span>
                <span class="nav-text">Sound</span>
              </div>
              <div class="nav-item ${this.activeSection === 'privacy' ? 'active' : ''}" data-section="privacy">
                <span class="nav-icon">🔒</span>
                <span class="nav-text">Privacy</span>
              </div>
              <div class="nav-item ${this.activeSection === 'personalization' ? 'active' : ''}" data-section="personalization">
                <span class="nav-icon">👤</span>
                <span class="nav-text">Personalization</span>
              </div>
              <div class="nav-item ${this.activeSection === 'about' ? 'active' : ''}" data-section="about">
                <span class="nav-icon">ℹ️</span>
                <span class="nav-text">About</span>
              </div>
            </div>
          </div>
          
          <div class="settings-content">
            <!-- Appearance Section -->
            <div class="settings-section ${this.activeSection === 'appearance' ? 'active' : ''}" id="appearance-section">
              <h2>Appearance</h2>
              <p class="section-description">Customize the look and feel of your system</p>              <div class="setting-group">
                <h3>Theme</h3>
                <div class="setting-control theme-selector">
                  ${Array.from(this.themeManager.getAllThemes().values()).map((theme: Theme) => `
                    <div class="theme-option" data-theme="${theme.id}">
                      <div class="theme-preview" style="background-color: ${theme.primaryColor}"></div>
                      <div class="theme-name">${theme.name}</div>
                    </div>
                  `).join('')}
                  <div class="theme-option open-theme-editor">
                    <div class="theme-preview" style="background: linear-gradient(135deg, #ff8a00, #e52e71)">+</div>
                    <div class="theme-name">Custom Theme</div>
                  </div>
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Accent Color</h3>
                <div class="setting-control color-selector">
                  ${this.accentColors.map(color => `
                    <div class="color-option" data-color="${color.id}">
                      <div class="color-preview" style="background-color: ${color.color}"></div>
                      <div class="color-name">${color.name}</div>
                    </div>
                  `).join('')}
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Desktop Background</h3>
                <div class="setting-control">
                  <select id="background-type" class="settings-select">
                    ${this.backgroundOptions.map(option => `
                      <option value="${option.id}">${option.name}</option>
                    `).join('')}
                  </select>
                  
                  <div class="background-settings">
                    <div id="solid-color-settings">
                      <input type="color" id="background-color" class="color-picker">
                      <label for="background-color">Select background color</label>
                    </div>
                    
                    <div id="gradient-settings" style="display: none;">
                      <div class="gradient-colors">
                        <div>
                          <input type="color" id="gradient-color-1" class="color-picker">
                          <label for="gradient-color-1">Color 1</label>
                        </div>
                        <div>
                          <input type="color" id="gradient-color-2" class="color-picker">
                          <label for="gradient-color-2">Color 2</label>
                        </div>
                      </div>
                      <select id="gradient-direction" class="settings-select">
                        <option value="to right">Horizontal</option>
                        <option value="to bottom">Vertical</option>
                        <option value="to bottom right">Diagonal</option>
                        <option value="radial">Radial</option>
                      </select>
                    </div>
                    
                    <div id="image-settings" style="display: none;">
                      <div class="image-preview" id="background-image-preview">
                        <div class="no-image">No image selected</div>
                      </div>
                      <button id="select-image" class="settings-button">Select Image</button>
                      <input type="file" id="background-image-file" style="display: none;" accept="image/*">
                      
                      <div class="image-fit-options">
                        <label>Fit:</label>
                        <select id="image-fit" class="settings-select">
                          <option value="cover">Fill screen</option>
                          <option value="contain">Fit to screen</option>
                          <option value="stretch">Stretch</option>
                          <option value="center">Center</option>
                          <option value="tile">Tile</option>
                        </select>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Font Size</h3>
                <div class="setting-control">
                  <div class="slider-control">
                    <input type="range" id="font-size" min="12" max="24" step="1" value="14">
                    <div class="slider-value"><span id="font-size-value">14</span>px</div>
                  </div>
                  <div class="font-preview">
                    <p>The quick brown fox jumps over the lazy dog.</p>
                  </div>
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Animations</h3>
                <div class="setting-control toggle-control">
                  <label class="toggle">
                    <input type="checkbox" id="animations-enabled" checked>
                    <span class="toggle-slider"></span>
                  </label>
                  <div class="toggle-label">Enable animations</div>
                </div>
              </div>
            </div>
            
            <!-- Display Section -->
            <div class="settings-section ${this.activeSection === 'display' ? 'active' : ''}" id="display-section">
              <h2>Display</h2>
              <p class="section-description">Configure your display settings</p>
              
              <div class="setting-group">
                <h3>Resolution</h3>
                <div class="setting-control">
                  <select id="resolution" class="settings-select">
                    <option value="1920x1080">1920 × 1080 (Full HD)</option>
                    <option value="1280x720">1280 × 720 (HD)</option>
                    <option value="2560x1440">2560 × 1440 (QHD)</option>
                    <option value="3840x2160">3840 × 2160 (4K UHD)</option>
                  </select>
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Display Scale</h3>
                <div class="setting-control">
                  <div class="slider-control">
                    <input type="range" id="display-scale" min="75" max="150" step="25" value="100">
                    <div class="slider-value"><span id="display-scale-value">100</span>%</div>
                  </div>
                </div>
              </div>
            </div>
            
            <!-- Sound Section -->
            <div class="settings-section ${this.activeSection === 'sound' ? 'active' : ''}" id="sound-section">
              <h2>Sound</h2>
              <p class="section-description">Configure your sound settings</p>
              
              <div class="setting-group">
                <h3>Master Volume</h3>
                <div class="setting-control">
                  <div class="slider-control">
                    <input type="range" id="master-volume" min="0" max="100" step="1" value="75">
                    <div class="slider-value"><span id="volume-value">75</span>%</div>
                  </div>
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Sound Effects</h3>
                <div class="setting-control toggle-control">
                  <label class="toggle">
                    <input type="checkbox" id="sound-effects-enabled" checked>
                    <span class="toggle-slider"></span>
                  </label>
                  <div class="toggle-label">Enable sound effects</div>
                </div>
              </div>
            </div>
            
            <!-- Privacy Section -->
            <div class="settings-section ${this.activeSection === 'privacy' ? 'active' : ''}" id="privacy-section">
              <h2>Privacy</h2>
              <p class="section-description">Manage your privacy settings</p>
                <div class="setting-group">
                <h3>Activity Tracking</h3>
                <div class="setting-control toggle-control">
                  <label class="toggle">
                    <input type="checkbox" id="activity-tracking">
                    <span class="toggle-slider"></span>
                  </label>
                  <div class="toggle-label">Track activity for personalized suggestions</div>
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Error Reporting</h3>
                <div class="setting-control toggle-control">
                  <label class="toggle">
                    <input type="checkbox" id="error-reporting-enabled" checked>
                    <span class="toggle-slider"></span>
                  </label>
                  <div class="toggle-label">Enable error reporting</div>
                </div>
                <p class="setting-description">Collect and store error information to help diagnose problems</p>
              </div>
              
              <div class="setting-group">
                <h3>Error Telemetry</h3>
                <div class="setting-control toggle-control">
                  <label class="toggle">
                    <input type="checkbox" id="error-telemetry-enabled">
                    <span class="toggle-slider"></span>
                  </label>
                  <div class="toggle-label">Send anonymous error data</div>
                </div>
                <p class="setting-description">Send anonymous error information to help improve the application</p>
              </div>
              
              <div class="setting-group">
                <h3>Error Log</h3>
                <div class="setting-control">
                  <button id="view-error-log" class="settings-button">View Error Log</button>
                  <button id="clear-error-log" class="settings-button">Clear Error Log</button>
                </div>
                <p class="setting-description">View or clear stored error information</p>
              </div>
              
              <div class="setting-group">
                <h3>Clear History</h3>
                <div class="setting-control">
                  <button id="clear-history" class="settings-button">Clear History</button>
                </div>
                <p class="setting-description">Clear your browsing history and command history</p>
              </div>
            </div>
            
            <!-- Personalization Section -->
            <div class="settings-section ${this.activeSection === 'personalization' ? 'active' : ''}" id="personalization-section">
              <h2>Personalization</h2>
              <p class="section-description">Customize your desktop experience</p>
              
              <div class="setting-group">
                <h3>Start Menu Layout</h3>
                <div class="setting-control">
                  <select id="start-menu-layout" class="settings-select">
                    <option value="default">Default</option>
                    <option value="compact">Compact</option>
                    <option value="expanded">Expanded</option>
                  </select>
                </div>
              </div>
              
              <div class="setting-group">
                <h3>Taskbar Position</h3>
                <div class="setting-control">
                  <select id="taskbar-position" class="settings-select">
                    <option value="bottom">Bottom</option>
                    <option value="top">Top</option>
                    <option value="left">Left</option>
                    <option value="right">Right</option>
                  </select>
                </div>
              </div>
            </div>
            
            <!-- About Section -->
            <div class="settings-section ${this.activeSection === 'about' ? 'active' : ''}" id="about-section">
              <h2>About</h2>
              <p class="section-description">System information</p>
              
              <div class="system-info">
                <div class="info-item">
                  <div class="info-label">System</div>
                  <div class="info-value">HackerOS 1.0</div>
                </div>
                <div class="info-item">
                  <div class="info-label">Processor</div>
                  <div class="info-value">Quantum Core i9</div>
                </div>
                <div class="info-item">
                  <div class="info-label">Memory</div>
                  <div class="info-value">16 GB</div>
                </div>
                <div class="info-item">
                  <div class="info-label">Storage</div>
                  <div class="info-value">500 GB</div>
                </div>
                <div class="info-item">
                  <div class="info-label">Version</div>
                  <div class="info-value">1.0.0 (Build 2025.04.21)</div>
                </div>
              </div>
            </div>
            
            <!-- Search Results -->
            <div class="settings-section" id="search-results-section" style="display: none;">
              <h2>Search Results</h2>
              <div id="search-results-container"></div>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  /**
   * Setup event listeners
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // Navigation
    const navItems = this.container.querySelectorAll('.nav-item');
    navItems.forEach(item => {
      item.addEventListener('click', () => {
        const section = item.getAttribute('data-section');
        if (section) {
          this.switchSection(section);
        }
      });
    });
    
    // Search functionality
    const searchInput = this.container.querySelector<HTMLInputElement>('.search-input');
    const clearSearch = this.container.querySelector('.clear-search');
    
    searchInput?.addEventListener('input', () => {
      if (searchInput.value) {
        this.searchQuery = searchInput.value.toLowerCase();
        this.performSearch();
        clearSearch?.removeAttribute('style');
      } else {
        this.searchQuery = '';
        this.switchSection(this.activeSection);
        clearSearch?.setAttribute('style', 'display: none;');
      }
    });
    
    clearSearch?.addEventListener('click', () => {
      if (searchInput) {
        searchInput.value = '';
        this.searchQuery = '';
        this.switchSection(this.activeSection);
        clearSearch.setAttribute('style', 'display: none;');
      }
    });
      // Theme selection
    const themeOptions = this.container.querySelectorAll('.theme-option:not(.open-theme-editor)');
    themeOptions.forEach(option => {
      option.addEventListener('click', () => {
        const theme = option.getAttribute('data-theme');
        if (theme) {
          this.themeManager.applyThemeById(theme).catch(error => {
            console.error('Error applying theme:', error);
          });
          this.updateThemeSelection(theme);
        }
      });
    });
    
    // Open Theme Editor option
    const openThemeEditorOption = this.container.querySelector('.open-theme-editor');
    openThemeEditorOption?.addEventListener('click', () => {
      this.os.getAppManager().launchApp('theme-editor');
    });
    
    // Accent color selection
    const colorOptions = this.container.querySelectorAll('.color-option');
    colorOptions.forEach(option => {
      option.addEventListener('click', () => {
        const color = option.getAttribute('data-color');
        if (color) {
          this.saveSettingValue('accentColor', color);
          this.updateColorSelection(color);
        }
      });
    });
    
    // Background type selection
    const backgroundType = this.container.querySelector<HTMLSelectElement>('#background-type');
    backgroundType?.addEventListener('change', () => {
      this.updateBackgroundTypeSettings(backgroundType.value);
      this.saveSettingValue('backgroundType', backgroundType.value);
    });
    
    // Background color picker
    const backgroundColorPicker = this.container.querySelector<HTMLInputElement>('#background-color');
    backgroundColorPicker?.addEventListener('change', () => {
      this.saveSettingValue('backgroundColor', backgroundColorPicker.value);
    });
    
    // Gradient color pickers
    const gradientColor1 = this.container.querySelector<HTMLInputElement>('#gradient-color-1');
    gradientColor1?.addEventListener('change', () => {
      this.saveSettingValue('gradientColor1', gradientColor1.value);
      this.updateGradientPreview();
    });
    
    const gradientColor2 = this.container.querySelector<HTMLInputElement>('#gradient-color-2');
    gradientColor2?.addEventListener('change', () => {
      this.saveSettingValue('gradientColor2', gradientColor2.value);
      this.updateGradientPreview();
    });
    
    const gradientDirection = this.container.querySelector<HTMLSelectElement>('#gradient-direction');
    gradientDirection?.addEventListener('change', () => {
      this.saveSettingValue('gradientDirection', gradientDirection.value);
      this.updateGradientPreview();
    });
    
    // Background image selection
    const selectImageBtn = this.container.querySelector('#select-image');
    const imageFileInput = this.container.querySelector<HTMLInputElement>('#background-image-file');
    
    selectImageBtn?.addEventListener('click', () => {
      imageFileInput?.click();
    });
    
    imageFileInput?.addEventListener('change', () => {
      if (imageFileInput.files && imageFileInput.files[0]) {
        const reader = new FileReader();
        reader.onload = (e) => {
          const result = e.target?.result as string;
          this.saveSettingValue('backgroundImage', result);
          this.updateBackgroundImagePreview(result);
        };
        reader.readAsDataURL(imageFileInput.files[0]);
      }
    });
    
    const imageFit = this.container.querySelector<HTMLSelectElement>('#image-fit');
    imageFit?.addEventListener('change', () => {
      this.saveSettingValue('backgroundImageFit', imageFit.value);
    });
    
    // Font size slider
    const fontSizeSlider = this.container.querySelector<HTMLInputElement>('#font-size');
    const fontSizeValue = this.container.querySelector('#font-size-value');
    
    fontSizeSlider?.addEventListener('input', () => {
      if (fontSizeValue) {
        fontSizeValue.textContent = fontSizeSlider.value;
      }
      if (!this.container) return;
      const fontPreview = this.container.querySelector('.font-preview');
      if (fontPreview) {
        (<HTMLElement>fontPreview).style.fontSize = `${fontSizeSlider.value}px`;
      }
    });
    
    fontSizeSlider?.addEventListener('change', () => {
      this.saveSettingValue('fontSize', parseInt(fontSizeSlider.value));
    });
    
    // Animations toggle
    const animationsToggle = this.container.querySelector<HTMLInputElement>('#animations-enabled');
    animationsToggle?.addEventListener('change', () => {
      this.saveSettingValue('animationsEnabled', animationsToggle.checked);
    });
    
    // Resolution setting
    const resolutionSelect = this.container.querySelector<HTMLSelectElement>('#resolution');
    resolutionSelect?.addEventListener('change', () => {
      this.saveSettingValue('resolution', resolutionSelect.value);
    });
    
    // Display scale slider
    const scaleSlider = this.container.querySelector<HTMLInputElement>('#display-scale');
    const scaleValue = this.container.querySelector('#display-scale-value');
    
    scaleSlider?.addEventListener('input', () => {
      if (scaleValue) {
        scaleValue.textContent = scaleSlider.value;
      }
    });
    
    scaleSlider?.addEventListener('change', () => {
      this.saveSettingValue('displayScale', parseInt(scaleSlider.value));
    });
    
    // Volume slider
    const volumeSlider = this.container.querySelector<HTMLInputElement>('#master-volume');
    const volumeValue = this.container.querySelector('#volume-value');
    
    volumeSlider?.addEventListener('input', () => {
      if (volumeValue) {
        volumeValue.textContent = volumeSlider.value;
      }
    });
      volumeSlider?.addEventListener('change', () => {
      this.saveSettingValue('masterVolume', parseInt(volumeSlider.value));
    });
    
    // Sound effects toggle
    const soundEffectsToggle = this.container.querySelector<HTMLInputElement>('#sound-effects-enabled');
    soundEffectsToggle?.addEventListener('change', () => {
      this.saveSettingValue('soundEffectsEnabled', soundEffectsToggle.checked);
    });
    
    // Activity tracking toggle
    const activityTrackingToggle = this.container.querySelector<HTMLInputElement>('#activity-tracking');
    activityTrackingToggle?.addEventListener('change', () => {
      this.saveSettingValue('activityTracking', activityTrackingToggle.checked);
    });
    
    // Error reporting toggle
    const errorReportingToggle = this.container.querySelector<HTMLInputElement>('#error-reporting-enabled');
    errorReportingToggle?.addEventListener('change', () => {
      this.saveSettingValue('errorReporting.enabled', errorReportingToggle.checked);
    });
    
    // Error telemetry toggle
    const errorTelemetryToggle = this.container.querySelector<HTMLInputElement>('#error-telemetry-enabled');
    errorTelemetryToggle?.addEventListener('change', () => {
      this.saveSettingValue('errorReporting.telemetry', errorTelemetryToggle.checked);
    });
    
    // View error log button
    const viewErrorLogBtn = this.container.querySelector('#view-error-log');
    viewErrorLogBtn?.addEventListener('click', () => {
      this.openErrorLogViewer();
    });
    
    // Clear error log button
    const clearErrorLogBtn = this.container.querySelector('#clear-error-log');
    clearErrorLogBtn?.addEventListener('click', () => {
      this.clearErrorLog();
    });
    
    // Clear history button
    const clearHistoryBtn = this.container.querySelector('#clear-history');
    clearHistoryBtn?.addEventListener('click', () => {
      this.clearHistory();
    });
    
    // Start menu layout
    const startMenuLayout = this.container.querySelector<HTMLSelectElement>('#start-menu-layout');
    startMenuLayout?.addEventListener('change', () => {
      this.saveSettingValue('startMenuLayout', startMenuLayout.value);
    });
    
    // Taskbar position
    const taskbarPosition = this.container.querySelector<HTMLSelectElement>('#taskbar-position');
    taskbarPosition?.addEventListener('change', () => {
      this.saveSettingValue('taskbarPosition', taskbarPosition.value);
    });
  }

  /**
   * Switch to a different settings section
   */
  private switchSection(section: string): void {
    if (!this.container) return;
    
    // Update active section
    this.activeSection = section;
    
    // Update navigation items
    const navItems = this.container.querySelectorAll('.nav-item');
    navItems.forEach(item => {
      const itemSection = item.getAttribute('data-section');
      item.classList.toggle('active', itemSection === section);
    });
    
    // Update visible section
    const sections = this.container.querySelectorAll('.settings-section');
    sections.forEach(sectionEl => {
      sectionEl.classList.remove('active');
    });
    
    // Show search results if we have a search query
    if (this.searchQuery) {
      const searchResultsSection = this.container.querySelector('#search-results-section');
      if (searchResultsSection) {
        searchResultsSection.setAttribute('style', 'display: block;');
      }
    } else {
      const activeSection = this.container.querySelector(`#${section}-section`);
      if (activeSection) {
        activeSection.classList.add('active');
      }
    }
  }

  /**
   * Perform a search across settings
   */
  private performSearch(): void {
    if (!this.container) return;
    
    const searchResultsContainer = this.container.querySelector('#search-results-container');
    if (!searchResultsContainer) return;
    
    // Show search results section
    const searchResultsSection = this.container.querySelector('#search-results-section');
    if (searchResultsSection) {
      searchResultsSection.setAttribute('style', 'display: block;');
    }
    
    // Hide all other sections
    const sections = this.container.querySelectorAll('.settings-section:not(#search-results-section)');
    sections.forEach(section => {
      section.classList.remove('active');
    });
    
    // Find matching settings
    const results: Array<{ id: string, section: string, title: string, description: string }> = [];
    
    this.settingsIndex.forEach((setting, id) => {
      const matches = 
        setting.title.toLowerCase().includes(this.searchQuery) ||
        setting.description.toLowerCase().includes(this.searchQuery) ||
        id.toLowerCase().includes(this.searchQuery);
      
      if (matches) {
        results.push({
          id,
          section: setting.section,
          title: setting.title,
          description: setting.description
        });
      }
    });
    
    // Render results
    if (results.length > 0) {
      searchResultsContainer.innerHTML = results.map(result => `
        <div class="search-result" data-section="${result.section}" data-setting="${result.id}">
          <div class="result-title">${result.title}</div>
          <div class="result-description">${result.description}</div>
          <div class="result-section">Section: ${this.capitalizeFirstLetter(result.section)}</div>
        </div>
      `).join('');
      
      // Add click handlers to results
      const resultElements = searchResultsContainer.querySelectorAll('.search-result');
      resultElements.forEach(element => {
        element.addEventListener('click', () => {
          const section = element.getAttribute('data-section');
          if (section) {
            // Clear search
            const searchInput = this.container?.querySelector<HTMLInputElement>('.search-input');
            if (searchInput) {
              searchInput.value = '';
            }
            
            const clearSearch = this.container?.querySelector('.clear-search');
            if (clearSearch) {
              clearSearch.setAttribute('style', 'display: none;');
            }
            
            this.searchQuery = '';
            this.switchSection(section);
            
            // Scroll to the specific setting
            const settingId = element.getAttribute('data-setting');
            setTimeout(() => {
              const settingElement = this.container?.querySelector(`[data-setting="${settingId}"]`);
              settingElement?.scrollIntoView({ behavior: 'smooth' });
            }, 100);
          }
        });
      });
    } else {
      searchResultsContainer.innerHTML = `
        <div class="no-results">
          <p>No settings match your search.</p>
        </div>
      `;
    }
  }

  /**
   * Load current user settings
   */
  private async loadCurrentSettings(): Promise<void> {
    if (!this.container) return;
    
    // Theme
    const theme = await this.loadSettingValue('theme', 'dark');
    this.updateThemeSelection(theme);
    
    // Accent color
    const accentColor = await this.loadSettingValue('accentColor', 'blue');
    this.updateColorSelection(accentColor);
    
    // Background type
    const backgroundType = await this.loadSettingValue('backgroundType', 'solid');
    const backgroundTypeSelect = this.container.querySelector<HTMLSelectElement>('#background-type');
    if (backgroundTypeSelect) {
      backgroundTypeSelect.value = backgroundType;
    }
    this.updateBackgroundTypeSettings(backgroundType);
    
    // Background color
    const backgroundColor = await this.loadSettingValue('backgroundColor', '#1e1e1e');
    const backgroundColorPicker = this.container.querySelector<HTMLInputElement>('#background-color');
    if (backgroundColorPicker) {
      backgroundColorPicker.value = backgroundColor;
    }
    
    // Gradient colors
    const gradientColor1 = await this.loadSettingValue('gradientColor1', '#1a2a3a');
    const gradientColor1Picker = this.container.querySelector<HTMLInputElement>('#gradient-color-1');
    if (gradientColor1Picker) {
      gradientColor1Picker.value = gradientColor1;
    }
    
    const gradientColor2 = await this.loadSettingValue('gradientColor2', '#3a2a1a');
    const gradientColor2Picker = this.container.querySelector<HTMLInputElement>('#gradient-color-2');
    if (gradientColor2Picker) {
      gradientColor2Picker.value = gradientColor2;
    }
    
    const gradientDirection = await this.loadSettingValue('gradientDirection', 'to right');
    const gradientDirectionSelect = this.container.querySelector<HTMLSelectElement>('#gradient-direction');
    if (gradientDirectionSelect) {
      gradientDirectionSelect.value = gradientDirection;
    }
    
    // Background image
    const backgroundImage = await this.loadSettingValue('backgroundImage', '');
    if (backgroundImage) {
      this.updateBackgroundImagePreview(backgroundImage);
    }
    
    const backgroundImageFit = await this.loadSettingValue('backgroundImageFit', 'cover');
    const imageFitSelect = this.container.querySelector<HTMLSelectElement>('#image-fit');
    if (imageFitSelect) {
      imageFitSelect.value = backgroundImageFit;
    }
    
    // Font size
    const fontSize = await this.loadSettingValue('fontSize', 14);
    const fontSizeSlider = this.container.querySelector<HTMLInputElement>('#font-size');
    const fontSizeValue = this.container.querySelector('#font-size-value');
    if (fontSizeSlider) {
      fontSizeSlider.value = fontSize.toString();
    }
    if (fontSizeValue) {
      fontSizeValue.textContent = fontSize.toString();
    }
    
    const fontPreview = this.container.querySelector('.font-preview');
    if (fontPreview) {
      (<HTMLElement>fontPreview).style.fontSize = `${fontSize}px`;
    }
    
    // Animations enabled
    const animationsEnabled = await this.loadSettingValue('animationsEnabled', true);
    const animationsToggle = this.container.querySelector<HTMLInputElement>('#animations-enabled');
    if (animationsToggle) {
      animationsToggle.checked = animationsEnabled;
    }
    
    // Display settings
    const resolution = await this.loadSettingValue('resolution', '1920x1080');
    const resolutionSelect = this.container.querySelector<HTMLSelectElement>('#resolution');
    if (resolutionSelect) {
      resolutionSelect.value = resolution;
    }
    
    const displayScale = await this.loadSettingValue('displayScale', 100);
    const scaleSlider = this.container.querySelector<HTMLInputElement>('#display-scale');
    const scaleValue = this.container.querySelector('#display-scale-value');
    if (scaleSlider) {
      scaleSlider.value = displayScale.toString();
    }
    if (scaleValue) {
      scaleValue.textContent = displayScale.toString();
    }
    
    // Sound settings
    const masterVolume = await this.loadSettingValue('masterVolume', 75);
    const volumeSlider = this.container.querySelector<HTMLInputElement>('#master-volume');
    const volumeValue = this.container.querySelector('#volume-value');
    if (volumeSlider) {
      volumeSlider.value = masterVolume.toString();
    }
    if (volumeValue) {
      volumeValue.textContent = masterVolume.toString();
    }
    
    const soundEffectsEnabled = await this.loadSettingValue('soundEffectsEnabled', true);
    const soundEffectsToggle = this.container.querySelector<HTMLInputElement>('#sound-effects-enabled');
    if (soundEffectsToggle) {
      soundEffectsToggle.checked = soundEffectsEnabled;
    }
    
    // Privacy settings
    const activityTracking = await this.loadSettingValue('activityTracking', false);
    const activityTrackingToggle = this.container.querySelector<HTMLInputElement>('#activity-tracking');
    if (activityTrackingToggle) {
      activityTrackingToggle.checked = activityTracking;
    }
    
    // Personalization settings
    const startMenuLayout = await this.loadSettingValue('startMenuLayout', 'default');
    const startMenuLayoutSelect = this.container.querySelector<HTMLSelectElement>('#start-menu-layout');
    if (startMenuLayoutSelect) {
      startMenuLayoutSelect.value = startMenuLayout;
    }
    
    const taskbarPosition = await this.loadSettingValue('taskbarPosition', 'bottom');
    const taskbarPositionSelect = this.container.querySelector<HTMLSelectElement>('#taskbar-position');
    if (taskbarPositionSelect) {
      taskbarPositionSelect.value = taskbarPosition;
    }
  }

  /**
   * Update theme selection UI
   */
  private updateThemeSelection(theme: string): void {
    if (!this.container) return;
    
    const themeOptions = this.container.querySelectorAll('.theme-option');
    themeOptions.forEach(option => {
      const optionTheme = option.getAttribute('data-theme');
      option.classList.toggle('selected', optionTheme === theme);
    });
  }

  /**
   * Update accent color selection UI
   */
  private updateColorSelection(color: string): void {
    if (!this.container) return;
    
    const colorOptions = this.container.querySelectorAll('.color-option');
    colorOptions.forEach(option => {
      const optionColor = option.getAttribute('data-color');
      option.classList.toggle('selected', optionColor === color);
    });
  }

  /**
   * Update background type settings UI
   */
  private updateBackgroundTypeSettings(type: string): void {
    if (!this.container) return;
    
    // Hide all settings
    const solidSettings = this.container.querySelector('#solid-color-settings');
    const gradientSettings = this.container.querySelector('#gradient-settings');
    const imageSettings = this.container.querySelector('#image-settings');
    
    if (solidSettings) {
      solidSettings.setAttribute('style', 'display: none;');
    }
    if (gradientSettings) {
      gradientSettings.setAttribute('style', 'display: none;');
    }
    if (imageSettings) {
      imageSettings.setAttribute('style', 'display: none;');
    }
    
    // Show relevant settings
    switch (type) {
      case 'solid':
        if (solidSettings) {
          solidSettings.removeAttribute('style');
        }
        break;
      case 'gradient':
        if (gradientSettings) {
          gradientSettings.removeAttribute('style');
        }
        this.updateGradientPreview();
        break;
      case 'image':
        if (imageSettings) {
          imageSettings.removeAttribute('style');
        }
        break;
    }
  }

  /**
   * Update gradient preview
   */
  private updateGradientPreview(): void {
    // This would update the desktop background in a real system
    // For now, just log that it would change
    console.log('Gradient background updated');
  }

  /**
   * Update background image preview
   */
  private updateBackgroundImagePreview(imageData: string): void {
    if (!this.container) return;
    
    const preview = this.container.querySelector('#background-image-preview');
    if (preview) {
      preview.innerHTML = `<img src="${imageData}" alt="Background Preview">`;
    }
  }

  /**
   * Clear history
   */
  private clearHistory(): void {
    // Show confirmation dialog
    if (confirm('Are you sure you want to clear all browsing and command history?')) {
      // Actually clear history
      this.saveSettingValue('clearHistoryTimestamp', Date.now());
      alert('History cleared successfully.');
    }
  }

  /**
   * Save setting value to user settings
   */
  private async saveSettingValue(key: string, value: any): Promise<void> {
    await this.userSettings.set('appearance', key, value);
    
    // Emit event so other parts of the system can react to setting changes
    const event = new CustomEvent('settings-changed', {
      detail: {
        key,
        value
      }
    });
    document.dispatchEvent(event);
  }

  /**
   * Load setting value from user settings
   */
  private async loadSettingValue<T>(key: string, defaultValue: T): Promise<T> {
    try {
      const value = await this.userSettings.get('appearance', key, defaultValue);
      return value;
    } catch (error) {
      console.error(`Error loading setting ${key}:`, error);
      return defaultValue;
    }
  }

  /**
   * Capitalize first letter of a string
   */
  private capitalizeFirstLetter(string: string): string {
    return string.charAt(0).toUpperCase() + string.slice(1);
  }
  /**
   * Open the Error Log Viewer application
   */
  private openErrorLogViewer(): void {
    // Launch the error log viewer app
    import('../apps/error-log-viewer').then(module => {
      this.os.getAppManager().launchApp('error-log-viewer');
    }).catch(error => {
      console.error('Failed to load Error Log Viewer:', error);
      this.showErrorDialog('Error', 'Failed to open Error Log Viewer');
    });
  }

  /**
   * Clear the error log
   */
  private clearErrorLog(): void {
    this.showConfirmDialog(
      'Clear Error Log',
      'Are you sure you want to clear all error logs? This action cannot be undone.',
      () => {
        const errorHandler = this.ErrorHandler;
        errorHandler.clearErrorLog();
        this.showInfoDialog('Success', 'Error log has been cleared');
      }
    );
  }

  /**
   * Show confirmation dialog
   */
  private showConfirmDialog(title: string, message: string, onConfirm: () => void): void {
    this.dialogManager.Msgbox.Show(title, message, ['yes', 'no'])
      .then(result => {
        if (result === 'yes') {
          onConfirm();
        }
      });
  }

  /**
   * Show error dialog
   */
  private showErrorDialog(title: string, message: string): void {
    this.dialogManager.Msgbox.Show(title, message, ['ok'], 'error');
  }
  /**
   * Show info dialog
   */
  private showInfoDialog(title: string, message: string): void {
    this.dialogManager.Msgbox.Show(title, message, ['ok']);
  }
}
