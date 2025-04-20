import { OS } from '../core/os';
import { GuiApplication } from '../core/gui-application';

/**
 * Calculator App for the Hacker Game
 * Provides basic arithmetic functions and potentially scientific operations
 */
export class CalculatorApp extends GuiApplication {
  private display: HTMLElement | null = null;
  private currentValue: string = '0';
  private previousValue: string = '';
  private operator: string = '';
  private waitingForOperand: boolean = true;
  private memoryValue: number = 0;

  constructor(os: OS) {
    super(os);
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'calculator';
  }

  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    this.render();
    this.setupEventListeners();
  }

  /**
   * Render the calculator UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="calculator-app">
        <div class="calculator-display">
          <div class="calculator-previous-operand"></div>
          <div class="calculator-current-operand">0</div>
        </div>
        <div class="calculator-buttons">
          <button class="calculator-button memory-clear">MC</button>
          <button class="calculator-button memory-recall">MR</button>
          <button class="calculator-button memory-add">M+</button>
          <button class="calculator-button memory-subtract">M-</button>
          <button class="calculator-button clear">C</button>
          <button class="calculator-button all-clear">AC</button>
          <button class="calculator-button number">7</button>
          <button class="calculator-button number">8</button>
          <button class="calculator-button number">9</button>
          <button class="calculator-button operator">/</button>
          <button class="calculator-button sqr">x²</button>
          <button class="calculator-button number">4</button>
          <button class="calculator-button number">5</button>
          <button class="calculator-button number">6</button>
          <button class="calculator-button operator">*</button>
          <button class="calculator-button sqrt">√</button>
          <button class="calculator-button number">1</button>
          <button class="calculator-button number">2</button>
          <button class="calculator-button number">3</button>
          <button class="calculator-button operator">-</button>
          <button class="calculator-button percentage">%</button>
          <button class="calculator-button number">0</button>
          <button class="calculator-button decimal">.</button>
          <button class="calculator-button equals">=</button>
          <button class="calculator-button operator">+</button>
          <button class="calculator-button negate">±</button>
        </div>
      </div>
      <style>
        .calculator-app {
          background-color: #202020;
          border-radius: 8px;
          width: 100%;
          height: 100%;
          display: flex;
          flex-direction: column;
          overflow: hidden;
        }
        .calculator-display {
          background-color: #111;
          color: #fff;
          padding: 20px;
          text-align: right;
          font-family: 'Courier New', monospace;
        }
        .calculator-previous-operand {
          color: rgba(255, 255, 255, 0.7);
          font-size: 16px;
          height: 20px;
          margin-bottom: 5px;
        }
        .calculator-current-operand {
          font-size: 36px;
          min-height: 40px;
        }
        .calculator-buttons {
          display: grid;
          grid-template-columns: repeat(5, 1fr);
          grid-gap: 1px;
          flex: 1;
        }
        .calculator-button {
          background-color: #333;
          border: none;
          color: white;
          font-size: 18px;
          cursor: pointer;
          transition: background-color 0.2s;
        }
        .calculator-button:hover {
          background-color: #555;
        }
        .calculator-button.operator, .calculator-button.equals {
          background-color: #ff9500;
        }
        .calculator-button.operator:hover, .calculator-button.equals:hover {
          background-color: #ffb74d;
        }
        .calculator-button.clear, .calculator-button.all-clear, 
        .calculator-button.memory-clear, .calculator-button.memory-recall,
        .calculator-button.memory-add, .calculator-button.memory-subtract {
          background-color: #a5a5a5;
          color: black;
        }
        .calculator-button.clear:hover, .calculator-button.all-clear:hover,
        .calculator-button.memory-clear:hover, .calculator-button.memory-recall:hover,
        .calculator-button.memory-add:hover, .calculator-button.memory-subtract:hover {
          background-color: #d4d4d2;
        }
      </style>
    `;
    
    this.display = this.container.querySelector('.calculator-current-operand');
  }

  /**
   * Setup event listeners for calculator buttons
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // Number buttons
    const numberButtons = this.container.querySelectorAll('.calculator-button.number');
    numberButtons.forEach(button => {
      button.addEventListener('click', () => {
        this.inputDigit(button.textContent || '');
      });
    });
    
    // Decimal button
    const decimalButton = this.container.querySelector('.calculator-button.decimal');
    decimalButton?.addEventListener('click', () => {
      this.inputDecimal();
    });
    
    // Operator buttons
    const operatorButtons = this.container.querySelectorAll('.calculator-button.operator');
    operatorButtons.forEach(button => {
      button.addEventListener('click', () => {
        this.handleOperator(button.textContent || '');
      });
    });
    
    // Equals button
    const equalsButton = this.container.querySelector('.calculator-button.equals');
    equalsButton?.addEventListener('click', () => {
      this.handleEquals();
    });
    
    // Clear button
    const clearButton = this.container.querySelector('.calculator-button.clear');
    clearButton?.addEventListener('click', () => {
      this.clear();
    });
    
    // All Clear button
    const allClearButton = this.container.querySelector('.calculator-button.all-clear');
    allClearButton?.addEventListener('click', () => {
      this.allClear();
    });
    
    // Negate button
    const negateButton = this.container.querySelector('.calculator-button.negate');
    negateButton?.addEventListener('click', () => {
      this.negate();
    });
    
    // Percentage button
    const percentageButton = this.container.querySelector('.calculator-button.percentage');
    percentageButton?.addEventListener('click', () => {
      this.percentage();
    });
    
    // Square button
    const sqrButton = this.container.querySelector('.calculator-button.sqr');
    sqrButton?.addEventListener('click', () => {
      this.square();
    });
    
    // Square root button
    const sqrtButton = this.container.querySelector('.calculator-button.sqrt');
    sqrtButton?.addEventListener('click', () => {
      this.squareRoot();
    });
    
    // Memory buttons
    const memClearButton = this.container.querySelector('.calculator-button.memory-clear');
    memClearButton?.addEventListener('click', () => {
      this.memoryClear();
    });
    
    const memRecallButton = this.container.querySelector('.calculator-button.memory-recall');
    memRecallButton?.addEventListener('click', () => {
      this.memoryRecall();
    });
    
    const memAddButton = this.container.querySelector('.calculator-button.memory-add');
    memAddButton?.addEventListener('click', () => {
      this.memoryAdd();
    });
    
    const memSubtractButton = this.container.querySelector('.calculator-button.memory-subtract');
    memSubtractButton?.addEventListener('click', () => {
      this.memorySubtract();
    });
    
    // Add keyboard support
    document.addEventListener('keydown', this.handleKeyDown.bind(this));
  }

  /**
   * Handle keyboard input
   */
  private handleKeyDown(event: KeyboardEvent): void {
    // Ignore keyboard events when calculator is not focused
    if (!this.container?.contains(document.activeElement)) return;
    
    // Prevent default behaviors
    event.preventDefault();
    
    const key = event.key;
    
    if (/[0-9]/.test(key)) {
      this.inputDigit(key);
    } else if (key === '.') {
      this.inputDecimal();
    } else if (['+', '-', '*', '/'].includes(key)) {
      this.handleOperator(key);
    } else if (key === 'Enter' || key === '=') {
      this.handleEquals();
    } else if (key === 'Escape') {
      this.allClear();
    } else if (key === 'Backspace') {
      this.clear();
    } else if (key === '%') {
      this.percentage();
    }
  }

  /**
   * Update the display
   */
  private updateDisplay(): void {
    if (!this.display) return;
    this.display.textContent = this.currentValue;
    
    // Update previous operand display
    const previousOperandDisplay = this.container?.querySelector('.calculator-previous-operand');
    if (previousOperandDisplay) {
      if (this.previousValue && this.operator) {
        previousOperandDisplay.textContent = `${this.previousValue} ${this.operator}`;
      } else {
        previousOperandDisplay.textContent = '';
      }
    }
  }

  /**
   * Input a digit
   */
  private inputDigit(digit: string): void {
    if (this.waitingForOperand) {
      this.currentValue = digit;
      this.waitingForOperand = false;
    } else {
      // Don't allow more than 12 digits to prevent overflow
      if (this.currentValue.replace(/[^0-9]/g, '').length >= 12) return;
      this.currentValue = this.currentValue === '0' ? digit : this.currentValue + digit;
    }
    this.updateDisplay();
  }

  /**
   * Input decimal point
   */
  private inputDecimal(): void {
    if (this.waitingForOperand) {
      this.currentValue = '0.';
      this.waitingForOperand = false;
    } else if (this.currentValue.indexOf('.') === -1) {
      this.currentValue += '.';
    }
    this.updateDisplay();
  }

  /**
   * Handle operator
   */
  private handleOperator(nextOperator: string): void {
    const inputValue = parseFloat(this.currentValue);
    
    if (this.operator && !this.waitingForOperand) {
      this.calculate();
    } else {
      this.previousValue = this.currentValue;
    }
    
    this.operator = nextOperator;
    this.waitingForOperand = true;
    this.updateDisplay();
  }

  /**
   * Perform calculation
   */
  private calculate(): void {
    const previousValue = parseFloat(this.previousValue);
    const currentValue = parseFloat(this.currentValue);
    
    let result = 0;
    
    switch (this.operator) {
      case '+':
        result = previousValue + currentValue;
        break;
      case '-':
        result = previousValue - currentValue;
        break;
      case '*':
        result = previousValue * currentValue;
        break;
      case '/':
        if (currentValue === 0) {
          this.currentValue = 'Error';
          this.waitingForOperand = true;
          this.updateDisplay();
          return;
        }
        result = previousValue / currentValue;
        break;
      default:
        return;
    }
    
    // Format the result
    this.currentValue = this.formatResult(result);
    this.previousValue = this.currentValue;
    this.updateDisplay();
  }

  /**
   * Format calculation result
   */
  private formatResult(value: number): string {
    // Check for errors
    if (!isFinite(value)) return 'Error';
    
    const stringValue = value.toString();
    
    // If the result is an integer, return it directly
    if (Number.isInteger(value)) return stringValue;
    
    // Limit decimal places to 10 to prevent display overflow
    if (stringValue.includes('.') && stringValue.split('.')[1].length > 10) {
      return value.toFixed(10).replace(/\.?0+$/, '');
    }
    
    return stringValue;
  }

  /**
   * Handle equals button
   */
  private handleEquals(): void {
    if (!this.operator || this.waitingForOperand) return;
    
    this.calculate();
    this.operator = '';
    this.waitingForOperand = true;
  }

  /**
   * Clear current input
   */
  private clear(): void {
    this.currentValue = '0';
    this.waitingForOperand = true;
    this.updateDisplay();
  }

  /**
   * Clear all (reset calculator)
   */
  private allClear(): void {
    this.currentValue = '0';
    this.previousValue = '';
    this.operator = '';
    this.waitingForOperand = true;
    this.updateDisplay();
  }

  /**
   * Negate the current value
   */
  private negate(): void {
    const value = parseFloat(this.currentValue);
    if (value === 0) return;
    
    this.currentValue = (-value).toString();
    this.updateDisplay();
  }

  /**
   * Convert to percentage
   */
  private percentage(): void {
    const value = parseFloat(this.currentValue);
    this.currentValue = (value / 100).toString();
    this.waitingForOperand = true;
    this.updateDisplay();
  }

  /**
   * Square the current value
   */
  private square(): void {
    const value = parseFloat(this.currentValue);
    this.currentValue = this.formatResult(value * value);
    this.waitingForOperand = true;
    this.updateDisplay();
  }

  /**
   * Calculate square root
   */
  private squareRoot(): void {
    const value = parseFloat(this.currentValue);
    if (value < 0) {
      this.currentValue = 'Error';
    } else {
      this.currentValue = this.formatResult(Math.sqrt(value));
    }
    this.waitingForOperand = true;
    this.updateDisplay();
  }
  
  /**
   * Memory clear
   */
  private memoryClear(): void {
    this.memoryValue = 0;
  }
  
  /**
   * Memory recall
   */
  private memoryRecall(): void {
    this.currentValue = this.memoryValue.toString();
    this.waitingForOperand = false;
    this.updateDisplay();
  }
  
  /**
   * Memory add
   */
  private memoryAdd(): void {
    this.memoryValue += parseFloat(this.currentValue);
  }
  
  /**
   * Memory subtract
   */
  private memorySubtract(): void {
    this.memoryValue -= parseFloat(this.currentValue);
  }
}
