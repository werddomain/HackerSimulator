import { OS } from './os';
import { WindowManager } from './window';
import { ProcessManager } from './process';

/**
 * Interface for application metadata
 */
export interface AppInfo {
  id: string;
  name: string;
  description: string;
  icon: string;
  launchable: boolean;
  singleton: boolean;
}

/**
 * Interface for application instances
 */
export interface AppInstance {
  id: string;
  appId: string;
  windowId?: string;
  processId?: number;
}

/**
 * App Manager class for managing applications
 */
export class AppManager {
  private os: OS;
  private apps: Map<string, AppInfo> = new Map();
  private instances: Map<string, AppInstance> = new Map();

  constructor(os: OS) {
    this.os = os;
  }

  /**
   * Initialize the app manager
   */
  public init(): void {
    console.log('Initializing App Manager...');
    
    // Register default applications
    this.registerDefaultApps();
      // Listen for window close events to clean up app instances
    const windowManager = this.os.getWindowManager();
    windowManager.on('close', (windowId: string) => {
      this.handleWindowClose(windowId);
    });
  }
  public getIconName(appOrIcon: AppInfo | string): string {
    if (typeof appOrIcon === 'string') {
      return appOrIcon;
    } else {
      return appOrIcon.icon;
    }
  }
  public displayIcon(appOrIcon: AppInfo | string, htmlElement: HTMLElement): void {
    const icon = this.getIconName(appOrIcon);
    // Check if the icon has a prefix
    if (icon.includes(':')) {
      if (icon.startsWith('ionicons:')) {
        const iconName = icon.substring('ionicons:'.length);
        htmlElement.innerHTML = `<ion-icon name="${iconName}"></ion-icon>`;
      } else if (icon.startsWith('data:')) {
        htmlElement.innerHTML = `<img src="${icon}" />`;
      } else {
        // Handle other prefixed icons if needed
        htmlElement.innerText = icon;
      }
    } else {
      // No prefix, just use the icon as text
      htmlElement.innerText = icon;
    }
  }
  /**
   * Register default applications
   */
  private registerDefaultApps(): void {
    // Terminal
    this.registerApp({
      id: 'terminal',
      name: 'Terminal',
      description: 'Command line interface',
      icon: '📺',
      launchable: true,
      singleton: false
    });
    
    // File Explorer
    this.registerApp({
      id: 'file-explorer',
      name: 'Files',
      description: 'Browse and manage files',
      icon: '📁',
      launchable: true,
      singleton: false
    });
    
    // Text Editor
    this.registerApp({
      id: 'text-editor',
      name: 'Text Editor',
      description: 'Edit text files',
      icon: '📝',
      launchable: true,
      singleton: false
    });
    
    // Code Editor
    this.registerApp({
      id: 'code-editor',
      name: 'Code Editor',
      description: 'Edit and run code',
      icon: '💻',
      launchable: true,
      singleton: false
    });
    
    // Web Browser
    this.registerApp({
      id: 'browser',
      name: 'Browser',
      description: 'Browse the web',
      icon: '🌐',
      launchable: true,
      singleton: false
    });
    
    // System Monitor
    this.registerApp({
      id: 'system-monitor',
      name: 'System Monitor',
      description: 'Monitor system resources',
      icon: '📊',
      launchable: true,
      singleton: true
    });
    
    // Calculator
    this.registerApp({
      id: 'calculator',
      name: 'Calculator',
      description: 'Perform calculations',
      icon: '🧮',
      launchable: true,
      singleton: true
    });

    this.registerApp({
      id: 'settings',
      name: 'Settings',
      description: 'System Settings',
      icon: '⚙️',
      launchable: true,
      singleton: true
    });

    // Error Log Viewer
    this.registerApp({
      id: 'error-log-viewer',
      name: 'Error Log Viewer',
      description: 'View and analyze system error logs',
      icon: '🚨',
      launchable: true,
      singleton: true
    });
  }

  /**
   * Register an application
   */
  public registerApp(appInfo: AppInfo): void {
    this.apps.set(appInfo.id, appInfo);
  }

  /**
   * Get all registered applications
   */
  public getAllApps(): AppInfo[] {
    return Array.from(this.apps.values());
  }

  /**
   * Get default apps for desktop icons
   */
  public getDefaultApps(): AppInfo[] {
    // Return subset of apps to show on desktop
    return [
      this.apps.get('terminal')!,
      this.apps.get('file-explorer')!,
      this.apps.get('browser')!,
      this.apps.get('code-editor')!,
      this.apps.get('system-monitor')!
    ];
  }

