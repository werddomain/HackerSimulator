using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HackerOs.OS.Applications;
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Interfaces;
using HackerOs.OS.HSystem.Text;
using HackerOs.OS.IO;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.UI.Windows.Calculator;

/// <summary>
/// Calculator application for HackerOS
/// </summary>
[App("Calculator", "calculator")]
[AppDescription("A calculator application supporting basic and scientific calculations.")]
public partial class CalculatorApp : WindowBase, IProcess, IApplication
{
    [Inject]
    protected IApplicationBridge ApplicationBridge { get; set; } = null!;
    
    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = null!;
    
    [Inject]
    protected IVirtualFileSystem FileSystem { get; set; } = null!;
    
    // Application state variables
    private bool _isInitialized = false;
    private string _currentDisplay = "0";
    private string _currentMode = "Standard"; // "Standard" or "Scientific"
    private List<string> _history = new();
    private CalculatorEngine _engine = new();
    private bool _showHistory = false;
    
    // Process and application properties
    public int ProcessId { get; private set; }
    public string Id => "calculator";
    public string Name => "Calculator";
    public string Description => "A calculator application supporting basic and scientific calculations.";
    public string Version => "1.0.0";
    public string IconPath => "icons/calculator.png";
    public ApplicationType Type => ApplicationType.Window;
    public ApplicationMetadata Manifest { get; } = new ApplicationMetadata 
    { 
        Id = "calculator",
        Name = "Calculator",
        Description = "A calculator application supporting basic and scientific calculations.",
        Version = "1.0.0",
        Type = ApplicationType.Window
    };
    
    public ApplicationState State { get; private set; } = ApplicationState.Stopped;
    public UserSession? OwnerSession { get; private set; }
    
    // Events for IApplication
    public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;
    public event EventHandler<ApplicationOutputEventArgs>? OutputReceived;
    public event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;
    
    // UI Binding Properties
    protected string Display => _currentDisplay;
    protected string Mode => _currentMode;
    protected List<string> History => _history;
    protected bool ShowHistory => _showHistory;
    protected string CurrentExpression { get; set; } = string.Empty;
    protected bool HasMemoryValue => _engine.HasMemoryValue;
    protected bool IsRadianMode => _engine.IsRadianMode;
    
    /// <summary>
    /// Gets the window title, which includes the calculator mode
    /// </summary>
    protected override string WindowTitle => $"Calculator - {_currentMode} Mode";
    
    /// <summary>
    /// Gets the window icon
    /// </summary>
    protected override RenderFragment WindowIcon => builder =>
    {
        builder.OpenElement(0, "i");
        builder.AddAttribute(1, "class", "fas fa-calculator");
        builder.CloseElement();
    };
    
    /// <summary>
    /// Initializes the component when it's first rendered
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        // Set default window properties
        this.SetTitle(WindowTitle);
        this.SetSize(370, 500); // Default size for standard mode
        this.SetResizable(true);
        
