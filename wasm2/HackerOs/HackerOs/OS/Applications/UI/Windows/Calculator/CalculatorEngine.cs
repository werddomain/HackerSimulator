using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HackerOs.OS.Applications.UI.Windows.Calculator;

/// <summary>
/// Core engine for calculator operations
/// </summary>
public class CalculatorEngine
{
    // Engine state
    private decimal _currentValue = 0;
    private decimal _storedValue = 0;
    private string _currentOperation = string.Empty;
    private bool _newOperandReady = true;
    private bool _lastButtonWasEquals = false;
    
    // Memory operations
    private decimal _memoryValue = 0;
    private bool _hasMemoryValue = false;
    
    // Scientific mode
    private bool _isRadianMode = true;
    private List<string> _expressionStack = new();
    
    /// <summary>
    /// Gets a value indicating whether there is a value stored in memory
    /// </summary>
    public bool HasMemoryValue => _hasMemoryValue;
    
    /// <summary>
    /// Gets or sets a value indicating whether the calculator is in radian mode
    /// </summary>
    public bool IsRadianMode
    {
        get => _isRadianMode;
        set => _isRadianMode = value;
    }
    
    /// <summary>
    /// Clears the current calculation
    /// </summary>
    public void Clear()
    {
        _currentValue = 0;
        _storedValue = 0;
        _currentOperation = string.Empty;
        _newOperandReady = true;
        _lastButtonWasEquals = false;
    }
    
    /// <summary>
    /// Clears the current entry (current value) but keeps the operation
    /// </summary>
    public void ClearEntry()
    {
        _currentValue = 0;
        _newOperandReady = true;
    }
    
    /// <summary>
    /// Adds a digit to the current value
    /// </summary>
    /// <param name="digit">The digit to add (0-9)</param>
    /// <param name="currentDisplay">The current display value</param>
    /// <returns>The updated display string</returns>
    public string AddDigit(int digit, string currentDisplay)
    {
        if (digit < 0 || digit > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(digit), "Digit must be between 0 and 9");
        }
        
        if (_lastButtonWasEquals)
        {
            Clear();
            _lastButtonWasEquals = false;
        }
        
        if (_newOperandReady)
        {
            _currentValue = digit;
            _newOperandReady = false;
            return digit.ToString();
        }
        
        // Check if adding would exceed decimal capacity
        if (currentDisplay.Replace(".", "").Replace("-", "").Length >= 16)
        {
            return currentDisplay;
        }
        
