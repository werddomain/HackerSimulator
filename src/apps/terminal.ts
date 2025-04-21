import { OS } from '../core/os';
import { Terminal } from 'xterm';
import { FitAddon } from 'xterm-addon-fit';
import 'xterm/css/xterm.css';
import { CommandContext, DirectoryChangeHandler } from '../commands/command-processor';
import { GuiApplication, SubProcessOptions } from '../core/gui-application';

/**
 * Terminal Application
 */
export class TerminalApp extends GuiApplication {
  private terminal: Terminal;
  private fitAddon: FitAddon;
  private currentPath: string = '/home/user';
  private commandHistory: string[] = [];
  private historyIndex: number = -1;
  private inputBuffer: string = '';
  private promptLength: number = 0;
  private cursorPosition: number = 0; // 0 = start of input, inputBuffer.length = end of input
  private commandContext: CommandContext;
  
  // Event handlers for directory changes
  private directoryChangeHandlers: DirectoryChangeHandler[] = [];

  private defaultKeyHandlingPaused: boolean = false;

  constructor(os: OS) {
    super(os);
    
    // Initialize xterm.js terminal
    this.terminal = new Terminal({
      cursorBlink: true,
      theme: {
        background: '#1e1e1e',
        foreground: '#f0f0f0',
        cursor: '#f0f0f0',
        selectionBackground: 'rgba(255, 255, 255, 0.3)',
        black: '#1e1e1e',
        red: '#ff5555',
        green: '#50fa7b',
        yellow: '#f1fa8c',
        blue: '#bd93f9',
        magenta: '#ff79c6',
        cyan: '#8be9fd',
        white: '#f8f8f2',
        brightBlack: '#6272a4',
        brightRed: '#ff6e6e',
        brightGreen: '#69ff94',
        brightYellow: '#ffffa5',
        brightBlue: '#d6acff',
        brightMagenta: '#ff92df',
        brightCyan: '#a4ffff',
        brightWhite: '#ffffff'
      },
      fontFamily: 'monospace',
      fontSize: 14,
      letterSpacing: 0,
      lineHeight: 1.2
    });
      // Initialize fit addon to make terminal responsive
    this.fitAddon = new FitAddon();
    this.terminal.loadAddon(this.fitAddon);
    
    // Initialize command context for terminal I/O
    this.commandContext = this.createCommandContext();
    
    // Register event handlers
    this.on('resize', () => this.handleResize());
    this.on('resized', () => this.handleResize());

    this.on('focus', () => this.handleFocus());
  }
  
  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'terminal';
  }
  
  /**
   * Application-specific initialization
   */
