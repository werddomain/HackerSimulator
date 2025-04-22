import { OS } from './os';
import { ProcessManager } from './process';
import { WindowManager } from './window';
import { DialogManager } from './dialog';
import { ErrorHandler } from './error-handler';

// Typings for window and process events
export type AppEventHandler = () => void;

/**
 * Options for creating a subprocess
 */
export interface SubProcessOptions {
  name: string;
  command?: string;
  user?: string;
  cpuUsage?: number;
  memoryUsage?: number;
  onKill?: () => void;
}

/**
 * Base class for GUI applications
 * Provides common functionality for process management and window events
 */
export abstract class GuiApplication {
  protected os: OS;
  protected container: HTMLElement | null = null;
  protected processId: number = -1;
  protected windowId: string | null = null;
  public get WindowId(): string | null {
    return this.windowId;
  }
  // Dialog manager for easy access to dialog functionality
  protected dialogManager: DialogManager;
  
  // Event handlers
  private onClosingHandlers: AppEventHandler[] = [];
  private onShowHandlers: AppEventHandler[] = [];
  private onMinimiseHandlers: AppEventHandler[] = [];
  private onMaximiseHandlers: AppEventHandler[] = [];
  private onResizeHandlers: AppEventHandler[] = [];
  private onResizedHandlers: AppEventHandler[] = [];
  private onFocusHandlers: AppEventHandler[] = [];
  protected readonly ErrorHandler: ErrorHandler;
  constructor(os: OS) {
    this.os = os;
    this.dialogManager = new DialogManager(os, this);
    this.ErrorHandler = ErrorHandler.getInstance(os);
  }
  
  /**
   * Initialize the application
   * @param container Container element
   * @param windowId Window ID
   */
  public init(container: HTMLElement, windowId?: string): void {
    this.container = container;
    this.windowId = windowId || null;
    
    // Register the application process
    this.registerProcess();
    
    // Call the application-specific initialization
    this.initApplication();
    
    // Set up window event listeners if we have a windowId
    if (this.windowId) {
      this.setupWindowEvents();
    }
  }
  
  /**
   * Application-specific initialization
   * Must be implemented by derived classes
   */
  protected abstract initApplication(): void;
  
  /**
   * Register the application process
   */
  protected registerProcess(): void {
    const processManager = this.os.getProcessManager();
    
    this.processId = processManager.createProcess(
      this.getApplicationName(),
      'user',
      0.1,
      10,
      undefined,
      this.windowId || undefined,
      undefined,
      () => {
        this.triggerOnClosing();
        this.closeWindow();
      }
    );
  }
  
  /**
   * Get application name for process registration
   */
  protected abstract getApplicationName(): string;
    /**
   * Set up window event listeners
   */
  private setupWindowEvents(): void {
    if (!this.windowId) return;
    
    // Listen for window events using the window manager
    const windowManager = this.os.getWindowManager();
    
    // Subscribe to window events
    windowManager.on('show', (windowId: string) => {
      if (windowId === this.windowId) {
        this.triggerOnShow();
      }
    });
    
    windowManager.on('minimize', (windowId: string) => {
      if (windowId === this.windowId) {
        this.triggerOnMinimise();
      }
    });
    
    windowManager.on('maximize', (windowId: string) => {
      if (windowId === this.windowId) {
        this.triggerOnMaximise();
      }
    });
    
    windowManager.on('resize', (windowId: string) => {
      if (windowId === this.windowId) {
        this.triggerOnResize();
      }
    });
    
    windowManager.on('resized', (windowId: string) => {
        if (windowId === this.windowId) {
          this.triggerOnResized();
        }
      });

    windowManager.on('focus', (windowId: string) => {
      if (windowId === this.windowId) {
        this.triggerOnFocus();
      }
    });
  }
  
  /**
   * Create a subprocess as a child of this application
   */
  protected createSubProcess(options: SubProcessOptions): number {
    const processManager = this.os.getProcessManager();
    
    return processManager.createProcess(
      options.name,
      options.user || 'user',
      options.cpuUsage || 0.2,
      options.memoryUsage || 5,
      options.command,
      undefined,
      this.processId,
      options.onKill
    );
  }
  
  /**
   * Focus the application window
   */
  public focus(): void {
    if (this.windowId) {
      this.os.getWindowManager().activateWindow(this.windowId);
    }
  }
  
  /**
   * Close the application window
   */
  public close(): void {
    if (this.processId >= 0) {
      this.os.getProcessManager().killProcess(this.processId);
    } else if (this.windowId) {
      this.closeWindow();
    }
  }
  
  /**
   * Helper to close just the window
   */
  private closeWindow(): void {
    if (this.windowId) {
      this.os.getWindowManager().closeWindow(this.windowId);
      this.windowId = null;
    }
  }
  
  /**
   * Change the window caption/title
   */
  public changeWindowCaption(newTitle: string): void {
    if (this.windowId) {
      const windowElement = document.querySelector(`.window[data-window-id="${this.windowId}"]`);
      if (windowElement) {
        const titleElement = windowElement.querySelector('.window-title');
        if (titleElement) {
          titleElement.textContent = newTitle;
        }
      }
    }
  }
  
  /**
   * Minimize the window
   */
  public minimize(): void {
    if (this.windowId) {
      this.os.getWindowManager().minimizeWindow(this.windowId);
    }
  }
  
  /**
   * Maximize or restore the window
   */
  public toggleMaximize(): void {
    if (this.windowId) {
      this.os.getWindowManager().toggleMaximizeWindow(this.windowId);
    }
  }
  
  /**
   * Register event handlers
   */
  public on(event: 'closing' | 'show' | 'minimise' | 'maximise' | 'resize' | 'resized' | 'focus', handler: AppEventHandler): void {
    switch (event) {
      case 'closing':
        this.onClosingHandlers.push(handler);
        break;
      case 'show':
        this.onShowHandlers.push(handler);
        break;
      case 'minimise':
        this.onMinimiseHandlers.push(handler);
        break;
      case 'maximise':
        this.onMaximiseHandlers.push(handler);
        break;
      case 'resize':
        this.onResizeHandlers.push(handler);
        break;
        case 'resized':
        this.onResizedHandlers.push(handler);
        break;
      case 'focus':
        this.onFocusHandlers.push(handler);
        break;
    }
  }
  
  /**
   * Event triggers
   */
  private triggerOnClosing(): void {
    this.onClosingHandlers.forEach(handler => handler());
  }
  
  private triggerOnShow(): void {
    this.onShowHandlers.forEach(handler => handler());
  }
  
  private triggerOnMinimise(): void {
    this.onMinimiseHandlers.forEach(handler => handler());
  }
  
  private triggerOnMaximise(): void {
    this.onMaximiseHandlers.forEach(handler => handler());
  }
  
  private triggerOnResize(): void {
    this.onResizeHandlers.forEach(handler => handler());
  }
  private triggerOnResized(): void {
    this.onResizedHandlers.forEach(handler => handler());
  }
  private triggerOnFocus(): void {
    this.onFocusHandlers.forEach(handler => handler());
  }

  /**
   * Access to MessageBox dialog
   */
  get Msgbox() {
    return this.dialogManager.Msgbox;
  }

  /**
   * Access to Prompt dialog
   */
  get Prompt() {
    return this.dialogManager.Prompt;
  }

  /**
   * Access to FilePicker dialog
   */
  get FilePicker() {
    return this.dialogManager.FilePicker;
  }

  /**
   * Access to DirectoryPicker dialog
   */
  get DirectoryPicker() {
    return this.dialogManager.DirectoryPicker;
  }
}
