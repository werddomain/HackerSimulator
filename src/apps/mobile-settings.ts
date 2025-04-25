// filepath: c:\Users\clefw\source\repos\HackerSimulator\src\apps\mobile-settings.ts
import { OS } from '../core/os';
import { GuiApplication } from '../core/gui-application';
import { BaseSettings } from '../core/BaseSettings';
import { UserSettings } from '../core/UserSettings';
import { ErrorHandler } from '../core/error-handler';
import { ThemeManager } from '../core/ThemeManager';
import { Theme } from '../core/theme';
import { AppManager } from '../core/app-manager';
import { PlatformDetector } from '../core/platform-detector';

/**
 * Mobile Settings App for the Hacker Game
 * Provides a touch-friendly interface to customize user preferences and system appearance
 */
export class MobileSettingsApp extends GuiApplication {
  private userSettings: UserSettings;
  private themeManager: ThemeManager;
  private activeSection: string = 'appearance';
  private searchQuery: string = '';
  private settingsIndex: Map<string, { section: string, key: string, title: string, description: string, icon?: string }> = new Map();
  private isSearchActive: boolean = false;
  private isKeyboardVisible: boolean = false;
  
  // Track touch gestures
  private touchStartX: number = 0;
  private touchStartY: number = 0;
  
  // Sections with icons for mobile navigation
  private sections = [
    { id: 'appearance', name: 'Appearance', icon: 'fa-palette' },
    { id: 'display', name: 'Display', icon: 'fa-desktop' },
    { id: 'system', name: 'System', icon: 'fa-cog' },
    { id: 'personalization', name: 'Personalization', icon: 'fa-user' },
    { id: 'accessibility', name: 'Accessibility', icon: 'fa-universal-access' },
    { id: 'privacy', name: 'Privacy', icon: 'fa-shield-alt' },
    { id: 'about', name: 'About', icon: 'fa-info-circle' }
  ];
  
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
   * Build the search index for settings with icons for mobile
   */
  private buildSettingsIndex(): void {
    // Appearance settings
    this.settingsIndex.set('theme', {
      section: 'appearance',
      key: 'theme',
      title: 'Theme',
      description: 'Change the overall color theme of the system',
      icon: 'fa-adjust'
    });
    
    this.settingsIndex.set('accent-color', {
      section: 'appearance',
      key: 'accentColor',
      title: 'Accent Color',
      description: 'Set the highlight color for buttons and interactive elements',
      icon: 'fa-paint-brush'
    });
    
    this.settingsIndex.set('background', {
      section: 'appearance',
      key: 'background',
      title: 'Desktop Background',
      description: 'Change your desktop wallpaper or background color',
      icon: 'fa-image'
    });
    
    this.settingsIndex.set('font-size', {
      section: 'appearance',
      key: 'fontSize',
      title: 'Font Size',
      description: 'Adjust the size of text throughout the system',
      icon: 'fa-text-height'
    });
    
    this.settingsIndex.set('animation', {
      section: 'appearance',
      key: 'animationsEnabled',
      title: 'Animations',
      description: 'Toggle system animations on or off',
      icon: 'fa-film'
    });
    
    // Display settings
    this.settingsIndex.set('brightness', {
      section: 'display',
      key: 'brightness',
      title: 'Brightness',
      description: 'Adjust screen brightness',
      icon: 'fa-sun'
    });
    
    this.settingsIndex.set('contrast', {
      section: 'display',
      key: 'contrast',
      title: 'Contrast',
      description: 'Adjust screen contrast',
      icon: 'fa-adjust'
    });
    
    this.settingsIndex.set('scaling', {
      section: 'display',
      key: 'scaling',
      title: 'Display Scaling',
      description: 'Change the size of text, apps, and other items',
      icon: 'fa-search-plus'
    });
    
    // System settings
    this.settingsIndex.set('performance', {
      section: 'system',
      key: 'performance',
      title: 'Performance Mode',
      description: 'Optimize for performance or appearance',
      icon: 'fa-tachometer-alt'
    });
    
    this.settingsIndex.set('platform', {
      section: 'system',
      key: 'platform',
      title: 'Platform Mode',
      description: 'Switch between mobile and desktop interface',
      icon: 'fa-mobile-alt'
    });
    
    this.settingsIndex.set('storage', {
      section: 'system',
      key: 'storage',
      title: 'Storage',
      description: 'Manage disk space and view storage usage',
      icon: 'fa-hdd'
    });
    
    // Personalization settings
    this.settingsIndex.set('username', {
      section: 'personalization',
      key: 'username',
      title: 'Username',
      description: 'Change your username',
      icon: 'fa-user'
    });
    
    this.settingsIndex.set('avatar', {
      section: 'personalization',
      key: 'avatar',
      title: 'Avatar',
      description: 'Change your profile picture',
      icon: 'fa-user-circle'
    });
    
    // Accessibility settings
    this.settingsIndex.set('high-contrast', {
      section: 'accessibility',
      key: 'highContrast',
      title: 'High Contrast',
      description: 'Use high contrast colors for better visibility',
      icon: 'fa-low-vision'
    });
    
    this.settingsIndex.set('screen-reader', {
      section: 'accessibility',
      key: 'screenReader',
      title: 'Screen Reader',
      description: 'Enable or disable screen reader functionality',
      icon: 'fa-audio-description'
    });
    
    this.settingsIndex.set('motion', {
      section: 'accessibility',
      key: 'reducedMotion',
      title: 'Reduced Motion',
      description: 'Minimize animations for users sensitive to motion',
      icon: 'fa-running'
    });
    
    // Privacy settings
    this.settingsIndex.set('tracking', {
      section: 'privacy',
      key: 'tracking',
      title: 'Activity Tracking',
      description: 'Control which activities are tracked by the system',
      icon: 'fa-user-secret'
    });
    
    this.settingsIndex.set('data-collection', {
      section: 'privacy',
      key: 'dataCollection',
      title: 'Data Collection',
      description: 'Manage data sent to improve the system',
      icon: 'fa-chart-bar'
    });
    
    // About settings
    this.settingsIndex.set('version', {
      section: 'about',
      key: 'version',
      title: 'System Version',
      description: 'View system version information',
      icon: 'fa-info'
    });
    
    this.settingsIndex.set('license', {
      section: 'about',
      key: 'license',
      title: 'License',
      description: 'View license information',
      icon: 'fa-file-contract'
    });
  }
  