protected initApplication(): void {
    if (!this.container) return;
    
    // Open terminal
    this.terminal.open(this.container);
    
    // Fit terminal to container
    this.fitAddon.fit();
    
    // Welcome message
    this.terminal.writeln('Welcome to HackerOS Terminal');
    this.terminal.writeln('Type "help" for a list of commands');
    this.terminal.writeln('');
    
    // Initial prompt
    this.showPrompt();
    
    // Set up terminal input
    this.terminal.onData(this.handleTerminalInput.bind(this));
    
    // Focus the terminal immediately on startup
    this.terminal.focus();
  }
    /**
   * Create command context for terminal I/O
   */  private createCommandContext(): CommandContext {
    const self = this;
    
    return {
      os: this.os,
      get xTerm(): Terminal {
        return self.terminal;
      },
      get cwd(): string {
        return self.currentPath;
      },
      set cwd(newPath: string) {
        if (self.currentPath !== newPath) {
          // Update terminal's internal path
          self.currentPath = newPath;
          
          // Update environment variables
          if (this.env) {
            this.env.PWD = newPath;
          }
          
          // Trigger directory change events
          self.triggerDirectoryChange(newPath);
          
          // Also trigger the handleDirectoryChange method directly
          self.handleDirectoryChange(newPath);
        }
      },
      terminalApp: self,
      stdin: {
        read: async (): Promise<string> => {
          return new Promise((resolve) => {
            let buffer = '';
            
            // Store current handler
            const originalHandler = this.handleTerminalInput.bind(this);
            
            // Create temporary input handler
            const tempInputHandler = (data: string) => {
              const code = data.charCodeAt(0);
              
              if (code === 13) { // Enter key
                // Restore original handler
                this.terminal.onData(originalHandler);
                this.terminal.writeln('');
                resolve(buffer);
              } else if (code === 8 || data === '\x7F') { // Backspace
                if (buffer.length > 0) {
                  buffer = buffer.slice(0, -1);
                  this.terminal.write('\b \b');
                }
              } else {
                buffer += data;
                this.terminal.write(data);
              }
            };
            
            // Set temporary handler
            this.terminal.onData(tempInputHandler);
          });
        },
        readLine: async (): Promise<string> => {
          return this.commandContext.stdin.read();
        },
        readChar: async (): Promise<string> => {
          return new Promise((resolve) => {
            // Store current handler
            const originalHandler = this.handleTerminalInput.bind(this);
            
            // Create temporary char handler
            const tempCharHandler = (data: string) => {
              // Restore original handler
              this.terminal.onData(originalHandler);
              this.terminal.write(data);
              resolve(data.charAt(0));
            };
            
            // Set temporary handler
            this.terminal.onData(tempCharHandler);
          });
        }
      },
      stdout: {
        write: (text: string): void => {
          this.terminal.write(text);
        },
        writeLine: (text: string): void => {
          this.terminal.writeln(text);
        },
        clear: (): void => {
          this.terminal.clear();
        }
      },
      stderr: {
        write: (text: string): void => {
          // For error messages, we'll use red text
          this.terminal.write(`\x1b[31m${text}\x1b[0m`);
        },
        writeLine: (text: string): void => {
          this.terminal.writeln(`\x1b[31m${text}\x1b[0m`);
        },
        clear: (): void => {
          this.terminal.clear();
        }
      },
      env: {
        'USER': 'user',
        'HOME': '/home/user',
        'PATH': '/bin:/usr/bin',
        'PWD': this.currentPath,
        'TERM': 'xterm-256color',
        'SHELL': '/bin/bash'
      }
    };
  }
  /**
   * Handle resize event
   */
  private handleResize(): void {
    if (this.fitAddon && this.container) {
      // Make sure terminal takes full height of container
      this.terminal.element?.style.setProperty('height', '100%');
      this.terminal.element?.style.setProperty('width', '100%');
      
      // Force a layout calculation before fitting
      setTimeout(() => {
        this.fitAddon.fit();
      }, 0);
    }
  }
  /**
   * Show terminal prompt
   */
  private showPrompt(): void {
    const prompt = `user@hacker-machine:${this.currentPath}$ `;
    this.terminal.write(prompt);
    this.promptLength = prompt.length;
  }

  /**
   * Pause the terminal's default key handling
   * This is used by terminal applications like editors that need full control over key events
   */
  public pauseDefaultKeyHandling(): void {
    this.defaultKeyHandlingPaused = true;
  }

  /**
   * Resume the terminal's default key handling
   * Called when terminal applications exit
   */
  public resumeDefaultKeyHandling(): void {
    this.defaultKeyHandlingPaused = false;
  }

  /**
   * Handle terminal input
   * @param data The input data
   */
  private handleTerminalInput(data: string): void {
    // If key handling is paused, don't process the input
    if (this.defaultKeyHandlingPaused) {
      return;
    }

    // Rest of the existing handleTerminalInput implementation
    // Handle special key sequences
    if (data === '\r') { // Enter
      this.handleEnterKey();
    } else if (data === '\x7F' || data === '\b') { // Backspace (different terminals may send different codes)
      this.handleBackspaceKey();
    } else if (data === '\x1b') { // Escape
      // Clear current input
      this.clearCurrentInput();
    } else if (data === '\u0003') { // Ctrl+C
      this.terminal.writeln('^C');
      this.showPrompt();
      this.inputBuffer = '';
      this.cursorPosition = 0;
      this.historyIndex = -1;
    } else if (data === '\t') { // Tab
      // Tab completion (TODO)
    } else if (data === '\x1b[A') { // Up arrow
      this.navigateHistory(-1);
    } else if (data === '\x1b[B') { // Down arrow
      this.navigateHistory(1);
    } else if (data === '\x1b[C') { // Right arrow
      this.moveCursor(1);
    } else if (data === '\x1b[D') { // Left arrow
      this.moveCursor(-1);
    } else if (data.length === 1 && data.charCodeAt(0) >= 32) {
      // Regular printable input - insert at cursor position
      this.insertAtCursor(data);
    }
  }

  /**
   * Move cursor in the input buffer
   * @param direction 1 for right, -1 for left
   */
  private moveCursor(direction: number): void {
    const newPosition = this.cursorPosition + direction;
    
    // Don't move cursor outside the input buffer
    if (newPosition < 0 || newPosition > this.inputBuffer.length) {
      return;
    }
    
    // Update cursor position
    this.cursorPosition = newPosition;
    
    // Move terminal cursor
    if (direction > 0) {
      this.terminal.write('\x1b[C'); // Move cursor right
    } else {
      this.terminal.write('\x1b[D'); // Move cursor left
    }
  }
  /**
   * Insert text at the current cursor position
   * @param text Text to insert
   */
  private insertAtCursor(text: string): void {
    // Insert text at cursor position
    const newBuffer = 
      this.inputBuffer.slice(0, this.cursorPosition) + 
      text + 
      this.inputBuffer.slice(this.cursorPosition);
    
    // Update input buffer
    this.inputBuffer = newBuffer;
    
    // Update cursor position
    this.cursorPosition += text.length;
    
    if (this.cursorPosition === this.inputBuffer.length) {
      // If cursor is at the end, just write the text without redrawing the whole line
      this.terminal.write(text);
    } else {
      // Redraw the line if cursor is in the middle of text
      this.redrawInputLine();
    }
  }
  /**
   * Handle Enter key
   */
  private async handleEnterKey(): Promise<void> {
    // Move cursor to end of line first
    if (this.cursorPosition < this.inputBuffer.length) {
      this.terminal.write('\x1b[' + (this.inputBuffer.length - this.cursorPosition) + 'C');
    }
    
    this.terminal.writeln('');
    
    // Add command to history if not empty
    if (this.inputBuffer.trim()) {
      this.commandHistory.push(this.inputBuffer);
      this.historyIndex = -1;
    }
    
    // Get the command
    const commandString = this.inputBuffer;
    this.inputBuffer = '';
    this.cursorPosition = 0;

    // Parse command with proper handling of quoted arguments
    const commandParts = this.parseCommandArgs(commandString.trim());
    
    if (commandParts.length > 0) {
      const commandName = commandParts[0];
      const args = commandParts.slice(1);
      
      // Use the createSubProcess method from the base class
      this.createSubProcess({
        name: commandName,
        command: commandString,
        user: 'user',
        cpuUsage: 0.2,
        memoryUsage: 5
      });
      
      // Process command and show output
      // We're using the original command string here so the command processor can 
      // handle parsing itself using our properly parsed arguments
      const output = await this.os.getCommandProcessor().processCommand(commandString, this.commandContext);
      if (output) {
        this.terminal.writeln(output);
      }
    }
    
    // Show new prompt
    this.showPrompt();
  }
  /**
   * Handle Backspace key
   */
  private handleBackspaceKey(): void {
    if (this.inputBuffer.length > 0 && this.cursorPosition > 0) {
      // Delete the character before the cursor
      this.inputBuffer = 
        this.inputBuffer.slice(0, this.cursorPosition - 1) + 
        this.inputBuffer.slice(this.cursorPosition);
      
      // Move cursor back
      this.cursorPosition--;
      
      if (this.cursorPosition === this.inputBuffer.length) {
        // If cursor is at the end after deletion, just move it back
        this.terminal.write('\b \b');
      } else {
        // Redraw the line if cursor is in the middle of text
        this.redrawInputLine();
      }
    }
  }
  /**
   * Redraw the current input line
   */
  private redrawInputLine(): void {
    // Clear the line and rewrite from the prompt
    this.terminal.write('\r\x1b[K'); // Clear line from cursor to end
    this.showPrompt();
    this.terminal.write(this.inputBuffer);
    
    // Reset cursor to correct position
    // First move to the end of the line
    // Then move back as needed
    const backSpaces = this.inputBuffer.length - this.cursorPosition;
    if (backSpaces > 0) {
      this.terminal.write(`\x1b[${backSpaces}D`);
    }
  }

  /**
   * Clear current input
   */
  private clearCurrentInput(): void {
    // Clear current line
    this.terminal.write('\r\x1b[K');
    this.showPrompt();
    this.inputBuffer = '';
    this.cursorPosition = 0;
  }

  /**
   * Navigate command history
   */
  private navigateHistory(direction: number): void {
    if (this.commandHistory.length === 0) return;
    
    // Calculate new history index
    let newIndex = this.historyIndex + direction;
    
    // Clamp index to valid range
    if (newIndex < -1) newIndex = -1;
    if (newIndex >= this.commandHistory.length) newIndex = this.commandHistory.length - 1;
    
    // If index didn't change, do nothing
    if (newIndex === this.historyIndex) return;
    
    // Update history index
    this.historyIndex = newIndex;
    
    // Show command from history or empty input
    if (this.historyIndex === -1) {
      this.inputBuffer = '';
    } else {
      this.inputBuffer = this.commandHistory[this.historyIndex];
    }
    
    // Update cursor position to end of line
    this.cursorPosition = this.inputBuffer.length;
    
    // Redraw the line
    this.redrawInputLine();
  }

  /**
   * Execute a command programmatically
   */
  public async executeCommand(command: string): Promise<void> {
    // Set input buffer
    this.inputBuffer = command;
    this.cursorPosition = command.length;
    
    // Write command to terminal
    this.redrawInputLine();
    
    // Process the command
    await this.handleEnterKey();
  }

  /**
   * Handle focus event
   */
  private handleFocus(): void {
    // Focus the terminal when the window gets focus
    if (this.terminal) {
      this.terminal.focus();
    }
  }

  /**
   * Handle directory change events
   */
  private handleDirectoryChange(newPath: string): void {
    // Update the current path (though this should already be updated)
    this.currentPath = newPath;
    
    // If we're at a command prompt (not in the middle of command execution)
    // update the prompt to reflect the new directory
    if (this.inputBuffer === '') {
      // Clear the current line and show the new prompt
      this.terminal.write('\r\x1b[K');
      this.showPrompt();
    }
  }

  /**
   * Register a directory change event handler
   * @param handler Function to call when directory changes
   */
  public onDirectoryChange(handler: DirectoryChangeHandler): void {
    this.directoryChangeHandlers.push(handler);
  }

  /**
   * Remove a directory change event handler
   * @param handler Handler to remove
   */
  public offDirectoryChange(handler: DirectoryChangeHandler): void {
    const index = this.directoryChangeHandlers.indexOf(handler);
    if (index !== -1) {
      this.directoryChangeHandlers.splice(index, 1);
    }
  }

  /**
   * Trigger directory change events
   * @param newPath New directory path
   */
  private triggerDirectoryChange(newPath: string): void {
    this.directoryChangeHandlers.forEach(handler => handler(newPath));
  }

  /**
   * Parse command arguments, respecting quotes
   * @param command The command string to parse
   * @returns Array of parsed arguments
   */
  private parseCommandArgs(command: string): string[] {
    const args: string[] = [];
    let currentArg = '';
    let inQuotes = false;
    let escapeNext = false;
    
    // Process each character in the command
    for (let i = 0; i < command.length; i++) {
      const char = command[i];
      
      // Handle escape character
      if (escapeNext) {
        currentArg += char;
        escapeNext = false;
        continue;
      }
      
      // Check for escape character
      if (char === '\\') {
        escapeNext = true;
        continue;
      }
      
      // Handle quotes
      if (char === '"' || char === "'") {
        inQuotes = !inQuotes;
        continue;
      }
      
      // Handle spaces
      if (char === ' ' && !inQuotes) {
        // If we have a current argument, add it to args and reset
        if (currentArg) {
          args.push(currentArg);
          currentArg = '';
        }
        continue;
      }
      
      // Add character to current argument
      currentArg += char;
    }
    
    // Add any remaining argument
    if (currentArg) {
      args.push(currentArg);
    }
    
    return args;
  }
}
