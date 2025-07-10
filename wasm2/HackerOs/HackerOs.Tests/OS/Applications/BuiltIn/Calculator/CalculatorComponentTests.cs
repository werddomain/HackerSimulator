using HackerOs.OS.Applications.BuiltIn.Calculator;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace HackerOs.Tests.OS.Applications.BuiltIn.Calculator;

/// <summary>
/// Tests for the Calculator component
/// </summary>
public class CalculatorComponentTests
{
    [Fact]
    public void CalculatorEngine_AddDigit_ShouldAppendDigitCorrectly()
    {
        // Arrange
        var engine = new CalculatorEngine();
        
        // Act - Add a digit to a fresh calculator
        string result = engine.AddDigit(5, "0");
        
        // Assert
        Assert.Equal("5", result);
        
        // Act - Add another digit
        result = engine.AddDigit(3, "5");
        
        // Assert
        Assert.Equal("53", result);
    }
    
    [Fact]
    public void CalculatorEngine_AddDecimalPoint_ShouldAddDecimalCorrectly()
    {
        // Arrange
        var engine = new CalculatorEngine();
        
        // Act - Add decimal to a fresh calculator
        string result = engine.AddDecimalPoint("0");
        
        // Assert
        Assert.Equal("0.", result);
        
        // Act - Add a digit after decimal
        result = engine.AddDigit(5, "0.");
        
        // Assert
        Assert.Equal("0.5", result);
        
        // Act - Try to add another decimal
        result = engine.AddDecimalPoint("0.5");
        
        // Assert - Should not add another decimal
        Assert.Equal("0.5", result);
    }
    
    [Fact]
    public void CalculatorEngine_PerformOperation_ShouldStoreValueAndOperation()
    {
        // Arrange
        var engine = new CalculatorEngine();
        engine.AddDigit(5, "0"); // Set current value to 5
        
        // Act
        string result = engine.PerformOperation("+");
        
        // Assert
        Assert.Equal("5", result); // Should return the stored value
        
        // Act - Add a new value and calculate
        engine.AddDigit(3, "0");
        result = engine.CalculateResult();
        
        // Assert
        Assert.Equal("8", result); // 5 + 3 = 8
    }
    
    [Fact]
    public void CalculatorEngine_ChangeSign_ShouldToggleSign()
    {
        // Arrange
        var engine = new CalculatorEngine();
        engine.AddDigit(5, "0"); // Set current value to 5
        
        // Act
        string result = engine.ChangeSign("5");
        
        // Assert
        Assert.Equal("-5", result);
        
        // Act - Toggle back
        result = engine.ChangeSign("-5");
        
        // Assert
        Assert.Equal("5", result);
    }
    
    [Fact]
    public void CalculatorEngine_CalculatePercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var engine = new CalculatorEngine();
        engine.AddDigit(5, "0"); // Set current value to 5
        
        // Act - Simple percentage (5%)
        string result = engine.CalculatePercentage();
        
        // Assert
        Assert.Equal("0.05", result);
        
        // Arrange for percentage in addition
        engine = new CalculatorEngine();
        engine.AddDigit(1, "0"); // Set current value to 100
        engine.AddDigit(0, "1");
        engine.AddDigit(0, "10");
        engine.PerformOperation("+"); // 100 +
        engine.AddDigit(5, "0"); // 5
        
        // Act - Percentage in operation (100 + 5% = 100 + 5)
        result = engine.CalculatePercentage();
        