  /**
   * Launch an application
   */
  public launchApp(appId: string, args: string[] = []): string | null {
    // Get app info
    const appInfo = this.apps.get(appId);
    if (!appInfo) {
      console.error(`App not found: ${appId}`);
      return null;
    }
    
    // Check if app is launchable
    if (!appInfo.launchable) {
      console.error(`App is not launchable: ${appId}`);
      return null;
    }
    
    // Check if singleton app is already running
    if (appInfo.singleton) {
      const existingInstance = this.findInstanceByAppId(appId);
      if (existingInstance) {
        // Focus the existing window
        const windowManager = this.os.getWindowManager();
        if (existingInstance.windowId) {
          windowManager.activateWindow(existingInstance.windowId);
          return existingInstance.id;
        }
      }
    }
    
    // Create app instance
    const instanceId = `${appId}-${Date.now()}`;
    const instance: AppInstance = {
      id: instanceId,
      appId: appId
    };
    
    // Create process for the app
    const processManager = this.os.getProcessManager();
    const processId = processManager.createProcess(
      appInfo.name,
      'user',
      0.3,  // Initial CPU usage
      15,   // Initial memory usage
      appId
    );
    
    instance.processId = processId;
    
    // Create window for the app (if it's UI-based)
    const windowManager = this.os.getWindowManager();
    const windowId = windowManager.createWindow({
      title: appInfo.name,
      width: 800,
      height: 600,
      appId: appId,
      icon: appInfo.icon,
      processId: processId
    });
    
    instance.windowId = windowId;
    
    // Store the instance
    this.instances.set(instanceId, instance);
    
    // Load the app's UI into the window
    this.loadAppUI(appId, windowId, args);
    
    return instanceId;
  }