  /**
   * Render the mobile settings UI
   */
  private render(): void {
    if (!this.container) return;

    this.container.innerHTML = `
      <div class="mobile-settings-app">
        <div class="mobile-settings-header">
          <h2>Settings</h2>
          <div class="mobile-settings-actions">
            <button class="mobile-search-btn" aria-label="Search Settings">
              <i class="fa fa-search"></i>
            </button>
          </div>
        </div>
        
        <div class="mobile-settings-search ${this.isSearchActive ? 'active' : ''}">
          <div class="search-input-container">
            <i class="fa fa-search search-icon"></i>
            <input type="text" class="mobile-search-input" placeholder="Search settings..." value="${this.searchQuery}">
            <button class="clear-search-btn ${this.searchQuery ? 'visible' : ''}" aria-label="Clear Search">
              <i class="fa fa-times"></i>
            </button>
          </div>
          <div class="search-results" id="mobile-search-results"></div>
        </div>
        
        <div class="mobile-settings-navigation">
          <div class="mobile-section-tabs">
            ${this.sections.map(section => `
              <button class="section-tab ${this.activeSection === section.id ? 'active' : ''}" data-section="${section.id}">
                <i class="fa ${section.icon}"></i>
                <span>${section.name}</span>
              </button>
            `).join('')}
          </div>
        </div>
        
        <div class="mobile-settings-content" id="mobile-settings-content">
          ${this.renderSettingsSection(this.activeSection)}
        </div>
      </div>
    `;
  }
  
