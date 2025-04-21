import { Terminal } from 'xterm';
import { OS } from '../../core/os';
import { CommandContext, CommandArgs } from '../command-processor';
import { TerminalAppBase } from '../terminal-app-base';
import { FileSystem } from '../../core/filesystem';

/**
 * Line representation in the editor
 */
interface EditorLine {
  text: string;
  // We could add more properties like syntax highlighting info
}

/**
 * Nano-like text editor for the terminal
 * A simple implementation of a text editor that runs inside the terminal
 */
export class NanoEditor extends TerminalAppBase {  
  public get name(): string {
      return "nano";
  }
  public get description(): string {
      return "Text editor with a command-line interface";
  }
  public get usage(): string {
      return "nano [file] [options]\n\nOptions:\n  --readonly     Open file in read-only mode";
  }  
  
  private filePath: string = '';
  private modified: boolean = false;
  private lines: EditorLine[] = [];
  private cursorX: number = 0;
  private cursorY: number = 0;
  private scrollY: number = 0;
  private fileSystem: FileSystem;
  private statusMessage: string = '';
  private statusTimeout: number | null = null;
  private readonly: boolean = false;
  private commandContext: CommandContext | null = null;

  /**
   * Create a new nano editor instance
   */
  constructor(os: OS) {
    super(os);
    this.fileSystem = os.getFileSystem();
    

    this.initDefaultKeyHandlers();
    
  }
public setup(args: CommandArgs, context: CommandContext): Promise<void>{
    // Store the command context for later use
    this.commandContext = context;
    
    // Extract file path and options from args
    const filePath = args[0] || '';
    const options = args && args.slice ? args.slice(1) : null;

    // Initialize with a file if provided
    if (filePath) {
        this.filePath = filePath;
        this.loadFile(filePath);
    } else {
        // Start with an empty file
        this.lines = [{ text: '' }];
    }
if (options){
    // Handle readonly option
    if (options.includes('--readonly')) {
        this.readonly = true;
        this.setStatusMessage('Opened in read-only mode');
    }
  }
    return Promise.resolve();
}
  /**
   * Initialize key handlers for editor functionality
   */
  protected initDefaultKeyHandlers(): void {
    // Call parent implementation first
    super.initDefaultKeyHandlers();
    
    // Ctrl+X - Exit
    this.keyHandlers.set('\u0018', () => this.handleExit());
    
    // Ctrl+O - Save
    this.keyHandlers.set('\u000f', () => this.handleSave());
    
    // Ctrl+G - Help
    this.keyHandlers.set('\u0007', () => this.showHelp());
    
    // Ctrl+W - Search
    this.keyHandlers.set('\u0017', () => this.handleSearch());
    
    // Navigation keys
    this.keyHandlers.set('\x1b[A', () => this.moveCursorY(-1)); // Up
    this.keyHandlers.set('\x1b[B', () => this.moveCursorY(1));  // Down
    this.keyHandlers.set('\x1b[C', () => this.moveCursorX(1));  // Right
    this.keyHandlers.set('\x1b[D', () => this.moveCursorX(-1)); // Left
    
    // Home and End
    this.keyHandlers.set('\x1b[H', () => this.cursorToStartOfLine());
    this.keyHandlers.set('\x1b[F', () => this.cursorToEndOfLine());
    
    // Page Up and Page Down
    this.keyHandlers.set('\x1b[5~', () => this.pageUp());
    this.keyHandlers.set('\x1b[6~', () => this.pageDown());
    
    // Delete keys
    this.keyHandlers.set('\x7F', () => this.handleBackspace()); // Backspace
    this.keyHandlers.set('\x1b[3~', () => this.handleDelete()); // Delete
    
    // Enter key
    this.keyHandlers.set('\r', () => this.handleEnter());
  }

  /**
   * Handle unknown keys as text input
   */
  protected handleUnknownKey(data: string): void {
    // Only handle printable characters
    if (data.length === 1 && data.charCodeAt(0) >= 32) {
      this.insertText(data);
    }
  }

