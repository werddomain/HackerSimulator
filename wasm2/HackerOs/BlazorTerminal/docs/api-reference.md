# BlazorTerminal API Reference

This document provides comprehensive documentation of all public APIs, parameters, and configuration options available in BlazorTerminal.

## Table of Contents

1. [Terminal Component](#terminal-component)
2. [Models](#models)
3. [Services](#services)
4. [Extensions](#extensions)
5. [Utilities](#utilities)
6. [Events](#events)
7. [Configuration](#configuration)

## Terminal Component

### BlazorTerminal.Components.Terminal

The main terminal component that provides a complete terminal emulator interface.

#### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Rows` | `int` | `24` | Number of rows (lines) in the terminal display |
| `Columns` | `int` | `80` | Number of columns (characters per line) in the terminal |
| `FontSize` | `int` | `16` | Font size in pixels |
| `FontFamily` | `string` | `"'Courier New', monospace"` | CSS font family for terminal text |
| `Theme` | `TerminalTheme?` | `null` | Custom theme for colors and styling |
| `EnableScrollback` | `bool` | `true` | Enable scrollback buffer to store off-screen content |
| `ScrollbackLines` | `int` | `1000` | Maximum number of lines to store in scrollback buffer |
| `EnableSelection` | `bool` | `true` | Enable text selection with mouse |
| `EnableVirtualization` | `bool` | `false` | Enable virtualization for large buffers (performance optimization) |
| `EnableProfiling` | `bool` | `false` | Enable performance profiling and metrics collection |
| `AutoScroll` | `bool` | `true` | Automatically scroll to bottom when new content is added |
| `CursorBlinking` | `bool` | `true` | Enable cursor blinking animation |
| `CursorStyle` | `CursorStyle` | `CursorStyle.Block` | Cursor appearance style |

#### Public Methods

##### Text Output Methods

```csharp
// Write text at current cursor position
void Write(string text)

// Write text and move to next line
void WriteLine(string text)

// Clear the entire terminal display
void Clear()
```

##### ANSI Processing Methods

```csharp
// Process text with ANSI escape sequences
void ProcessAnsiText(string text)
```

##### Cursor Control Methods

```csharp
// Move cursor to specific position (0-based)
void SetCursorPosition(int x, int y)

// Get current cursor position
(int X, int Y) GetCursorPosition()
```

##### Scrolling Methods

```csharp
// Scroll up by specified number of lines
void ScrollUp(int lines = 1)

// Scroll down by specified number of lines
void ScrollDown(int lines = 1)

// Scroll to top of buffer
void ScrollToTop()

// Scroll to bottom of buffer
void ScrollToBottom()

// Get current scroll position as percentage (0.0 to 1.0)
double GetScrollPercentage()

// Set scroll position as percentage (0.0 to 1.0)
void SetScrollPercentage(double percentage)
```

##### Performance Methods

```csharp
// Get performance metrics (when profiling is enabled)
PerformanceMetric[] GetPerformanceMetrics()

// Clear all performance metrics
void ClearPerformanceMetrics()
```

##### Buffer Information Methods

```csharp
// Get total number of lines including scrollback
int GetTotalLines()

// Check if currently scrolled to bottom
bool IsAtBottom()
```

#### Events

##### OnInput Event

```csharp
EventCallback<string> OnInput
```

Fired when user provides input through keyboard. The string parameter contains the processed input.

**Example:**
```csharp
private void HandleInput(string input)
{
    // Process user input
    Console.WriteLine($"User typed: {input}");
}
```

##### OnSelectionChanged Event

```csharp
EventCallback<(int startRow, int startCol, int endRow, int endCol)> OnSelectionChanged
```

Fired when text selection changes. Parameters indicate the selection boundaries.

## Models

### TerminalTheme

Defines the visual appearance of the terminal.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Theme name for identification |
| `BackgroundColor` | `string` | Background color (CSS color value) |
| `ForegroundColor` | `string` | Default text color (CSS color value) |
| `CursorColor` | `string` | Cursor color (CSS color value) |
| `SelectionColor` | `string` | Text selection highlight color (CSS color value) |
| `AnsiColors` | `Dictionary<int, string>` | ANSI color palette (indices 0-15) |

#### Example

```csharp
var darkTheme = new TerminalTheme
{
    Name = "Dark",
    BackgroundColor = "#1e1e1e",
    ForegroundColor = "#d4d4d4",
    CursorColor = "#ffffff",
    SelectionColor = "#264f78",
    AnsiColors = new Dictionary<int, string>
    {
        { 0, "#000000" },  // Black
        { 1, "#cd3131" },  // Red
        { 2, "#0dbc79" },  // Green
        // ... more colors
    }
};
```

### TerminalStyle

Defines text styling attributes for terminal content.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ForegroundColor` | `Color?` | Text color |
| `BackgroundColor` | `Color?` | Background color |
| `IsBold` | `bool` | Bold text attribute |
| `IsItalic` | `bool` | Italic text attribute |
| `IsUnderline` | `bool` | Underlined text attribute |
| `IsStrikethrough` | `bool` | Strikethrough text attribute |
| `IsInverse` | `bool` | Inverted colors attribute |

### TerminalCursor

Represents the terminal cursor state and appearance.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `X` | `int` | Horizontal position (0-based) |
| `Y` | `int` | Vertical position (0-based) |
| `IsVisible` | `bool` | Whether cursor is visible |
| `IsBlinking` | `bool` | Whether cursor should blink |
| `Style` | `CursorStyle` | Cursor appearance style |
| `BlinkRate` | `int` | Blink rate in milliseconds |

### CursorStyle Enum

Defines available cursor styles.

```csharp
public enum CursorStyle
{
    Block,      // Solid block cursor
    Underline,  // Underline cursor
    Bar         // Vertical bar cursor
}
```

### TerminalBuffer

Internal buffer management class (advanced usage).

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Width` | `int` | Buffer width in columns |
| `Height` | `int` | Buffer height in rows |
| `Lines` | `ReadOnlyCollection<TerminalLine>` | Collection of terminal lines |

#### Events

```csharp
event EventHandler<EventArgs> ContentChanged;  // Fired when buffer content changes
event EventHandler<EventArgs> BufferChanged;   // Fired when buffer structure changes
```

### ScrollbackBuffer

Manages scrollback history for the terminal.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Number of lines in scrollback |
| `MaxLines` | `int` | Maximum number of lines to store |
| `Lines` | `ReadOnlyCollection<TerminalLine>` | Collection of scrollback lines |

#### Methods

```csharp
void AddLine(TerminalLine line);     // Add line to scrollback
void Clear();                        // Clear all scrollback content
void Resize(int newMaxLines);        // Change scrollback capacity
```

## Services

### AnsiParser

Parses ANSI escape sequences and converts them to terminal actions.

#### Events

```csharp
event EventHandler<string> TextReceived;           // Plain text content
event EventHandler<CursorUpdateEventArgs> CursorUpdate;  // Cursor movement commands
event EventHandler<StyleUpdateEventArgs> StyleUpdate;   // Text styling commands
event EventHandler<EraseEventArgs> EraseRequest;        // Screen/line clearing commands
```

#### Methods

```csharp
void ProcessText(string text);  // Process text containing ANSI sequences
```

### KeyboardService

Handles keyboard input and converts it to terminal-compatible sequences.

#### Events

```csharp
event EventHandler<string> InputReceived;  // Fired when keyboard input is processed
```

#### Methods

```csharp
string? ProcessKeyEvent(KeyboardEventArgs e);  // Process keyboard event
void HandleKeyDown(KeyboardEventArgs e);       // Handle key down event
```

### SelectionService

Manages text selection within the terminal.

#### Methods

```csharp
void StartSelection(int row, int column);         // Begin text selection
void UpdateSelection(int row, int column);        // Update selection end point
void EndSelection();                             // Complete selection
void ClearSelection();                           // Clear current selection
bool IsPositionSelected(int row, int column);    // Check if position is selected
string GetSelectedText(TerminalBuffer buffer);   // Extract selected text
```

### VirtualizationService

Optimizes rendering performance for large terminal buffers.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ViewportStart` | `int` | First visible line index |
| `ViewportSize` | `int` | Number of visible lines |
| `TotalLines` | `int` | Total lines available |

#### Methods

```csharp
void SetViewportSize(int size);           // Set viewport size
void ScrollToPosition(int position);      // Scroll to specific position
void ScrollBy(int lines);                 // Scroll by relative amount
void ScrollToTop();                       // Scroll to buffer top
void ScrollToBottom();                    // Scroll to buffer bottom
bool IsAtBottom();                        // Check if at bottom
VirtualizedLine[] GetVisibleLines();      // Get lines to render
double GetScrollPercentage();             // Get scroll position as percentage
void SetScrollPercentage(double pct);     // Set scroll position as percentage
```

### PerformanceProfiler

Collects and analyzes performance metrics.

#### Methods

```csharp
IDisposable StartTiming(string operationName);           // Start timing operation
void RecordTiming(string operationName, double ms);     // Record timing manually
void IncrementCounter(string counterName, long count);  // Increment counter
PerformanceMetric[] GetMetrics();                       // Get all metrics
PerformanceMetric? GetMetric(string name);             // Get specific metric
void ClearMetrics();                                    // Clear all metrics
string GetPerformanceSummary();                         // Get formatted summary
```

### PerformanceMetric

Individual performance metric data.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Metric name |
| `TimingCount` | `int` | Number of timing recordings |
| `CounterValue` | `long` | Counter value |
| `AverageMs` | `double` | Average timing in milliseconds |
| `MinMs` | `double` | Minimum timing in milliseconds |
| `MaxMs` | `double` | Maximum timing in milliseconds |
| `TotalMs` | `double` | Total timing in milliseconds |

## Extensions

### TerminalExtensions

Extension methods for enhanced terminal functionality.

#### Text Styling Methods

```csharp
Terminal WriteSuccess(this Terminal terminal, string text);  // Write green text
Terminal WriteError(this Terminal terminal, string text);    // Write red text
Terminal WriteWarning(this Terminal terminal, string text);  // Write yellow text
Terminal WriteInfo(this Terminal terminal, string text);     // Write blue text
```

#### Drawing Methods

```csharp
Terminal DrawBox(this Terminal terminal, string title, string? content = null, int width = 0);
Terminal DrawProgressBar(this Terminal terminal, int progress, int width = 40);
```

#### Example Usage

```csharp
terminal.WriteSuccess("Operation completed!")
        .WriteError("An error occurred!")
        .DrawBox("Status", "Everything is working")
        .DrawProgressBar(75, 50);
```

## Utilities

### ColorExtensions

Color utility methods for terminal color handling.

#### Methods

```csharp
string ToHtmlString(this Color color);          // Convert Color to CSS string
Color GetAnsiColor(int index);                  // Get ANSI palette color (0-15)
Color From256ColorIndex(int index);             // Convert 256-color index to Color
```

## Events

### Event Arguments Classes

#### CursorUpdateEventArgs

```csharp
public class CursorUpdateEventArgs : EventArgs
{
    public CursorDirection Direction { get; set; }
    public int Count { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
}
```

#### StyleUpdateEventArgs

```csharp
public class StyleUpdateEventArgs : EventArgs
{
    public TerminalStyle Style { get; set; }
}
```

#### EraseEventArgs

```csharp
public class EraseEventArgs : EventArgs
{
    public EraseType Type { get; set; }  // Display, Line
    public int Mode { get; set; }        // 0=cursor to end, 1=start to cursor, 2=entire
}
```

#### ViewportChangedEventArgs

```csharp
public class ViewportChangedEventArgs : EventArgs
{
    public int ViewportStart { get; set; }
    public int ViewportSize { get; set; }
    public int PreviousSize { get; set; }
}
```

## Configuration

### CSS Classes

The terminal component uses scoped CSS with the following key classes:

- `.terminal-container` - Main terminal container
- `.terminal-content` - Terminal content area
- `.terminal-line` - Individual terminal line
- `.terminal-character` - Individual character span
- `.terminal-cursor` - Cursor element
- `.terminal-selection` - Selected text highlighting

### CSS Custom Properties

You can customize appearance using CSS custom properties:

```css
.terminal-container {
    --terminal-font-family: 'Courier New', monospace;
    --terminal-font-size: 16px;
    --terminal-line-height: 1.2;
    --terminal-background: #1e1e1e;
    --terminal-foreground: #d4d4d4;
    --terminal-cursor-color: #ffffff;
    --terminal-selection-color: rgba(255, 255, 255, 0.3);
}
```

### JavaScript Interop

The component minimizes JavaScript usage but includes these interop functions:

- Clipboard operations (copy/paste)
- Focus management
- Scroll position synchronization

### Performance Considerations

#### Virtualization

Enable virtualization for terminals with large amounts of content:

```csharp
<Terminal EnableVirtualization="true" ScrollbackLines="10000" />
```

#### Bulk Updates

For batch operations, use bulk update methods to improve performance:

```csharp
// In custom implementations
buffer.BeginBulkUpdate();
try
{
    // Multiple operations
    buffer.Write("Line 1");
    buffer.WriteLine("Line 2");
    // etc.
}
finally
{
    buffer.EndBulkUpdate();
}
```

#### Profiling

Enable profiling to monitor performance:

```csharp
<Terminal EnableProfiling="true" />

// Later, check metrics
var metrics = terminal.GetPerformanceMetrics();
foreach (var metric in metrics)
{
    Console.WriteLine(metric.ToString());
}
```

This API reference covers all public interfaces available in BlazorTerminal. For more detailed examples, see the [Usage Examples](examples.md) documentation.