  /**
   * Render a specific settings section
   */
  private renderSettingsSection(sectionId: string): string {
    // Get all settings for this section
    const sectionSettings = Array.from(this.settingsIndex.values())
      .filter(setting => setting.section === sectionId);
    
    // Group settings by type
    const groups = new Map<string, Array<any>>();
    
    sectionSettings.forEach(setting => {
      if (!groups.has(setting.key.split('-')[0])) {
        groups.set(setting.key.split('-')[0], []);
      }
      groups.get(setting.key.split('-')[0])?.push(setting);
    });
    
    // If section is empty
    if (sectionSettings.length === 0) {
      return `<div class="mobile-settings-empty">No settings found in this section.</div>`;
    }
    
    let html = `<div class="mobile-settings-section" id="section-${sectionId}">`;
    
    // Section header with title
    html += `
      <div class="mobile-section-header">
        <h3>${this.sections.find(s => s.id === sectionId)?.name || 'Settings'}</h3>
      </div>
    `;
    
    // For each group, create a collapsible card
    groups.forEach((settings, groupName) => {
      html += `
        <div class="mobile-settings-group">
          <div class="mobile-group-header" data-group="${groupName}">
            <h4>${this.capitalizeFirstLetter(groupName)}</h4>
            <i class="fa fa-chevron-down"></i>
          </div>
          <div class="mobile-group-content">
      `;
      
      // Add settings to the group
      settings.forEach(setting => {
        html += this.renderSettingControl(setting);
      });
      
      html += `
          </div>
        </div>
      `;
    });
    
    html += `</div>`;
    return html;
  }
  
  /**
   * Render the appropriate control for a setting
   */
  private renderSettingControl(setting: any): string {
    let controlHtml = '';
    
    // Icon for the setting
    const iconHtml = `<i class="fa ${setting.icon || 'fa-cog'} setting-icon"></i>`;
    
    switch(setting.key) {
      case 'theme':
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control">
              <select class="mobile-select" id="setting-theme">
                ${this.getThemeOptions()}
              </select>
            </div>
          </div>
        `;
        break;
        
      case 'accentColor':
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control accent-color-picker">
              ${this.accentColors.map(color => `                <button class="color-option ${this.userSettings.get('user-preferences', 'accentColor', '') === color.id ? 'active' : ''}" 
                  data-color="${color.id}" 
                  style="background-color: ${color.color};"
                  aria-label="${color.name}">
                </button>
              `).join('')}
            </div>
          </div>
        `;
        break;
        
      case 'background':
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control">
              <select class="mobile-select" id="setting-background">
                ${this.backgroundOptions.map(option => `                  <option value="${option.id}" ${this.userSettings.get('user-preferences', 'background', '') === option.id ? 'selected' : ''}>
                    ${option.name}
                  </option>
                `).join('')}
              </select>
            </div>
          </div>
        `;
        break;
        
      case 'fontSize':
      case 'brightness':
      case 'contrast':
      case 'scaling':        // Slider control for numeric values
        const value = this.userSettings.get('user-preferences', setting.key, 100);
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control slider-control">
              <input type="range" class="mobile-slider" id="setting-${setting.key}" 
                min="50" max="150" value="${value}" 
                data-setting="${setting.key}">
              <div class="slider-value">${value}%</div>
            </div>
          </div>
        `;
        break;
        
      case 'animationsEnabled':
      case 'highContrast':
      case 'screenReader':
      case 'reducedMotion':
      case 'tracking':
      case 'dataCollection':        // Toggle switch for boolean values
        const checked = this.userSettings.get('user-preferences', setting.key, false) ? 'checked' : '';
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control">
              <label class="mobile-toggle-switch">
                <input type="checkbox" class="toggle-input" id="setting-${setting.key}" ${checked}>
                <span class="toggle-slider"></span>
              </label>
            </div>
          </div>
        `;
        break;
        
      case 'performance':        // Radio buttons/segmented control for performance mode
        const performanceMode = this.userSettings.get('user-preferences', 'performance', 'balanced');
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control segmented-control">
              <div class="segment-option ${performanceMode === 'performance' ? 'active' : ''}" data-value="performance">Fast</div>
              <div class="segment-option ${performanceMode === 'balanced' ? 'active' : ''}" data-value="balanced">Balanced</div>
              <div class="segment-option ${performanceMode === 'appearance' ? 'active' : ''}" data-value="appearance">Pretty</div>
            </div>
          </div>
        `;
        break;
        
      case 'platform':
        // Platform mode toggle (mobile/desktop)
        const platformMode = PlatformDetector.getInstance().getCurrentMode();
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control platform-toggle">
              <button class="platform-btn ${platformMode === 'desktop' ? 'active' : ''}" data-platform="desktop">
                <i class="fa fa-desktop"></i>
                <span>Desktop</span>
              </button>
              <button class="platform-btn ${platformMode === 'mobile' ? 'active' : ''}" data-platform="mobile">
                <i class="fa fa-mobile-alt"></i>
                <span>Mobile</span>
              </button>
            </div>
          </div>
        `;
        break;
        
      case 'username':
      case 'avatar':        // Text input
        const textValue = this.userSettings.get('user-preferences', setting.key, '');
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control">
              <input type="text" class="mobile-text-input" id="setting-${setting.key}" value="${textValue}">
            </div>
          </div>
        `;
        break;
        
      case 'version':
      case 'license':
        // Display-only setting
        const displayValue = setting.key === 'version' ? 'HackerSimulator 2.0' : 'MIT License';
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control">
              <div class="setting-display-value">${displayValue}</div>
            </div>
          </div>
        `;
        break;
        
      case 'storage':
        // Storage usage visualization
        const usedStorage = 75; // Example value
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control">
              <div class="storage-usage">
                <div class="storage-bar">
                  <div class="storage-used" style="width: ${usedStorage}%"></div>
                </div>
                <div class="storage-text">${usedStorage}% used</div>
              </div>
            </div>
          </div>
        `;
        break;
        
      default:
        // Generic control
        controlHtml = `
          <div class="mobile-setting-item" data-setting="${setting.key}">
            ${iconHtml}
            <div class="setting-info">
              <div class="setting-title">${setting.title}</div>
              <div class="setting-description">${setting.description}</div>
            </div>
            <div class="setting-control">
              <button class="mobile-setting-btn">Configure</button>
            </div>
          </div>
        `;
    }
    
    return controlHtml;
  }
  
