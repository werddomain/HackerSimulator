# BlazorTerminal Usage Examples

This document provides comprehensive examples of how to use the BlazorTerminal component in your Blazor applications.

## Table of Contents

1. [Basic Setup](#basic-setup)
2. [Simple Terminal](#simple-terminal)
3. [Interactive Terminal](#interactive-terminal)
4. [Styled Output](#styled-output)
5. [Performance Optimization](#performance-optimization)
6. [Custom Themes](#custom-themes)
7. [Advanced Features](#advanced-features)
8. [JavaScript Interop](#javascript-interop)

## Basic Setup

### 1. Install the Package

```bash
dotnet add package BlazorTerminal
```

### 2. Add to Program.cs (Blazor WebAssembly)

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
```

### 3. Add CSS Reference (in index.html)

```html
<link href="_content/BlazorTerminal/BlazorTerminal.bundle.scp.css" rel="stylesheet" />
```

## Simple Terminal

Here's a basic terminal that displays text and handles user input:

```razor
@page "/"
@using BlazorTerminal.Components

<h1>Simple Terminal</h1>

<Terminal @ref="terminal" 
          Rows="24" 
          Columns="80" 
          FontSize="16"
          OnInput="HandleInput" />

@code {
    private Terminal? terminal;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && terminal != null)
        {
            terminal.WriteLine("Welcome to BlazorTerminal!");
            terminal.WriteLine("Type 'help' for available commands.");
            terminal.Write("$ ");
        }
    }

    private void HandleInput(string input)
    {
        terminal?.WriteLine($"You typed: {input}");
        terminal?.Write("$ ");
    }
}
```

## Interactive Terminal

A more sophisticated terminal that processes commands:

```razor
@page "/interactive"
@using BlazorTerminal.Components
@using BlazorTerminal.Extensions

<h1>Interactive Terminal</h1>

<Terminal @ref="terminal" 
          Rows="30" 
          Columns="100" 
          FontSize="14"
          OnInput="HandleCommand"
          EnableScrollback="true"
          ScrollbackLines="1000" />

@code {
    private Terminal? terminal;
    private readonly Dictionary<string, Func<string[], Task>> commands = new();

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && terminal != null)
        {
            SetupCommands();
            ShowWelcome();
            ShowPrompt();
        }
    }

    private void SetupCommands()
    {
        commands["help"] = HelpCommand;
        commands["clear"] = ClearCommand;
        commands["echo"] = EchoCommand;
        commands["date"] = DateCommand;
        commands["progress"] = ProgressCommand;
        commands["colors"] = ColorsCommand;
    }

    private void ShowWelcome()
    {
        terminal?.WriteLine("Interactive Terminal Demo");
        terminal?.WriteLine("========================");
        terminal?.WriteLine("Type 'help' to see available commands.");
        terminal?.WriteLine();
    }

    private void ShowPrompt()
    {
        terminal?.Write("\x1b[32m$\x1b[0m "); // Green prompt
    }

    private async void HandleCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            ShowPrompt();
            return;
        }

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var args = parts.Skip(1).ToArray();

        if (commands.TryGetValue(command, out var handler))
        {
            await handler(args);
        }
        else
        {
            terminal?.WriteError($"Command not found: {command}");
        }

        ShowPrompt();
    }

    private Task HelpCommand(string[] args)
    {
        terminal?.WriteLine("Available commands:");
        terminal?.WriteLine("  help     - Show this help message");
        terminal?.WriteLine("  clear    - Clear the terminal");
        terminal?.WriteLine("  echo     - Echo text back");
        terminal?.WriteLine("  date     - Show current date/time");
        terminal?.WriteLine("  progress - Show a progress bar demo");
        terminal?.WriteLine("  colors   - Show color palette");
        return Task.CompletedTask;
    }

    private Task ClearCommand(string[] args)
    {
        terminal?.Clear();
        ShowWelcome();
        return Task.CompletedTask;
    }

    private Task EchoCommand(string[] args)
    {
        terminal?.WriteLine(string.Join(" ", args));
        return Task.CompletedTask;
    }

    private Task DateCommand(string[] args)
    {
        terminal?.WriteLine($"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        return Task.CompletedTask;
    }

    private async Task ProgressCommand(string[] args)
    {
        terminal?.WriteLine("Running progress demo...");
        
        for (int i = 0; i <= 100; i += 5)
        {
            terminal?.Write($"\rProgress: ");
            terminal?.DrawProgressBar(i, 30);
            terminal?.Write($" {i}%");
            
            await Task.Delay(100);
        }
        
        terminal?.WriteLine("\nCompleted!");
    }

    private Task ColorsCommand(string[] args)
    {
        terminal?.WriteLine("Standard colors:");
        
        // Show standard ANSI colors
        for (int i = 30; i <= 37; i++)
        {
            terminal?.Write($"\x1b[{i}m■\x1b[0m ");
        }
        terminal?.WriteLine();
        
        terminal?.WriteLine("Bright colors:");
        for (int i = 90; i <= 97; i++)
        {
            terminal?.Write($"\x1b[{i}m■\x1b[0m ");
        }
        terminal?.WriteLine();
        
        return Task.CompletedTask;
    }
}
```

## Styled Output

Demonstrating ANSI escape sequences for text styling:

```razor
@page "/styled"
@using BlazorTerminal.Components

<h1>Styled Terminal Output</h1>

<Terminal @ref="terminal" 
          Rows="25" 
          Columns="80" 
          FontSize="16" />

@code {
    private Terminal? terminal;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && terminal != null)
        {
            ShowStyledOutput();
        }
    }

    private void ShowStyledOutput()
    {
        // Colors
        terminal?.WriteLine("\x1b[31mRed text\x1b[0m");
        terminal?.WriteLine("\x1b[32mGreen text\x1b[0m");
        terminal?.WriteLine("\x1b[34mBlue text\x1b[0m");
        terminal?.WriteLine("\x1b[33mYellow text\x1b[0m");
        
        // Background colors
        terminal?.WriteLine("\x1b[41mRed background\x1b[0m");
        terminal?.WriteLine("\x1b[42mGreen background\x1b[0m");
        
        // Text attributes
        terminal?.WriteLine("\x1b[1mBold text\x1b[0m");
        terminal?.WriteLine("\x1b[3mItalic text\x1b[0m");
        terminal?.WriteLine("\x1b[4mUnderlined text\x1b[0m");
        
        // Combined styling
        terminal?.WriteLine("\x1b[1;31;42mBold red text on green background\x1b[0m");
        
        // 256-color mode
        terminal?.WriteLine("\x1b[38;5;196mBright red (256-color)\x1b[0m");
        terminal?.WriteLine("\x1b[38;5;46mBright green (256-color)\x1b[0m");
        
        // RGB colors
        terminal?.WriteLine("\x1b[38;2;255;165;0mOrange RGB color\x1b[0m");
        terminal?.WriteLine("\x1b[38;2;138;43;226mBlue violet RGB color\x1b[0m");
    }
}
```

## Performance Optimization

Example showing performance features for handling large amounts of data:

```razor
@page "/performance"
@using BlazorTerminal.Components

<h1>Performance Demo</h1>

<div class="controls">
    <button @onclick="StartOutput">Start Output</button>
    <button @onclick="StopOutput">Stop Output</button>
    <button @onclick="ShowMetrics">Show Performance Metrics</button>
</div>

<Terminal @ref="terminal" 
          Rows="30" 
          Columns="120" 
          FontSize="12"
          EnableVirtualization="true"
          EnableProfiling="true"
          ScrollbackLines="10000" />

@code {
    private Terminal? terminal;
    private CancellationTokenSource? cancellationTokenSource;

    private async void StartOutput()
    {
        if (terminal == null) return;
        
        cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            int lineNumber = 1;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Simulate log output
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var level = lineNumber % 10 == 0 ? "ERROR" : lineNumber % 5 == 0 ? "WARN" : "INFO";
                var color = level switch
                {
                    "ERROR" => "\x1b[31m",
                    "WARN" => "\x1b[33m",
                    _ => "\x1b[32m"
                };
                
                terminal.WriteLine($"{timestamp} [{color}{level}\x1b[0m] Line {lineNumber}: Sample log message with some data");
                lineNumber++;
                
                // Batch updates for better performance
                if (lineNumber % 10 == 0)
                {
                    await Task.Delay(50, cancellationTokenSource.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            terminal.WriteLine("\nOutput stopped.");
        }
    }

    private void StopOutput()
    {
        cancellationTokenSource?.Cancel();
    }

    private void ShowMetrics()
    {
        if (terminal == null) return;
        
        var metrics = terminal.GetPerformanceMetrics();
        
        terminal.WriteLine("\n=== Performance Metrics ===");
        foreach (var metric in metrics)
        {
            terminal.WriteLine(metric.ToString());
        }
        terminal.WriteLine("=========================");
    }
}
```

## Custom Themes

Example of creating and applying custom themes:

```razor
@page "/themes"
@using BlazorTerminal.Components
@using BlazorTerminal.Models

<h1>Custom Themes</h1>

<div class="theme-selector">
    <button @onclick="() => ApplyTheme(CreateDarkTheme())">Dark Theme</button>
    <button @onclick="() => ApplyTheme(CreateLightTheme())">Light Theme</button>
    <button @onclick="() => ApplyTheme(CreateMatrixTheme())">Matrix Theme</button>
    <button @onclick="() => ApplyTheme(CreateRetroTheme())">Retro Theme</button>
</div>

<Terminal @ref="terminal" 
          Rows="25" 
          Columns="80" 
          FontSize="16"
          Theme="currentTheme" />

@code {
    private Terminal? terminal;
    private TerminalTheme currentTheme = CreateDarkTheme();

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && terminal != null)
        {
            ShowThemeDemo();
        }
    }

    private void ApplyTheme(TerminalTheme theme)
    {
        currentTheme = theme;
        StateHasChanged();
        
        terminal?.WriteLine($"Applied {theme.Name} theme!");
    }

    private void ShowThemeDemo()
    {
        terminal?.WriteLine("Theme Demo Terminal");
        terminal?.WriteLine("==================");
        terminal?.WriteLine("Use the buttons above to switch themes.");
        terminal?.WriteLine();
        terminal?.WriteLine("\x1b[31mRed\x1b[0m \x1b[32mGreen\x1b[0m \x1b[33mYellow\x1b[0m \x1b[34mBlue\x1b[0m \x1b[35mMagenta\x1b[0m \x1b[36mCyan\x1b[0m");
        terminal?.WriteLine("\x1b[1mBold\x1b[0m \x1b[3mItalic\x1b[0m \x1b[4mUnderline\x1b[0m");
    }

    private static TerminalTheme CreateDarkTheme()
    {
        return new TerminalTheme
        {
            Name = "Dark",
            BackgroundColor = "#1e1e1e",
            ForegroundColor = "#d4d4d4",
            CursorColor = "#ffffff",
            SelectionColor = "#264f78"
        };
    }

    private static TerminalTheme CreateLightTheme()
    {
        return new TerminalTheme
        {
            Name = "Light",
            BackgroundColor = "#ffffff",
            ForegroundColor = "#000000",
            CursorColor = "#000000",
            SelectionColor = "#add6ff"
        };
    }

    private static TerminalTheme CreateMatrixTheme()
    {
        return new TerminalTheme
        {
            Name = "Matrix",
            BackgroundColor = "#000000",
            ForegroundColor = "#00ff00",
            CursorColor = "#00ff00",
            SelectionColor = "#003300"
        };
    }

    private static TerminalTheme CreateRetroTheme()
    {
        return new TerminalTheme
        {
            Name = "Retro",
            BackgroundColor = "#000040",
            ForegroundColor = "#ffff00",
            CursorColor = "#ffff00",
            SelectionColor = "#404000"
        };
    }
}
```

## Advanced Features

Example showcasing advanced terminal features:

```razor
@page "/advanced"
@using BlazorTerminal.Components
@using BlazorTerminal.Extensions

