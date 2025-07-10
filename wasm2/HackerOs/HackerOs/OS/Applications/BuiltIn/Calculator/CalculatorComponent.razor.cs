using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Applications.BuiltIn.Calculator;

/// <summary>
/// Code-behind for the Calculator component
/// </summary>
public partial class CalculatorComponent : ComponentBase
{
    /// <summary>
    /// The current display value
    /// </summary>
    [Parameter]
    public string Display { get; set; } = "0";

    /// <summary>
    /// Event callback for when the display value changes
    /// </summary>
    [Parameter]
    public EventCallback<string> DisplayChanged { get; set; }

    /// <summary>
    /// The current calculator mode
    /// </summary>
    [Parameter]
    public string Mode { get; set; } = "Standard";

    /// <summary>
    /// Event callback for when the calculator mode changes
    /// </summary>
    [Parameter]
    public EventCallback<string> ModeChanged { get; set; }

    /// <summary>
    /// The calculation history
    /// </summary>
    [Parameter]
    public List<string> History { get; set; } = new();

    /// <summary>
    /// Event callback for when the calculation history changes
    /// </summary>
    [Parameter]
    public EventCallback<List<string>> HistoryChanged { get; set; }

    /// <summary>
    /// The calculator engine
    /// </summary>
    [Parameter]
    public CalculatorEngine Engine { get; set; } = new();
    
    /// <summary>
    /// Flag indicating whether to show the history panel
    /// </summary>
    [Parameter]
    public bool ShowHistory { get; set; } = false;
    
    /// <summary>
    /// The current expression being built
    /// </summary>
    private string CurrentExpression { get; set; } = string.Empty;
    