        // Assert
        Assert.Equal("5", result);
    }
    
    [Fact]
    public void CalculatorEngine_ScientificFunctions_ShouldCalculateCorrectly()
    {
        // Arrange
        var engine = new CalculatorEngine();
        
        // Act & Assert - Square root of 4
        engine.AddDigit(4, "0");
        string result = engine.CalculateSquareRoot();
        Assert.Equal("2", result);
        
        // Act & Assert - Square of 3
        engine.AddDigit(3, "0");
        result = engine.CalculateSquare();
        Assert.Equal("9", result);
        
        // Act & Assert - Reciprocal of 2
        engine.AddDigit(2, "0");
        result = engine.CalculateReciprocal();
        Assert.Equal("0.5", result);
        
        // Act & Assert - Sine of 0 (in radians)
        engine.AddDigit(0, "0");
        result = engine.CalculateSine();
        Assert.Equal("0", result);
    }
    
    [Fact]
    public void CalculatorEngine_Memory_ShouldStoreAndRecallValues()
    {
        // Arrange
        var engine = new CalculatorEngine();
        
        // Act - Initial state
        bool hasMemory = engine.HasMemoryValue;
        
        // Assert
        Assert.False(hasMemory);
        
        // Act - Store a value
        engine.AddDigit(5, "0");
        engine.MemoryStore();
        
        // Assert
        Assert.True(engine.HasMemoryValue);
        
        // Act - Recall the value
        string result = engine.MemoryRecall();
        
        // Assert
        Assert.Equal("5", result);
        
        // Act - Add to memory
        engine.AddDigit(3, "0");
        engine.MemoryAdd();
        result = engine.MemoryRecall();
        
        // Assert
        Assert.Equal("8", result); // 5 + 3 = 8
        
        // Act - Clear memory
        engine.MemoryClear();
        
        // Assert
        Assert.False(engine.HasMemoryValue);
    }
    
    [Fact]
    public void CalculatorEngine_StateManagement_ShouldPersistState()
    {
        // Arrange
        var engine = new CalculatorEngine();
        engine.AddDigit(5, "0");
        engine.PerformOperation("+");
        engine.AddDigit(3, "0");
        
        // Act - Get state
        var state = engine.GetState();
        
        // Create a new engine and set the state
        var newEngine = new CalculatorEngine();
        newEngine.SetState(state);
        
        // Act - Calculate result with the new engine
        string result = newEngine.CalculateResult();
        
        // Assert
        Assert.Equal("8", result); // 5 + 3 = 8
    }
    
    [Fact]
    public async Task CalculatorComponent_DigitClick_ShouldUpdateDisplay()
    {
        // Arrange
        var component = new CalculatorComponent
        {
            Display = "0",
            Engine = new CalculatorEngine()
        };
        
        var displayChangedCalled = false;
        var newDisplayValue = string.Empty;
        
        component.DisplayChanged = EventCallback.Factory.Create<string>(this, (value) =>
        {
            displayChangedCalled = true;
            newDisplayValue = value;
            return Task.CompletedTask;
        });
        
        // Act - Call the digit click method via reflection
        await component.GetType().GetMethod("DigitClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(component, new object[] { 5 }) as Task;
        
        // Assert
        Assert.True(displayChangedCalled);
        Assert.Equal("5", newDisplayValue);
    }
    
    [Fact]
    public async Task CalculatorComponent_OperationClick_ShouldUpdateExpression()
    {
        // Arrange
        var component = new CalculatorComponent
        {
            Display = "5",
            Engine = new CalculatorEngine()
        };
        
        // Set the engine state
        component.Engine.AddDigit(5, "0");
        
        var displayChangedCalled = false;
        var newDisplayValue = string.Empty;
        
        component.DisplayChanged = EventCallback.Factory.Create<string>(this, (value) =>
        {
            displayChangedCalled = true;
            newDisplayValue = value;
            return Task.CompletedTask;
        });
        
        // Act - Call the operation click method via reflection
        await component.GetType().GetMethod("OperationClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(component, new object[] { "+" }) as Task;
        
        // Assert
        Assert.True(displayChangedCalled);
        Assert.Equal("5", newDisplayValue);
        
        // Check the expression
        var expressionField = component.GetType().GetField("CurrentExpression", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var expression = expressionField.GetValue(component) as string;
        Assert.Equal("5 +", expression);
    }
    
    [Fact]
    public async Task CalculatorComponent_EqualsClick_ShouldCalculateAndAddToHistory()
    {
        // Arrange
        var component = new CalculatorComponent
        {
            Display = "3",
            Engine = new CalculatorEngine(),
            History = new List<string>()
        };
        
        // Set up the engine for calculation
        component.Engine.AddDigit(5, "0");
        component.Engine.PerformOperation("+");
        component.Engine.AddDigit(3, "0");
        
        // Set the expression
        var expressionField = component.GetType().GetField("CurrentExpression", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        expressionField.SetValue(component, "5 +");
        
        var displayChangedCalled = false;
        var newDisplayValue = string.Empty;
        
        component.DisplayChanged = EventCallback.Factory.Create<string>(this, (value) =>
        {
            displayChangedCalled = true;
            newDisplayValue = value;
            return Task.CompletedTask;
        });
        
        var historyChangedCalled = false;
        List<string> newHistory = null;
        
        component.HistoryChanged = EventCallback.Factory.Create<List<string>>(this, (value) =>
        {
            historyChangedCalled = true;
            newHistory = value;
            return Task.CompletedTask;
        });
        
        // Act - Call the equals click method via reflection
        await component.GetType().GetMethod("EqualsClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(component, null) as Task;
        
        // Assert
        Assert.True(displayChangedCalled);
        Assert.Equal("8", newDisplayValue); // 5 + 3 = 8
        
        Assert.True(historyChangedCalled);
        Assert.Single(newHistory);
        Assert.Contains("5 + 3 = 8", newHistory[0]);
    }
    
    [Fact]
    public async Task CalculatorComponent_ClearClick_ShouldResetCalculator()
    {
        // Arrange
        var component = new CalculatorComponent
        {
            Display = "123",
            Engine = new CalculatorEngine()
        };
        
        var displayChangedCalled = false;
        var newDisplayValue = string.Empty;
        
        component.DisplayChanged = EventCallback.Factory.Create<string>(this, (value) =>
        {
            displayChangedCalled = true;
            newDisplayValue = value;
            return Task.CompletedTask;
        });
        
        // Act - Call the clear click method via reflection
        await component.GetType().GetMethod("ClearClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(component, null) as Task;
        
        // Assert
        Assert.True(displayChangedCalled);
        Assert.Equal("0", newDisplayValue);
        
        // Check the expression is cleared
        var expressionField = component.GetType().GetField("CurrentExpression", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var expression = expressionField.GetValue(component) as string;
        Assert.Equal(string.Empty, expression);
    }
    
    [Fact]
    public async Task CalculatorComponent_OnKeyDown_ShouldHandleKeyboardInput()
    {
        // Arrange
        var component = new CalculatorComponent
        {
            Display = "0",
            Engine = new CalculatorEngine()
        };
        
        var displayChangedCalled = false;
        var newDisplayValue = string.Empty;
        
        component.DisplayChanged = EventCallback.Factory.Create<string>(this, (value) =>
        {
            displayChangedCalled = true;
            newDisplayValue = value;
            return Task.CompletedTask;
        });
        
        // Act - Call OnKeyDown with a digit key
        await component.OnKeyDown(new KeyboardEventArgs { Key = "5" });
        
        // Assert
        Assert.True(displayChangedCalled);
        Assert.Equal("5", newDisplayValue);
        
        // Reset for next test
        displayChangedCalled = false;
        
        // Act - Call OnKeyDown with an operation key
        await component.OnKeyDown(new KeyboardEventArgs { Key = "+" });
        
        // Assert
        Assert.True(displayChangedCalled);
        
        // Reset for next test
        displayChangedCalled = false;
        
        // Act - Call OnKeyDown with enter key (equals)
        await component.OnKeyDown(new KeyboardEventArgs { Key = "Enter" });
        
        // Assert
        Assert.True(displayChangedCalled);
    }
}