  /**
   * Helper method to get theme options
   */  private getThemeOptions(): string {
    const themesMap = this.themeManager.getAllThemes();
    // Use synchronous get with default for rendering
    const currentTheme = this.userSettings.get('user-preferences', 'themeName', 'default');
    
    // Convert Map to Array for mapping
    const themes = Array.from(themesMap.values());
    return themes.map((theme: Theme) => `
      <option value="${theme.id}" ${currentTheme === theme.id ? 'selected' : ''}>
        ${theme.name}
      </option>
    `).join('');
  }
  
  /**
   * Set up event listeners for UI interaction
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // Section tab navigation
    const sectionTabs = this.container.querySelectorAll('.section-tab');
    sectionTabs.forEach(tab => {
      tab.addEventListener('click', (e) => {
        const sectionId = (e.currentTarget as HTMLElement).getAttribute('data-section');
        if (sectionId) {
          this.setActiveSection(sectionId);
        }
      });
    });
    
    // Search toggle
    const searchBtn = this.container.querySelector('.mobile-search-btn');
    if (searchBtn) {
      searchBtn.addEventListener('click', () => {
        this.toggleSearch();
      });
    }
    
    // Search input
    const searchInput = this.container.querySelector('.mobile-search-input');
    if (searchInput) {      searchInput.addEventListener('input', (e) => {
        this.searchQuery = (e.target as HTMLInputElement).value;
        this.updateSearchResults();
        
        // Show/hide clear button
        const clearBtn = this.container?.querySelector('.clear-search-btn');
        if (clearBtn) {
          if (this.searchQuery) {
            clearBtn.classList.add('visible');
          } else {
            clearBtn.classList.remove('visible');
          }
        }
      });
      
      // Focus event to show virtual keyboard
      searchInput.addEventListener('focus', () => {
        this.isKeyboardVisible = true;
        // In a real implementation, this would trigger the virtual keyboard
      });
    }
    
    // Clear search button
    const clearSearchBtn = this.container.querySelector('.clear-search-btn');
    if (clearSearchBtn) {      clearSearchBtn.addEventListener('click', () => {
        this.searchQuery = '';
        const searchInput = this.container?.querySelector('.mobile-search-input') as HTMLInputElement | null;
        if (searchInput) {
          searchInput.value = '';
          searchInput.focus();
        }
        this.updateSearchResults();
        clearSearchBtn.classList.remove('visible');
      });
    }
    
    // Group headers (collapsible)
    const groupHeaders = this.container.querySelectorAll('.mobile-group-header');
    groupHeaders.forEach(header => {
      header.addEventListener('click', (e) => {
        const header = e.currentTarget as HTMLElement;
        const content = header.nextElementSibling as HTMLElement;
        header.classList.toggle('collapsed');
        
        if (content) {
          if (header.classList.contains('collapsed')) {
            content.style.maxHeight = '0';
          } else {
            content.style.maxHeight = content.scrollHeight + 'px';
          }
        }
      });
    });
    
    // Setup swipe navigation for sections
    this.setupSwipeNavigation();
    
    // Setup individual setting controls
    this.setupSettingControls();
  }
  
  /**
   * Setup touch swipe navigation between setting sections
   */
  private setupSwipeNavigation(): void {
    if (!this.container) return;
    
    const contentArea = this.container.querySelector('.mobile-settings-content');
    if (!contentArea) return;    contentArea.addEventListener('touchstart', (e) => {
      const touchEvent = e as unknown as TouchEvent;
      this.touchStartX = touchEvent.touches[0].clientX;
      this.touchStartY = touchEvent.touches[0].clientY;
    });
    
    contentArea.addEventListener('touchmove', (e) => {
      const touchEvent = e as unknown as TouchEvent;
      const xDiff = this.touchStartX - touchEvent.touches[0].clientX;
      const yDiff = Math.abs(this.touchStartY - touchEvent.touches[0].clientY);
      
      // Only handle horizontal swipes (if vertical movement is less significant)
      if (Math.abs(xDiff) > 50 && Math.abs(xDiff) > yDiff) {
        e.preventDefault();
      }
    });
    
    contentArea.addEventListener('touchend', (e) => {
      const touchEvent = e as unknown as TouchEvent;
      const touchEndX = touchEvent.changedTouches[0].clientX;
      const xDiff = this.touchStartX - touchEndX;
      const yDiff = Math.abs(this.touchStartY - touchEvent.changedTouches[0].clientY);
      
      // Only trigger if horizontal swipe is dominant motion
      if (Math.abs(xDiff) > 100 && Math.abs(xDiff) > yDiff) {
        const sections = this.sections.map(s => s.id);
        const currentIndex = sections.indexOf(this.activeSection);
        
        if (xDiff > 0 && currentIndex < sections.length - 1) {
          // Swipe left - go to next section
          this.setActiveSection(sections[currentIndex + 1]);
        } else if (xDiff < 0 && currentIndex > 0) {
          // Swipe right - go to previous section
          this.setActiveSection(sections[currentIndex - 1]);
        }
      }
    });
  }
  