<h1>Advanced Features</h1>

<div class="controls">
    <button @onclick="ShowScrollDemo">Scroll Demo</button>
    <button @onclick="ShowSelectionDemo">Selection Demo</button>
    <button @onclick="ShowBoxDemo">Box Drawing</button>
    <button @onclick="ShowCursorDemo">Cursor Demo</button>
</div>

<Terminal @ref="terminal" 
          Rows="30" 
          Columns="100" 
          FontSize="14"
          EnableScrollback="true"
          EnableSelection="true"
          ScrollbackLines="1000" />

@code {
    private Terminal? terminal;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && terminal != null)
        {
            terminal.WriteLine("Advanced Features Demo");
            terminal.WriteLine("======================");
            terminal.WriteLine("Use the buttons above to try different features.");
            terminal.WriteLine();
        }
    }

    private async void ShowScrollDemo()
    {
        terminal?.WriteLine("Scroll Demo - Generating 100 lines...");
        
        for (int i = 1; i <= 100; i++)
        {
            terminal?.WriteLine($"Line {i:D3}: This is a sample line to demonstrate scrolling functionality.");
            
            if (i % 10 == 0)
            {
                await Task.Delay(100);
            }
        }
        
        terminal?.WriteLine("Done! Try scrolling up to see previous content.");
    }

    private void ShowSelectionDemo()
    {
        terminal?.WriteLine("Selection Demo");
        terminal?.WriteLine("==============");
        terminal?.WriteLine("You can select text by clicking and dragging.");
        terminal?.WriteLine("Selected text can be copied to clipboard.");
        terminal?.WriteLine("Try selecting this text with your mouse!");
        terminal?.WriteLine("Multi-line selections are also supported.");
        terminal?.WriteLine("The selection will highlight in the configured color.");
    }

    private void ShowBoxDemo()
    {
        terminal?.WriteLine("Box Drawing Demo");
        terminal?.WriteLine("================");
        
        // Draw some boxes using extension methods
        terminal?.DrawBox("Simple Box");
        terminal?.WriteLine();
        
        terminal?.DrawBox("Box with Content", "This box contains\nmultiple lines\nof content!", 40);
        terminal?.WriteLine();
        
        // Draw custom box manually
        terminal?.WriteLine("┌─────────────────────────┐");
        terminal?.WriteLine("│     Custom Box          │");
        terminal?.WriteLine("│                         │");
        terminal?.WriteLine("│  ┌─────────────────┐    │");
        terminal?.WriteLine("│  │   Nested Box    │    │");
        terminal?.WriteLine("│  └─────────────────┘    │");
        terminal?.WriteLine("└─────────────────────────┘");
    }

    private async void ShowCursorDemo()
    {
        terminal?.WriteLine("Cursor Movement Demo");
        terminal?.WriteLine("====================");
        
        // Save cursor position
        terminal?.Write("\x1b[s");
        
        // Move cursor and write
        terminal?.Write("\x1b[10;10H"); // Move to row 10, column 10
        terminal?.Write("Cursor at (10,10)");
        
        await Task.Delay(1000);
        
        terminal?.Write("\x1b[15;20H"); // Move to row 15, column 20
        terminal?.Write("Cursor at (15,20)");
        
        await Task.Delay(1000);
        
        // Restore cursor position
        terminal?.Write("\x1b[u");
        terminal?.WriteLine("Cursor restored!");
        
        // Clear line demo
        terminal?.Write("This line will be partially cleared...");
        await Task.Delay(1000);
        terminal?.Write("\x1b[K"); // Clear from cursor to end of line
        terminal?.WriteLine("Cleared!");
    }
}