  /**
   * Render the editor UI
   */
  protected render(): void {
    this.clearScreen();
    
    // Calculate editor area size (leaving room for header and footer)
    const editorHeight = this.height - 3; // 2 for header, 1 for footer
    
    // Render header
    this.renderHeader();
    
    // Render editor content
    this.renderContent(editorHeight);
    
    // Render footer with keyboard shortcuts
    this.renderFooter();
    
    // Position cursor
    this.updateCursorPosition();
  }

  /**
   * Render editor header
   */
  private renderHeader(): void {
    this.setTextAttributes([7]); // Inverse colors
    
    // Clear the line
    this.moveCursor(0, 0);
    if (this.terminal)
    this.terminal.write(' '.repeat(this.width));
    
    // Show title and version
    const title = this.filePath ? ` File: ${this.filePath}` : ' New Buffer';
    const modified = this.modified ? ' (modified)' : '';
    const headerText = `${title}${modified}`;
    
    this.writeAt(0, 0, headerText);
    this.writeAt(this.width - 13, 0, ' Nano Editor ');
    
    this.resetAttributes();
  }

  /**
   * Render editor content area
   */
  private renderContent(editorHeight: number): void {
    // Ensure scroll position is valid
    this.validateScroll(editorHeight);
    if (!this.terminal) return;
    // Render visible lines
    for (let i = 0; i < editorHeight; i++) {
      const lineIndex = this.scrollY + i;
      const line = this.lines[lineIndex] || { text: '' };
      
      // Clear line
      this.moveCursor(0, i + 1);
      this.terminal.write('\x1B[K');
      
      // Write line content
      this.writeAt(0, i + 1, line.text);
    }
  }

  /**
   * Render footer with commands
   */
  private renderFooter(): void {
    if (!this.terminal) return;
    const commands = [
      { key: '^X', description: 'Exit' },
      { key: '^O', description: 'Save' },
      { key: '^G', description: 'Help' },
      { key: '^W', description: 'Search' }
    ];
    
    // Render status line
    this.moveCursor(0, this.height - 2);
    this.terminal.write('\x1B[K');
    
    if (this.statusMessage) {
      this.setTextAttributes([1]);  // Bold
      this.terminal.write(this.statusMessage);
      this.resetAttributes();
    }
    
    // Render command bar
    this.drawStatusBar(commands);
  }

  /**
   * Update the cursor position based on editor state
   */
  private updateCursorPosition(): void {
    // Calculate visible cursor position
    const visibleX = this.cursorX;
    const visibleY = this.cursorY - this.scrollY + 1; // +1 for header
    
    this.moveCursor(visibleX, visibleY);
  }

  /**
   * Validate scroll position
   */
  private validateScroll(editorHeight: number): void {
    // Check if cursor is above viewport
    if (this.cursorY < this.scrollY) {
      this.scrollY = this.cursorY;
    }
    
    // Check if cursor is below viewport
    if (this.cursorY >= this.scrollY + editorHeight) {
      this.scrollY = this.cursorY - editorHeight + 1;
    }
    
    // Clamp scroll position to valid range
    this.scrollY = Math.max(0, Math.min(this.scrollY, this.lines.length - 1));
  }

