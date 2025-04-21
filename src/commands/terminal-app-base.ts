import { Terminal } from 'xterm';
import { OS } from '../core/os';
import { CommandContext, CommandModule, CommandArgs, ExecuteMigrator } from './command-processor';
import { TerminalApp } from '../apps/terminal';

/**
 * Handler type for key events
 */
export type KeyEventHandler = (data: string) => void;

/**
 * Base class for creating advanced terminal applications that run within the terminal
 * This provides common functionality for terminal UI apps like text editors, file browsers, etc.
 */
export abstract class TerminalAppBase implements CommandModule {
  protected terminal: Terminal | null = null;
  protected os: OS;
  protected running: boolean = false;
  protected dataDisposable: { dispose: () => void } | null = null;
  protected resizeDisposable: { dispose: () => void } | null = null;
  protected keyHandlers: Map<string, KeyEventHandler> = new Map();
  protected context: CommandContext | null = null;
  protected width: number = 80;
  protected height: number = 24;
  protected commandExecuting: boolean = false;
  private appPromiseResolve: ((value: number) => void) | null = null;
  
  /**
   * Command name - must be implemented by derived classes
   */
  public abstract get name(): string;
  
  /**
   * Command description - must be implemented by derived classes
   */
  public abstract get description(): string;
  
  /**
   * Command usage - must be implemented by derived classes
   */
  public abstract get usage(): string;

  /**
   * Create a new terminal application
   * 
   * @param os The OS instance
   */
  constructor(os: OS) {
    this.os = os;
  }

  public abstract setup(args: CommandArgs, context: CommandContext): Promise<void>;

  /**
   * Execute method required by CommandModule interface
   * This serves as the entry point when invoked as a command
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Check if we have a terminal app reference
      if (!context.terminalApp) {
        context.stderr.writeLine(`Error: ${this.name} requires a terminal`);
        return 1;
      }
      
      // Get the terminal from the terminal app
      const terminalApp = context.terminalApp as TerminalApp;
      if (!terminalApp || !context.xTerm) {
        context.stderr.writeLine(`Error: could not access terminal`);
        return 1;
      }
      
      this.terminal = context.xTerm;
      this.context = context;
      
      // Store terminal dimensions if terminal exists
      if (this.terminal) {
        this.width = this.terminal.cols;
        this.height = this.terminal.rows;
      }
      
      // Initialize the app
      this.setupEvents();
      
      await this.setup(args, context);

      // Start the application with a promise that resolves when the app exits
      return new Promise<number>((resolve) => {
        this.appPromiseResolve = resolve;
        
        this.start();
      });
    } catch (error) {
      if (context && context.stderr) {
        context.stderr.writeLine(`Error: ${error instanceof Error ? error.message : String(error)}`);
      }
      this.cleanup();
      return 1;
    }
  }
  
  /**
   * Set up terminal event handling
   */
  protected setupEvents(): void {
    if (!this.terminal) return;
    
    // Set up data event handler
    this.dataDisposable = this.terminal.onData(this.handleKeyInput.bind(this));
    
    // Set up resize event handler
    this.resizeDisposable = this.terminal.onResize(this.handleResize.bind(this));
    
    // Initialize default key handlers
    this.initDefaultKeyHandlers();
    
    // Set running state
    this.running = true;
    
    // Disable terminal's default key handling behavior
    // This prevents keys like Enter from having double effect
    if (this.context && this.context.terminalApp) {
      (this.context.terminalApp as TerminalApp).pauseDefaultKeyHandling();
    }
  }

  /**
   * Initialize default key handlers
   * These are common actions like Ctrl+C to exit
   */
  protected initDefaultKeyHandlers(): void {
    // Ctrl+C - Exit application
    this.keyHandlers.set('\u0003', () => this.exit());
    
    // Add more default handlers here as needed
  }

  /**
   * Handle key input from terminal
   */
  protected handleKeyInput(data: string): void {
    if (!this.running) return;
    
    // Check if we have a specific handler for this key
    const handler = this.keyHandlers.get(data);
    
    if (handler) {
      handler(data);
    } else {
      // Default handler for unhandled keys
      this.handleUnknownKey(data);
    }
  }

  /**
   * Handle terminal resize events
   */
  protected handleResize(size: { cols: number; rows: number }): void {
    this.width = size.cols;
    this.height = size.rows;
    
    // Trigger redraw when terminal is resized
    if (this.running) {
      this.render();
    }
  }

  /**
   * Handle unknown/unregistered keys
   * Subclasses should override this to handle app-specific input
   */
  protected abstract handleUnknownKey(data: string): void;

  /**
   * Render the application UI
   * Subclasses must implement this to draw their UI
   */
  protected abstract render(): void;

  /**
   * Start the application
   */
  public start(): void {
    if (!this.terminal) return;
    
    // Set running state
    this.running = true;
    
    // Clear terminal and render initial UI
    this.clearScreen();
    this.render();
  }

  /**
   * Clean up resources
   */
  protected cleanup(): void {
    // Dispose of event handlers
    if (this.dataDisposable) {
      this.dataDisposable.dispose();
      this.dataDisposable = null;
    }
    
    if (this.resizeDisposable) {
      this.resizeDisposable.dispose();
      this.resizeDisposable = null;
    }
    
    // Clear key handlers
    this.keyHandlers.clear();
    
    // Reset state
    this.running = false;
  }
  /**
   * Exit the application
   */
  public exit(): void {
    if (!this.running || !this.terminal) return;
    
    // Resume terminal's default key handling
    if (this.context && this.context.terminalApp) {
      (this.context.terminalApp as TerminalApp).resumeDefaultKeyHandling();
    }
    
    // Clean up resources
    this.cleanup();
    
    // Clear screen
    this.clearScreen();
    
    // Notify completion
    this.onExit();
    
    // Resolve the app promise if it exists
    if (this.appPromiseResolve) {
      this.appPromiseResolve(0);
      this.appPromiseResolve = null;
    }
  }

