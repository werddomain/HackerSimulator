/**
 * Mobile Terminal implementation for HackerSimulator
 * Extends the base terminal with touch-friendly controls and virtual keyboard
 */

import { OS } from '../core/os';
import { TerminalApp } from './terminal';
import { PlatformType, platformDetector } from '../core/platform-detector';
import { Terminal } from 'xterm';

/**
 * Mobile Terminal Application
 * Extends the base TerminalApp with mobile-specific enhancements
 */
export class MobileTerminalApp extends TerminalApp {
  // Mobile-specific elements
  private mobileControls: HTMLElement | null = null;
  private virtualKeyboard: HTMLElement | null = null;
  private touchToolbar: HTMLElement | null = null;
  private commandSuggestions: HTMLElement | null = null;
  private isVirtualKeyboardVisible: boolean = false;
  private swipeStartX: number = 0;
  private swipeStartY: number = 0;
  private isSwiping: boolean = false;
  
  // Command suggestions
  private recentCommands: Set<string> = new Set<string>();
  private commonCommands: string[] = [
    'ls', 'cd', 'pwd', 'cat', 'mkdir', 'rm', 
    'touch', 'echo', 'grep', 'sudo', 'chmod',
    'cp', 'mv', 'help', 'clear', 'exit'
  ];

  /**
   * Constructor
   * @param os The operating system instance
   */
  constructor(os: OS) {
    super(os);
    
    // Adjust terminal settings for mobile
    this.adjustTerminalForMobile();
  }

  /**
   * Adjust terminal settings for mobile display
   */
  private adjustTerminalForMobile(): void {
    // Get xterm instance
    const xterm = this.getTerminal();
    if (!xterm) return;
    
    // Adjust font size based on device
    if (platformDetector.getPlatformType() === PlatformType.Mobile) {
      // Increase font size for touch
      xterm.options.fontSize = 16;
      
      // Increase line height for better touch targets
      xterm.options.lineHeight = 1.4;
      
      // Adjust letter spacing
      xterm.options.letterSpacing = 0.5;
    }
  }

  /**
   * Get the xterm.js Terminal instance
   */
  public getTerminal(): Terminal | null {
    // This requires accessing the protected terminal property
    // We'll need to add a getter in the parent class
    return (this as any).terminal || null;
  }

  /**
   * Application-specific initialization
   * Overrides the base initApplication to add mobile-specific elements
   */
  protected initApplication(): void {
    // Call parent init first
    super.initApplication();
    
    // If we're on mobile, create mobile UI elements
    if (platformDetector.getPlatformType() === PlatformType.Mobile) {
      this.initMobileUI();
    }
  }

  /**
   * Initialize mobile-specific UI elements
   */
  private initMobileUI(): void {
    if (!this.container) return;
    
    // Create mobile controls container
    this.mobileControls = document.createElement('div');
    this.mobileControls.className = 'mobile-terminal-controls';
    this.container.appendChild(this.mobileControls);
    
    // Create touch toolbar (common terminal shortcuts)
    this.createTouchToolbar();
    
    // Create command suggestions container
    this.createCommandSuggestions();
    
    // Create virtual keyboard
    this.createVirtualKeyboard();
    
    // Set up touch event handlers
    this.setupTouchEvents();
  }

  /**
   * Create touch toolbar with common terminal actions
   */
  private createTouchToolbar(): void {
    if (!this.mobileControls) return;
    
    this.touchToolbar = document.createElement('div');
    this.touchToolbar.className = 'terminal-touch-toolbar';
    
    // Common actions as buttons
    const actions = [
      { key: 'Tab', label: 'Tab', icon: '↹' },
      { key: 'Ctrl+C', label: 'Ctrl+C', icon: '⌃C' },
      { key: 'ArrowUp', label: 'History', icon: '↑' },
      { key: 'ArrowDown', label: 'History', icon: '↓' },
      { key: 'Ctrl+L', label: 'Clear', icon: '⌫' },
      { key: 'Esc', label: 'Esc', icon: 'Esc' },
      { key: 'Keyboard', label: 'Keyboard', icon: '⌨' }
    ];
    
    actions.forEach(action => {
      const button = document.createElement('button');
      button.className = 'toolbar-button';
      button.setAttribute('data-key', action.key);
      button.setAttribute('aria-label', action.label);
      button.innerHTML = action.icon;
      
      // Add click event
      button.addEventListener('click', (e) => {
        e.preventDefault();
        this.handleToolbarAction(action.key);
      });
      
      this.touchToolbar!.appendChild(button);
    });
    
    this.mobileControls.appendChild(this.touchToolbar);
  }

