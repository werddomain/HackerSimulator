# BlazorWindowManager Analysis Document

## Overview
This document analyzes the existing BlazorWindowManager implementation and how it should integrate with the HackerOS application framework. The goal is to identify extension points, understand the component lifecycle, and design an effective bridge between applications and windows.

## Core Components

### 1. Window Manager Service
**File:** `BlazorWindowManager/Services/WindowManagerService.cs`

#### Key Features:
- Window registration and tracking
- Z-index management for stacking
- Window activation/focus handling
- State management (normal, minimized, maximized)
- Extensive event system
- Window lifecycle (create, close, state change)

#### Events:
- `WindowCreated`
- `WindowBeforeClose` (cancellable)
- `WindowCloseCancelled`
- `WindowAfterClose`
- `WindowStateChanged`
- `WindowActivated`
- `WindowDeactivated`
- `WindowZIndexChanged`
- `WindowPositionChanged`
- `WindowSizeChanged`

#### Public Methods:
- `RegisterWindow` - Add window to manager
- `UnregisterWindow` - Remove window from manager
- `ActivateWindow` - Bring window to front
- `SetWindowZIndex` - Change stacking order
- `UpdateWindowState` - Change window state
- `UpdateWindowPosition` - Move window
- `UpdateWindowSize` - Resize window
- `CloseWindow` - Close window with confirmation
- `CloseWindowForce` - Close window without confirmation

### 2. Window Models
**File:** `BlazorWindowManager/Models/WindowInfo.cs`

#### Key Properties:
- `Id` - Unique identifier (Guid)
- `Name` - User-defined name (optional)
- `Title` - Display title
- `Icon` - RenderFragment for window icon
- `State` - Current window state (normal, minimized, maximized)
- `Bounds` - Position and size
- `RestoreBounds` - Size/position before maximizing
- `ZIndex` - Stacking order position
- `IsActive` - Whether window is focused
- `Resizable` - Whether window can be resized
- `MinWidth/MinHeight/MaxWidth/MaxHeight` - Size constraints
- `ComponentRef` - Reference to the actual window component
- `CreatedAt` - Creation timestamp
- `ComponentType` - Type name of window component
- `Parameters` - Dictionary of parameters for component

### 3. Dialog Service
**File:** `BlazorWindowManager/Services/DialogService.cs`

Provides methods for displaying dialog windows with various options:
- `ShowDialogAsync` - Show modal dialog with custom content
- `ShowMessageAsync` - Show simple message dialog
- `ShowConfirmAsync` - Show confirmation dialog with Yes/No
- `ShowPromptAsync` - Show text input dialog

## HackerOS Application Framework

### 1. Application Interface
**File:** `HackerOs/OS/Applications/IApplication.cs`

#### Key Properties:
- `Id` - Unique application identifier
- `Name` - Display name
- `Description` - Application description
- `Version` - Version string
- `Type` - Application type (windowed, CLI, etc.)
- `Manifest` - Application metadata
- `State` - Current application state
- `OwnerSession` - User session that owns this instance

### 2. Application Manager
**File:** `HackerOs/OS/Applications/ApplicationManager.cs`

Manages application lifecycle:
- Registration of available applications
- Application launching
- Process tracking
- Application termination
- State change management
- Resource allocation/deallocation

#### Events:
- `ApplicationLaunched`
- `ApplicationTerminated`
- `ApplicationStateChanged`

## Integration Points

### 1. Window-Application Mapping
Each windowed application needs a corresponding window:
- `ApplicationWindow` wrapper class needed to bridge IApplication and WindowInfo
- Bidirectional communication between window and application

### 2. Lifecycle Synchronization
Application lifecycle must be synchronized with window lifecycle:
- Window closed → Application terminated
- Application terminated → Window closed
- Window state changes → Application state changes

### 3. Resource Management
Need proper resource cleanup:
- Memory management when window/application closes
- Event handler unsubscription
- Component disposal

### 4. Persistent Window State
Applications should remember their window state:
- Position/size persistence between sessions
- Window grouping for multi-window applications
- Layout management

## Extension Requirements