  /**
   * Called when the application exits
   * Subclasses can override to add cleanup logic
   */
  protected onExit(): void {
    // Default implementation does nothing
  }

  /**
   * Clear the terminal screen
   */
  protected clearScreen(): void {
    if (!this.terminal) return;
    this.terminal.write('\x1B[2J\x1B[0f');
  }

  /**
   * Move cursor to a specific position
   */
  protected moveCursor(x: number, y: number): void {
    if (!this.terminal) return;
    this.terminal.write(`\x1B[${y + 1};${x + 1}H`);
  }

  /**
   * Set text attributes 
   * @param attrs Array of attribute codes
   */
  protected setTextAttributes(attrs: number[]): void {
    if (!this.terminal) return;
    this.terminal.write(`\x1B[${attrs.join(';')}m`);
  }

  /**
   * Reset text attributes to default
   */
  protected resetAttributes(): void {
    if (!this.terminal) return;
    this.terminal.write('\x1B[0m');
  }

  /**
   * Write text at a specific position
   */
  protected writeAt(x: number, y: number, text: string): void {
    if (!this.terminal) return;
    this.moveCursor(x, y);
    this.terminal.write(text);
  }

  /**
   * Draw a box in the terminal
   */
  protected drawBox(x: number, y: number, width: number, height: number, title?: string): void {
    if (!this.terminal) return;
    
    // Draw top border
    this.writeAt(x, y, '┌' + '─'.repeat(width - 2) + '┐');
    
    // Draw sides
    for (let i = 1; i < height - 1; i++) {
      this.writeAt(x, y + i, '│' + ' '.repeat(width - 2) + '│');
    }
    
    // Draw bottom border
    this.writeAt(x, y + height - 1, '└' + '─'.repeat(width - 2) + '┘');
    
    // Add title if provided
    if (title) {
      const titleText = ` ${title} `;
      const titlePos = x + Math.floor((width - titleText.length) / 2);
      this.writeAt(titlePos, y, titleText);
    }
  }

  /**
   * Draw a status bar at the bottom of the terminal
   */
  protected drawStatusBar(commands: { key: string; description: string }[]): void {
    if (!this.terminal) return;
    
    // Position at the bottom row
    const y = this.height - 1;
    
    // Clear the line
    this.moveCursor(0, y);
    this.terminal.write('\x1B[K');
    
    // Set inverse colors for status bar
    this.setTextAttributes([7]);
    
    // Draw the commands
    let x = 0;
    for (const cmd of commands) {
      const text = ` ${cmd.key}:${cmd.description} `;
      this.writeAt(x, y, text);
      x += text.length + 1;
    }
    
    // Reset attributes
    this.resetAttributes();
  }

  /**
   * Show a message box in the center of the terminal
   */
  protected showMessageBox(title: string, message: string): Promise<void> {
    if (!this.terminal) return Promise.resolve();
    
    return new Promise<void>((resolve) => {
      // Calculate box dimensions and position
      const width = Math.min(Math.max(message.length + 4, title.length + 4), this.width - 4);
      const height = 5;
      const x = Math.floor((this.width - width) / 2);
      const y = Math.floor((this.height - height) / 2);
      
      // Store original key handlers
      const originalHandlers = new Map(this.keyHandlers);
      
      // Set new handler just for Enter
      this.keyHandlers.clear();
      this.keyHandlers.set('\r', () => {
        // Restore original handlers
        this.keyHandlers = originalHandlers;
        
        // Redraw UI
        this.render();
        
        // Resolve promise
        resolve();
      });
      
      // Draw message box
      this.drawBox(x, y, width, height, title);
      this.writeAt(x + 2, y + 2, message);
      this.writeAt(x + 2, y + 3, 'Press ENTER to continue');
    });
  }

  /**
   * Show a prompt for user input
   */
  protected promptInput(prompt: string, defaultValue: string = ''): Promise<string> {
    if (!this.terminal) return Promise.resolve('');
    
    return new Promise<string>((resolve) => {
      if (!this.terminal) return resolve('');
      // Calculate position for prompt (near bottom of screen)
      const y = this.height - 3;
      
      // Clear line
      this.moveCursor(0, y);
      this.terminal.write('\x1B[K');
      
      // Show prompt
      this.terminal.write(prompt);
      
      // Show default value
      let input = defaultValue;
      this.terminal.write(input);
      
      // Store original key handlers
      const originalHandlers = new Map(this.keyHandlers);
      
      // Create input handler
      this.keyHandlers.clear();
      
      // Handle Enter
      this.keyHandlers.set('\r', () => {
        // Restore original handlers
        this.keyHandlers = originalHandlers;
        
        // Redraw UI
        this.render();
        
        // Resolve with input
        resolve(input);
      });
      
      // Handle Escape
      this.keyHandlers.set('\x1b', () => {
        // Restore original handlers
        this.keyHandlers = originalHandlers;
        
        // Redraw UI
        this.render();
        
        // Resolve with empty string (cancel)
        resolve('');
      });
      
      // Handle Backspace
      this.keyHandlers.set('\x7F', () => {
        if (input.length > 0 && this.terminal) {
          input = input.slice(0, -1);
          this.moveCursor(0, y);
          this.terminal.write(prompt + input + ' ');
          this.moveCursor(prompt.length + input.length, y);
        }
      });
      
      // Handle regular input
      this.handleUnknownKey = (data: string) => {
        if (data.length === 1 && data.charCodeAt(0) >= 32 && this.terminal) {
          input += data;
          this.terminal.write(data);
        }
      };
    });
  }
}
