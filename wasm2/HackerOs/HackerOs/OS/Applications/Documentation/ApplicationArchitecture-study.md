# Application Architecture Study - HackerOS

## Overview

This document outlines a comprehensive redesign of the HackerOS application architecture to create a more unified, process-oriented system that simplifies application creation while maintaining clean separation of concerns.

## Current State Analysis

### Current Problems

1. **Complex Application Creation**: The current system as shown in `BlazorWindowManagerUsage.md` requires:
   - Creating a separate `WindowApplicationBase` class
   - Implementing `GetWindowContent()` method 
   - Managing state separately from UI components
   - Complex inheritance hierarchy with `ApplicationBase` → `WindowApplicationBase` → Custom App

2. **Inconsistent Application Types**: Current system has:
   - WindowApplicationBase for GUI apps
   - No clear pattern for service/daemon applications
   - No clear pattern for command-line applications
   - Mixed responsibilities between application logic and UI presentation

3. **Tight Coupling**: Applications are tightly coupled to specific UI frameworks and window management

### Successful Pattern (DemoApplication.razor)

The `DemoApplication.razor` example shows a much cleaner approach:
- Inherits directly from `WindowBase`  
- Uses `<WindowContent>` component for rendering
- Clean, simple component structure
- Self-contained logic and UI

## Proposed Architecture

### Core Principle: Process-Oriented Design

All applications should inherit from `IProcess` and `IApplication` interface and be managed by the kernel's process management system and ApplicationManager. This provides:
- Unified lifecycle management
- Consistent resource tracking
- Process hierarchy support
- Standard inter-process communication

### Three Application Base Types

#### 1. WindowBase Applications (GUI Applications)
- **Target**: Interactive desktop applications with windows
- **Base Class**: Extended `WindowBase` (from BlazorWindowManager) and (implements `IProcess` and 'IApplication')
- **Pattern**: Similar to `DemoApplication.razor`
- **Features**: 
  - Direct inheritance from `WindowBase`
  - Built-in window management using the WindowManager/ApplicationManager
  - Process integration using ProcessManager
  - Theme support (allredy supported by BlazorWindowManager)

#### 2. ServiceBase Applications (Background Services)
- **Target**: System services, daemons, background processes
- **Base Class**: `ServiceBase` (implements `IProcess` and 'IApplication')
- **Pattern**: Headless applications with lifecycle management
- **Features**:
  - No UI components
  - Automatic startup/shutdown
  - Service status reporting
  - Configuration management

#### 3. CommandBase Applications (CLI Tools)
- **Target**: Command-line utilities and terminal applications
- **Base Class**: `CommandBase` (implements `IProcess`, `IApplication` and `ICommand`)
- **Pattern**: Terminal-based interaction
- **Features**:
  - Input/output stream management
  - Argument parsing
  - Exit code handling
  - Terminal integration

## Detailed Design

### 1. Core Application Framework

Since all applications now implement both `IProcess` and `IApplication` interfaces directly, we'll create a unified base class that handles the common functionality for all application types.

#### ApplicationCoreBase Class
```csharp
public abstract class ApplicationCoreBase : IProcess, IApplication
{
    // Common application functionality that all app types will use:
    // - Process ID management (via IProcess)
    // - State management (via IApplication)
    // - Event handling
    // - Resource tracking
    // - User session management
    // - Error handling
    // - Statistics collection
    // - Integration with ProcessManager and ApplicationManager
    
    // IProcess implementation
    public int ProcessId { get; protected set; }
    public int ParentProcessId { get; protected set; }
    public string Owner { get; protected set; } = string.Empty;
    public ProcessState State { get; protected set; }
    public DateTime StartTime { get; protected set; }
    public long MemoryUsage { get; protected set; }
    public TimeSpan CpuTime { get; protected set; }
    public string CommandLine { get; protected set; } = string.Empty;
    public string WorkingDirectory { get; set; } = "/";
    public Dictionary<string, string> Environment { get; protected set; } = new();
    
    // IApplication implementation
    public string Id { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public string Description { get; protected set; } = string.Empty;
    public string Version { get; protected set; } = "1.0.0";
    public string? IconPath { get; protected set; }
    public ApplicationType Type { get; protected set; }
    public ApplicationManifest Manifest { get; protected set; } = new();
    public ApplicationState ApplicationState { get; protected set; }
    public UserSession? OwnerSession { get; protected set; }
    
    // Events from both interfaces
    public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;
    public event EventHandler<ApplicationOutputEventArgs>? OutputReceived;
    public event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;
    
    protected abstract Task<bool> OnStartAsync(ApplicationLaunchContext context);
    protected abstract Task<bool> OnStopAsync();
    protected virtual Task<bool> OnPauseAsync() => Task.FromResult(true);
    protected virtual Task<bool> OnResumeAsync() => Task.FromResult(true);
}
```

### 2. Window Applications (Enhanced WindowBase)

#### Application Bridge Pattern

Since `WindowBase` must inherit from `BlazorWindowManager.Components.WindowBase` but still needs the functionality provided by `ApplicationCoreBase`, we'll use a bridge pattern to connect these components without multiple inheritance.