  /**
   * Handle toolbar button actions
   */
  private handleToolbarAction(key: string): void {
    const xterm = this.getTerminal();
    if (!xterm) return;
    
    switch (key) {
      case 'Tab':
        // Trigger tab completion
        xterm.focus();
        this.sendKeyToTerminal('\t');
        break;
      case 'Ctrl+C':
        // Send interrupt signal
        xterm.focus();
        this.sendKeyToTerminal('\x03');
        break;
      case 'ArrowUp':
        // Navigate command history up
        xterm.focus();
        this.sendKeyToTerminal('\x1b[A'); // ESC [ A (up arrow)
        break;
      case 'ArrowDown':
        // Navigate command history down
        xterm.focus();
        this.sendKeyToTerminal('\x1b[B'); // ESC [ B (down arrow)
        break;
      case 'Ctrl+L':
        // Clear terminal
        xterm.focus();
        this.sendKeyToTerminal('\x0C'); // ASCII Form feed (Ctrl+L)
        break;
      case 'Esc':
        // Escape key
        xterm.focus();
        this.sendKeyToTerminal('\x1b'); // ESC
        break;
      case 'Keyboard':
        // Toggle virtual keyboard
        this.toggleVirtualKeyboard();
        break;
    }
  }

  /**
   * Create command suggestions container
   */
  private createCommandSuggestions(): void {
    if (!this.mobileControls) return;
    
    this.commandSuggestions = document.createElement('div');
    this.commandSuggestions.className = 'terminal-command-suggestions';
    
    // Initially hide suggestions
    this.commandSuggestions.style.display = 'none';
    
    this.mobileControls.appendChild(this.commandSuggestions);
  }

  /**
   * Create and populate command suggestions
   */
  private updateCommandSuggestions(currentInput: string = ''): void {
    if (!this.commandSuggestions) return;
    
    // Clear previous suggestions
    this.commandSuggestions.innerHTML = '';
    
    // If input is empty, show most recent commands
    if (!currentInput.trim()) {
      // Convert Set to Array and take last 5 recent commands
      const recentCmds = Array.from(this.recentCommands).slice(-5).reverse();
      
      if (recentCmds.length > 0) {
        recentCmds.forEach(cmd => this.addCommandSuggestion(cmd));
        this.commandSuggestions.style.display = 'flex';
      } else {
        // If no recent commands, show some common ones
        this.commonCommands.slice(0, 5).forEach(cmd => 
          this.addCommandSuggestion(cmd)
        );
        this.commandSuggestions.style.display = 'flex';
      }
      return;
    }
    
    // Filter commands based on current input
    const suggestions: string[] = [];
    
    // Add matching commands from history
    Array.from(this.recentCommands).forEach(cmd => {
      if (cmd.startsWith(currentInput) && !suggestions.includes(cmd)) {
        suggestions.push(cmd);
      }
    });
    
    // Add matching common commands
    this.commonCommands.forEach(cmd => {
      if (cmd.startsWith(currentInput) && !suggestions.includes(cmd)) {
        suggestions.push(cmd);
      }
    });
    
    // Show suggestions if we have any
    if (suggestions.length > 0) {
      suggestions.slice(0, 5).forEach(cmd => this.addCommandSuggestion(cmd));
      this.commandSuggestions.style.display = 'flex';
    } else {
      this.commandSuggestions.style.display = 'none';
    }
  }

