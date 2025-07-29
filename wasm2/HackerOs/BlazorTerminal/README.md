# BlazorTerminal

A fully-featured terminal emulator component for Blazor applications, built primarily in C# with minimal JavaScript. BlazorTerminal provides a complete terminal experience with advanced features like ANSI support, scrollback buffer, and text selection.

## Features

- 🖥️ **Terminal Emulation** - Full terminal emulation with cursor positioning and ANSI escape sequence support
- 🎨 **Text Styling** - Colors, bold, italic, underline, and other text attributes
- ⌨️ **Input Handling** - Complete keyboard support including special keys and key combinations
- 📜 **Scrollback Buffer** - Configurable buffer for viewing previous output
- ✂️ **Selection & Clipboard** - Select text with mouse and integrate with clipboard
- 🎭 **Theming** - Customize appearance with different themes and color schemes
- 📦 **Pure Blazor** - Built primarily in C# with minimal JavaScript dependencies

## Installation

```shell
dotnet add package BlazorTerminal
```

## Basic Usage

```html
@page "/"

<h1>BlazorTerminal Demo</h1>

<BlazorTerminal.Components.Terminal 
    @ref="terminal"
    Rows="24" 
    Columns="80" 
    FontSize="16" />

<button @onclick="RunCommand">Run Command</button>

@code {
    private Terminal? terminal;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && terminal != null)
        {
            terminal.WriteLine("Welcome to BlazorTerminal!");
            terminal.WriteLine("Type 'help' for available commands.");
            terminal.OnInput += HandleInput;
        }
    }

    private void HandleInput(string input)
    {
        if (terminal == null) return;
        
        // Echo input
        terminal.WriteLine($"> {input}");
        
        // Process commands
        if (input.Trim().ToLower() == "help")
        {
            terminal.WriteLine("Available commands:");
            terminal.WriteLine("  clear - Clear the terminal");
            terminal.WriteLine("  colors - Show ANSI colors");
            terminal.WriteLine("  help - Show this help");
        }
        else if (input.Trim().ToLower() == "clear")
        {
            terminal.Clear();
        }
        else if (input.Trim().ToLower() == "colors")
        {
            ShowColors();
        }
    }
    
    private void RunCommand()
    {
        if (terminal != null)
        {
            terminal.WriteLine("\u001b[32mRunning demo command...\u001b[0m");
            // Simulate command output
            terminal.WriteLine("Processing...");
            terminal.WriteLine("Done!");
        }
    }
    
    private void ShowColors()
    {
        if (terminal == null) return;
        
        terminal.WriteLine("ANSI Colors Demo:");
        
        // Foreground colors
        terminal.WriteLine("Foreground colors:");
        for (int i = 30; i <= 37; i++)
        {
            terminal.Write($"\u001b[{i}m Color {i} \u001b[0m ");
        }
        terminal.WriteLine();
        
        // Background colors
        terminal.WriteLine("Background colors:");
        for (int i = 40; i <= 47; i++)
        {
            terminal.Write($"\u001b[{i}m Color {i} \u001b[0m ");
        }
        terminal.WriteLine();
    }
}
```

## Configuration

The Terminal component supports numerous configuration options:

```html
<Terminal 
    Rows="24"                  <!-- Number of rows in the terminal -->
    Columns="80"               <!-- Number of columns in the terminal -->
    FontSize="16"              <!-- Font size in pixels -->
    FontFamily="monospace"     <!-- Font family -->
    Theme="@darkTheme"         <!-- Theme object for styling -->
    CursorStyle="Block"        <!-- Cursor style: Block, Underline, or Bar -->
    CursorBlink="true"         <!-- Whether cursor blinks -->
    EnableSelection="true"     <!-- Enable text selection -->
    ScrollbackBufferSize="1000" <!-- Number of lines in scrollback buffer -->
    AutoScrollToBottom="true"  <!-- Auto-scroll on new output -->
    @ref="terminalRef" />
```

## Events

The Terminal component provides several events:

```csharp
// Called when user enters input
terminal.OnInput += (input) => Console.WriteLine($"User input: {input}");

// Called when text is selected
terminal.OnSelection += (text) => Console.WriteLine($"Selected: {text}");

// Called when terminal content changes
terminal.OnChange += () => StateHasChanged();
```

## Theming

You can create custom themes:

```csharp
// Create a dark theme
var darkTheme = new TerminalTheme
{
    Background = "#1E1E1E",
    Foreground = "#FFFFFF",
    FontFamily = "Consolas, monospace",
    CursorColor = "#FFFFFF",
    CursorStyle = CursorStyle.Block
};

// Create a light theme
var lightTheme = TerminalTheme.Light();

// Create a retro theme
var retroTheme = TerminalTheme.Retro();
```

## ANSI Support

The terminal supports standard ANSI escape sequences:

- **Text Styling**: Colors, bold, italic, underline, etc.
- **Cursor Movement**: Up, down, left, right, absolute positioning
- **Screen Control**: Clear screen, clear line, scroll
- **Special Keys**: Arrow keys, function keys, etc.

Example:
```csharp
// Bold red text
terminal.Write("\u001b[1;31mBold Red\u001b[0m");

// Move cursor to position 10,5
terminal.Write("\u001b[5;10H");

// Clear screen
terminal.Write("\u001b[2J");
```

## Advanced Usage

### Running a Command with Output

```csharp
public async Task SimulateCommand(string command)
{
    if (terminal == null) return;
    
    terminal.WriteLine($"> {command}");
    
    // Simulate process output
    await Task.Delay(500);
    terminal.Write("Loading"); 
    
    for (int i = 0; i < 3; i++)
    {
        await Task.Delay(300);
        terminal.Write(".");
    }
    
    terminal.WriteLine();
    terminal.WriteLine("Complete!");
}
```

### Creating an Interactive Terminal

```csharp
private string commandBuffer = string.Empty;
private List<string> commandHistory = new List<string>();
private int historyIndex = -1;

private void HandleTerminalKeyDown(KeyboardEventArgs e)
{
    if (e.Key == "Enter")
    {
        ProcessCommand(commandBuffer);
        commandBuffer = string.Empty;
    }
    else if (e.Key == "Backspace" && commandBuffer.Length > 0)
    {
        commandBuffer = commandBuffer.Substring(0, commandBuffer.Length - 1);
    }
    else if (e.Key == "ArrowUp")
    {
        NavigateHistory(-1);
    }
    else if (e.Key == "ArrowDown")
    {
        NavigateHistory(1);
    }
    else if (e.Key.Length == 1)
    {
        commandBuffer += e.Key;
    }
    
    RenderPrompt();
}

private void RenderPrompt()
{
    terminal.Clear();
    terminal.Write("> " + commandBuffer);
}

private void NavigateHistory(int direction)
{
    if (commandHistory.Count == 0) return;
    
    historyIndex = Math.Clamp(historyIndex + direction, -1, commandHistory.Count - 1);
    
    if (historyIndex == -1)
        commandBuffer = string.Empty;
    else
        commandBuffer = commandHistory[historyIndex];
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Inspired by xterm.js but built specifically for Blazor
- Thanks to the Blazor team for making this possible