```csharp
/// <summary>
/// Interface for bridging WindowBase with ApplicationCoreBase functionality
/// </summary>
public interface IApplicationBridge
{
    void Initialize(IProcess process, IApplication application);
    Task<bool> RegisterApplicationAsync(IApplication application);
    Task<bool> UnregisterApplicationAsync(IApplication application);
    Task<bool> RegisterProcessAsync(IProcess process, int? processId = null);
    Task<bool> TerminateProcessAsync(IProcess process);
    void OnStateChanged(IApplication application, ApplicationState oldState, ApplicationState newState);
    void OnOutput(IApplication application, string output, OutputStreamType streamType);
    void OnError(IApplication application, string error, Exception? exception = null);
}

/// <summary>
/// Concrete implementation of the application bridge
/// </summary>
public class ApplicationBridge : IApplicationBridge
{
    private readonly IApplicationManager _applicationManager;
    private readonly IProcessManager _processManager;
    private readonly ILogger<ApplicationBridge> _logger;
    
    public ApplicationBridge(
        IApplicationManager applicationManager,
        IProcessManager processManager,
        ILogger<ApplicationBridge> logger)
    {
        _applicationManager = applicationManager;
        _processManager = processManager;
        _logger = logger;
    }
    
    public void Initialize(IProcess process, IApplication application)
    {
        _logger.LogInformation("Initializing bridge for {ApplicationId}", application.Id);
    }
    
    public async Task<bool> RegisterApplicationAsync(IApplication application)
    {
        return await _applicationManager.RegisterApplicationAsync(application);
    }
    
    public async Task<bool> UnregisterApplicationAsync(IApplication application)
    {
        return await _applicationManager.UnregisterApplicationAsync(application.Id);
    }
    
    public async Task<bool> RegisterProcessAsync(IProcess process, int? processId = null)
    {
        try
        {
            var pid = processId ?? await _processManager.GetNextProcessIdAsync();
            process.ProcessId = pid;
            process.StartTime = DateTime.Now;
            process.State = ProcessState.Running;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register process");
            return false;
        }
    }
    
    public async Task<bool> TerminateProcessAsync(IProcess process)
    {
        return await _processManager.TerminateProcessAsync(process.ProcessId);
    }
    
    public void OnStateChanged(IApplication application, ApplicationState oldState, ApplicationState newState)
    {
        if (application is IApplicationEventSource eventSource)
        {
            eventSource.RaiseStateChangedEvent(oldState, newState);
        }
    }
    
    public void OnOutput(IApplication application, string output, OutputStreamType streamType)
    {
        if (application is IApplicationEventSource eventSource)
        {
            eventSource.RaiseOutputEvent(output, streamType);
        }
    }
    
    public void OnError(IApplication application, string error, Exception? exception = null)
    {
        if (application is IApplicationEventSource eventSource)
        {
            eventSource.RaiseErrorEvent(error, exception);
        }
    }
}

/// <summary>
/// Interface for raising application events
/// </summary>
public interface IApplicationEventSource
{
    void RaiseStateChangedEvent(ApplicationState oldState, ApplicationState newState);
    void RaiseOutputEvent(string output, OutputStreamType streamType);
    void RaiseErrorEvent(string error, Exception? exception);
}

#### Enhanced WindowBase

**WindowBase.razor** (Markup only)
```razor
@using BlazorWindowManager.Models
@using BlazorWindowManager.Services
@using HackerOs.OS.Applications
@inherits BlazorWindowManager.Components.WindowBase
@implements IWindowMessageReceiver
@implements IProcess
@implements IApplication

<WindowContent Window="this">
    <div class="window-inner @CssClass" style="@Style">
        <div class="window-header @(IsActive ? "active" : "")">
            <div class="window-icon">@Icon</div>
            <span class="window-title">@Title</span>
            <div class="window-controls">
                <button @onclick="MinimizeWindow" class="control minimize">-</button>
                <button @onclick="MaximizeWindow" class="control maximize">□</button>
                <button @onclick="CloseWindow" class="control close">×</button>
            </div>
        </div>
        <div class="window-content">
            @ChildContent
        </div>
        <div class="window-resize-handles" @hidden="IsMaximized">
            <div class="resize-handle resize-n"></div>
            <div class="resize-handle resize-e"></div>
            <div class="resize-handle resize-s"></div>
            <div class="resize-handle resize-w"></div>
            <div class="resize-handle resize-ne"></div>
            <div class="resize-handle resize-se"></div>
            <div class="resize-handle resize-sw"></div>
            <div class="resize-handle resize-nw"></div>
        </div>
    </div>
