/**
 * Virtual Keyboard System for HackerSimulator
 * Provides touch-friendly keyboard input for mobile devices
 */

import { PlatformType, platformDetector } from '../core/platform-detector';

/**
 * Key definition interface
 */
export interface VirtualKey {
  value: string;       // The value to input when key is pressed
  display: string;     // Display text/symbol for the key
  width?: number;      // Width of key (1 = standard, 2 = double width, etc.)
  type?: string;       // Key type (character, function, modifier, etc.)
  action?: string;     // Special action for function keys
}

/**
 * Keyboard layout definition
 */
export interface KeyboardLayout {
  name: string;        // Layout name (e.g., 'qwerty', 'numeric', 'email')
  rows: VirtualKey[][]; // Array of key rows
  meta?: any;          // Additional layout metadata
}

/**
 * Virtual Keyboard Options
 */
export interface VirtualKeyboardOptions {
  target?: HTMLElement | null; // Target input element
  layout?: string;            // Initial keyboard layout
  position?: 'bottom' | 'top' | 'float'; // Keyboard position
  autoShow?: boolean;         // Show keyboard automatically when input focused
  predictiveText?: boolean;   // Enable predictive text
  theme?: string;             // Keyboard theme
  onInput?: (value: string) => void; // Input callback
  onClose?: () => void;       // Close callback
}

/**
 * Virtual Keyboard Class
 * Provides a touch-friendly keyboard for text input on mobile devices
 */
export class VirtualKeyboard {
  private element: HTMLElement | null = null;
  private keysContainer: HTMLElement | null = null;
  private predictiveBar: HTMLElement | null = null;
  private target: HTMLElement | null = null;
  private currentLayout: string = 'qwerty';
  private shiftActive: boolean = false;
  private symbolsActive: boolean = false;
  private layouts: Map<string, KeyboardLayout> = new Map();
  private isVisible: boolean = false;
  private options: VirtualKeyboardOptions = {
    position: 'bottom',
    autoShow: true,
    predictiveText: false
  };
  
  // Words for predictive text suggestions
  private commonWords: string[] = [
    'the', 'to', 'and', 'in', 'is', 'it', 'you', 'that', 'was', 'for',
    'on', 'are', 'with', 'as', 'this', 'be', 'at', 'have', 'from', 'or',
    'but', 'not', 'by', 'what', 'all', 'when', 'we', 'can', 'an', 'your',
    'which', 'their', 'if', 'will', 'one', 'about', 'up', 'there', 'so', 'out'
  ];
  
  // For tracking input position and content
  private inputValue: string = '';
  private cursorPosition: number = 0;
  
  // Recent words typed by the user
  private recentWords: string[] = [];
  
  // Singleton instance
  private static instance: VirtualKeyboard | null = null;
  
  /**
   * Get the singleton instance of VirtualKeyboard
   */
  public static getInstance(): VirtualKeyboard {
    if (!VirtualKeyboard.instance) {
      VirtualKeyboard.instance = new VirtualKeyboard();
    }
    return VirtualKeyboard.instance;
  }
  
  /**
   * Constructor
   */
  private constructor() {
    // Register default layouts
    this.registerDefaultLayouts();
    
    // Create keyboard DOM element
    this.createKeyboardElement();
  }
  
  /**
   * Initialize with options
   */
  public init(options: Partial<VirtualKeyboardOptions> = {}): void {
    // Merge options
    this.options = { ...this.options, ...options };
    
    // Set target if provided
    if (options.target) {
      this.setTarget(options.target);
    }
    
    // Set initial layout
    if (options.layout) {
      this.setLayout(options.layout);
    }
    
    // Add global handlers for auto-showing keyboard on input focus
    if (this.options.autoShow) {
      this.setupAutoShowHandlers();
    }
  }
  