### 1. Application Window Component
Need a specialized window component for applications:
- Inherit from base Window component
- Add application-specific chrome/decorations
- Handle application state transitions
- Support for non-rectangular windows

### 2. Application Launch Pipeline
Create a pipeline for application launching:
1. User initiates launch (desktop icon, start menu, etc.)
2. ApplicationManager creates application instance
3. Window creation request sent to WindowManager
4. Window rendered with application content
5. Application started within window context

### 3. Window Templates
Different application types may need different window templates:
- Standard application window
- Dialog window
- Tool window
- Fullscreen window
- System tray window

## Performance Considerations

### 1. Lazy Loading
- Defer loading application content until window is visible
- Use virtualization for large application UIs

### 2. Window Hibernation
- Minimize memory usage for inactive windows
- State serialization/restoration

### 3. Event Optimization
- Throttle window resize/move events
- Batch update notifications

## Security Considerations

### 1. Application Isolation
- Each application window should have isolated context
- Prevent cross-application DOM access

### 2. Permission Enforcement
- Window operations may require permissions
- Screen recording/sharing permissions

## Theme Integration

### 1. Theme Propagation
- Window chrome should respect system theme
- Application content should inherit theme context

### 2. Transition Animations
- Smooth transitions between themes
- Consistent animation patterns

## Recommendations

### 1. Create ApplicationWindow Bridge Class
Implement a new class that connects IApplication with WindowInfo:
```csharp
public class ApplicationWindow
{
    private readonly IApplication _application;
    private readonly WindowInfo _windowInfo;
    
    // Methods for synchronizing state
    public void SyncWindowStateToApplication();
    public void SyncApplicationStateToWindow();
    
    // Window event handlers
    private void OnWindowClosing(object sender, WindowCancelEventArgs e);
    private void OnWindowStateChanged(object sender, WindowStateChangedEventArgs e);
    
    // Application event handlers
    private void OnApplicationStateChanged(object sender, ApplicationStateChangedEventArgs e);
}
```

### 2. Extend ApplicationManager
Add window management methods to ApplicationManager:
```csharp
// In ApplicationManager.cs
public void LaunchWindowedApplication(string appId, UserSession session)
{
    // Create application instance
    var app = CreateApplicationInstance(appId, session);
    
    // Create window for application
    var windowInfo = _windowManager.CreateWindowForApplication(app);
    
    // Create bridge to manage relationship
    var appWindow = new ApplicationWindow(app, windowInfo);
    
    // Track relationship
    _applicationWindows[app.Id] = appWindow;
}
```

### 3. Implement Window State Persistence
Save and restore window state:
```csharp
// Window state persistence
public void SaveWindowState(IApplication app, WindowInfo windowInfo)
{
    var settings = _settingsService.GetUserSettings(app.OwnerSession.User);
    settings.SetValue($"app.{app.Id}.window.x", windowInfo.Bounds.X);
    settings.SetValue($"app.{app.Id}.window.y", windowInfo.Bounds.Y);
    settings.SetValue($"app.{app.Id}.window.width", windowInfo.Bounds.Width);
    settings.SetValue($"app.{app.Id}.window.height", windowInfo.Bounds.Height);
    settings.SetValue($"app.{app.Id}.window.state", (int)windowInfo.State);
}

public WindowBounds RestoreWindowState(IApplication app)
{
    var settings = _settingsService.GetUserSettings(app.OwnerSession.User);
    var bounds = new WindowBounds();
    
    bounds.X = settings.GetValue<double>($"app.{app.Id}.window.x", 100);
    bounds.Y = settings.GetValue<double>($"app.{app.Id}.window.y", 100);
    bounds.Width = settings.GetValue<double>($"app.{app.Id}.window.width", 800);
    bounds.Height = settings.GetValue<double>($"app.{app.Id}.window.height", 600);
    
    return bounds;
}
```

## Next Steps

1. Create test harness to explore window system behavior
2. Document detailed API requirements for ApplicationWindow bridge
3. Implement prototype of ApplicationWindow class
4. Test window-application lifecycle synchronization
5. Add window state persistence for applications