</WindowContent>
```

**WindowBase.razor.cs** (Code-behind - Partial class)
```csharp
using BlazorWindowManager.Components;
using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using HackerOs.OS.Applications;
using HackerOs.OS.Kernel.Process;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Applications.Windows
{
    // WindowBase inherits from BlazorWindowManager's WindowBase but implements IProcess and IApplication
    public partial class WindowBase : BlazorWindowManager.Components.WindowBase, IProcess, IApplication, IWindowMessageReceiver, IAsyncDisposable, IApplicationEventSource
    {
        [Inject] protected DialogService DialogService { get; set; } = null!;
        [Inject] protected IApplicationManager ApplicationManager { get; set; } = null!;
        [Inject] protected IProcessManager ProcessManager { get; set; } = null!;
        [Inject] protected IApplicationBridge ApplicationBridge { get; set; } = null!;
        
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter] public string Icon { get; set; } = "fa-window";
        [Parameter] public string CssClass { get; set; } = "";
        [Parameter] public string Style { get; set; } = "";
        
        protected bool IsActive { get; private set; }
        protected string Name { get; set; } = "";
        
        // IProcess implementation
        public int ProcessId { get; set; }
        public int ParentProcessId { get; set; }
        public string Owner { get; set; } = string.Empty;
        public ProcessState State { get; set; }
        public DateTime StartTime { get; set; }
        public long MemoryUsage { get; set; }
        public TimeSpan CpuTime { get; set; }
        public string CommandLine { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = "/";
        public Dictionary<string, string> Environment { get; set; } = new();
        
        // IApplication implementation
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? IconPath { get; set; }
        public ApplicationType Type { get; set; }
        public ApplicationManifest Manifest { get; set; } = new();
        public ApplicationState ApplicationState { get; set; }
        public UserSession? OwnerSession { get; set; }
        
        // Application events
        public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;
        public event EventHandler<ApplicationOutputEventArgs>? OutputReceived;
        public event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;
        
        // IApplicationEventSource implementation
        public void RaiseStateChangedEvent(ApplicationState oldState, ApplicationState newState)
        {
            StateChanged?.Invoke(this, new ApplicationStateChangedEventArgs(this, oldState, newState, null));
        }
        
        public void RaiseOutputEvent(string output, OutputStreamType streamType)
        {
            OutputReceived?.Invoke(this, new ApplicationOutputEventArgs(output, streamType));
        }
        
        public void RaiseErrorEvent(string error, Exception? exception)
        {
            ErrorReceived?.Invoke(this, new ApplicationErrorEventArgs(error, exception));
        }
        
        protected override void OnInitialized()
        {
            base.OnInitialized();
            // Initialize the bridge with this instance
            ApplicationBridge.Initialize(this, this);
        }
        
        public async Task<bool> StartAsync(ApplicationLaunchContext context)
        {
            // Use the bridge to register with ApplicationManager and ProcessManager
            await ApplicationBridge.RegisterProcessAsync(this, context.ProcessId);
            await ApplicationBridge.RegisterApplicationAsync(this);
            
            // Set application properties
            Id = context.ApplicationId ?? GetType().Name;
            Name = Title = context.Manifest?.Name ?? Id;
            Description = context.Manifest?.Description ?? "";
            Type = ApplicationType.WindowedApplication;
            OwnerSession = context.UserSession;
            ApplicationState = ApplicationState.Running;
            
            // Return result of window-specific initialization
            return await OnWindowStartAsync(context);
        }
        
        public async Task<bool> StopAsync()
        {
            var oldState = ApplicationState;
            var result = await OnWindowStopAsync();
            
            // Use the bridge to unregister from managers
            await ApplicationBridge.UnregisterApplicationAsync(this);
            
            // Update state
            ApplicationState = ApplicationState.Stopped;
            State = ProcessState.Terminated;
            
            // Notify state change
            ApplicationBridge.OnStateChanged(this, oldState, ApplicationState);
            
            return result;
        }
        
        protected virtual void MinimizeWindow()
        {
            WindowManager.MinimizeWindowAsync(Id);
            IsMinimized = true;
            StateHasChanged();
        }

        protected virtual void MaximizeWindow()
        {
            if (IsMaximized)
                WindowManager.RestoreWindowAsync(Id);
            else
                WindowManager.MaximizeWindowAsync(Id);
                
            IsMaximized = !IsMaximized;
            StateHasChanged();
        }

        protected virtual void CloseWindow()
        {
            // Request termination through the bridge
            _ = ApplicationBridge.TerminateProcessAsync(this);
        }
        
        public virtual void OnMessageReceived(WindowMessageEventArgs args)
        {
            // Default implementation - apps can override to handle inter-window messages
        }
        
        public async ValueTask DisposeAsync()
        {
            // Unregister window when component is disposed
            try
            {
                await WindowManager.CloseWindowAsync(Id);
                await ApplicationBridge.UnregisterApplicationAsync(this);
            }
            catch { /* Ignore disposal errors */ }
        }
        
        protected virtual Task<bool> OnWindowStartAsync(ApplicationLaunchContext context) => Task.FromResult(true);
        protected virtual Task<bool> OnWindowStopAsync() => Task.FromResult(true);
        
        // Helper methods for dialogs
        protected async Task<bool> ShowConfirmDialogAsync(string message, string title = "Confirm")
        {
            return await DialogService.ShowConfirmDialogAsync(message, title);
        }
        
        protected async Task ShowErrorDialogAsync(string message, string title = "Error")
        {
            await DialogService.ShowErrorDialogAsync(message, title);
        }
        
        // Helper for raising process/application events
        protected void OnOutput(string output, OutputStreamType streamType = OutputStreamType.StandardOutput)
        {
            ApplicationBridge.OnOutput(this, output, streamType);
        }
        
        protected void OnError(string error, Exception? exception = null)
        {
            ApplicationBridge.OnError(this, error, exception);
        }
        
        protected void OnStateChanged(ApplicationState newState)
        {
            var oldState = ApplicationState;
            ApplicationState = newState;
            ApplicationBridge.OnStateChanged(this, oldState, newState);
        }
    }
}
           
        
        public async Task<bool> StopAsync()
        {
            var result = await OnWindowStopAsync();
            
            // Unregister from managers
            await WindowManager.CloseWindowAsync(Id);
            await ApplicationManager.UnregisterApplicationAsync(this);
            
            State = ProcessState.Terminated;
            ApplicationState = ApplicationState.Stopped;
            
            return result;
        }
        
        protected virtual void MinimizeWindow()
        {
            WindowManager.MinimizeWindowAsync(Id);
            IsMinimized = true;
            StateHasChanged();
        }

        protected virtual void MaximizeWindow()
        {
            if (IsMaximized)
                WindowManager.RestoreWindowAsync(Id);
            else
                WindowManager.MaximizeWindowAsync(Id);
                
            IsMaximized = !IsMaximized;
            StateHasChanged();
        }

        protected virtual void CloseWindow()
        {
            // Request termination via ProcessManager
            RequestTermination();
        }
        
        public virtual void OnMessageReceived(WindowMessageEventArgs args)
        {
            // Default implementation - apps can override to handle inter-window messages
        }
        
        public async ValueTask DisposeAsync()
        {
            // Unregister window when component is disposed
            try
            {
                await WindowManager.CloseWindowAsync(Id);
                await ApplicationManager.UnregisterApplicationAsync(this);
            }
            catch { /* Ignore disposal errors */ }
        }
        
        protected virtual Task<bool> OnWindowStartAsync(ApplicationLaunchContext context) => Task.FromResult(true);
        protected virtual Task<bool> OnWindowStopAsync() => Task.FromResult(true);
        
        // Helper methods for dialogs
        protected async Task<bool> ShowConfirmDialogAsync(string message, string title = "Confirm")
        {
            return await DialogService.ShowConfirmDialogAsync(message, title);
        }
        
        protected async Task ShowErrorDialogAsync(string message, string title = "Error")
        {
            await DialogService.ShowErrorDialogAsync(message, title);
        }
        
        // Helper for raising process/application events
        protected void OnOutput(string output, OutputStreamType streamType = OutputStreamType.StandardOutput)
        {
            OutputReceived?.Invoke(this, new ApplicationOutputEventArgs(output, streamType));
        }
        
        protected void OnError(string error, Exception? exception = null)
        {
            ErrorReceived?.Invoke(this, new ApplicationErrorEventArgs(error, exception));
        }
        
        protected void OnStateChanged(ApplicationState oldState, ApplicationState newState)
        {
            ApplicationState = newState;
            StateChanged?.Invoke(this, new ApplicationStateChangedEventArgs(oldState, newState));
        }
    }
}
```

**WindowBase.razor.css** (Isolated styles)
```css
.default-app-content {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100%;
    padding: 20px;
    color: var(--text-muted);
    font-style: italic;
}
```

#### Using WindowContent for Window Applications

A key aspect of the window application architecture is the use of the `<WindowContent>` component from BlazorWindowManager. This component serves as the connection point between the window system and the application's content.

#### Why WindowContent is Necessary

The `<WindowContent>` component plays several crucial roles:

1. **Content Rendering**: It provides the mechanism for an application to render its content within the window frame
2. **Event Handling**: It connects drag, resize, and other window events to the underlying window system
3. **Window State Management**: It ensures window state (minimized, maximized, etc.) is properly applied
4. **Window Identity**: It links the window content to the specific window instance via the `Window` parameter

#### Proper Usage Pattern

For all window applications in HackerOS, we follow this pattern:

1. **WindowBase Template**:
   ```razor
   @inherits BlazorWindowManager.Components.WindowBase
   
   <WindowContent Window="this">
       <div class="window-inner">
           <!-- Window chrome (header, etc.) -->
           <div class="window-content">
               @ChildContent
           </div>
       </div>
   </WindowContent>
   ```

2. **Application Components**:
   ```razor
   @inherits WindowBase
   
   <WindowContent Window="this">
       <!-- Application-specific content -->
       <div class="app-container">
           <!-- Application UI -->
       </div>
   </WindowContent>
   ```

The `Window="this"` parameter is critical - it tells the `WindowContent` component which window instance to associate with this content. Without this, the window system wouldn't know how to manage this particular window.

#### Implementation Details

When a window application is created:

1. The `WindowBase` inherits from `BlazorWindowManager.Components.WindowBase`
2. The `WindowContent` component receives the window instance (`this`) via the `Window` parameter
3. The `WindowContent` registers with the window system to manage this window's content
4. The application's specific UI renders inside the `WindowContent`

This pattern ensures that:
- The window management system can properly control the window
- The application's content is correctly positioned and sized within the window
- Window operations (minimize, maximize, close) work properly
- The window can be moved, resized, and interact with other windows correctly

### 3. Service Applications

#### ServiceBase Class
```csharp
public abstract class ServiceBase : ApplicationCoreBase
{
    [Inject] protected IApplicationManager ApplicationManager { get; set; } = null!;
    [Inject] protected IProcessManager ProcessManager { get; set; } = null!;
    
