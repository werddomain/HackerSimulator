# BlazorWindowManager Usage Guide

This document provides comprehensive documentation on how to use the BlazorWindowManager to create window-based applications in HackerOS.

## Table of Contents

1. [Window Component Structure](#window-component-structure)
2. [Creating a Basic Window Application](#creating-a-basic-window-application)
3. [Window Lifecycle](#window-lifecycle)
4. [JavaScript Integration](#javascript-integration)
5. [Styling Windows](#styling-windows)
6. [Advanced Window Features](#advanced-window-features)

## Window Component Structure

Window-based applications in HackerOS follow a specific structure with separate files for different aspects of the component:

- **ComponentName.razor** - Main component with HTML/Razor markup
- **ComponentName.razor.cs** - Code-behind class with C# logic
- **ComponentName.razor.css** - Isolated CSS styles for the component
- **ComponentName.razor.js** - JavaScript interop code (optional)

This separation allows for cleaner code organization and better maintainability.

## Creating a Basic Window Application

### Step 1: Create Application Class

First, create a class that inherits from `WindowApplicationBase` and implements the required methods:

```csharp
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.UI;
using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.BuiltIn;

[App(
    Id = "builtin.Notepad", 
    Name = "Notepad",
    IconPath = "fa-solid:file-alt"
)]
[AppDescription("Simple text editor for creating and editing text files.")]
public class NotepadApplication : WindowApplicationBase
{
    private string _content = string.Empty;
    private string? _currentFilePath;
    
    protected override RenderFragment GetWindowContent()
    {
        return builder =>
        {
            builder.OpenComponent<NotepadComponent>(0);
            builder.AddAttribute(1, "Content", _content);
            builder.AddAttribute(2, "ContentChanged", EventCallback.Factory.Create<string>(this, OnContentChanged));
            builder.AddAttribute(3, "FilePath", _currentFilePath);
            builder.AddAttribute(4, "FilePathChanged", EventCallback.Factory.Create<string?>(this, OnFilePathChanged));
            builder.CloseComponent();
        };
    }
    
    private void OnContentChanged(string newContent)
    {
        _content = newContent;
    }
    
    private void OnFilePathChanged(string? newPath)
    {
        _currentFilePath = newPath;
    }
    
    protected override Dictionary<string, object> GetStateForSerialization()
    {
        var state = new Dictionary<string, object>();
        
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            state["filePath"] = _currentFilePath;
        }
        
        return state;
    }
}
```

### Step 2: Create the Component Files

Next, create the component files for your application:

#### NotepadComponent.razor

```razor
@using HackerOs.OS.Applications.UI
@inherits ComponentBase

<div class="notepad-container">
    <div class="notepad-toolbar">
        <button class="toolbar-button" @onclick="NewFile">New</button>
        <button class="toolbar-button" @onclick="OpenFile">Open</button>
        <button class="toolbar-button" @onclick="SaveFile">Save</button>
        <button class="toolbar-button" @onclick="SaveFileAs">Save As</button>
    </div>
    
    <div class="notepad-editor">
        <textarea 
            @bind="Content" 
            @bind:event="oninput" 
            placeholder="Enter text here..."
            @ref="textAreaRef"
        ></textarea>
    </div>
    
    <div class="notepad-statusbar">
        @if (!string.IsNullOrEmpty(FilePath))
        {
            <span>@FilePath</span>
        }
        else
        {
            <span>Untitled</span>
        }
    </div>
</div>
```

#### NotepadComponent.razor.cs

```csharp
using HackerOs.OS.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.BuiltIn;

public partial class NotepadComponent
{
    [Inject] private IVirtualFileSystem FileSystem { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    
    [Parameter] public string Content { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ContentChanged { get; set; }
    
    [Parameter] public string? FilePath { get; set; }
    [Parameter] public EventCallback<string?> FilePathChanged { get; set; }
    
    private ElementReference textAreaRef;
    private IJSObjectReference? _jsModule;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Applications/BuiltIn/NotepadComponent.razor.js");
                
            if (_jsModule != null)
            {
                await _jsModule.InvokeVoidAsync("initializeNotepad", textAreaRef);
            }
        }
    }
    
    private async Task NewFile()
    {
        if (!string.IsNullOrEmpty(Content))
        {
            // Ask for confirmation if there's unsaved content
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", "Discard current changes?");
            if (!confirmed) return;
        }
        
        Content = string.Empty;
        await ContentChanged.InvokeAsync(Content);
        
        FilePath = null;
        await FilePathChanged.InvokeAsync(FilePath);
    }
    
    private async Task OpenFile()
    {
        // Logic to open a file
        // This would typically show a file dialog and load content
    }
    
    private async Task SaveFile()
    {
        // Logic to save the file
    }
    
    private async Task SaveFileAs()
    {
        // Logic to save as a new file
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            await _jsModule.DisposeAsync();
        }
    }
}
```

#### NotepadComponent.razor.css

```css
.notepad-container {
    display: flex;
    flex-direction: column;
    height: 100%;
    width: 100%;
    background-color: var(--background-color);
    color: var(--text-color);
}

.notepad-toolbar {
    display: flex;
    padding: 5px;
    background-color: var(--toolbar-background);
    border-bottom: 1px solid var(--border-color);
}

.toolbar-button {
    margin-right: 5px;
    padding: 3px 8px;
    background-color: var(--button-background);
    border: 1px solid var(--border-color);
    color: var(--button-text-color);
    cursor: pointer;
}

.toolbar-button:hover {
    background-color: var(--button-hover-background);
}

.notepad-editor {
    flex: 1;
    overflow: hidden;
}

textarea {
    width: 100%;
    height: 100%;
    padding: 5px;
    border: none;
    resize: none;
    font-family: monospace;
    background-color: var(--editor-background);
    color: var(--editor-text-color);
}

.notepad-statusbar {
    padding: 3px 5px;
    font-size: 0.8em;
    background-color: var(--statusbar-background);
    border-top: 1px solid var(--border-color);
    color: var(--statusbar-text-color);
}
```

#### NotepadComponent.razor.js

```javascript
export function initializeNotepad(textArea) {
    // Set up key handlers
    textArea.addEventListener('keydown', handleKeyDown);
    
    // Focus the text area
    textArea.focus();
    
    return {
        // This is called when the module is disposed
        dispose: () => {
            textArea.removeEventListener('keydown', handleKeyDown);
        }
    };
}

function handleKeyDown(event) {
    // Handle tab key
    if (event.key === 'Tab') {
        event.preventDefault();
        
        const start = event.target.selectionStart;
        const end = event.target.selectionEnd;
        
        // Insert tab at the current cursor position
        event.target.value = 
            event.target.value.substring(0, start) + 
            '    ' + 
            event.target.value.substring(end);
            
        // Set cursor position after the inserted tab
        event.target.selectionStart = 
        event.target.selectionEnd = start + 4;
        
        // Trigger input event to update the bound value
        event.target.dispatchEvent(new Event('input', { bubbles: true }));
    }
}
```

## Window Lifecycle

Window-based applications in HackerOS follow a specific lifecycle:

1. **Creation**: The application is instantiated and `OnStartAsync` is called
2. **Window Creation**: The window is created and displayed
3. **Activation/Deactivation**: The window can be activated or deactivated as the user interacts with it
4. **State Persistence**: State can be saved and loaded using the `SaveStateAsync` and `LoadStateAsync` methods
5. **Closure**: When the window is closed, `OnCloseRequestAsync` is called first to check if closing is allowed, then `OnCloseAsync` is called

## JavaScript Integration

For advanced functionality, you can use JavaScript interop to interact with the browser's APIs:

1. **Create a .razor.js file** with your JavaScript functions
2. **Import the module** in your component's code-behind:

```csharp
private IJSObjectReference? _jsModule;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./YourComponent.razor.js");
    }
}
```

3. **Call JavaScript functions** from your C# code:

```csharp
await _jsModule!.InvokeVoidAsync("yourFunction", parameter1, parameter2);
```

4. **Dispose of the module** when your component is disposed:

```csharp
public async ValueTask DisposeAsync()
{
    if (_jsModule != null)
    {
        await _jsModule.DisposeAsync();
    }
}
```

## Styling Windows

Each component can have its own isolated CSS using a .razor.css file. The styles are automatically scoped to the component.

You can also use CSS variables to support themes:

```css
.my-component {
    background-color: var(--background-color);
    color: var(--text-color);
    border: 1px solid var(--border-color);
}
```

## Advanced Window Features

The `WindowBase` class provides several advanced features:

- **Window Positioning**: Set the position and size of the window
- **Window States**: Normal, Minimized, and Maximized states
- **Window Events**: Various events for window actions (resize, move, etc.)
- **Window Focus**: Methods to bring the window to the front or send it to the back
- **Modal Windows**: Create modal dialogs that block interaction with other windows

Example of creating a modal dialog:

```csharp
// Create a modal dialog
var parameters = new Dictionary<string, object?>
{
    { "Title", "Confirm Action" },
    { "IsModal", true },
    { "ParentWindow", Window }
};

var dialog = await WindowManager.CreateWindowAsync<ConfirmDialog>(parameters);

// Wait for dialog result
var result = await dialog.GetDialogResultAsync<bool>();
```

---

This documentation should help you get started with creating window-based applications in HackerOS using the BlazorWindowManager.