        // Initialize the application
        await InitializeApplicationAsync();
    }
    
    /// <summary>
    /// Initializes the application, registers with the ApplicationBridge,
    /// and performs initial setup.
    /// </summary>
    private async Task InitializeApplicationAsync()
    {
        try
        {
            // Register with application bridge
            await ApplicationBridge.InitializeAsync(this, this);
            await ApplicationBridge.RegisterProcessAsync(this);
            await ApplicationBridge.RegisterApplicationAsync(this);
            
            // Try to load saved state
            await LoadStateAsync();
            
            // Set application state to running
            await SetStateAsync(ApplicationState.Running);
            
            _isInitialized = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error initializing Calculator: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Called when the window is closing. Performs cleanup operations.
    /// </summary>
    protected override async Task OnWindowClosingAsync()
    {
        try
        {
            // Save state before closing
            await SaveStateAsync();
            
            // Unregister application and process
            await ApplicationBridge.UnregisterApplicationAsync(this);
            await ApplicationBridge.TerminateProcessAsync(this);
            
            await base.OnWindowClosingAsync();
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error closing Calculator: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Raises an error event
    /// </summary>
    private async Task RaiseErrorAsync(string message, Exception? exception = null)
    {
        ErrorReceived?.Invoke(this, new ApplicationErrorEventArgs(message, exception));
        await ApplicationBridge.OnErrorAsync(this, message, exception);
    }
    
    /// <summary>
    /// Sets the application state
    /// </summary>
    public async Task SetStateAsync(ApplicationState newState)
    {
        if (State == newState) return;
        
        var oldState = State;
        State = newState;
        
        // Raise state changed event
        StateChanged?.Invoke(this, new ApplicationStateChangedEventArgs(oldState, newState));
        await ApplicationBridge.OnStateChangedAsync(this, oldState, newState);
        
        StateHasChanged();
    }
    
    /// <summary>
    /// Loads calculator state from the file system
    /// </summary>
    private async Task LoadStateAsync()
    {
        try
        {
            string appStateDir = $"/home/{OwnerSession?.Username}/.config/calculator";
            string stateFile = $"{appStateDir}/state.json";
            
            // Try to load saved state if it exists
            if (await FileSystem.ExistsAsync(stateFile))
            {
                var stateBytes = await FileSystem.ReadFileAsync(stateFile);
                if (stateBytes != null)
                {
                    var stateJson = Encoding.UTF8.GetString(stateBytes);
                    var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson);
                    
                    if (state != null)
                    {
                        // Restore display
                        if (state.TryGetValue("display", out var display))
                        {
                            _currentDisplay = display.GetString() ?? "0";
                        }
                        
                        // Restore mode
                        if (state.TryGetValue("mode", out var mode))
                        {
                            _currentMode = mode.GetString() ?? "Standard";
                        }
                        
                        // Restore history
                        if (state.TryGetValue("history", out var history))
                        {
                            var historyList = new List<string>();
                            foreach (var item in history.EnumerateArray())
                            {
                                var historyItem = item.GetString();
                                if (!string.IsNullOrEmpty(historyItem))
                                {
                                    historyList.Add(historyItem);
                                }
                            }
                            _history = historyList;
                        }
                        
                        // Restore engine state
                        if (state.TryGetValue("engineState", out var engineState))
                        {
                            _engine.SetState(engineState);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error loading calculator state: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Saves calculator state to the file system
    /// </summary>
    private async Task SaveStateAsync()
    {
        try
        {
            string? appStateDir = null;
            string? stateFile = null;
            
            // Get paths
            if (OwnerSession != null)
            {
                appStateDir = $"/home/{OwnerSession.Username}/.config/calculator";
                stateFile = $"{appStateDir}/state.json";
                
                // Ensure state directory exists
                await FileSystem.CreateDirectoryAsync(appStateDir);
                
                // Create state object
                var state = new Dictionary<string, object>
                {
                    { "display", _currentDisplay },
                    { "mode", _currentMode }
                };
                
                // Only save history if not empty
                if (_history.Count > 0)
                {
                    state["history"] = _history;
                }
                
                // Save calculator engine state
                state["engineState"] = _engine.GetState();
                
                // Serialize and save
                var stateJson = JsonSerializer.Serialize(state);
                var stateBytes = Encoding.UTF8.GetBytes(stateJson);
                await FileSystem.WriteFileAsync(stateFile, stateBytes);
            }
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error saving calculator state: {ex.Message}", ex);
        }
    }
    
    #region Calculator UI Events
    
    /// <summary>
    /// Handles digit button clicks
    /// </summary>
    protected async Task DigitClick(int digit)
    {
        try
        {
            string newDisplay = _engine.AddDigit(digit, _currentDisplay);
            _currentDisplay = newDisplay;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error processing digit: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Handles decimal point button click
    /// </summary>
    protected async Task DecimalPointClick()
    {
        try
        {
            string newDisplay = _engine.AddDecimalPoint(_currentDisplay);
            _currentDisplay = newDisplay;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error adding decimal point: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Handles operation button clicks
    /// </summary>
    protected async Task OperationClick(string operation)
    {
        try
        {
            string newDisplay = _engine.PerformOperation(_currentDisplay, operation);
            CurrentExpression = _engine.GetExpression();
            _currentDisplay = newDisplay;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error performing operation: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Handles equals button click
    /// </summary>
    protected async Task EqualsClick()
    {
        try
        {
            string result = _engine.CalculateResult(_currentDisplay);
            string expression = _engine.GetExpression();
            
            // Add to history
            if (!string.IsNullOrEmpty(expression) && expression != result)
            {
                _history.Add($"{expression} = {result}");
                await ApplicationBridge.OnOutputAsync(this, $"{expression} = {result}", OutputStreamType.Output);
            }
            
            _currentDisplay = result;
            CurrentExpression = string.Empty;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error calculating result: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Handles clear button click
    /// </summary>
    protected void ClearClick()
    {
        _engine.Clear();
        _currentDisplay = "0";
        CurrentExpression = string.Empty;
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles clear entry button click
    /// </summary>
    protected void ClearEntryClick()
    {
        _engine.ClearEntry();
        _currentDisplay = "0";
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles backspace button click
    /// </summary>
    protected void BackspaceClick()
    {
        if (_currentDisplay.Length <= 1 || _currentDisplay == "0" || _currentDisplay.StartsWith("Error"))
        {
            _currentDisplay = "0";
        }
        else
        {
            _currentDisplay = _currentDisplay.Substring(0, _currentDisplay.Length - 1);
            if (_currentDisplay == "-")
            {
                _currentDisplay = "0";
            }
        }
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles toggle sign button click
    /// </summary>
    protected void ToggleSignClick()
    {
        if (_currentDisplay.StartsWith("-"))
        {
            _currentDisplay = _currentDisplay.Substring(1);
        }
        else if (_currentDisplay != "0")
        {
            _currentDisplay = "-" + _currentDisplay;
        }
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles percentage button click
    /// </summary>
    protected void PercentageClick()
    {
        if (decimal.TryParse(_currentDisplay, out decimal value))
        {
            _currentDisplay = (value / 100).ToString();
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// Handles scientific function button clicks
    /// </summary>
    protected async Task ScientificFunctionClick(string function)
    {
        try
        {
            string result = _engine.PerformScientificFunction(_currentDisplay, function);
            _currentDisplay = result;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync($"Error performing function: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Toggles between standard and scientific modes
    /// </summary>
    protected void SwitchMode(string mode)
    {
        if (_currentMode != mode)
        {
            _currentMode = mode;
            
            // Adjust window size based on mode
            if (mode == "Scientific")
            {
                this.SetSize(580, 500);
            }
            else
            {
                this.SetSize(370, 500);
            }
            
            // Update window title
            this.SetTitle(WindowTitle);
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// Toggles between radian and degree mode
    /// </summary>
    protected void ToggleAngleMode()
    {
        _engine.IsRadianMode = !_engine.IsRadianMode;
        StateHasChanged();
    }
    
    /// <summary>
    /// Clears the calculation history
    /// </summary>
    protected void ClearHistory()
    {
        _history.Clear();
        StateHasChanged();
    }
    
    /// <summary>
    /// Uses a history item for a new calculation
    /// </summary>
    protected void UseHistoryItem(string historyItem)
    {
        int equalsIndex = historyItem.LastIndexOf('=');
        if (equalsIndex >= 0)
        {
            _currentDisplay = historyItem.Substring(equalsIndex + 1).Trim();
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// Handles memory clear button click
    /// </summary>
    protected void MemoryClear()
    {
        _engine.MemoryClear();
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles memory recall button click
    /// </summary>
    protected void MemoryRecall()
    {
        _currentDisplay = _engine.MemoryRecall();
        StateHasChanged();
    }
    
    /// <summary>
    /// Handles memory store button click
    /// </summary>
    protected void MemoryStore()
    {
        if (decimal.TryParse(_currentDisplay, out decimal value))
        {
            _engine.MemoryStore(value);
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// Handles memory add button click
    /// </summary>
    protected void MemoryAdd()
    {
        if (decimal.TryParse(_currentDisplay, out decimal value))
        {
            _engine.MemoryAdd(value);
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// Handles memory subtract button click
    /// </summary>
    protected void MemorySubtract()
    {
        if (decimal.TryParse(_currentDisplay, out decimal value))
        {
            _engine.MemorySubtract(value);
            StateHasChanged();
        }
    }
    
    #endregion
    
    #region IProcess Implementation
    
    public async Task<bool> StartAsync()
    {
        await SetStateAsync(ApplicationState.Starting);
        await SetStateAsync(ApplicationState.Running);
        return true;
    }
    
    public async Task<bool> StopAsync()
    {
        await SetStateAsync(ApplicationState.Stopping);
        
        // Save state before stopping
        await SaveStateAsync();
        
        await SetStateAsync(ApplicationState.Stopped);
        return true;
    }
    
    public Task<bool> KillAsync()
    {
        return StopAsync();
    }
    
    public async Task<bool> SuspendAsync()
    {
        await SetStateAsync(ApplicationState.Paused);
        return true;
    }
    
    public async Task<bool> ResumeAsync()
    {
        await SetStateAsync(ApplicationState.Running);
        return true;
    }
    
    #endregion
    
    #region IApplication Implementation
    
    public async Task StartAsync(ApplicationLaunchContext context)
    {
        OwnerSession = context.User;
        await StartAsync();
    }
    
    public Task StopAsync()
    {
        return StopAsync();
    }
    
    public Task PauseAsync()
    {
        return SuspendAsync();
    }
    
    #endregion
}
