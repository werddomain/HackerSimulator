# Analysis Plan: HackerOS Application Registry

## Overview

This document outlines the plan for implementing the Application Registry system for HackerOS. The Application Registry will be responsible for discovering, managing, and launching applications within the HackerOS environment. It will use an attribute-based approach to identify applications and integrate with the BlazorWindowManager for window-based applications.

## Current State Assessment

Based on the existing codebase, the following components are relevant to our implementation:

1. **BlazorWindowManager**: Already provides window creation and management capabilities
   - `WindowBase.razor` and related files provide the core window functionality
   - `WindowContent` component manages window content rendering
   - `WindowManagerService` handles window operations

2. **Application-related code**: 
   - Limited application management functionality exists
   - Need to create a comprehensive application framework

## Key Requirements

1. **Attribute-based Application Declaration**:
   - Applications should be discoverable via attributes
   - Required attributes: `[App(Id, Name, Icon)]`
   - Optional attributes: `[AppDescription]`
   
2. **Icon Management**:
   - Support for file-based icons and icon libraries (Font Awesome, etc.)
   - Format: `[fa/iconic/mid]-{name}` or file paths
   
3. **Application Lifecycle**:
   - Integration with window management system
   - Standard lifecycle hooks (start, activate, deactivate, close)
   - State persistence

4. **Documentation**:
   - Clear guidelines for creating new applications
   - Examples of window-based applications

## Architecture Design

### 1. Core Components

#### 1.1 Application Attributes

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AppAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Icon { get; }
    
    public AppAttribute(string id, string name, string icon)
    {
        Id = id;
        Name = name;
        Icon = icon;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AppDescriptionAttribute : Attribute
{
    public string Description { get; }
    
    public AppDescriptionAttribute(string description)
    {
        Description = description;
    }
}
```

#### 1.2 Icon Management

```csharp
public interface IIconProvider
{
    bool CanHandleIcon(string iconPath);
    RenderFragment GetIcon(string iconPath);
}

public class IconFactory
{
    private readonly List<IIconProvider> _providers;
    
    // Providers registered in order of priority
    public IconFactory(IEnumerable<IIconProvider> providers)
    {
        _providers = providers.ToList();
    }
    
    public RenderFragment GetIcon(string iconPath)
    {
        // Find the first provider that can handle this icon
        var provider = _providers.FirstOrDefault(p => p.CanHandleIcon(iconPath));
        return provider?.GetIcon(iconPath) ?? DefaultIcon();
    }
    
    private RenderFragment DefaultIcon() => builder =>
    {
        // Default icon implementation
    };
}
```

#### 1.3 Application Metadata

```csharp
public class ApplicationMetadata
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string IconPath { get; set; }
    public RenderFragment IconComponent { get; set; }
    public Type ApplicationType { get; set; }
    public bool IsPinned { get; set; }
    public DateTime LastLaunched { get; set; }
    public int LaunchCount { get; set; }
    public string Category { get; set; }
    public string[] Tags { get; set; }
}
```

#### 1.4 Application Registry

```csharp
public interface IApplicationRegistry
{
    IEnumerable<ApplicationMetadata> GetApplications();
    ApplicationMetadata GetApplicationById(string id);
    void RegisterApplication(ApplicationMetadata metadata);
    void RefreshApplications();
}

public class ApplicationRegistry : IApplicationRegistry
{
    private readonly Dictionary<string, ApplicationMetadata> _applications = new();
    private readonly IconFactory _iconFactory;
    private readonly IServiceProvider _serviceProvider;
    
    // Implementation details
}
```

#### 1.5 Application Launcher

```csharp
public interface IApplicationLauncher
{
    Task<bool> LaunchApplication(string applicationId);
    Task<bool> LaunchApplication(ApplicationMetadata metadata);
    Task CloseApplication(string applicationId);
    IEnumerable<string> GetRunningApplications();
}

public class ApplicationLauncher : IApplicationLauncher
{
    private readonly IApplicationRegistry _registry;
    private readonly WindowManagerService _windowManager;
    
    // Implementation details
}
```

### 2. Application Lifecycle

#### 2.1 IApplication Interface

```csharp
public interface IApplication
{
    Task OnStartAsync();
    Task OnActivateAsync();
    Task OnDeactivateAsync();
    Task OnCloseAsync();
    Task SaveStateAsync();
    Task LoadStateAsync();
}
```

### 3. Integration Points

#### 3.1 BlazorWindowManager Integration

1. **Window Creation**:
   - Applications implement `IApplication` and inherit from `WindowBase`
   - Application launcher creates windows via `WindowManagerService`

2. **Lifecycle Events**:
   - Map window events to application lifecycle methods
   - `WindowActivated` → `OnActivateAsync`
   - `WindowDeactivated` → `OnDeactivateAsync`
   - `WindowClosed` → `OnCloseAsync`

3. **State Management**:
   - Use the file system to store application state
   - Standard location: `/home/{user}/.config/{appId}/`

#### 3.2 Desktop Integration

1. **Desktop Icons**:
   - Create desktop shortcuts from `ApplicationMetadata`
   - Use `IconFactory` for icon rendering

2. **Start Menu Integration**:
   - List applications in start menu
   - Support for application categories and search

## Implementation Strategy

### Phase 1: Core Framework

1. Create application attributes
2. Implement icon providers and factory
3. Build application registry with discovery mechanism
4. Create application interface

### Phase 2: Launcher & Integration

1. Implement application launcher
2. Create integration with window manager
3. Build lifecycle management
4. Implement state persistence

### Phase 3: Sample Applications & Documentation

1. Create sample Notepad application
2. Document application development process
3. Create templates for new applications

## Potential Challenges

1. **Performance**: Application discovery via reflection might be slow at startup
   - Solution: Cache discovered applications

2. **Icon Rendering**: Different icon sources require different rendering approaches
   - Solution: Provider pattern with specialized renderers

3. **Window Integration**: Ensuring smooth integration between applications and window system
   - Solution: Clear event flow and lifecycle management

4. **State Management**: Handling application state persistence properly
   - Solution: Standardized state serialization and storage

## Conclusion

This analysis plan outlines a comprehensive approach to implementing the Application Registry for HackerOS. By using attribute-based discovery and integrating with the existing window management system, we can create a flexible and robust application framework that aligns with the Linux-like architecture of the system.

The implementation will follow a phased approach, starting with the core framework, followed by integration components, and finally sample applications and documentation.