    protected ServiceConfiguration Configuration { get; private set; } = new();
    protected bool IsRunning { get; private set; }
    
    protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
    {
        // Register with ApplicationManager and ProcessManager
        await ApplicationManager.RegisterApplicationAsync(this);
        
        // Load service configuration
        Configuration = await LoadConfigurationAsync();
        
        // Start service-specific functionality
        IsRunning = await OnServiceStartAsync(context);
        
        if (IsRunning)
        {
            // Start background processing
            _ = Task.Run(ServiceWorkerAsync, CancellationToken);
        }
        
        return IsRunning;
    }
    
    protected abstract Task<ServiceConfiguration> LoadConfigurationAsync();
    protected abstract Task<bool> OnServiceStartAsync(ApplicationLaunchContext context);
    protected abstract Task ServiceWorkerAsync();
    
    protected override async Task<bool> OnStopAsync()
    {
        IsRunning = false;
        var result = await OnServiceStopAsync();
        
        // Unregister from ApplicationManager
        await ApplicationManager.UnregisterApplicationAsync(this);
        
        return result;
    }
    
    protected abstract Task<bool> OnServiceStopAsync();
}
```

#### Service Application Example
```csharp
[App(Id = "system.filewatch", Name = "File Watch Service", Type = ApplicationType.SystemService)]
public class FileWatchService : ServiceBase
{
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    
    protected override async Task<ServiceConfiguration> LoadConfigurationAsync()
    {
        return new ServiceConfiguration
        {
            Name = "File Watch Service",
            AutoStart = true,
            WatchDirectories = new[] { "/home", "/tmp" }
        };
    }
    
    protected override async Task<bool> OnServiceStartAsync(ApplicationLaunchContext context)
    {
        // Set application properties inherited from ApplicationCoreBase
        Type = ApplicationType.SystemService;
        
        foreach (var directory in Configuration.WatchDirectories)
        {
            var watcher = new FileSystemWatcher(directory);
            watcher.Changed += OnFileChanged;
            watcher.EnableRaisingEvents = true;
            _watchers[directory] = watcher;
        }
        
        return true;
    }
    
    protected override async Task ServiceWorkerAsync()
    {
        while (IsRunning && !CancellationToken.IsCancellationRequested)
        {
            // Periodic service tasks
            await ProcessQueuedEventsAsync();
            await Task.Delay(1000, CancellationToken);
        }
    }
    
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Handle file system events
        OnOutput($"File changed: {e.FullPath}", OutputStreamType.StandardOutput);
    }
    
    protected override async Task<bool> OnServiceStopAsync()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
        return true;
    }
}
```

### 4. Command Applications

#### CommandBase Class
```csharp
public abstract class CommandBase : ApplicationCoreBase, ICommand
{
    [Inject] protected IApplicationManager ApplicationManager { get; set; } = null!;
    [Inject] protected IProcessManager ProcessManager { get; set; } = null!;
    
