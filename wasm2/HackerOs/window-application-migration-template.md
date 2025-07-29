# Window Application Migration Template

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `worksheet.md` FOR PROJECT GUIDELINES**

## Overview

This document provides a step-by-step template for migrating existing window applications to the new unified architecture. The template includes migration steps, code examples, and testing guidelines to ensure consistent implementation.

## Migration Checklist

### Pre-Migration Assessment
- [ ] Review the application's current implementation
- [ ] Identify dependencies and integration points
- [ ] Document current functionality and behavior
- [ ] Verify application complexity against migration inventory

### Code Migration Steps
- [ ] Update file structure (if needed)
- [ ] Convert to Razor component with WindowBase inheritance
- [ ] Implement proper WindowContent usage
- [ ] Update dependency injection and service usage
- [ ] Implement IProcess and IApplication interfaces
- [ ] Add lifecycle method implementations
- [ ] Add event handling through ApplicationBridge
- [ ] Update UI components and styling
- [ ] Implement error handling

### Testing Steps
- [ ] Verify window creation and rendering
- [ ] Test window controls (minimize, maximize, close)
- [ ] Verify application functionality matches original
- [ ] Test integration with other components
- [ ] Verify process lifecycle (start, stop)
- [ ] Test error scenarios
- [ ] Performance testing (if applicable)

## Directory Structure

Migrated window applications should follow this directory structure:

```
HackerOs/
└── OS/
    └── Applications/
        └── UI/
            └── Windows/
                └── {ApplicationName}/
                    ├── {ApplicationName}App.razor
                    ├── {ApplicationName}App.razor.cs
                    └── {ApplicationName}App.razor.css
```

## Code Templates

### Razor Component (.razor)

```razor
@inherits WindowBase
@using HackerOs.OS.UI.Components
@using HackerOs.OS.Applications.UI.Components

<WindowContent Window="this">
    <div class="application-container">
        <div class="toolbar">
            <!-- Application-specific toolbar content -->
        </div>
        
        <div class="content-area">
            <!-- Application-specific content -->
        </div>
        
        <div class="status-bar">
            <!-- Application-specific status information -->
        </div>
    </div>
</WindowContent>
```

### Code-Behind (.razor.cs)

```csharp
using System;
using System.Threading.Tasks;
using HackerOs.OS.Core;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.UI.Windows;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.UI.Windows.{ApplicationName}
{
    public partial class {ApplicationName}App
    {
        [Inject]
        protected IApplicationBridge ApplicationBridge { get; set; } = null!;
        
        [Inject]
        protected IJSRuntime JSRuntime { get; set; } = null!;
        
        // Add other required service injections
        
        // Application state variables
        private bool _isInitialized = false;
        
        /// <summary>
        /// Initializes the application when the component is first rendered.
        /// </summary>
        /// <param name="firstRender">True if this is the first time the component has been rendered.</param>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await InitializeApplicationAsync();
            }
            
            await base.OnAfterRenderAsync(firstRender);
        }
        
        /// <summary>
        /// Initializes the application, registers with the ApplicationBridge,
        /// and performs initial setup.
        /// </summary>
        private async Task InitializeApplicationAsync()
        {
            try
            {
                await ApplicationBridge.InitializeAsync(this);
                
                // Application-specific initialization
                
                _isInitialized = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Error initializing {WindowTitle}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called when the window is closing. Performs cleanup operations.
        /// </summary>
        protected override async Task OnWindowClosingAsync()
        {
            try
            {
                // Application-specific cleanup
                
                await base.OnWindowClosingAsync();
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Error closing {WindowTitle}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Implements the SetStateAsync method from IApplication.
        /// </summary>
        /// <param name="state">The new application state.</param>
        public override async Task SetStateAsync(ApplicationState state)
        {
            try
            {
                await base.SetStateAsync(state);
                
                // Application-specific state handling
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Error setting state for {WindowTitle}: {ex.Message}");
            }
        }
        
        // Application-specific methods
    }
}
```

### CSS Styles (.razor.css)