        // Handle existing decimal
        if (currentDisplay.Contains('.'))
        {
            string newDisplay = currentDisplay + digit.ToString();
            _currentValue = decimal.Parse(newDisplay);
            return newDisplay;
        }
        else
        {
            _currentValue = _currentValue * 10 + digit;
            return _currentValue.ToString();
        }
    }
    
    /// <summary>
    /// Adds a decimal point to the current value
    /// </summary>
    /// <param name="currentDisplay">The current display value</param>
    /// <returns>The updated display string</returns>
    public string AddDecimalPoint(string currentDisplay)
    {
        if (_lastButtonWasEquals)
        {
            Clear();
            _lastButtonWasEquals = false;
        }
        
        if (_newOperandReady)
        {
            _currentValue = 0;
            _newOperandReady = false;
            return "0.";
        }
        
        if (!currentDisplay.Contains('.'))
        {
            return currentDisplay + ".";
        }
        
        return currentDisplay;
    }
    
    /// <summary>
    /// Performs an operation
    /// </summary>
    /// <param name="currentDisplay">The current display value</param>
    /// <param name="operation">The operation to perform</param>
    /// <returns>The updated display string</returns>
    public string PerformOperation(string currentDisplay, string operation)
    {
        if (!decimal.TryParse(currentDisplay, out decimal displayValue))
        {
            return "Error: Invalid input";
        }
        
        _lastButtonWasEquals = false;
        
        if (string.IsNullOrEmpty(_currentOperation))
        {
            _storedValue = displayValue;
            _currentOperation = operation;
            _newOperandReady = true;
            return displayValue.ToString();
        }
        else
        {
            decimal result = CalculateInternal(_storedValue, displayValue, _currentOperation);
            _storedValue = result;
            _currentOperation = operation;
            _newOperandReady = true;
            return result.ToString();
        }
    }
    
    /// <summary>
    /// Calculates the result of the current operation
    /// </summary>
    /// <param name="currentDisplay">The current display value</param>
    /// <returns>The result of the calculation</returns>
    public string CalculateResult(string currentDisplay)
    {
        if (!decimal.TryParse(currentDisplay, out decimal displayValue))
        {
            return "Error: Invalid input";
        }
        
        if (string.IsNullOrEmpty(_currentOperation))
        {
            return displayValue.ToString();
        }
        
        try
        {
            decimal result = CalculateInternal(_storedValue, displayValue, _currentOperation);
            _currentValue = result;
            _storedValue = result;
            _lastButtonWasEquals = true;
            _currentOperation = string.Empty;
            _newOperandReady = true;
            return result.ToString();
        }
        catch (DivideByZeroException)
        {
            return "Error: Division by zero";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Gets the current expression
    /// </summary>
    /// <returns>The current expression as a string</returns>
    public string GetExpression()
    {
        if (string.IsNullOrEmpty(_currentOperation))
        {
            return string.Empty;
        }
        
        string opSymbol = _currentOperation switch
        {
            "+" => "+",
            "-" => "−",
            "*" => "×",
            "/" => "÷",
            _ => _currentOperation
        };
        
        return $"{_storedValue} {opSymbol}";
    }
    
    /// <summary>
    /// Performs a scientific function
    /// </summary>
    /// <param name="currentDisplay">The current display value</param>
    /// <param name="function">The function to perform</param>
    /// <returns>The result of the function</returns>
    public string PerformScientificFunction(string currentDisplay, string function)
    {
        if (!decimal.TryParse(currentDisplay, out decimal value))
        {
            return "Error: Invalid input";
        }
        
        try
        {
            return function switch
            {
                "1/x" => (1 / value).ToString(),
                "x²" => (value * value).ToString(),
                "√" => ((decimal)Math.Sqrt((double)value)).ToString(),
                "sin" => PerformTrigFunction(value, Math.Sin),
                "cos" => PerformTrigFunction(value, Math.Cos),
                "tan" => PerformTrigFunction(value, Math.Tan),
                "log" => ((decimal)Math.Log10((double)value)).ToString(),
                "ln" => ((decimal)Math.Log((double)value)).ToString(),
                "π" => Math.PI.ToString(),
                "e" => Math.E.ToString(),
                "2ⁿ" => ((decimal)Math.Pow(2, (double)value)).ToString(),
                "10ˣ" => ((decimal)Math.Pow(10, (double)value)).ToString(),
                "eˣ" => ((decimal)Math.Pow(Math.E, (double)value)).ToString(),
                "n!" => CalculateFactorial(value),
                _ => currentDisplay
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Calculates the factorial of a number
    /// </summary>
    private string CalculateFactorial(decimal n)
    {
        if (n < 0)
        {
            return "Error: Cannot calculate factorial of negative number";
        }
        
        if (n > 170)
        {
            return "Error: Result too large";
        }
        
        int intValue = (int)n;
        if (intValue != n)
        {
            return "Error: Factorial only works with integers";
        }
        
        decimal result = 1;
        for (int i = 2; i <= intValue; i++)
        {
            result *= i;
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// Performs a trigonometric function
    /// </summary>
    private string PerformTrigFunction(decimal value, Func<double, double> func)
    {
        double doubleValue = (double)value;
        
        if (!_isRadianMode)
        {
            // Convert from degrees to radians
            doubleValue = doubleValue * Math.PI / 180;
        }
        
        double result = func(doubleValue);
        return ((decimal)result).ToString();
    }
    
    /// <summary>
    /// Performs the internal calculation
    /// </summary>
    private decimal CalculateInternal(decimal left, decimal right, string operation)
    {
        return operation switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => right != 0 ? left / right : throw new DivideByZeroException(),
            "mod" => left % right,
            "xʸ" => (decimal)Math.Pow((double)left, (double)right),
            _ => throw new NotSupportedException($"Operation not supported: {operation}")
        };
    }
    
    /// <summary>
    /// Clears the memory value
    /// </summary>
    public void MemoryClear()
    {
        _memoryValue = 0;
        _hasMemoryValue = false;
    }
    
    /// <summary>
    /// Recalls the memory value
    /// </summary>
    public string MemoryRecall()
    {
        if (!_hasMemoryValue)
        {
            return "0";
        }
        
        _newOperandReady = true;
        return _memoryValue.ToString();
    }
    
    /// <summary>
    /// Stores a value in memory
    /// </summary>
    public void MemoryStore(decimal value)
    {
        _memoryValue = value;
        _hasMemoryValue = true;
    }
    
    /// <summary>
    /// Adds a value to the memory
    /// </summary>
    public void MemoryAdd(decimal value)
    {
        _memoryValue += value;
        _hasMemoryValue = true;
    }
    
    /// <summary>
    /// Subtracts a value from the memory
    /// </summary>
    public void MemorySubtract(decimal value)
    {
        _memoryValue -= value;
        _hasMemoryValue = true;
    }
    
    /// <summary>
    /// Gets the engine state for serialization
    /// </summary>
    public object GetState()
    {
        return new
        {
            CurrentValue = _currentValue,
            StoredValue = _storedValue,
            CurrentOperation = _currentOperation,
            NewOperandReady = _newOperandReady,
            LastButtonWasEquals = _lastButtonWasEquals,
            MemoryValue = _memoryValue,
            HasMemoryValue = _hasMemoryValue,
            IsRadianMode = _isRadianMode
        };
    }
    
    /// <summary>
    /// Sets the engine state from deserialized data
    /// </summary>
    public void SetState(JsonElement stateElement)
    {
        if (stateElement.TryGetProperty("CurrentValue", out var currentValue))
        {
            _currentValue = currentValue.GetDecimal();
        }
        
        if (stateElement.TryGetProperty("StoredValue", out var storedValue))
        {
            _storedValue = storedValue.GetDecimal();
        }
        
        if (stateElement.TryGetProperty("CurrentOperation", out var currentOperation))
        {
            _currentOperation = currentOperation.GetString() ?? string.Empty;
        }
        
        if (stateElement.TryGetProperty("NewOperandReady", out var newOperandReady))
        {
            _newOperandReady = newOperandReady.GetBoolean();
        }
        
        if (stateElement.TryGetProperty("LastButtonWasEquals", out var lastButtonWasEquals))
        {
            _lastButtonWasEquals = lastButtonWasEquals.GetBoolean();
        }
        
        if (stateElement.TryGetProperty("MemoryValue", out var memoryValue))
        {
            _memoryValue = memoryValue.GetDecimal();
        }
        
        if (stateElement.TryGetProperty("HasMemoryValue", out var hasMemoryValue))
        {
            _hasMemoryValue = hasMemoryValue.GetBoolean();
        }
        
        if (stateElement.TryGetProperty("IsRadianMode", out var isRadianMode))
        {
            _isRadianMode = isRadianMode.GetBoolean();
        }
    }
}