    protected string[] Arguments { get; private set; } = Array.Empty<string>();
    protected ITerminalInterface Terminal { get; private set; } = new NullTerminal();
    
    // ICommand implementation
    public string CommandName { get; protected set; } = string.Empty;
    public string Usage { get; protected set; } = string.Empty;
    public string HelpText { get; protected set; } = string.Empty;
    
    protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
    {
        // Register with ApplicationManager and ProcessManager
        await ApplicationManager.RegisterApplicationAsync(this);
        
        Arguments = context.Arguments ?? Array.Empty<string>();
        Terminal = context.Terminal ?? new NullTerminal();
        
        try
        {
            var exitCode = await ExecuteCommandAsync(Arguments);
            return exitCode == 0;
        }
        catch (Exception ex)
        {
            await Terminal.WriteLineAsync($"Error: {ex.Message}");
            return false;
        }
        finally
        {
            // Unregister from ApplicationManager
            await ApplicationManager.UnregisterApplicationAsync(this);
        }
    }
    
    protected abstract Task<int> ExecuteCommandAsync(string[] args);
    
    // Helper methods for common CLI patterns
    protected async Task WriteLineAsync(string message)
    {
        await Terminal.WriteLineAsync(message);
    }
    
    protected async Task<string> ReadLineAsync(string? prompt = null)
    {
        return await Terminal.ReadLineAsync(prompt);
    }
    
    protected void ShowHelp(string usage, params (string option, string description)[] options)
    {
        Terminal.WriteLine($"Usage: {usage}");
        Terminal.WriteLine();
        foreach (var (option, description) in options)
        {
            Terminal.WriteLine($"  {option,-20} {description}");
        }
    }
}
```

#### Command Application Example
```csharp
[App(Id = "cmd.ls", Name = "ls", Type = ApplicationType.CommandLineTool)]
public class ListCommand : CommandBase
{
    [Inject] private IVirtualFileSystem FileSystem { get; set; } = null!;
    
    public ListCommand()
    {
        CommandName = "ls";
        Usage = "ls [OPTIONS] [PATH]";
        HelpText = "List directory contents";
    }
    
    protected override async Task<int> ExecuteCommandAsync(string[] args)
    {
        // Set application properties inherited from ApplicationCoreBase
        Type = ApplicationType.CommandLineTool;
        
        var showHidden = args.Contains("-a");
        var longFormat = args.Contains("-l");
        var path = args.LastOrDefault(a => !a.StartsWith("-")) ?? "/";
        
        if (args.Contains("--help"))
        {
            ShowHelp("ls [OPTIONS] [PATH]",
                ("-a", "Show hidden files"),
                ("-l", "Long format listing"),
                ("--help", "Show this help"));
            return 0;
        }
        
        try
        {
            var entries = await FileSystem.ListDirectoryAsync(path);
            
            if (!showHidden)
            {
                entries = entries.Where(e => !e.Name.StartsWith("."));
            }
            
            foreach (var entry in entries)
            {
                if (longFormat)
                {
                    await WriteLineAsync($"{entry.Permissions} {entry.Size,10} {entry.ModifiedDate:MMM dd HH:mm} {entry.Name}");
                }
                else
                {
                    await WriteLineAsync(entry.Name);
                }
            }
            
            return 0;
        }
        catch (DirectoryNotFoundException)
        {
            await WriteLineAsync($"ls: {path}: No such file or directory");
            return 1;
        }
    }
}
```

## Process Integration Strategy

### 1. Bridge Pattern for Window Applications

Since the `WindowBase` class must inherit from `BlazorWindowManager.Components.WindowBase` but still needs the functionality of `ApplicationCoreBase`, we use a bridge pattern to connect these components without multiple inheritance.

```csharp
public class ApplicationProcessManager : IApplicationManager
{
    private readonly IProcessManager _processManager;
    private readonly WindowManagerService _windowManager;
    private readonly IApplicationRegistry _applicationRegistry;
    private readonly IApplicationBridge _applicationBridge;
    
    public async Task<IApplication> LaunchApplicationAsync(string appId, ApplicationLaunchContext context)
    {
        // Create application instance
        var app = _applicationRegistry.CreateApplication(appId);
        
        // Register as a process with ProcessManager
        var processInfo = new ProcessStartInfo
        {
            Name = app.Name,
            Owner = context.UserSession?.User?.Username ?? "system",
            Arguments = context.Arguments
        };
        
        var process = await _processManager.CreateProcessAsync(processInfo);
        context.ProcessId = process.ProcessId;
        context.ParentProcessId = process.ParentProcessId;
        
        // Start the application based on its type
        if (await app.StartAsync(context))
        {
            // Additional registration based on application type
            if (app is WindowBase windowApp)
            {
                // WindowBase uses ApplicationBridge to interact with managers
                // The bridge handles registration with WindowManager and ApplicationManager
            }
            else if (app is ServiceBase serviceApp)
            {
                // ServiceBase directly inherits from ApplicationCoreBase
                // and handles its own registration with ApplicationManager
            }
            else if (app is CommandBase commandApp)
            {
                // CommandBase directly inherits from ApplicationCoreBase
                // and handles its own registration and cleanup
            }
            
            return app;
        }
        
        // Cleanup on failure
        await _processManager.TerminateProcessAsync(process.ProcessId);
        throw new ApplicationStartException($"Failed to start application {appId}");
    }
}
```

### 2. Bridge Pattern for Application-Process Integration

The bridge pattern solves the C# single inheritance constraint for window applications:

```csharp
// Bridge Interface - connects WindowBase with ApplicationCoreBase functionality
public interface IApplicationBridge
{
    // Initialize with both interfaces from the same instance
    void Initialize(IProcess process, IApplication application);
    
    // Process registration methods
    Task<bool> RegisterProcessAsync(IProcess process, int? processId = null);
    Task<bool> TerminateProcessAsync(IProcess process);
    