  /**
   * Setup individual setting control event handlers
   */
  private setupSettingControls(): void {
    if (!this.container) return;
    
    // Toggle switches
    const toggleInputs = this.container.querySelectorAll('.toggle-input');
    toggleInputs.forEach(input => {
      input.addEventListener('change', (e) => {
        const settingId = (e.target as HTMLElement).id.replace('setting-', '');
        const isChecked = (e.target as HTMLInputElement).checked;
        this.updateSetting(settingId, isChecked);
      });
    });
    
    // Sliders
    const sliders = this.container.querySelectorAll('.mobile-slider');
    sliders.forEach(slider => {
      slider.addEventListener('input', (e) => {
        const settingId = (e.target as HTMLElement).getAttribute('data-setting') || '';
        const value = parseInt((e.target as HTMLInputElement).value);
        
        // Update slider value display
        const valueDisplay = (e.target as HTMLElement).parentElement?.querySelector('.slider-value');
        if (valueDisplay) {
          valueDisplay.textContent = `${value}%`;
        }
        
        this.updateSetting(settingId, value);
      });
    });
    
    // Select dropdowns
    const selects = this.container.querySelectorAll('.mobile-select');
    selects.forEach(select => {
      select.addEventListener('change', (e) => {
        const settingId = (e.target as HTMLElement).id.replace('setting-', '');
        const value = (e.target as HTMLSelectElement).value;
        this.updateSetting(settingId, value);
      });
    });
    
    // Text inputs
    const textInputs = this.container.querySelectorAll('.mobile-text-input');
    textInputs.forEach(input => {
      input.addEventListener('change', (e) => {
        const settingId = (e.target as HTMLElement).id.replace('setting-', '');
        const value = (e.target as HTMLInputElement).value;
        this.updateSetting(settingId, value);
      });
    });
    
    // Color picker options
    const colorOptions = this.container.querySelectorAll('.color-option');
    colorOptions.forEach(option => {
      option.addEventListener('click', (e) => {
        const color = (e.target as HTMLElement).getAttribute('data-color');
        if (color) {
          // Remove active class from all options
          colorOptions.forEach(opt => opt.classList.remove('active'));
          // Add active class to selected option
          (e.target as HTMLElement).classList.add('active');
          this.updateSetting('accentColor', color);
        }
      });
    });
    
    // Segmented control
    const segmentOptions = this.container.querySelectorAll('.segment-option');
    segmentOptions.forEach(option => {
      option.addEventListener('click', (e) => {
        const value = (e.target as HTMLElement).getAttribute('data-value');
        if (value) {
          // Remove active class from all options
          segmentOptions.forEach(opt => opt.classList.remove('active'));
          // Add active class to selected option
          (e.target as HTMLElement).classList.add('active');
          this.updateSetting('performance', value);
        }
      });
    });
    
    // Platform toggle
    const platformBtns = this.container.querySelectorAll('.platform-btn');
    platformBtns.forEach(btn => {
      btn.addEventListener('click', (e) => {
        const platform = (e.currentTarget as HTMLElement).getAttribute('data-platform');
        if (platform) {
          // Remove active class from all buttons
          platformBtns.forEach(b => b.classList.remove('active'));
          // Add active class to selected button
          (e.currentTarget as HTMLElement).classList.add('active');
          
          // Switch platform
          PlatformDetector.getInstance().setMode(platform as 'mobile' | 'desktop');
          
          // Show confirmation dialog
          this.showConfirmationDialog('Platform mode changed to ' + platform);
        }
      });
    });
  }
    /**
   * Update a setting in UserSettings
   */
  private updateSetting(key: string, value: any): void {
    try {
      this.userSettings.set('user-preferences', key, value);
      
      // Apply certain settings immediately
      if (key === 'themeName') {
        this.themeManager.applyTheme(value);
      } else if (key === 'accentColor') {
        document.documentElement.style.setProperty('--accent-color', this.getAccentColorValue(value));
      }
      
      // Show feedback
      this.showConfirmationDialog('Setting updated');
    } catch (error) {
      ErrorHandler.handleError(error as Error);
    }
  }
  