  /**
   * Set a status message that disappears after a short time
   */
  private setStatusMessage(message: string, duration: number = 3000): void {
    this.statusMessage = message;
    
    // Clear any existing timeout
    if (this.statusTimeout !== null) {
      window.clearTimeout(this.statusTimeout);
    }
    
    // Set timeout to clear message
    this.statusTimeout = window.setTimeout(() => {
      this.statusMessage = '';
      this.render();
      this.statusTimeout = null;
    }, duration);
    
    // Update display
    this.render();
  }
  /**
   * Insert text at current cursor position
   */
  private insertText(text: string): void {
    // Don't allow editing in readonly mode
    if (this.readonly) {
      this.setStatusMessage("Read-only mode: Cannot edit text", 2000);
      return;
    }
    
    const currentLine = this.lines[this.cursorY];
    
    // Insert text at cursor position
    currentLine.text = 
      currentLine.text.substring(0, this.cursorX) + 
      text + 
      currentLine.text.substring(this.cursorX);
    
    // Move cursor forward
    this.cursorX += text.length;
    
    // Mark as modified
    this.modified = true;
    
    // Update display
    this.render();
  }
  /**
   * Handle backspace key
   */
  private handleBackspace(): void {
    // Don't allow editing in readonly mode
    if (this.readonly) {
      this.setStatusMessage("Read-only mode: Cannot edit text", 2000);
      return;
    }
    
    // If at start of line and not at the first line, join with previous line
    if (this.cursorX === 0 && this.cursorY > 0) {
      const previousLine = this.lines[this.cursorY - 1];
      const currentLine = this.lines[this.cursorY];
      
      // Set cursor position to end of previous line
      this.cursorX = previousLine.text.length;
      this.cursorY--;
      
      // Join lines
      previousLine.text += currentLine.text;
      this.lines.splice(this.cursorY + 1, 1);
      
      // Mark as modified
      this.modified = true;
    } 
    // Otherwise delete character before cursor
    else if (this.cursorX > 0) {
      const currentLine = this.lines[this.cursorY];
      
      // Remove character before cursor
      currentLine.text = 
        currentLine.text.substring(0, this.cursorX - 1) + 
        currentLine.text.substring(this.cursorX);
      
      // Move cursor back
      this.cursorX--;
      
      // Mark as modified
      this.modified = true;
    }
    
    // Update display
    this.render();
  }

  /**
   * Handle delete key
   */
  private handleDelete(): void {
    // Don't allow editing in readonly mode
    if (this.readonly) {
      this.setStatusMessage("Read-only mode: Cannot edit text", 2000);
      return;
    }
    
    const currentLine = this.lines[this.cursorY];
    
    // If at end of line and not at the last line, join with next line
    if (this.cursorX === currentLine.text.length && this.cursorY < this.lines.length - 1) {
      const nextLine = this.lines[this.cursorY + 1];
      
      // Append next line to current line
      currentLine.text += nextLine.text;
      
      // Remove next line
      this.lines.splice(this.cursorY + 1, 1);
      
      // Mark as modified
      this.modified = true;
    } 
    // Otherwise delete character at cursor
    else if (this.cursorX < currentLine.text.length) {
      // Remove character at cursor
      currentLine.text = 
        currentLine.text.substring(0, this.cursorX) + 
        currentLine.text.substring(this.cursorX + 1);
      
      // Mark as modified
      this.modified = true;
    }
    
    // Update display
    this.render();
  }

  /**
   * Handle Enter key
   */
  private handleEnter(): void {
    // Don't allow editing in readonly mode
    if (this.readonly) {
      this.setStatusMessage("Read-only mode: Cannot edit text", 2000);
      return;
    }
    
    const currentLine = this.lines[this.cursorY];
    
    // Create new line
    const newLine: EditorLine = {
      text: currentLine.text.substring(this.cursorX)
    };
    
    // Truncate current line
    currentLine.text = currentLine.text.substring(0, this.cursorX);
    
    // Insert new line after current line
    this.lines.splice(this.cursorY + 1, 0, newLine);
    
    // Move cursor to start of new line
    this.cursorX = 0;
    this.cursorY++;
    
    // Mark as modified
    this.modified = true;
    
    // Update display
    this.render();
  }

  /**
   * Move cursor horizontally
   */
  private moveCursorX(delta: number): void {
    const currentLine = this.lines[this.cursorY];
    
    // Calculate new position
    let newX = this.cursorX + delta;
    
    // Clamp to line bounds
    newX = Math.max(0, Math.min(newX, currentLine.text.length));
    
    // Update if changed
    if (newX !== this.cursorX) {
      this.cursorX = newX;
      this.updateCursorPosition();
    }
  }

  /**
   * Move cursor vertically
   */
  private moveCursorY(delta: number): void {
    // Calculate new position
    let newY = this.cursorY + delta;
    
    // Clamp to document bounds
    newY = Math.max(0, Math.min(newY, this.lines.length - 1));
    
    // Update if changed
    if (newY !== this.cursorY) {
      this.cursorY = newY;
      
      // Make sure cursor X is valid for the new line
      const currentLine = this.lines[this.cursorY];
      this.cursorX = Math.min(this.cursorX, currentLine.text.length);
      
      // Update display
      this.render();
    }
  }