  /**
   * Add a command suggestion button
   */
  private addCommandSuggestion(command: string): void {
    if (!this.commandSuggestions) return;
    
    const suggestionBtn = document.createElement('button');
    suggestionBtn.className = 'command-suggestion';
    suggestionBtn.textContent = command;
    
    suggestionBtn.addEventListener('click', () => {
      // Replace current command with suggested one
      this.replaceCurrentCommandWith(command);
      
      // Focus the terminal
      const xterm = this.getTerminal();
      if (xterm) xterm.focus();
    });
    
    this.commandSuggestions.appendChild(suggestionBtn);
  }

  /**
   * Replace current command in terminal with new text
   */
  private replaceCurrentCommandWith(newCommand: string): void {
    const xterm = this.getTerminal();
    if (!xterm) return;
    
    // This implementation depends on internal details of how terminal.ts 
    // manages the input buffer. A proper implementation would need to:
    // 1. Clear the existing input (Ctrl+U)
    // 2. Type the new command
    
    // For now, we'll use a simplified approach
    // First clear the current line (Ctrl+U)
    this.sendKeyToTerminal('\x15');
    
    // Then send the new command
    this.sendKeyToTerminal(newCommand);
  }

  /**
   * Create virtual keyboard for terminal
   */
  private createVirtualKeyboard(): void {
    if (!this.mobileControls) return;
    
    this.virtualKeyboard = document.createElement('div');
    this.virtualKeyboard.className = 'terminal-virtual-keyboard';
    
    // Initially hide keyboard
    this.virtualKeyboard.style.display = 'none';
    
    // Create keyboard rows
    this.createVirtualKeyboardRows();
    
    this.mobileControls.appendChild(this.virtualKeyboard);
  }

  /**
   * Create rows of virtual keyboard
   */
  private createVirtualKeyboardRows(): void {
    if (!this.virtualKeyboard) return;
    
    // Define keyboard layout
    const keyboardRows = [
      // First row (numbers and symbols)
      ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '-', '='],
      // Second row (qwerty)
      ['q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p', '[', ']', '\\'],
      // Third row (asdf)
      ['a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', ';', '\''],
      // Fourth row (zxcv)
      ['z', 'x', 'c', 'v', 'b', 'n', 'm', ',', '.', '/'],
      // Last row (special keys)
      ['Ctrl', 'Alt', 'Space', 'Tab', '←', '→']
    ];
    