  /**
   * Get the color value for an accent color ID
   */
  private getAccentColorValue(colorId: string): string {
    const color = this.accentColors.find(c => c.id === colorId);
    return color ? color.color : '#0078d7';
  }
  
  /**
   * Set the active settings section
   */
  private setActiveSection(sectionId: string): void {
    this.activeSection = sectionId;
    
    if (!this.container) return;
    
    // Update section tabs
    const sectionTabs = this.container.querySelectorAll('.section-tab');
    sectionTabs.forEach(tab => {
      const tabSection = tab.getAttribute('data-section');
      if (tabSection === sectionId) {
        tab.classList.add('active');
      } else {
        tab.classList.remove('active');
      }
    });
    
    // Update content area
    const contentArea = this.container.querySelector('#mobile-settings-content');
    if (contentArea) {
      contentArea.innerHTML = this.renderSettingsSection(sectionId);
      
      // Re-attach event listeners for the new content
      this.setupSettingControls();
    }
  }
  
  /**
   * Toggle search panel visibility
   */
  private toggleSearch(): void {
    this.isSearchActive = !this.isSearchActive;
    
    if (!this.container) return;
    
    const searchArea = this.container.querySelector('.mobile-settings-search');
    if (searchArea) {
      if (this.isSearchActive) {
        searchArea.classList.add('active');
        const searchInput = searchArea.querySelector('.mobile-search-input') as HTMLInputElement;
        if (searchInput) {
          searchInput.focus();
        }
      } else {
        searchArea.classList.remove('active');
      }
    }
  }
  