    /// <summary>
    /// Handles digit button clicks
    /// </summary>
    /// <param name="digit">The digit that was clicked</param>
    private async Task DigitClick(int digit)
    {
        try
        {
            string newDisplay = Engine.AddDigit(digit, Display);
            await UpdateDisplayAsync(newDisplay);
        }
        catch (Exception ex)
        {
            await UpdateDisplayAsync("Error: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Handles decimal point button click
    /// </summary>
    private async Task DecimalPointClick()
    {
        try
        {
            string newDisplay = Engine.AddDecimalPoint(Display);
            await UpdateDisplayAsync(newDisplay);
        }
        catch (Exception ex)
        {
            await UpdateDisplayAsync("Error: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Handles operation button clicks
    /// </summary>
    /// <param name="operation">The operation that was clicked</param>
    private async Task OperationClick(string operation)
    {
        try
        {
            string newDisplay = Engine.PerformOperation(operation);
            CurrentExpression = $"{newDisplay} {GetOperationSymbol(operation)}";
            await UpdateDisplayAsync(newDisplay);
        }
        catch (DivideByZeroException)
        {
            await UpdateDisplayAsync("Cannot divide by zero");
        }
        catch (Exception ex)
        {
            await UpdateDisplayAsync("Error: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Handles equals button click
    /// </summary>
    private async Task EqualsClick()
    {
        try
        {
            string newDisplay = Engine.CalculateResult();
            string historyEntry = $"{CurrentExpression} {Display} = {newDisplay}";
            CurrentExpression = string.Empty;
            
            // Add to history
            History.Add(historyEntry);
            await HistoryChanged.InvokeAsync(History);
            
            await UpdateDisplayAsync(newDisplay);
        }
        catch (DivideByZeroException)
        {
            await UpdateDisplayAsync("Cannot divide by zero");
        }
        catch (Exception ex)
        {
            await UpdateDisplayAsync("Error: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Handles clear button click
    /// </summary>
    private async Task ClearClick()
    {
        Engine.Clear();
        CurrentExpression = string.Empty;
        await UpdateDisplayAsync("0");
    }
    
    /// <summary>
    /// Handles clear entry button click
    /// </summary>
    private async Task ClearEntryClick()
    {
        Engine.ClearEntry();
        await UpdateDisplayAsync("0");
    }
    
    /// <summary>
    /// Handles backspace button click
    /// </summary>
    private async Task BackspaceClick()
    {
        if (Display == "0" || Display.StartsWith("Error") || Display.Length == 1)
        {
            await UpdateDisplayAsync("0");
            return;
        }
        
        string newDisplay = Display.Substring(0, Display.Length - 1);
        if (string.IsNullOrEmpty(newDisplay) || newDisplay == "-")
        {
            newDisplay = "0";
        }
        
        await UpdateDisplayAsync(newDisplay);
    }
    
    /// <summary>
    /// Handles toggle sign button click
    /// </summary>
    private async Task ToggleSignClick()
    {
        try
        {
            string newDisplay = Engine.ChangeSign(Display);
            await UpdateDisplayAsync(newDisplay);
        }
        catch (Exception ex)
        {
            await UpdateDisplayAsync("Error: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Handles percentage button click
    /// </summary>
    private async Task PercentageClick()
    {
        try
        {
            string newDisplay = Engine.CalculatePercentage();
            await UpdateDisplayAsync(newDisplay);
        }
        catch (Exception ex)
        {
            await UpdateDisplayAsync("Error: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Handles scientific function button clicks
    /// </summary>
    /// <param name="function">The function that was clicked</param>
    private async Task ScientificFunctionClick(string function)
    {
        try
        {
            string newDisplay = function switch
            {
                "√" => Engine.CalculateSquareRoot(),
                "x²" => Engine.CalculateSquare(),
                "1/x" => Engine.CalculateReciprocal(),
                "sin" => Engine.CalculateSine(),
                "cos" => Engine.CalculateCosine(),
                "tan" => Engine.CalculateTangent(),
                "log" => Engine.CalculateLog10(),
                "ln" => Engine.CalculateNaturalLog(),
                "eˣ" => Engine.CalculateExp(),
                "π" => Engine.InsertPi(),
                "e" => Engine.InsertE(),
                "n!" => Engine.CalculateFactorial(),
                _ => Display
            };
            
            await UpdateDisplayAsync(newDisplay);
        }
        catch (DivideByZeroException)
        {
            await UpdateDisplayAsync("Cannot divide by zero");
        }
        catch (Exception ex)
        {
            await UpdateDisplayAsync("Error: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Handles memory clear button click
    /// </summary>
    private void MemoryClear()
    {
        Engine.MemoryClear();
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles memory recall button click
    /// </summary>
    private async Task MemoryRecall()
    {
        string newDisplay = Engine.MemoryRecall();
        await UpdateDisplayAsync(newDisplay);
    }
    
    /// <summary>
    /// Handles memory store button click
    /// </summary>
    private void MemoryStore()
    {
        Engine.MemoryStore();
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles memory add button click
    /// </summary>
    private void MemoryAdd()
    {
        Engine.MemoryAdd();
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles memory subtract button click
    /// </summary>
    private void MemorySubtract()
    {
        Engine.MemorySubtract();
        StateHasChanged();
    }
    
    /// <summary>
    /// Switches the calculator mode
    /// </summary>
    /// <param name="newMode">The new mode to switch to</param>
    private async Task SwitchMode(string newMode)
    {
        if (Mode != newMode)
        {
            Mode = newMode;
            await ModeChanged.InvokeAsync(Mode);
        }
    }
    
    /// <summary>
    /// Toggles between degrees and radians mode
    /// </summary>
    private void ToggleAngleMode()
    {
        Engine.ToggleAngleMode();
        StateHasChanged();
    }
    
    /// <summary>
    /// Clears the calculation history
    /// </summary>
    private async Task ClearHistory()
    {
        History.Clear();
        await HistoryChanged.InvokeAsync(History);
    }
    
    /// <summary>
    /// Uses a history item to set the current display
    /// </summary>
    /// <param name="historyItem">The history item to use</param>
    private async Task UseHistoryItem(string historyItem)
    {
        // Extract the result part after the equals sign
        int equalsIndex = historyItem.LastIndexOf('=');
        if (equalsIndex >= 0 && equalsIndex < historyItem.Length - 1)
        {
            string result = historyItem.Substring(equalsIndex + 1).Trim();
            await UpdateDisplayAsync(result);
        }
    }
    
    /// <summary>
    /// Toggles the history panel visibility
    /// </summary>
    private void ToggleHistory()
    {
        ShowHistory = !ShowHistory;
        StateHasChanged();
    }
    
    /// <summary>
    /// Updates the display and notifies the parent component
    /// </summary>
    /// <param name="newDisplay">The new display value</param>
    private async Task UpdateDisplayAsync(string newDisplay)
    {
        Display = newDisplay;
        await DisplayChanged.InvokeAsync(Display);
    }
    
    /// <summary>
    /// Gets the symbol for an operation
    /// </summary>
    /// <param name="operation">The operation</param>
    /// <returns>The symbol for the operation</returns>
    private string GetOperationSymbol(string operation)
    {
        return operation switch
        {
            "+" => "+",
            "-" => "−",
            "*" => "×",
            "/" => "÷",
            _ => operation
        };
    }
    
    /// <summary>
    /// Handles keyboard events
    /// </summary>
    /// <param name="e">The keyboard event args</param>
    /// <returns>A task representing the asynchronous operation</returns>
    [JSInvokable]
    public async Task OnKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "0":
            case "1":
            case "2":
            case "3":
            case "4":
            case "5":
            case "6":
            case "7":
            case "8":
            case "9":
                await DigitClick(int.Parse(e.Key));
                break;
            case ".":
            case ",":
                await DecimalPointClick();
                break;
            case "+":
                await OperationClick("+");
                break;
            case "-":
                await OperationClick("-");
                break;
            case "*":
                await OperationClick("*");
                break;
            case "/":
                await OperationClick("/");
                break;
            case "Enter":
            case "=":
                await EqualsClick();
                break;
            case "Backspace":
                await BackspaceClick();
                break;
            case "Delete":
            case "Escape":
                await ClearClick();
                break;
            case "%":
                await PercentageClick();
                break;
        }
    }
}