    // Create each row
    keyboardRows.forEach(row => {
      const keyboardRow = document.createElement('div');
      keyboardRow.className = 'keyboard-row';
      
      // Create each key
      row.forEach(key => {
        const keyButton = document.createElement('button');
        keyButton.className = 'keyboard-key';
        
        // Special styling for wider keys
        if (key === 'Space') {
          keyButton.className += ' key-space';
          keyButton.textContent = ' ';
        } else if (key === 'Tab') {
          keyButton.className += ' key-tab';
          keyButton.textContent = '↹';
        } else if (key === 'Ctrl' || key === 'Alt') {
          keyButton.className += ' key-control';
          keyButton.textContent = key;
        } else if (key === '←' || key === '→') {
          keyButton.className += ' key-arrow';
          keyButton.textContent = key;
        } else {
          keyButton.textContent = key;
        }
        
        // Add key press handler
        keyButton.addEventListener('click', () => {
          this.handleVirtualKeyPress(key);
        });
        
        keyboardRow.appendChild(keyButton);
      });
      
      this.virtualKeyboard!.appendChild(keyboardRow);
    });
  }

  /**
   * Handle virtual keyboard key press
   */
  private handleVirtualKeyPress(key: string): void {
    const xterm = this.getTerminal();
    if (!xterm) return;
    
    // Focus the terminal
    xterm.focus();
    
    // Send key to terminal
    switch (key) {
      case 'Space':
        this.sendKeyToTerminal(' ');
        break;
      case 'Tab':
        this.sendKeyToTerminal('\t');
        break;
      case 'Ctrl':
        // Toggle Ctrl modifier
        // This would require state tracking
        break;
      case 'Alt':
        // Toggle Alt modifier
        // This would require state tracking
        break;
      case '←':
        this.sendKeyToTerminal('\x1b[D'); // Left arrow
        break;
      case '→':
        this.sendKeyToTerminal('\x1b[C'); // Right arrow
        break;
      default:
        this.sendKeyToTerminal(key);
        break;
    }
  }

  /**
   * Toggle virtual keyboard visibility
   */
  private toggleVirtualKeyboard(): void {
    if (!this.virtualKeyboard) return;
    
    this.isVirtualKeyboardVisible = !this.isVirtualKeyboardVisible;
    
    if (this.isVirtualKeyboardVisible) {
      this.virtualKeyboard.style.display = 'flex';
    } else {
      this.virtualKeyboard.style.display = 'none';
    }
    
    // Make sure terminal is focused
    const xterm = this.getTerminal();
    if (xterm) xterm.focus();
  }

  /**
   * Send a key sequence to the terminal
   */
  private sendKeyToTerminal(keySequence: string): void {
    const xterm = this.getTerminal();
    if (!xterm) return;
    
    // This mimics keyboard input by sending characters to terminal
    // For a real implementation, this would need to interact with the 
    // internal TerminalApp input handling
    
    // Simple approach that may not work for all cases
    if (typeof xterm.paste === 'function') {
      xterm.paste(keySequence);
    }
  }

  /**
   * Set up touch events for terminal
   */
  private setupTouchEvents(): void {
    if (!this.container) return;
    
    // Set up event handlers for touch gestures
    this.container.addEventListener('touchstart', this.handleTouchStart.bind(this));
    this.container.addEventListener('touchmove', this.handleTouchMove.bind(this));
    this.container.addEventListener('touchend', this.handleTouchEnd.bind(this));
    
    // Handle tap to focus
    this.container.addEventListener('click', () => {
      const xterm = this.getTerminal();
      if (xterm) xterm.focus();
    });
  }

  /**
   * Handle touch start event
   */
  private handleTouchStart(e: TouchEvent): void {
    // Store initial touch position for gesture detection
    if (e.touches.length === 1) {
      this.swipeStartX = e.touches[0].clientX;
      this.swipeStartY = e.touches[0].clientY;
      this.isSwiping = false;
    }
  }

  /**
   * Handle touch move event
   */
  private handleTouchMove(e: TouchEvent): void {
    // Detect horizontal swipe gestures
    if (e.touches.length === 1) {
      const touchX = e.touches[0].clientX;
      const touchY = e.touches[0].clientY;
      
      const deltaX = touchX - this.swipeStartX;
      const deltaY = touchY - this.swipeStartY;
      
      // If horizontal movement is greater than vertical and exceeds threshold
      if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 30) {
        this.isSwiping = true;
      }
    }
  }

  /**
   * Handle touch end event
   */
  private handleTouchEnd(e: TouchEvent): void {
    if (this.isSwiping) {
      const xterm = this.getTerminal();
      if (!xterm) return;
      
      const deltaX = e.changedTouches[0].clientX - this.swipeStartX;
      
      // Swipe right to navigate command history up
      if (deltaX > 70) {
        this.sendKeyToTerminal('\x1b[A'); // Up arrow
      } 
      // Swipe left to navigate command history down
      else if (deltaX < -70) {
        this.sendKeyToTerminal('\x1b[B'); // Down arrow
      }
    }
    
    // Reset swipe state
    this.isSwiping = false;
  }

  /**
   * Called when a command is run in the terminal
   * Used to track command history for suggestions
   */
  public onCommandRun(command: string): void {
    // Add to recent commands if not empty
    if (command && command.trim()) {
      this.recentCommands.add(command.trim());
      
      // Update suggestions
      this.updateCommandSuggestions();
    }
  }

  /**
   * Override handleResize method to resize terminal and mobile UI
   */
  protected handleResize(): void {
    // Call parent resize handler
    super.handleResize();
    
    // Resize mobile UI elements if needed
    if (this.mobileControls) {
      // Ensure mobile controls layout adapts to new size
      // This would depend on specific UI implementation
    }
  }

  /**
   * Override window close to clean up mobile-specific elements
   */
  public closeWindow(): void {
    // Clean up mobile-specific elements
    if (this.mobileControls) {
      this.mobileControls.remove();
      this.mobileControls = null;
    }
    
    // Call parent close
    super.closeWindow();
  }
}