<style>
    .controls {
        margin-bottom: 20px;
    }
    
    .controls button {
        margin-right: 10px;
        padding: 8px 16px;
        background-color: #007bff;
        color: white;
        border: none;
        border-radius: 4px;
        cursor: pointer;
    }
    
    .controls button:hover {
        background-color: #0056b3;
    }
    
    .theme-selector {
        margin-bottom: 20px;
    }
    
    .theme-selector button {
        margin-right: 10px;
        padding: 8px 16px;
        background-color: #28a745;
        color: white;
        border: none;
        border-radius: 4px;
        cursor: pointer;
    }
    
    .theme-selector button:hover {
        background-color: #1e7e34;
    }
</style>
```

## JavaScript Interop

Example of integrating with JavaScript for clipboard operations:

```razor
@page "/clipboard"
@using BlazorTerminal.Components
@inject IJSRuntime JSRuntime

<h1>Clipboard Integration</h1>

<Terminal @ref="terminal" 
          Rows="20" 
          Columns="80" 
          FontSize="16"
          OnInput="HandleInput"
          EnableSelection="true" />

@code {
    private Terminal? terminal;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && terminal != null)
        {
            terminal.WriteLine("Clipboard Integration Demo");
            terminal.WriteLine("==========================");
            terminal.WriteLine("Commands:");
            terminal.WriteLine("  copy <text>  - Copy text to clipboard");
            terminal.WriteLine("  paste        - Paste from clipboard");
            terminal.WriteLine("  clear        - Clear terminal");
            terminal.WriteLine();
            terminal.Write("$ ");
        }
    }

    private async void HandleInput(string input)
    {
        var parts = input.Split(' ', 2);
        var command = parts[0].ToLower();

        switch (command)
        {
            case "copy":
                if (parts.Length > 1)
                {
                    await CopyToClipboard(parts[1]);
                    terminal?.WriteLine($"Copied: {parts[1]}");
                }
                else
                {
                    terminal?.WriteLine("Usage: copy <text>");
                }
                break;

            case "paste":
                var pastedText = await PasteFromClipboard();
                terminal?.WriteLine($"Pasted: {pastedText}");
                break;

            case "clear":
                terminal?.Clear();
                terminal?.WriteLine("Terminal cleared.");
                break;

            default:
                terminal?.WriteLine($"Unknown command: {command}");
                break;
        }

        terminal?.Write("$ ");
    }

    private async Task CopyToClipboard(string text)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (Exception ex)
        {
            terminal?.WriteLine($"Failed to copy: {ex.Message}");
        }
    }

    private async Task<string> PasteFromClipboard()
    {
        try
        {
            return await JSRuntime.InvokeAsync<string>("navigator.clipboard.readText");
        }
        catch (Exception ex)
        {
            terminal?.WriteLine($"Failed to paste: {ex.Message}");
            return "";
        }
    }
}
```

## Configuration Options

Complete list of available parameters:

```razor
<Terminal @ref="terminal"
          Rows="24"                          @* Number of rows *@
          Columns="80"                       @* Number of columns *@
          FontSize="16"                      @* Font size in pixels *@
          FontFamily="'Courier New', monospace" @* Font family *@
          Theme="customTheme"                @* Custom theme *@
          EnableScrollback="true"            @* Enable scrollback buffer *@
          ScrollbackLines="1000"             @* Number of lines in scrollback *@
          EnableSelection="true"             @* Enable text selection *@
          EnableVirtualization="true"        @* Enable virtualization for performance *@
          EnableProfiling="false"            @* Enable performance profiling *@
          AutoScroll="true"                  @* Auto-scroll on new content *@
          CursorBlinking="true"              @* Enable cursor blinking *@
          CursorStyle="Block"                @* Cursor style (Block, Underline, Bar) *@
          OnInput="HandleInput"              @* Input event handler *@
          OnSelectionChanged="HandleSelection" @* Selection changed event *@ />
```

These examples demonstrate the full range of capabilities available in BlazorTerminal. You can combine these features to create rich terminal interfaces for your Blazor applications.