  /**
   * Move cursor to start of line
   */
  private cursorToStartOfLine(): void {
    if (this.cursorX !== 0) {
      this.cursorX = 0;
      this.updateCursorPosition();
    }
  }

  /**
   * Move cursor to end of line
   */
  private cursorToEndOfLine(): void {
    const currentLine = this.lines[this.cursorY];
    const endOfLine = currentLine.text.length;
    
    if (this.cursorX !== endOfLine) {
      this.cursorX = endOfLine;
      this.updateCursorPosition();
    }
  }

  /**
   * Page up - move up by a page
   */
  private pageUp(): void {
    const pageSize = this.height - 3;
    this.moveCursorY(-pageSize);
  }

  /**
   * Page down - move down by a page
   */
  private pageDown(): void {
    const pageSize = this.height - 3;
    this.moveCursorY(pageSize);
  }

  /**
   * Handle exit command
   */
  private async handleExit(): Promise<void> {
    // Check if we have unsaved changes
    if (this.modified) {
      const message = 'Save modified buffer before exiting?';
      const result = await this.showYesNoCancel(message);
      
      if (result === 'cancel') {
        return; // Cancel exit
      } else if (result === 'yes') {
        const saved = await this.handleSave();
        if (!saved) return; // Cancel exit if save was cancelled
      }
    }
    
    // Exit the editor
    this.exit();
  }

  /**
   * Show a Yes/No/Cancel dialog
   */
  private async showYesNoCancel(message: string): Promise<'yes' | 'no' | 'cancel'> {
    return new Promise((resolve) => {
      // Calculate box dimensions and position
      const width = Math.min(message.length + 10, this.width - 4);
      const height = 5;
      const x = Math.floor((this.width - width) / 2);
      const y = Math.floor((this.height - height) / 2);
      
      // Draw box
      this.drawBox(x, y, width, height, 'Confirm');
      this.writeAt(x + 2, y + 1, message);
      this.writeAt(x + 2, y + 3, 'Y:Yes  N:No  C:Cancel');
      
      // Store original key handlers
      const originalHandlers = new Map(this.keyHandlers);
      
      // Set new handlers
      this.keyHandlers.clear();
      
      // Yes
      this.keyHandlers.set('y', () => {
        this.keyHandlers = originalHandlers;
        this.render();
        resolve('yes');
      });
      
      this.keyHandlers.set('Y', () => {
        this.keyHandlers = originalHandlers;
        this.render();
        resolve('yes');
      });
      
      // No
      this.keyHandlers.set('n', () => {
        this.keyHandlers = originalHandlers;
        this.render();
        resolve('no');
      });
      
      this.keyHandlers.set('N', () => {
        this.keyHandlers = originalHandlers;
        this.render();
        resolve('no');
      });
      
      // Cancel
      this.keyHandlers.set('c', () => {
        this.keyHandlers = originalHandlers;
        this.render();
        resolve('cancel');
      });
      
      this.keyHandlers.set('C', () => {
        this.keyHandlers = originalHandlers;
        this.render();
        resolve('cancel');
      });
      
      // Escape
      this.keyHandlers.set('\x1b', () => {
        this.keyHandlers = originalHandlers;
        this.render();
        resolve('cancel');
      });
    });
  }
/**
   * Handle save command
   */
  private async handleSave(): Promise<boolean> {
    // Don't allow saving in readonly mode
    if (this.readonly) {
      this.setStatusMessage("Read-only mode: Cannot save changes", 2000);
      return false;
    }
    
    // If we don't have a file path, prompt for one
    if (!this.filePath) {
      const path = await this.promptInput('File Name to Write: ');
      
      // Cancel if no path provided
      if (!path) {
        this.setStatusMessage('Save cancelled');
        return false;
      }
      
      this.filePath = path;
    }
    
    try {
      // Convert lines to string
      const content = this.lines.map(line => line.text).join('\n');
      
      // Resolve file path - if it doesn't start with '/', prepend current directory
      let resolvedPath = this.filePath;
      if (this.filePath && !this.filePath.startsWith('/')) {
        // Use command context's current working directory for relative paths
        const cwd = this.commandContext?.cwd || '/home/user';
        resolvedPath = `${cwd}/${this.filePath}`;
        
        // Log what we're doing
        this.setStatusMessage(`Saving to current directory: ${resolvedPath}`, 1000);
      }
      
      // Save file
      await this.fileSystem.writeFile(resolvedPath, content);
      
      // Update modified flag
      this.modified = false;
      
      // Show success message
      this.setStatusMessage(`Wrote ${resolvedPath}`);
      
      return true;
    } catch (error) {
      this.setStatusMessage(`Error: ${error instanceof Error ? error.message : String(error)}`);
      return false;
    }
  }