```css
.application-container {
    display: flex;
    flex-direction: column;
    height: 100%;
    width: 100%;
    overflow: hidden;
}

.toolbar {
    flex: 0 0 auto;
    padding: 4px 8px;
    background-color: var(--app-toolbar-bg);
    border-bottom: 1px solid var(--app-border-color);
}

.content-area {
    flex: 1 1 auto;
    overflow: auto;
    padding: 8px;
    background-color: var(--app-content-bg);
}

.status-bar {
    flex: 0 0 auto;
    padding: 2px 8px;
    background-color: var(--app-statusbar-bg);
    border-top: 1px solid var(--app-border-color);
    font-size: 0.8rem;
}

/* Application-specific styles */
```

## Integration with JavaScript

For applications that require JavaScript interop, add a JS file in the wwwroot/js directory:

```javascript
// wwwroot/js/applicationName.js

window.applicationNameApp = {
    // JavaScript methods that can be called from .NET
    
    initialize: function () {
        // Initialization logic
        console.log("ApplicationName initialized");
        return true;
    },
    
    // Add application-specific methods
};
```

Then call these methods from your Razor component:

```csharp
// In .razor.cs file

private async Task InitializeJsAsync()
{
    await JSRuntime.InvokeVoidAsync("applicationNameApp.initialize");
}
```

## Common Migration Patterns

### Application Settings

For applications that need to save/load settings:

```csharp
private readonly string _settingsPath = "/home/user/.config/applicationName/settings.json";
private ApplicationSettings _settings = new();

private async Task LoadSettingsAsync()
{
    try
    {
        if (await _fileSystem.FileExistsAsync(_settingsPath))
        {
            var content = await _fileSystem.ReadAllTextAsync(_settingsPath);
            _settings = System.Text.Json.JsonSerializer.Deserialize<ApplicationSettings>(content) 
                ?? new ApplicationSettings();
        }
        
        // Apply loaded settings
    }
    catch (Exception ex)
    {
        await RaiseErrorAsync($"Error loading settings: {ex.Message}");
        _settings = new ApplicationSettings(); // Use defaults
    }
}

private async Task SaveSettingsAsync()
{
    try
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!await _fileSystem.DirectoryExistsAsync(directory))
        {
            await _fileSystem.CreateDirectoryAsync(directory);
        }
        
        var content = System.Text.Json.JsonSerializer.Serialize(_settings);
        await _fileSystem.WriteAllTextAsync(_settingsPath, content);
    }
    catch (Exception ex)
    {
        await RaiseErrorAsync($"Error saving settings: {ex.Message}");
    }
}
```

### Dialog Implementation

For applications that need to show dialogs:

```csharp
private async Task ShowDialogAsync(string title, string message)
{
    try
    {
        var parameters = new DialogParameters
        {
            Title = title,
            Message = message,
            OkText = "OK",
            ShowCancel = false
        };
        
        var result = await DialogService.ShowAsync<DialogComponent>(parameters);
        // Handle result
    }
    catch (Exception ex)
    {
        await RaiseErrorAsync($"Error showing dialog: {ex.Message}");
    }
}
```

## Troubleshooting Common Issues

### Window Not Appearing

Check:
- Verify ApplicationBridge initialization
- Check for exceptions in the browser console
- Ensure the window was properly registered

### Application State Not Updating

Check:
- Verify SetStateAsync implementation
- Check event propagation through ApplicationBridge
- Ensure StateHasChanged is called when needed

### JavaScript Interop Issues

Check:
- Verify JS file is included in _Host.cshtml
- Check for proper method names and parameters
- Look for JS errors in browser console

## Examples

For complete examples of migrated window applications, refer to:

1. `NotepadApp.razor` - Basic text editor example
2. `CalculatorApp.razor` - Simple calculator example (after migration)

## Testing Guidelines

1. **Unit Testing**
   - Test individual components in isolation
   - Mock dependencies for controlled testing

2. **Integration Testing**
   - Test integration with ApplicationBridge
   - Verify process lifecycle events

3. **UI Testing**
   - Test window controls and behavior
   - Verify visual appearance matches design

4. **Performance Testing**
   - Monitor memory usage during operation
   - Check for rendering performance issues
   - Test with large data sets (if applicable)

## Final Validation Checklist

- [ ] Application starts correctly
- [ ] UI renders as expected
- [ ] All functionality from original application works
- [ ] Window controls operate correctly
- [ ] Application responds to system events
- [ ] Application cleans up resources on close
- [ ] Error handling works as expected
- [ ] Application follows the new architecture guidelines