    // Application registration methods
    Task<bool> RegisterApplicationAsync(IApplication application);
    Task<bool> UnregisterApplicationAsync(IApplication application);
    
    // Event handling methods
    void OnStateChanged(IApplication application, ApplicationState oldState, ApplicationState newState);
    void OnOutput(IApplication application, string output, OutputStreamType streamType);
    void OnError(IApplication application, string error, Exception? exception = null);
}

// Implementation allows window apps to access functionality without inheritance
public class ApplicationBridge : IApplicationBridge
{
    private readonly IApplicationManager _applicationManager;
    private readonly IProcessManager _processManager;
    
    // Bridge implementation provides the same functionality as ApplicationCoreBase
    // but through composition instead of inheritance
    public void Initialize(IProcess process, IApplication application) 
    {
        // Connect the process and application to their managers
    }
    
    public async Task<bool> RegisterProcessAsync(IProcess process, int? processId)
    {
        // Same implementation as in ApplicationCoreBase
        // but accessed through the bridge
    }
    
    // Other bridge methods...
}
```

### 3. Unified Application Discovery
This is only an exemple. We will keep the existing AppAttribute and adjust the needs.
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class AppAttribute : Attribute
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ApplicationType Type { get; set; } = ApplicationType.WindowedApplication;
    public string? IconPath { get; set; }
    public bool AutoStart { get; set; } = false;
    public string[] FileExtensions { get; set; } = Array.Empty<string>();
}

// Applications are automatically discovered and registered:
[App(Id = "builtin.notepad", Name = "Notepad", Type = ApplicationType.WindowedApplication)]
public class NotepadApp : WindowBase 
{ 
    // Implements IProcess and IApplication via WindowBase
    // Integrates with WindowManager and ApplicationManager automatically
}

// NotepadApp.razor
@inherits WindowBase
@using HackerOs.OS.Applications.Windows
@using HackerOs.OS.IO.FileSystem

<WindowContent Window="this">
    <div class="notepad-container">
        <div class="toolbar">
            <button @onclick="New">New</button>
            <button @onclick="Open">Open</button>
            <button @onclick="Save">Save</button>
            <span>|</span>
            <button @onclick="Cut">Cut</button>
            <button @onclick="Copy">Copy</button>
            <button @onclick="Paste">Paste</button>
        </div>
        <textarea @bind="TextContent" @bind:event="oninput" class="editor-area"></textarea>
        <div class="status-bar">
            <span>@CurrentFilePath</span>
            <span>@(IsDirty ? "*" : "")</span>
            <span>Lines: @LineCount</span>
        </div>
    </div>
</WindowContent>

[App(Id = "system.filewatch", Name = "File Watcher", Type = ApplicationType.SystemService)]
public class FileWatchService : ServiceBase 
{ 
    // Implements IProcess and IApplication via ApplicationCoreBase
    // Integrates with ProcessManager and ApplicationManager automatically
}

[App(Id = "cmd.ls", Name = "ls", Type = ApplicationType.CommandLineTool)]
public class ListCommand : CommandBase 
{ 
    // Implements IProcess, IApplication, and ICommand via ApplicationCoreBase
    // Integrates with ProcessManager and ApplicationManager automatically
}
```

## Migration Strategy

### Phase 1: Core Infrastructure (Week 1)
1. **Create ApplicationCoreBase class** implementing both `IProcess` and `IApplication` interfaces
2. **Create ApplicationBridge** for connecting WindowBase with ApplicationCoreBase functionality
3. **Update WindowBase** to inherit from BlazorWindowManager.Components.WindowBase and use the bridge
4. **Create ServiceBase and CommandBase** abstract classes inheriting from ApplicationCoreBase
5. **Update existing interfaces** to ensure compatibility with new architecture

### Phase 2: Application Type Implementation (Week 2)
1. **Implement enhanced WindowBase** component with proper WindowContent usage and bridge integration
2. **Create ServiceBase implementation** with background processing and ApplicationManager integration
3. **Create CommandBase implementation** with terminal integration and ICommand interface
4. **Update ApplicationManager** for unified process and application management

### Phase 3: Application Migration (Week 3)
1. **Migrate existing window applications** to new WindowBase pattern with `<WindowContent Window="this">`
2. **Convert system services** to ServiceBase with proper IProcess and IApplication implementation
3. **Create command-line utilities** using CommandBase with ICommand interface
4. **Update application discovery and registration** to handle all three application types

### Phase 4: Testing and Optimization (Week 4)
1. **Comprehensive testing** of all application types and their manager integrations
2. **Performance optimization** for unified process and application management
3. **Documentation updates** with new patterns and interface implementations
4. **Developer tooling** for simplified application creation

## Benefits of New Architecture

### For Developers
1. **Simplified Application Creation**: 
   - Window apps: Just inherit from WindowBase and implement UI
   - Services: Inherit from ServiceBase and implement worker logic
   - Commands: Inherit from CommandBase and implement execution logic

2. **Consistent Patterns**: All applications follow the same lifecycle and process model

3. **Better Separation of Concerns**: UI, business logic, and system integration are clearly separated

4. **Rich Base Functionality**: Built-in support for configuration, logging, error handling, and resource management

### For System Architecture
1. **Unified Process Management**: All applications are proper OS processes managed by ProcessManager
2. **Better Resource Tracking**: Memory, CPU, and other resources are tracked per application through IProcess
3. **Improved Security**: Process-based isolation and permission management via ProcessManager
4. **Enhanced Debugging**: Process tree visualization and monitoring through unified process/application model
5. **Manager Integration**: Automatic registration with appropriate managers (WindowManager, ApplicationManager, ProcessManager)

### For Users
1. **Consistent Experience**: All applications behave consistently
2. **Better System Monitoring**: All applications appear in process lists
3. **Improved Performance**: Better resource management and optimization
4. **Enhanced Reliability**: Process isolation prevents cascading failures

