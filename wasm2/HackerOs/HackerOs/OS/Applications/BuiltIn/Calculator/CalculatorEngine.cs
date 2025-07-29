using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HackerOs.OS.Applications.BuiltIn.Calculator;

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
            return "0.";
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
    /// Changes the sign of the current value
    /// </summary>
    /// <param name="currentDisplay">The current display value</param>
    /// <returns>The updated display string</returns>
    public string ChangeSign(string currentDisplay)
    {
        _currentValue = -_currentValue;
        
        if (currentDisplay.StartsWith("-"))
        {
            return currentDisplay.Substring(1);
        }
        else
        {
            return "-" + currentDisplay;
        }
    }
    
    /// <summary>
    /// Performs the specified basic operation
    /// </summary>
    /// <param name="operation">The operation to perform (+, -, *, /)</param>
    /// <returns>The result display string</returns>
    public string PerformOperation(string operation)
    {
        if (_lastButtonWasEquals)
        {
            _lastButtonWasEquals = false;
        }
        
        // If there's a pending operation, calculate it first
        if (!string.IsNullOrEmpty(_currentOperation) && !_newOperandReady)
        {
            _storedValue = Calculate(_storedValue, _currentValue, _currentOperation);
            _currentValue = _storedValue;
        }
        else if (_newOperandReady && !string.IsNullOrEmpty(_currentOperation))
        {
            // Just changing the operation
        }
        else
        {
            _storedValue = _currentValue;
        }
        
        _currentOperation = operation;
        _newOperandReady = true;
        
        return _storedValue.ToString();
    }
    
    /// <summary>
    /// Calculates the result of the current operation
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateResult()
    {
        if (string.IsNullOrEmpty(_currentOperation))
        {
            return _currentValue.ToString();
        }
        
        if (_lastButtonWasEquals)
        {
            // If equals was already pressed, repeat the last operation
            _currentValue = Calculate(_currentValue, _storedValue, _currentOperation);
        }
        else
        {
            decimal result = Calculate(_storedValue, _currentValue, _currentOperation);
            _currentValue = result;
        }
        
        _lastButtonWasEquals = true;
        _newOperandReady = true;
        
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Performs a percentage calculation
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculatePercentage()
    {
        if (string.IsNullOrEmpty(_currentOperation))
        {
            _currentValue = _currentValue / 100;
        }
        else
        {
            switch (_currentOperation)
            {
                case "+":
                case "-":
                    _currentValue = _storedValue * (_currentValue / 100);
                    break;
                case "*":
                case "/":
                    _currentValue = _currentValue / 100;
                    break;
            }
        }
        
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the square root of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateSquareRoot()
    {
        if (_currentValue < 0)
        {
            return "Error";
        }
        
        _currentValue = (decimal)Math.Sqrt((double)_currentValue);
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the square of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateSquare()
    {
        _currentValue = _currentValue * _currentValue;
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the reciprocal of the current value (1/x)
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateReciprocal()
    {
        if (_currentValue == 0)
        {
            return "Cannot divide by zero";
        }
        
        _currentValue = 1 / _currentValue;
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Clears the memory
    /// </summary>
    public void MemoryClear()
    {
        _memoryValue = 0;
        _hasMemoryValue = false;
    }
    
    /// <summary>
    /// Recalls the memory value
    /// </summary>
    /// <returns>The memory value as a string, or "0" if no memory value</returns>
    public string MemoryRecall()
    {
        if (!_hasMemoryValue)
        {
            return "0";
        }
        
        _currentValue = _memoryValue;
        _newOperandReady = true;
        return _memoryValue.ToString();
    }
    
    /// <summary>
    /// Stores the current value in memory
    /// </summary>
    public void MemoryStore()
    {
        _memoryValue = _currentValue;
        _hasMemoryValue = true;
        _newOperandReady = true;
    }
    
    /// <summary>
    /// Adds the current value to memory
    /// </summary>
    public void MemoryAdd()
    {
        _memoryValue += _currentValue;
        _hasMemoryValue = true;
    }
    
    /// <summary>
    /// Subtracts the current value from memory
    /// </summary>
    public void MemorySubtract()
    {
        _memoryValue -= _currentValue;
        _hasMemoryValue = true;
    }
    
    /// <summary>
    /// Gets whether there is a value stored in memory
    /// </summary>
    public bool HasMemoryValue => _hasMemoryValue;
    
    /// <summary>
    /// Toggles between degrees and radians mode
    /// </summary>
    public void ToggleAngleMode()
    {
        _isRadianMode = !_isRadianMode;
    }
    
    /// <summary>
    /// Gets whether the calculator is in radian mode
    /// </summary>
    public bool IsRadianMode => _isRadianMode;
    
    /// <summary>
    /// Calculates the sine of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateSine()
    {
        double angle = (double)_currentValue;
        if (!_isRadianMode)
        {
            angle = angle * Math.PI / 180.0;
        }
        
        _currentValue = (decimal)Math.Sin(angle);
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the cosine of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateCosine()
    {
        double angle = (double)_currentValue;
        if (!_isRadianMode)
        {
            angle = angle * Math.PI / 180.0;
        }
        
        _currentValue = (decimal)Math.Cos(angle);
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the tangent of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateTangent()
    {
        double angle = (double)_currentValue;
        if (!_isRadianMode)
        {
            angle = angle * Math.PI / 180.0;
        }
        
        _currentValue = (decimal)Math.Tan(angle);
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the natural logarithm of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateNaturalLog()
    {
        if (_currentValue <= 0)
        {
            return "Error";
        }
        
        _currentValue = (decimal)Math.Log((double)_currentValue);
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the base-10 logarithm of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateLog10()
    {
        if (_currentValue <= 0)
        {
            return "Error";
        }
        
        _currentValue = (decimal)Math.Log10((double)_currentValue);
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates e raised to the power of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateExp()
    {
        _currentValue = (decimal)Math.Exp((double)_currentValue);
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Inserts the constant pi
    /// </summary>
    /// <returns>The pi value as a string</returns>
    public string InsertPi()
    {
        _currentValue = (decimal)Math.PI;
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Inserts the constant e
    /// </summary>
    /// <returns>The e value as a string</returns>
    public string InsertE()
    {
        _currentValue = (decimal)Math.E;
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Calculates the factorial of the current value
    /// </summary>
    /// <returns>The result display string</returns>
    public string CalculateFactorial()
    {
        if (_currentValue < 0 || _currentValue > 170 || _currentValue != Math.Floor((double)_currentValue))
        {
            return "Error";
        }
        
        decimal result = 1;
        for (int i = 1; i <= (int)_currentValue; i++)
        {
            result *= i;
        }
        
        _currentValue = result;
        _newOperandReady = true;
        return _currentValue.ToString();
    }
    
    /// <summary>
    /// Gets the state of the calculator engine for serialization
    /// </summary>
    /// <returns>The state as a JsonElement</returns>
    public JsonElement GetState()
    {
        var state = new Dictionary<string, object>
        {
            { "currentValue", _currentValue },
            { "storedValue", _storedValue },
            { "currentOperation", _currentOperation },
            { "newOperandReady", _newOperandReady },
            { "memoryValue", _memoryValue },
            { "hasMemoryValue", _hasMemoryValue },
            { "isRadianMode", _isRadianMode }
        };
        
        return JsonSerializer.SerializeToElement(state);
    }
    
    /// <summary>
    /// Sets the state of the calculator engine from serialization
    /// </summary>
    /// <param name="state">The state as a JsonElement</param>
    public void SetState(JsonElement state)
    {
        if (state.TryGetProperty("currentValue", out var currentValueElement))
        {
            _currentValue = currentValueElement.GetDecimal();
        }
        
        if (state.TryGetProperty("storedValue", out var storedValueElement))
        {
            _storedValue = storedValueElement.GetDecimal();
        }
        
        if (state.TryGetProperty("currentOperation", out var currentOperationElement) &&
            currentOperationElement.ValueKind == JsonValueKind.String)
        {
            _currentOperation = currentOperationElement.GetString() ?? string.Empty;
        }
        
        if (state.TryGetProperty("newOperandReady", out var newOperandReadyElement))
        {
            _newOperandReady = newOperandReadyElement.GetBoolean();
        }
        
        if (state.TryGetProperty("memoryValue", out var memoryValueElement))
        {
            _memoryValue = memoryValueElement.GetDecimal();
        }
        
        if (state.TryGetProperty("hasMemoryValue", out var hasMemoryValueElement))
        {
            _hasMemoryValue = hasMemoryValueElement.GetBoolean();
        }
        
        if (state.TryGetProperty("isRadianMode", out var isRadianModeElement))
        {
            _isRadianMode = isRadianModeElement.GetBoolean();
        }
    }
    
    #region Private Methods
    
    /// <summary>
    /// Calculates the result of an operation
    /// </summary>
    /// <param name="left">Left operand</param>
    /// <param name="right">Right operand</param>
    /// <param name="operation">Operation to perform</param>
    /// <returns>The calculation result</returns>
    private decimal Calculate(decimal left, decimal right, string operation)
    {
        return operation switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => right == 0 ? throw new DivideByZeroException() : left / right,
            _ => right
        };
    }
    
    #endregion
}
