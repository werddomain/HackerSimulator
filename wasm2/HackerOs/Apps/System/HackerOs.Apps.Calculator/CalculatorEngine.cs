using System.Globalization;

namespace HackerOs.Apps.Calculator;

/// <summary>
/// Deterministic C# engine powering the Calculator application.
/// Implements arithmetic operations, unary functions (sqrt, square, negate, percentage),
/// memory registers, and bounded string formatting.
/// </summary>
public sealed class CalculatorEngine
{
    private const int MaxDigits = 12;

    public string CurrentValue { get; private set; } = "0";
    public string PreviousValue { get; private set; } = "";
    public string Operator { get; private set; } = "";
    public bool WaitingForOperand { get; private set; } = true;
    public double MemoryValue { get; private set; } = 0;

    public void InputDigit(string digit)
    {
        if (WaitingForOperand)
        {
            CurrentValue = digit;
            WaitingForOperand = false;
        }
        else
        {
            if (CurrentValue.Replace("-", "").Replace(".", "").Length >= MaxDigits) return;
            CurrentValue = CurrentValue == "0" ? digit : CurrentValue + digit;
        }
    }

    public void InputDecimal()
    {
        if (WaitingForOperand)
        {
            CurrentValue = "0.";
            WaitingForOperand = false;
        }
        else if (!CurrentValue.Contains('.'))
        {
            CurrentValue += ".";
        }
    }

    public void HandleOperator(string nextOperator)
    {
        if (!string.IsNullOrEmpty(Operator) && !WaitingForOperand)
        {
            Calculate();
        }
        else
        {
            PreviousValue = CurrentValue;
        }

        Operator = nextOperator;
        WaitingForOperand = true;
    }

    public void HandleEquals()
    {
        if (string.IsNullOrEmpty(Operator) || WaitingForOperand) return;
        Calculate();
        Operator = "";
        WaitingForOperand = true;
    }

    public void Calculate()
    {
        if (!double.TryParse(PreviousValue, CultureInfo.InvariantCulture, out double prev) ||
            !double.TryParse(CurrentValue, CultureInfo.InvariantCulture, out double curr))
            return;

        double result = 0;
        switch (Operator)
        {
            case "+": result = prev + curr; break;
            case "-": result = prev - curr; break;
            case "*": result = prev * curr; break;
            case "/":
                if (curr == 0)
                {
                    CurrentValue = "Error";
                    WaitingForOperand = true;
                    return;
                }
                result = prev / curr;
                break;
            default: return;
        }

        CurrentValue = FormatResult(result);
        PreviousValue = CurrentValue;
    }

    public void Clear()
    {
        CurrentValue = "0";
        WaitingForOperand = true;
    }

    public void AllClear()
    {
        CurrentValue = "0";
        PreviousValue = "";
        Operator = "";
        WaitingForOperand = true;
    }

    public void Negate()
    {
        if (double.TryParse(CurrentValue, CultureInfo.InvariantCulture, out double val) && val != 0)
        {
            CurrentValue = FormatResult(-val);
        }
    }

    public void Percentage()
    {
        if (double.TryParse(CurrentValue, CultureInfo.InvariantCulture, out double val))
        {
            CurrentValue = FormatResult(val / 100.0);
            WaitingForOperand = true;
        }
    }

    public void Square()
    {
        if (double.TryParse(CurrentValue, CultureInfo.InvariantCulture, out double val))
        {
            CurrentValue = FormatResult(val * val);
            WaitingForOperand = true;
        }
    }

    public void SquareRoot()
    {
        if (double.TryParse(CurrentValue, CultureInfo.InvariantCulture, out double val))
        {
            if (val < 0)
            {
                CurrentValue = "Error";
            }
            else
            {
                CurrentValue = FormatResult(Math.Sqrt(val));
            }
            WaitingForOperand = true;
        }
    }

    public void MemoryClear() => MemoryValue = 0;
    public void MemoryRecall()
    {
        CurrentValue = FormatResult(MemoryValue);
        WaitingForOperand = false;
    }

    public void MemoryAdd()
    {
        if (double.TryParse(CurrentValue, CultureInfo.InvariantCulture, out double val))
            MemoryValue += val;
    }

    public void MemorySubtract()
    {
        if (double.TryParse(CurrentValue, CultureInfo.InvariantCulture, out double val))
            MemoryValue -= val;
    }

    public static string FormatResult(double val)
    {
        if (double.IsNaN(val) || double.IsInfinity(val)) return "Error";
        string str = val.ToString("G12", CultureInfo.InvariantCulture);
        return str;
    }
}