## File Structure Changes

```
HackerOs/
├── BlazorWindowManager/
│   ├── Components/
│   │   ├── WindowBase.cs               # Original window base from BlazorWindowManager library
│   │   └── ...
│   ├── Models/
│   │   ├── WindowInfo.cs               # Window information model
│   │   └── ...
│   └── Services/
│       ├── WindowManagerService.cs      # Core window management service
│       ├── DialogService.cs            # Dialog service for window applications
│       └── ...
├── HackerOS/OS/
    ├── Applications/
    │   ├── Core/
    │   │   ├── AppAttribute.cs          # Application registration attribute
    │   │   ├── ApplicationCoreBase.cs    # Common application functionality (IProcess + IApplication)
    │   │   ├── ICommand.cs              # Command interface for CLI applications
    │   │   ├── IApplicationBridge.cs     # Bridge interface for WindowBase integration
    │   │   ├── ApplicationBridge.cs      # Bridge implementation for WindowBase
    │   │   └── ApplicationProcessManager.cs # Unified process/application manager
    │   ├── Windows/
    │   │   ├── WindowBase.razor         # Enhanced window base (inherits from BlazorWindowManager.Components.WindowBase)
    │   │   ├── WindowBase.razor.cs      # Window implementation using bridge pattern for ApplicationCoreBase functionality
    │   │   ├── IApplicationEventSource.cs # Interface for event handling in window applications
    │   │   └── WindowApplicationExtensions.cs # Helper extensions for window applications
    │   ├── Services/
    │   │   ├── ServiceBase.cs           # Base class for services (inherits ApplicationCoreBase)
    │   │   ├── ServiceConfiguration.cs   # Service configuration model
    │   │   └── ServiceManager.cs        # Service lifecycle management
    │   ├── Commands/
    │   │   ├── CommandBase.cs           # Base class for CLI applications (inherits ApplicationCoreBase + ICommand)
    │   │   ├── ITerminalInterface.cs    # Terminal interaction interface
    │   │   └── CommandArgumentParser.cs # Argument parsing utilities
    │   ├── Registry/
    │   │   ├── ApplicationDiscovery.cs   # Enhanced app discovery for all types (uses App attribute)
    │   │   └── AppRegistry.cs           # Application registry service
    │   └── BuiltIn/                    # Built-in applications
    │       ├── NotepadApp.razor         # Notepad application markup
    │       ├── NotepadApp.razor.cs      # Notepad application code-behind
    │       └── ...
    ├── Kernel/
    │   └── Process/
    │       ├── IProcess.cs              # Process interface
    │       ├── ProcessManager.cs        # Process management service
    │       └── ...
    └── ...
```
├── HackerOS/Registry/
│   ├── ApplicationDiscovery.cs          # Enhanced app discovery for all types
└── Documentation/
    ├── WindowApplicationGuide.md        # Guide for window apps (IProcess + IApplication)
    ├── ServiceApplicationGuide.md       # Guide for services (IProcess + IApplication)
    └── CommandApplicationGuide.md       # Guide for CLI apps (IProcess + IApplication + ICommand)