  /**
   * Load application UI into a window
   */
  private loadAppUI(appId: string, windowId: string, args: string[] = []): void {
    const windowManager = this.os.getWindowManager();
    const contentElement = windowManager.getWindowContentElement(windowId);
    if (!contentElement) return;
    
    // Load different UI based on app ID
    switch (appId) {
      case 'terminal':
        this.loadTerminalUI(contentElement,windowId, args);
        break;
      case 'file-explorer':
        this.loadFileExplorerUI(contentElement,windowId, args);
        break;
      case 'text-editor':
        this.loadTextEditorUI(contentElement,windowId, args);
        break;
      case 'code-editor':
        this.loadCodeEditorUI(contentElement,windowId, args);
        break;
      case 'browser':
        this.loadBrowserUI(contentElement,windowId, args);
        break;
      case 'system-monitor':
        this.loadSystemMonitorUI(contentElement,windowId, args);
        break;
      case 'calculator':
        this.loadCalculatorUI(contentElement,windowId, args);
        break;
      case 'settings':
        this.loadSettingsUI(contentElement,windowId, args);
        break;
      case 'error-log-viewer':
        this.loadErrorLogViewerUI(contentElement,windowId, args);
        break;
      default:
        contentElement.innerHTML = `<div style="padding: 20px;">App '${appId}' UI not implemented yet.</div>`;
    }
  }  /**
   * Load Terminal UI
   */
  loadSettingsUI(contentElement: HTMLElement, windowId: string, args: string[] = []) {
    // Create settings container
    contentElement.innerHTML = '<div class="settings-container"></div>';
    
    // Lazily load the settings app
    import('../apps/settings').then(module => {
      const settingsApp = new module.SettingsApp(this.os);
      settingsApp.init(contentElement.querySelector('.settings-container')!, windowId, args);
    }).catch(error => {
      console.error('Failed to load settings app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load settings app.</div>';
    });
  }
  private loadTerminalUI(contentElement: HTMLElement, windowId: string, args: string[]): void {
    // Create terminal container
    contentElement.innerHTML = '<div class="terminal-container"></div>';
    
    // Lazily load the terminal app
    import('../apps/terminal').then(module => {
      const terminalApp = new module.TerminalApp(this.os);
      terminalApp.init(contentElement.querySelector('.terminal-container')!, windowId, args);
      
      // Execute initial command if provided
      if (args.length > 0) {
        terminalApp.executeCommand(args.join(' '));
      }
    }).catch(error => {
      console.error('Failed to load terminal app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load terminal app.</div>';
    });
  }

  /**
   * Load File Explorer UI
   */
  private loadFileExplorerUI(contentElement: HTMLElement, windowId: string, args: string[]): void {
    // Create file explorer container
    contentElement.innerHTML = '<div class="file-explorer"></div>';
    
    // Lazily load the file explorer app
    import('../apps/file-explorer').then(module => {
      const fileExplorerApp = new module.FileExplorerApp(this.os);
      fileExplorerApp.init(contentElement.querySelector('.file-explorer')!, windowId, args);
      
      // Navigate to initial path if provided
      if (args.length > 0) {
        fileExplorerApp.navigateTo(args[0]);
      }
    }).catch(error => {
      console.error('Failed to load file explorer app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load file explorer app.</div>';
    });
  }

  /**
   * Load Text Editor UI
   */
  private loadTextEditorUI(contentElement: HTMLElement, windowId: string, args: string[]): void {
    // Create text editor container
    contentElement.innerHTML = '<div class="text-editor"></div>';
    
    // Lazily load the text editor app
    import('../apps/text-editor').then(module => {
      const textEditorApp = new module.TextEditorApp(this.os);
      textEditorApp.init(contentElement.querySelector('.text-editor')!, windowId, args);
      
      // Open file if provided
      if (args.length > 0) {
        textEditorApp.openFile(args[0]);
      }
    }).catch(error => {
      console.error('Failed to load text editor app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load text editor app.</div>';
    });
  }

  /**
   * Load Code Editor UI
   */
  private loadCodeEditorUI(contentElement: HTMLElement, windowId: string, args: string[]): void {
    // Create code editor container
    contentElement.innerHTML = '<div class="editor-container"></div>';
    
    // Lazily load the code editor app
    import('../apps/code-editor').then(module => {
      const codeEditorApp = new module.CodeEditorApp(this.os);
      codeEditorApp.init(contentElement.querySelector('.editor-container')!, windowId, args);
      
      // Open file if provided
      if (args.length > 0) {
        codeEditorApp.openFile(args[0]);
      }
    }).catch(error => {
      console.error('Failed to load code editor app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load code editor app.</div>';
    });
  }

  /**
   * Load Browser UI
   */
  private loadBrowserUI(contentElement: HTMLElement, windowId: string, args: string[]): void {
    // Create browser container
    contentElement.innerHTML = '<div class="browser-container"></div>';
    
    // Lazily load the browser app
    import('../apps/browser').then(module => {
      const browserApp = new module.BrowserApp(this.os);
      browserApp.init(contentElement.querySelector('.browser-container')!, windowId, args);
      
      // Navigate to URL if provided
      if (args.length > 0) {
        browserApp.navigate(args[0]);
      }
    }).catch(error => {
      console.error('Failed to load browser app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load browser app.</div>';
    });
  }

  /**
   * Load System Monitor UI
   */
  private loadSystemMonitorUI(contentElement: HTMLElement, windowId: string, args: string[] = []): void {
    // Create system monitor container
    contentElement.innerHTML = '<div class="system-monitor"></div>';
    
    // Lazily load the system monitor app
    import('../apps/system-monitor').then(module => {
      const systemMonitorApp = new module.SystemMonitorApp(this.os);
      systemMonitorApp.init(contentElement.querySelector('.system-monitor')!, windowId, args);
    }).catch(error => {
      console.error('Failed to load system monitor app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load system monitor app.</div>';
    });
  }

  /**
   * Load Calculator UI
   */
  private loadCalculatorUI(contentElement: HTMLElement, windowId: string, args: string[] = []): void {
    // Create calculator container
    contentElement.innerHTML = '<div class="calculator"></div>';
    
    // Lazily load the calculator app
    import('../apps/calculator').then(module => {
      const calculatorApp = new module.CalculatorApp(this.os);
      calculatorApp.init(contentElement.querySelector('.calculator')!, windowId, args);
    }).catch(error => {
      console.error('Failed to load calculator app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load calculator app.</div>';
    });
  }
  /**
   * Load Error Log Viewer UI
   */
  private loadErrorLogViewerUI(contentElement: HTMLElement, windowId: string, args: string[] = []): void {
    // Create error log viewer container
    contentElement.innerHTML = '<div class="error-log-viewer"></div>';
    
    // Lazily load the error log viewer app
    import('../apps/error-log-viewer').then(module => {
      const errorLogViewerApp = new module.ErrorLogViewerApp(this.os);
      errorLogViewerApp.init(contentElement.querySelector('.error-log-viewer')!, windowId, args);
    }).catch(error => {
      console.error('Failed to load error log viewer app:', error);
      contentElement.innerHTML = '<div style="padding: 20px;">Failed to load error log viewer app.</div>';
    });
  }
  /**
   * Handle window close event
   */
  private handleWindowClose(windowId: string): void {
    // Find instance with this window ID
    for (const [instanceId, instance] of this.instances.entries()) {
      if (instance.windowId === windowId) {
        // Kill the process
        const processManager = this.os.getProcessManager();
        if (instance.processId) {
          processManager.killProcess(instance.processId);
        }
        
        // Remove the instance
        this.instances.delete(instanceId);
        
        break;
      }
    }
  }

  /**
   * Find app instance by app ID
   */
  private findInstanceByAppId(appId: string): AppInstance | undefined {
    for (const instance of this.instances.values()) {
      if (instance.appId === appId) {
        return instance;
      }
    }
    return undefined;
  }
}
