using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.UI;
using HackerOs.OS.Applications.Lifecycle;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using HackerOs.OS.IO;
using HackerOs.OS.User;
using HackerOs.OS.HSystem.Text;

namespace HackerOs.OS.Applications.BuiltIn.Calculator;

/// <summary>
/// Calculator application for HackerOS
/// </summary>
[App("Calculator", "builtin.Calculator")]
[AppDescription("A calculator application supporting basic and scientific calculations.")]
public class CalculatorApplication : WindowApplicationBase
{
    private string _currentDisplay = "0";
    private string _currentMode = "Standard"; // "Standard" or "Scientific"
    private List<string> _history = new();
    private CalculatorEngine _engine = new();
    
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
    /// Get the content for the application window
    /// </summary>
    protected override RenderFragment GetWindowContent() => builder =>
    {
        builder.OpenComponent<CalculatorComponent>(0);
        builder.AddAttribute(1, "Display", _currentDisplay);
        builder.AddAttribute(2, "DisplayChanged", EventCallback.Factory.Create<string>(this, OnDisplayChanged));
        builder.AddAttribute(3, "Mode", _currentMode);
        builder.AddAttribute(4, "ModeChanged", EventCallback.Factory.Create<string>(this, OnModeChanged));
        builder.AddAttribute(5, "History", _history);
        builder.AddAttribute(6, "HistoryChanged", EventCallback.Factory.Create<List<string>>(this, OnHistoryChanged));
        builder.AddAttribute(7, "Engine", _engine);
        builder.CloseComponent();
    };

    /// <summary>
    /// Called when the application starts
    /// </summary>
    protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
    {
        try
        {
            string appStateDir = $"/home/{context.User?.Username}/.config/calculator";
            string stateFile = $"{appStateDir}/state.json";

            var virtualFileSystem = context.FileSystem;
            if (virtualFileSystem != null)
            {
                // Try to load saved state if it exists
                if (await virtualFileSystem.ExistsAsync(stateFile))
                {
                    var stateBytes = await virtualFileSystem.ReadFileAsync(stateFile);
                    if (stateBytes != null)
                    {
                        var stateJson = Encoding.UTF8.GetString(stateBytes);
                        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson);
                        if (state != null)
                        {
                            await LoadStateAsync(state);
                        }
                    }
                }
            }

            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Called when the application stops
    /// </summary>
    protected override async Task<bool> OnStopAsync()
    {
        try
        {
            string? appStateDir = null;
            string? stateFile = null;

            // Get paths
            var userRef = base.User;
            var metadataRef = base.Metadata;
            if (userRef != null && metadataRef != null)
            {
                appStateDir = $"/home/{userRef.Username}/.config/{metadataRef.Id}";
                stateFile = $"{appStateDir}/state.json";
            }

            var fsRef = base.FileSystem;
            if (fsRef != null && appStateDir != null && stateFile != null)
            {
                // Ensure state directory exists
                await fsRef.CreateDirectoryAsync(appStateDir);

                // Save state
                var state = await SaveStateAsync();
                var stateJson = JsonSerializer.Serialize(state);
                var stateBytes = Encoding.UTF8.GetBytes(stateJson);
                await fsRef.WriteFileAsync(stateFile, stateBytes);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task LoadStateAsync(Dictionary<string, JsonElement> state)
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
            _history = history.EnumerateArray()
                .Select(h => h.GetString() ?? string.Empty)
                .Where(h => !string.IsNullOrEmpty(h))
                .ToList();
        }

        // Restore engine state
        if (state.TryGetValue("engineState", out var engineState))
        {
            _engine.SetState(engineState);
        }

        await Task.CompletedTask;
    }

    private async Task<Dictionary<string, object>> SaveStateAsync()
    {
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

        await Task.CompletedTask;
        return state;
    }

    private void OnDisplayChanged(string newDisplay)
    {
        _currentDisplay = newDisplay;
    }
    
    private void OnModeChanged(string newMode)
    {
        _currentMode = newMode;
        // Update window title when mode changes
        Window?.SetTitle(WindowTitle);
    }
    
    private void OnHistoryChanged(List<string> newHistory)
    {
        _history = newHistory;
    }
}