```

## Conclusion

This redesigned architecture provides a clean, consistent, and powerful foundation for application development in HackerOS. By implementing both `IProcess` and `IApplication` interfaces directly in all application types, we achieve:

1. **Simplified Development**: Applications follow consistent patterns based on their type (window, service, command)
2. **Unified Management**: All applications are managed by both ProcessManager and ApplicationManager seamlessly
3. **Automatic Integration**: Applications automatically register with appropriate managers (WindowManager for GUI apps, etc.)
4. **Interface Compliance**: All applications properly implement both process and application contracts

The new approach eliminates the complexity of the current `WindowApplicationBase` pattern while providing richer functionality and better integration with the underlying OS services. Applications become first-class citizens in both the process hierarchy and application ecosystem, enabling better monitoring, resource management, and system integration.

Key improvements over the current system:
- **Bridge pattern for WindowBase**: Solves the C# single inheritance constraint by connecting WindowBase with ApplicationCoreBase functionality
- **Direct interface implementation**: WindowBase inherits from BlazorWindowManager.Components.WindowBase and implements IProcess/IApplication
- **Unified lifecycle management**: All application types share the same lifecycle through common interfaces
- **Consistent patterns** across all application types (Window, Service, Command)
- **Better resource tracking** through integrated process management
- **<WindowContent Window="this">** usage for proper window content rendering and management

### Bridge Pattern Implementation

The bridge pattern is key to this architecture as it allows WindowBase to inherit from BlazorWindowManager.Components.WindowBase while still utilizing the functionality that would normally come from ApplicationCoreBase. This pattern provides:

1. **Inheritance from proper UI base**: WindowBase inherits from BlazorWindowManager.Components.WindowBase for UI functionality
2. **Process/Application functionality**: WindowBase gets process and application functionality through the ApplicationBridge service
3. **Code reuse**: Common functionality is shared through the bridge without requiring multiple inheritance
4. **Clean separation of concerns**: UI code in WindowBase, process/application management in the bridge

### BlazorWindowManager Integration

The HackerOS architecture leverages the BlazorWindowManager library to provide the windowing functionality. This integration is a key part of the architecture:

#### Relationship between WindowBase Classes

1. **BlazorWindowManager.Components.WindowBase** (Original Base Class):
   - Provides the core window functionality (position, size, state)
   - Manages window lifecycle events (create, close, minimize, maximize)
   - Implements core rendering patterns

2. **HackerOs.OS.Applications.Windows.WindowBase** (HackerOS Extension):
   - Inherits from BlazorWindowManager.Components.WindowBase for window functionality
   - Implements ApplicationCoreBase for process integration
   - Adds application-specific functionality like title bar, resize handles
   - Provides integration with ProcessManager, ApplicationManager, and WindowManagerService

#### Service Integration

The HackerOS architecture uses these key services from BlazorWindowManager:

1. **WindowManagerService**: 
   ```csharp
   // Core service from BlazorWindowManager providing window operations
   [Inject] protected WindowManagerService WindowManager { get; set; } = null!;
   
   // Used to register/unregister windows and manage window state
   await WindowManager.RegisterWindowAsync(this, WindowId, Title, Name);
   await WindowManager.CloseWindowAsync(WindowId);
   ```

2. **DialogService**:
   ```csharp
   // Service for showing dialogs and modal windows
   [Inject] protected DialogService DialogService { get; set; } = null!;
   
   // Used to display error and confirmation dialogs
   await DialogService.ShowConfirmDialogAsync("Discard changes?");
   await DialogService.ShowErrorDialogAsync("File not found");
   ```

3. **ThemeService**:
   ```csharp
   // Service for theme management in window applications
   [Inject] protected ThemeService ThemeService { get; set; } = null!;
   
   // Used to apply themes to application windows
   var theme = ThemeService.GetCurrentTheme();
   ```

#### Example Window Registration Flow

Here's how the window registration flow works between the HackerOS and BlazorWindowManager:

1. User launches an application:
   ```csharp
   await _shell.LaunchApplicationAsync("system.notepad");
   ```

2. ApplicationManager creates the application and registers it as a process:
   ```csharp
   var app = _appRegistry.CreateApplication(appId);
   var process = await _processManager.CreateProcessAsync(processInfo);
   await app.StartAsync(context);
   ```

3. WindowBase.OnStartAsync registers with WindowManager:
   ```csharp
   protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
   {
       await base.OnStartAsync(context);
       await WindowManager.RegisterWindowAsync(this, WindowId, Title, Name);
       return await OnWindowStartAsync(context);
   }
   ```

4. WindowManagerService manages the window lifecycle:
   ```csharp
   // Inside WindowManagerService
   public WindowInfo RegisterWindow(ComponentBase window, Guid id, string title, string? name = null)
   {
       var windowInfo = new WindowInfo
       {
           Id = id,
           Name = name,
           Title = title,
           ComponentRef = window,
           // Other window properties...
       };
       
       _windows.Add(windowInfo);
       WindowCreated?.Invoke(this, windowInfo);
       return windowInfo;
   }
   ```

This integration allows HackerOS applications to leverage the robust window management capabilities of BlazorWindowManager while extending them with process management and other OS features.

### Bridge Pattern for Application-Process Integration

Since the `WindowBase` class must inherit from `BlazorWindowManager.Components.WindowBase` but still needs all the functionality of the `IProcess` and `IApplication` interfaces, we use a bridge pattern to solve this inheritance constraint. This is a key architectural solution that enables window applications to integrate with both the window management system and the process management system.

#### The Problem: Multiple Inheritance

In C#, a class can only inherit from a single base class. Our architecture faces a challenge:

1. `WindowBase` must inherit from `BlazorWindowManager.Components.WindowBase` to get window functionality
2. But it also needs to implement `IProcess` and `IApplication` interfaces and share code with other application types

Simply implementing the interfaces would work, but it would require duplicating a lot of code that could be shared between application types.

#### The Solution: Application Bridge

We solve this with a bridge design pattern:

1. `WindowBase` directly inherits from `BlazorWindowManager.Components.WindowBase`
2. `WindowBase` implements `IProcess` and `IApplication` interfaces directly
3. We introduce an `IApplicationBridge` service that:
   - Provides shared implementation of process and application functionality
   - Connects window applications to the process and application management systems
   - Abstracts the common code that would typically be in `ApplicationCoreBase`

```csharp
// ApplicationBridge Implementation
public class ApplicationBridge : IApplicationBridge
{
    private readonly IProcessManager _processManager;
    private readonly IApplicationManager _applicationManager;
    
    private IProcess? _process;
    private IApplication? _application;
    
    public ApplicationBridge(
        IProcessManager processManager,
        IApplicationManager applicationManager)
    {
        _processManager = processManager;
        _applicationManager = applicationManager;
    }
    
    public void Initialize(IProcess process, IApplication application)
    {
        _process = process;
        _application = application;
    }
    
    public void Initialize(object component)
    {
        if (component is IProcess process && component is IApplication application)
        {
            Initialize(process, application);
        }
        else
        {
            throw new ArgumentException("Component must implement both IProcess and IApplication");
        }
    }
    
    public async Task<bool> StartAsync(ApplicationLaunchContext context)
    {
        if (_process == null || _application == null)
            throw new InvalidOperationException("Bridge not initialized");
            
        // Handle process registration
        _process.ProcessId = context.ProcessId ?? await _processManager.GetNextProcessIdAsync();
        _process.ParentProcessId = context.ParentProcessId ?? 0;
        _process.StartTime = DateTime.Now;
        _process.State = ProcessState.Running;
        
        // Handle application registration
        _application.ApplicationState = ApplicationState.Running;
        await _applicationManager.RegisterApplicationAsync(_application);
        
        return true;
    }
    
    public async Task<bool> StopAsync()
    {
        if (_process == null || _application == null)
            throw new InvalidOperationException("Bridge not initialized");
            
        // Handle process termination
        _process.State = ProcessState.Terminated;
        
        // Handle application unregistration
        _application.ApplicationState = ApplicationState.Stopped;
        await _applicationManager.UnregisterApplicationAsync(_application);
        
        return true;
    }
    
    // Other bridge methods...
}
```

#### Benefits of the Bridge Pattern

1. **Clean Inheritance**: `WindowBase` inherits properly from `BlazorWindowManager.Components.WindowBase`
2. **Interface Implementation**: All applications still implement `IProcess` and `IApplication`
3. **Code Reuse**: Common functionality is shared through the bridge service
4. **Flexibility**: Each application type can have specialized behavior when needed
5. **Proper Integration**: Windows integrate correctly with both window management and process management

This approach achieves the architectural goals while respecting C#'s single inheritance constraint.