  /**
   * Set up handlers to automatically show keyboard when inputs are focused
   */  private setupAutoShowHandlers(): void {
    // Only add these handlers on mobile
    if (platformDetector.getPlatformType() !== PlatformType.MOBILE) {
      return;
    }
    
    // Add global focus handler for input elements
    document.addEventListener('focusin', (e) => {
      const target = e.target as HTMLElement;
      if (this.isInputElement(target)) {
        // Set this input as the target
        this.setTarget(target);
        
        // Show keyboard
        this.show();
        
        // Prevent native keyboard on mobile
        if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
          target.blur();
          // Ensure our virtual keyboard is shown but no native keyboard
          setTimeout(() => {
            // Focus back but prevent native keyboard
            target.focus();
            if ('readOnly' in target) {
              const originalReadOnly = target.readOnly;
              target.readOnly = true;
              setTimeout(() => {
                target.readOnly = originalReadOnly;
              }, 100);
            }
          }, 100);
        }
      }
    });
  }
  
  /**
   * Check if an element is an input element that should trigger the keyboard
   */
  private isInputElement(element: HTMLElement): boolean {
    const tagName = element.tagName.toLowerCase();
    return (
      tagName === 'input' ||
      tagName === 'textarea' ||
      element.isContentEditable
    );
  }
  
  /**
   * Create the keyboard DOM element
   */
  private createKeyboardElement(): void {
    // Create main container
    this.element = document.createElement('div');
    this.element.className = 'virtual-keyboard';
    
    // Add predictive text bar
    this.predictiveBar = document.createElement('div');
    this.predictiveBar.className = 'predictive-text-bar';
    this.element.appendChild(this.predictiveBar);
    
    // Add keys container
    this.keysContainer = document.createElement('div');
    this.keysContainer.className = 'keyboard-keys';
    this.element.appendChild(this.keysContainer);
    
    // Add to document body (hidden initially)
    document.body.appendChild(this.element);
    this.element.style.display = 'none';
  }
  
  /**
   * Register default keyboard layouts
   */
  private registerDefaultLayouts(): void {
    // QWERTY layout (standard)
    this.registerLayout({
      name: 'qwerty',
      rows: [
        // First row
        [
          { value: 'q', display: 'q' },
          { value: 'w', display: 'w' },
          { value: 'e', display: 'e' },
          { value: 'r', display: 'r' },
          { value: 't', display: 't' },
          { value: 'y', display: 'y' },
          { value: 'u', display: 'u' },
          { value: 'i', display: 'i' },
          { value: 'o', display: 'o' },
          { value: 'p', display: 'p' }
        ],
        // Second row
        [
          { value: 'a', display: 'a' },
          { value: 's', display: 's' },
          { value: 'd', display: 'd' },
          { value: 'f', display: 'f' },
          { value: 'g', display: 'g' },
          { value: 'h', display: 'h' },
          { value: 'j', display: 'j' },
          { value: 'k', display: 'k' },
          { value: 'l', display: 'l' }
        ],
        // Third row
        [
          { value: 'shift', display: '⇧', type: 'modifier', width: 1.5 },
          { value: 'z', display: 'z' },
          { value: 'x', display: 'x' },
          { value: 'c', display: 'c' },
          { value: 'v', display: 'v' },
          { value: 'b', display: 'b' },
          { value: 'n', display: 'n' },
          { value: 'm', display: 'm' },
          { value: 'backspace', display: '⌫', type: 'function', width: 1.5 }
        ],
        // Fourth row (bottom)
        [
          { value: 'symbols', display: '123', type: 'modifier', width: 1.5 },
          { value: ',', display: ',' },
          { value: ' ', display: 'Space', width: 4 },
          { value: '.', display: '.' },
          { value: 'enter', display: 'Enter', type: 'function', width: 2 }
        ]
      ]
    });
    
    // Symbols layout
    this.registerLayout({
      name: 'symbols',
      rows: [
        // First row
        [
          { value: '1', display: '1' },
          { value: '2', display: '2' },
          { value: '3', display: '3' },
          { value: '4', display: '4' },
          { value: '5', display: '5' },
          { value: '6', display: '6' },
          { value: '7', display: '7' },
          { value: '8', display: '8' },
          { value: '9', display: '9' },
          { value: '0', display: '0' }
        ],
        // Second row
        [
          { value: '@', display: '@' },
          { value: '#', display: '#' },
          { value: '$', display: '$' },
          { value: '%', display: '%' },
          { value: '&', display: '&' },
          { value: '*', display: '*' },
          { value: '-', display: '-' },
          { value: '+', display: '+' },
          { value: '(', display: '(' },
          { value: ')', display: ')' }
        ],
        // Third row
        [
          { value: 'symbols2', display: '=\\<', type: 'modifier', width: 1.5 },
          { value: '!', display: '!' },
          { value: '"', display: '"' },
          { value: '\'', display: '\'' },
          { value: ':', display: ':' },
          { value: ';', display: ';' },
          { value: '/', display: '/' },
          { value: '?', display: '?' },
          { value: 'backspace', display: '⌫', type: 'function', width: 1.5 }
        ],
        // Fourth row (bottom)
        [
          { value: 'letters', display: 'ABC', type: 'modifier', width: 1.5 },
          { value: ',', display: ',' },
          { value: ' ', display: 'Space', width: 4 },
          { value: '.', display: '.' },
          { value: 'enter', display: 'Enter', type: 'function', width: 2 }
        ]
      ]
    });
    
    // Extended symbols layout
    this.registerLayout({
      name: 'symbols2',
      rows: [
        // First row
        [
          { value: '~', display: '~' },
          { value: '`', display: '`' },
          { value: '|', display: '|' },
          { value: '•', display: '•' },
          { value: '√', display: '√' },
          { value: 'π', display: 'π' },
          { value: '÷', display: '÷' },
          { value: '×', display: '×' },
          { value: '¶', display: '¶' },
          { value: '∆', display: '∆' }
        ],
        // Second row
        [
          { value: '£', display: '£' },
          { value: '¢', display: '¢' },
          { value: '€', display: '€' },
          { value: '¥', display: '¥' },
          { value: '^', display: '^' },
          { value: '°', display: '°' },
          { value: '=', display: '=' },
          { value: '{', display: '{' },
          { value: '}', display: '}' },
          { value: '\\', display: '\\' }
        ],
        // Third row
        [
          { value: 'symbols', display: '123', type: 'modifier', width: 1.5 },
          { value: '[', display: '[' },
          { value: ']', display: ']' },
          { value: '<', display: '<' },
          { value: '>', display: '>' },
          { value: '_', display: '_' },
          { value: '±', display: '±' },
          { value: '§', display: '§' },
          { value: 'backspace', display: '⌫', type: 'function', width: 1.5 }
        ],
        // Fourth row (bottom)
        [
          { value: 'letters', display: 'ABC', type: 'modifier', width: 1.5 },
          { value: ',', display: ',' },
          { value: ' ', display: 'Space', width: 4 },
          { value: '.', display: '.' },
          { value: 'enter', display: 'Enter', type: 'function', width: 2 }
        ]
      ]
    });
    
    // Numeric keyboard layout
    this.registerLayout({
      name: 'numeric',
      rows: [
        // First row
        [
          { value: '1', display: '1' },
          { value: '2', display: '2' },
          { value: '3', display: '3' }
        ],
        // Second row
        [
          { value: '4', display: '4' },
          { value: '5', display: '5' },
          { value: '6', display: '6' }
        ],
        // Third row
        [
          { value: '7', display: '7' },
          { value: '8', display: '8' },
          { value: '9', display: '9' }
        ],
        // Fourth row
        [
          { value: '.', display: '.' },
          { value: '0', display: '0' },
          { value: 'backspace', display: '⌫', type: 'function' }
        ],
        // Fifth row (bottom)
        [
          { value: 'cancel', display: 'Cancel', type: 'function', width: 1.5 },
          { value: ' ', display: 'Space', width: 2 },
          { value: 'enter', display: 'Enter', type: 'function', width: 1.5 }
        ]
      ]
    });
    
    // Email keyboard layout
    this.registerLayout({
      name: 'email',
      rows: [
        // First row
        [
          { value: 'q', display: 'q' },
          { value: 'w', display: 'w' },
          { value: 'e', display: 'e' },
          { value: 'r', display: 'r' },
          { value: 't', display: 't' },
          { value: 'y', display: 'y' },
          { value: 'u', display: 'u' },
          { value: 'i', display: 'i' },
          { value: 'o', display: 'o' },
          { value: 'p', display: 'p' }
        ],
        // Second row
        [
          { value: 'a', display: 'a' },
          { value: 's', display: 's' },
          { value: 'd', display: 'd' },
          { value: 'f', display: 'f' },
          { value: 'g', display: 'g' },
          { value: 'h', display: 'h' },
          { value: 'j', display: 'j' },
          { value: 'k', display: 'k' },
          { value: 'l', display: 'l' }
        ],
        // Third row
        [
          { value: 'shift', display: '⇧', type: 'modifier', width: 1.5 },
          { value: 'z', display: 'z' },
          { value: 'x', display: 'x' },
          { value: 'c', display: 'c' },
          { value: 'v', display: 'v' },
          { value: 'b', display: 'b' },
          { value: 'n', display: 'n' },
          { value: 'm', display: 'm' },
          { value: 'backspace', display: '⌫', type: 'function', width: 1.5 }
        ],
        // Fourth row (bottom)
        [
          { value: 'symbols', display: '123', type: 'modifier', width: 1.5 },
          { value: '@', display: '@' },
          { value: '_', display: '_' },
          { value: '.', display: '.' },
          { value: ' ', display: 'Space', width: 2 },
          { value: '.com', display: '.com', width: 1.5 },
          { value: 'enter', display: 'Enter', type: 'function', width: 1.5 }
        ]
      ]
    });
  }
  
  /**
   * Register a custom keyboard layout
   */
  public registerLayout(layout: KeyboardLayout): void {
    this.layouts.set(layout.name, layout);
  }
  
  /**
   * Set the active keyboard layout
   */
  public setLayout(layoutName: string): boolean {
    if (!this.layouts.has(layoutName)) {
      console.error(`Keyboard layout "${layoutName}" not found`);
      return false;
    }
    
    this.currentLayout = layoutName;
    
    // If keyboard is visible, update the display
    if (this.isVisible) {
      this.renderKeys();
    }
    
    return true;
  }
  
  /**
   * Set the target input element
   */
  public setTarget(target: HTMLElement): void {
    this.target = target;
    
    // Get current value from target
    this.updateInputValue();
    
    // Update predictive text based on new target
    if (this.options.predictiveText) {
      this.updatePredictiveText();
    }
  }
  
  /**
   * Update the internal input value from the target
   */
  private updateInputValue(): void {
    if (!this.target) return;
    
    // Extract value from different element types
    if (this.target instanceof HTMLInputElement || this.target instanceof HTMLTextAreaElement) {
      this.inputValue = this.target.value;
      this.cursorPosition = this.target.selectionStart || this.inputValue.length;
    } else if (this.target.isContentEditable) {
      // For contentEditable elements, this is more complex
      // For simplicity, we're just getting the text content
      this.inputValue = this.target.textContent || '';
      this.cursorPosition = this.inputValue.length;
      
      // In a real implementation, would need to handle selection within contentEditable
    }
  }
  
  /**
   * Show the virtual keyboard
   */
  public show(): void {
    if (!this.element) return;
    
    // Make keyboard visible
    this.element.style.display = 'flex';
    this.isVisible = true;
    
    // Position the keyboard based on options
    this.positionKeyboard();
    
    // Render the keys for current layout
    this.renderKeys();
    
    // Update predictive text if enabled
    if (this.options.predictiveText) {
      this.updatePredictiveText();
    }
    
    // Add 'keyboard-visible' class to body for styling
    document.body.classList.add('keyboard-visible');
    
    // Dispatch keyboard show event
    window.dispatchEvent(new CustomEvent('virtualkeyboardshow'));
  }
  
  /**
   * Hide the virtual keyboard
   */
  public hide(): void {
    if (!this.element || !this.isVisible) return;
    
    // Hide keyboard
    this.element.style.display = 'none';
    this.isVisible = false;
    
    // Remove 'keyboard-visible' class from body
    document.body.classList.remove('keyboard-visible');
    
    // Dispatch keyboard hide event
    window.dispatchEvent(new CustomEvent('virtualkeyboardhide'));
    
    // Call onClose callback if defined
    if (this.options.onClose) {
      this.options.onClose();
    }
  }
  
  /**
   * Position the keyboard on screen
   */
  private positionKeyboard(): void {
    if (!this.element) return;
    
    // Apply position based on options
    switch (this.options.position) {
      case 'top':
        this.element.style.top = '0';
        this.element.style.bottom = 'auto';
        break;
      case 'float':
        // For float, we would position near the input
        // This is more complex and would need input position calculation
        // For now, default to bottom
        this.element.style.top = 'auto';
        this.element.style.bottom = '0';
        break;
      case 'bottom':
      default:
        this.element.style.top = 'auto';
        this.element.style.bottom = '0';
        break;
    }
  }
  
  /**
   * Render the keys based on current layout
   */
  private renderKeys(): void {
    if (!this.keysContainer) return;
    
    // Clear existing keys
    this.keysContainer.innerHTML = '';
    
    // Get current layout
    const layout = this.layouts.get(this.currentLayout);
    if (!layout) return;
    
    // Create each row
    layout.rows.forEach(row => {
      const rowElement = document.createElement('div');
      rowElement.className = 'keyboard-row';
      
      // Create each key in the row
      row.forEach(key => {
        const keyElement = document.createElement('button');
        keyElement.className = 'keyboard-key';
        keyElement.setAttribute('type', 'button');
        keyElement.setAttribute('data-value', key.value);
        
        // Apply key type class if specified
        if (key.type) {
          keyElement.classList.add(`key-${key.type}`);
        }
        
        // Apply special classes for modifier keys
        if (key.value === 'shift' && this.shiftActive) {
          keyElement.classList.add('active');
        }
        if ((key.value === 'symbols' || key.value === 'symbols2') && this.symbolsActive) {
          keyElement.classList.add('active');
        }
        
        // Transform display text based on state
        let displayText = key.display;
        if (key.value.length === 1 && this.shiftActive) {
          // Apply shift to single character keys
          displayText = key.value.toUpperCase();
        }
        
        // Set inner text
        keyElement.textContent = displayText;
        
        // Set width if specified
        if (key.width) {
          keyElement.style.flex = `${key.width}`;
        }
        
        // Add event handler
        keyElement.addEventListener('click', () => {
          this.handleKeyPress(key);
        });
        
        // Add to row
        rowElement.appendChild(keyElement);
      });
      
      // Add row to keyboard
      this.keysContainer!.appendChild(rowElement);
    });
  }
  
  /**
   * Handle key press
   */
  private handleKeyPress(key: VirtualKey): void {
    // Play key sound/haptic feedback
    this.playKeyFeedback();
    
    // Handle different key types
    switch (key.type) {
      case 'modifier':
        this.handleModifierKey(key.value);
        break;
      case 'function':
        this.handleFunctionKey(key.value);
        break;
      default:
        // Regular character input
        this.handleCharacterInput(key.value);
        break;
    }
  }
  
  /**
   * Handle modifier keys (shift, symbols)
   */
  private handleModifierKey(value: string): void {
    switch (value) {
      case 'shift':
        // Toggle shift state
        this.shiftActive = !this.shiftActive;
        this.renderKeys(); // Update display
        break;
      case 'symbols':
        // Switch to symbols layout
        this.symbolsActive = true;
        this.setLayout('symbols');
        break;
      case 'symbols2':
        // Switch to extended symbols layout
        this.symbolsActive = true;
        this.setLayout('symbols2');
        break;
      case 'letters':
        // Switch back to standard layout
        this.symbolsActive = false;
        this.setLayout('qwerty');
        break;
    }
  }
  
  /**
   * Handle function keys (enter, backspace, etc.)
   */
  private handleFunctionKey(value: string): void {
    switch (value) {
      case 'backspace':
        this.handleBackspace();
        break;
      case 'enter':
        this.handleEnter();
        break;
      case 'cancel':
        this.hide();
        break;
    }
  }
  
  /**
   * Handle regular character input
   */
  private handleCharacterInput(value: string): void {
    // If no target, do nothing
    if (!this.target) return;
    
    // Apply shift if active and is a single character
    let inputChar = value;
    if (value.length === 1 && this.shiftActive) {
      inputChar = value.toUpperCase();
      
      // Turn off shift after one character
      this.shiftActive = false;
      this.renderKeys();
    }
    
    // Update internal value
    const beforeCursor = this.inputValue.substring(0, this.cursorPosition);
    const afterCursor = this.inputValue.substring(this.cursorPosition);
    this.inputValue = beforeCursor + inputChar + afterCursor;
    this.cursorPosition += inputChar.length;
    
    // Update target element
    this.updateTargetValue();
    
    // Update predictive text
    if (this.options.predictiveText) {
      this.updatePredictiveText();
    }
    
    // Call input callback if defined
    if (this.options.onInput) {
      this.options.onInput(this.inputValue);
    }
  }
  
  /**
   * Handle backspace key
   */
  private handleBackspace(): void {
    // If no target or cursor at beginning, do nothing
    if (!this.target || this.cursorPosition === 0) return;
    
    // Remove character before cursor
    const beforeCursor = this.inputValue.substring(0, this.cursorPosition - 1);
    const afterCursor = this.inputValue.substring(this.cursorPosition);
    this.inputValue = beforeCursor + afterCursor;
    this.cursorPosition--;
    
    // Update target element
    this.updateTargetValue();
    
    // Update predictive text
    if (this.options.predictiveText) {
      this.updatePredictiveText();
    }
    
    // Call input callback if defined
    if (this.options.onInput) {
      this.options.onInput(this.inputValue);
    }
  }
  
  /**
   * Handle enter key
   */
  private handleEnter(): void {
    // If no target, do nothing
    if (!this.target) return;
    
    // Add newline or submit based on target
    if (this.target instanceof HTMLTextAreaElement) {
      // For textarea, insert a newline
      this.handleCharacterInput('\n');
    } else if (this.target instanceof HTMLInputElement) {
      // For normal inputs, trigger form submit if in a form
      const form = this.target.form;
      if (form) {
        // Create and dispatch submit event
        const submitEvent = new Event('submit', { cancelable: true });
        const submitted = form.dispatchEvent(submitEvent);
        
        if (submitted) {
          // If event wasn't cancelled, actually submit the form
          form.submit();
        }
      }
      
      // Hide keyboard after enter on input
      this.hide();
    } else {
      // For contentEditable, insert a newline
      this.handleCharacterInput('\n');
    }
  }
  
  /**
   * Update the value in the target element
   */
  private updateTargetValue(): void {
    if (!this.target) return;
    
    if (this.target instanceof HTMLInputElement || this.target instanceof HTMLTextAreaElement) {
      // For input elements, update value and selection
      this.target.value = this.inputValue;
      this.target.setSelectionRange(this.cursorPosition, this.cursorPosition);
      
      // Trigger input event
      this.target.dispatchEvent(new Event('input', { bubbles: true }));
    } else if (this.target.isContentEditable) {
      // For contentEditable elements
      this.target.textContent = this.inputValue;
      
      // Update cursor position in contentEditable (simplified)
      // In real implementation, should use Range and Selection API
      // to set proper caret position
      
      // Trigger input event
      this.target.dispatchEvent(new Event('input', { bubbles: true }));
    }
  }
  
  /**
   * Update predictive text suggestions
   */
  private updatePredictiveText(): void {
    if (!this.predictiveBar || !this.options.predictiveText) return;
    
    // Clear previous suggestions
    this.predictiveBar.innerHTML = '';
    
    // Get current word being typed
    const text = this.inputValue.substring(0, this.cursorPosition);
    const words = text.split(/\s+/);
    const currentWord = words[words.length - 1].toLowerCase();
    
    // If current word is empty, no suggestions needed
    if (!currentWord) {
      this.predictiveBar.style.display = 'none';
      return;
    }
    
    // Combine recent and common words, prioritizing recent
    const allWords = [...new Set([...this.recentWords, ...this.commonWords])];
    
    // Filter words that match current input
    const suggestions = allWords
      .filter(word => word.toLowerCase().startsWith(currentWord) && word.length > currentWord.length)
      .slice(0, 5); // Limit to 5 suggestions
    
    // If no suggestions, hide bar
    if (suggestions.length === 0) {
      this.predictiveBar.style.display = 'none';
      return;
    }
    
    // Show suggestions
    this.predictiveBar.style.display = 'flex';
    
    // Create buttons for each suggestion
    suggestions.forEach(word => {
      const button = document.createElement('button');
      button.className = 'prediction-item';
      button.textContent = word;
      
      // Add click handler
      button.addEventListener('click', () => {
        this.applySuggestion(word, currentWord);
      });
      
      this.predictiveBar!.appendChild(button);
    });
  }
  
  /**
   * Apply selected word suggestion
   */
  private applySuggestion(suggestion: string, currentWord: string): void {
    // If no target, do nothing
    if (!this.target) return;
    
    // Find the start position of current word
    const wordStart = this.cursorPosition - currentWord.length;
    
    // Replace current word with suggestion
    const beforeWord = this.inputValue.substring(0, wordStart);
    const afterCursor = this.inputValue.substring(this.cursorPosition);
    this.inputValue = beforeWord + suggestion + afterCursor;
    
    // Update cursor position
    this.cursorPosition = wordStart + suggestion.length;
    
    // Update target element
    this.updateTargetValue();
    
    // Add to recent words
    this.addToRecentWords(suggestion);
    
    // Update predictive text for next word
    this.updatePredictiveText();
    
    // Call input callback if defined
    if (this.options.onInput) {
      this.options.onInput(this.inputValue);
    }
  }
  
  /**
   * Add a word to recent words list
   */
  private addToRecentWords(word: string): void {
    // Don't add short words or duplicate the last entry
    if (word.length < 3 || this.recentWords[0] === word) return;
    
    // Add to the beginning of the array
    this.recentWords.unshift(word.toLowerCase());
    
    // Limit size of recent words list
    if (this.recentWords.length > 50) {
      this.recentWords = this.recentWords.slice(0, 50);
    }
  }
  
  /**
   * Play feedback for key press (sound/haptic)
   */
  private playKeyFeedback(): void {
    // Trigger haptic feedback if available
    if (navigator.vibrate) {
      navigator.vibrate(10); // 10ms vibration
    }
    
    // Could also play sound effect here
  }
  
  /**
   * Get current input value
   */
  public getValue(): string {
    return this.inputValue;
  }
  
  /**
   * Set current input value
   */
  public setValue(value: string, cursorPos: number = -1): void {
    this.inputValue = value;
    this.cursorPosition = cursorPos >= 0 ? cursorPos : value.length;
    
    // Update target if set
    if (this.target) {
      this.updateTargetValue();
    }
    
    // Update predictive text
    if (this.options.predictiveText) {
      this.updatePredictiveText();
    }
  }
  
  /**
   * Check if keyboard is currently visible
   */
  public isKeyboardVisible(): boolean {
    return this.isVisible;
  }
  
  /**
   * Toggle keyboard visibility
   */
  public toggle(): void {
    if (this.isVisible) {
      this.hide();
    } else {
      this.show();
    }
  }
}