  /**
   * Load file content
   */
  private async loadFile(filePath: string): Promise<void> {
    try {
      // Check if file exists
      const exists = await this.fileSystem.exists(filePath);
      if (!exists) {
        this.lines = [{ text: '' }];
        this.setStatusMessage(`New file: ${filePath}`);
        return;
      }
      
      // Read file
      const content = await this.fileSystem.readFile(filePath);
      
      // Split into lines
      this.lines = content.split('\n').map(text => ({ text }));
      
      // Reset cursor position
      this.cursorX = 0;
      this.cursorY = 0;
      this.scrollY = 0;
      
      // Reset modified flag
      this.modified = false;
      
      // Show success message
      this.setStatusMessage(`Read ${filePath}`);
    } catch (error) {
      this.setStatusMessage(`Error: ${error instanceof Error ? error.message : String(error)}`);
      this.lines = [{ text: '' }];
    }
  }

  /**
   * Show help screen
   */
  private async showHelp(): Promise<void> {
    // Store original key handlers
    const originalHandlers = new Map(this.keyHandlers);
    
    // Clear screen
    this.clearScreen();
    
    // Show help content
    this.writeAt(0, 0, 'Nano Editor Help');
    this.writeAt(0, 2, 'Keyboard shortcuts:');
    this.writeAt(2, 4, '^X (Ctrl+X) - Exit');
    this.writeAt(2, 5, '^O (Ctrl+O) - Save');
    this.writeAt(2, 6, '^G (Ctrl+G) - Help');
    this.writeAt(2, 7, '^W (Ctrl+W) - Search');
    this.writeAt(2, 9, 'Arrow keys - Move cursor');
    this.writeAt(2, 10, 'Home/End - Start/end of line');
    this.writeAt(2, 11, 'PgUp/PgDn - Page up/down');
    
    this.writeAt(0, this.height - 2, 'Press any key to return to editor');
    
    // Set handler to return to editor
    this.keyHandlers.clear();
    this.keyHandlers.set('*', () => {
      this.keyHandlers = originalHandlers;
      this.render();
    });
    
    // Set catch-all handler
    this.handleUnknownKey = () => {
      this.keyHandlers = originalHandlers;
      this.render();
    };
  }

  /**
   * Handle search functionality
   */
  private async handleSearch(): Promise<void> {
    const searchTerm = await this.promptInput('Search: ');
    
    if (!searchTerm) {
      this.setStatusMessage('Search cancelled');
      return;
    }
    
    // Start search from current position
    let startY = this.cursorY;
    let startX = this.cursorX + 1; // Start after current position
    
    // Search for the term
    for (let i = 0; i < this.lines.length; i++) {
      const lineIndex = (startY + i) % this.lines.length;
      const line = this.lines[lineIndex];
      
      // If this is the starting line, start from the cursor position
      const startPos = (i === 0) ? startX : 0;
      
      const pos = line.text.indexOf(searchTerm, startPos);
      
      if (pos !== -1) {
        // Found! Move cursor to this position
        this.cursorY = lineIndex;
        this.cursorX = pos;
        
        // Ensure the found text is visible
        this.render();
        
        // Highlight found text
        this.setStatusMessage(`Found at line ${lineIndex + 1}, column ${pos + 1}`);
        return;
      }
    }
    
    // Not found
    this.setStatusMessage(`"${searchTerm}" not found`);
  }
}