  /**
   * Update search results based on the current query
   */
  private updateSearchResults(): void {
    if (!this.container) return;
    
    const resultsContainer = this.container.querySelector('#mobile-search-results');
    if (!resultsContainer) return;
    
    if (!this.searchQuery) {
      resultsContainer.innerHTML = '';
      return;
    }
    
    // Find matching settings
    const matches = Array.from(this.settingsIndex.values()).filter(setting => {
      const searchText = `${setting.title} ${setting.description}`.toLowerCase();
      return searchText.includes(this.searchQuery.toLowerCase());
    });
    
    if (matches.length === 0) {
      resultsContainer.innerHTML = '<div class="no-results">No matching settings found</div>';
      return;
    }
    
    // Render matches
    let html = '';
    matches.forEach(setting => {
      html += `
        <div class="search-result-item" data-section="${setting.section}" data-setting="${setting.key}">
          <i class="fa ${setting.icon || 'fa-cog'} result-icon"></i>
          <div class="result-info">
            <div class="result-title">${setting.title}</div>
            <div class="result-section">${this.getReadableSection(setting.section)}</div>
          </div>
          <i class="fa fa-chevron-right"></i>
        </div>
      `;
    });
    
    resultsContainer.innerHTML = html;
    
    // Add click handlers for search results
    const resultItems = resultsContainer.querySelectorAll('.search-result-item');
    resultItems.forEach(item => {
      item.addEventListener('click', (e) => {
        const section = (e.currentTarget as HTMLElement).getAttribute('data-section');
        const setting = (e.currentTarget as HTMLElement).getAttribute('data-setting');
        
        if (section) {
          this.setActiveSection(section);
          this.toggleSearch();
          
          // Scroll to the specific setting if available
          if (setting) {
            setTimeout(() => {
              const settingElement = this.container?.querySelector(`[data-setting="${setting}"]`);
              if (settingElement) {
                settingElement.scrollIntoView({ behavior: 'smooth' });
                
                // Highlight the setting briefly
                settingElement.classList.add('highlight');
                setTimeout(() => {
                  settingElement.classList.remove('highlight');
                }, 1500);
              }
            }, 300);
          }
        }
      });
    });
  }
  
  /**
   * Get a readable section name
   */
  private getReadableSection(sectionId: string): string {
    const section = this.sections.find(s => s.id === sectionId);
    return section ? section.name : this.capitalizeFirstLetter(sectionId);
  }
  
  /**
   * Load current settings into the UI
   */
  private loadCurrentSettings(): void {
    // This will be handled by the setupSettingControls method
    // as controls are created with current values
  }
  
  /**
   * Show a confirmation dialog/toast
   */
  private showConfirmationDialog(message: string): void {
    if (!this.container) return;
    
    // Remove any existing confirmation
    const existingToast = document.querySelector('.mobile-settings-toast');
    if (existingToast) {
      existingToast.remove();
    }
    
    // Create toast element
    const toast = document.createElement('div');
    toast.className = 'mobile-settings-toast';
    toast.textContent = message;
    
    // Add to DOM
    document.body.appendChild(toast);
    
    // Animate in
    setTimeout(() => {
      toast.classList.add('visible');
    }, 10);
    
    // Remove after delay
    setTimeout(() => {
      toast.classList.remove('visible');
      setTimeout(() => {
        toast.remove();
      }, 300);
    }, 2000);
  }
  
  /**
   * Helper method to capitalize first letter of a string
   */
  private capitalizeFirstLetter(str: string): string {
    return str.charAt(0).toUpperCase() + str.slice(1);
  }
  
  /**
   * Cleanup resources when application is closed
   */
  protected cleanup(): void {
    // No specific cleanup needed
  }
}
