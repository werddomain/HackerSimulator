# WorkSheet.md - HackerOS Simulator C#/Blazor Implementation Guide

## Project Overview

This worksheet provides a comprehensive guide for implementing HackerOS Simulator in C#/Blazor WebAssembly, following a proper OS architecture with strict module isolation and Linux-like behavior.
Only work in this dirrectory: 'wasm2\HackerOs'
The main project is 'wasm2\HackerOs\HackerOs\HackerOs.csproj'

The WindowManager (UI) is in this project: wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager\BlazorWindowManager.csproj

There's a terminal project started but not tested here : wasm2\HackerOs\BlazorTerminal\src\BlazorTerminal\BlazorTerminal.csproj


to build the solution, just run: 'cd C:\Users\clefw\source\repos\HackerSimulator\wasm2\HackerOs; dotnet build'

## Core Architectural Principles

### 1. Module Isolation and Dependencies
```
┌─────────────────────────────────────────────────────────┐
│                     Blazor UI Layer                      │
│  (Only accesses: Shell, Applications, Settings, Theme)   │
└─────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    Shell & Applications                   │
│        (User-facing layer with Kernel access)            │
└─────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────┐
│                      System Services                      │
│          (Bridges between userspace and kernel)          │
└─────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────┐
│                         Kernel                           │
│  (Process, Memory, Interrupts - STRICTLY ISOLATED)       │
└─────────────────────────────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────┐
│              Core Subsystems (IO, Network, Driver)       │
│              (Only accessible via Kernel)                │
└─────────────────────────────────────────────────────────┘
```

### 2. File System-Based Configuration
- **NO LocalStorage usage** - All settings stored in virtual file system
- Configuration files follow Linux conventions:
  - System-wide: `/etc/hackeros.conf`, `/etc/system.d/`
  - User-specific: `/home/{username}/.config/`
  - Application settings: `/home/{username}/.config/{appname}/`

## Module Implementation Guide

### Phase 1: Core Infrastructure

#### 1.1 Kernel Module (`wasm2/HackerOs/HackerOs/OS/Kernel/`)
```
Task Breakdown:
[ ] Create IKernel interface with process, memory, and interrupt management
[ ] Implement ProcessManager with PID allocation and process lifecycle
[ ] Implement MemoryManager with virtual memory simulation based on the total memory in the scope of the Process Scope. Each application Can report memory usage to be able to be display somewhere else.
[ ] Create InterruptHandler for system calls
[ ] Implement KernelPanic and error handling
[ ] Create SystemCall interface for controlled kernel access
[ ] Add kernel boot sequence and initialization
```

**Key Classes:**
- `Kernel.cs` - Main kernel implementation
- `ProcessManager.cs` - Process creation, scheduling, termination
- `MemoryManager.cs` - Virtual memory allocation/deallocation
- `InterruptHandler.cs` - System call handling
- `ISystemCall.cs` - Interface for kernel services

**Design Principles:**
- Kernel MUST NOT reference any UI components
- All kernel access through defined system calls
- Simulate real Linux kernel behavior (simplified)

#### 1.2 IO Module (`wasm2/HackerOs/HackerOs/OS/IO/`)
```
Task Breakdown:
[ ] Design VirtualFileSystem interface
[ ] Implement VirtualFile and VirtualDirectory classes
[ ] Create FileSystemService with CRUD operations
[ ] Implement Linux-style path resolution (., .., ~)
[ ] Add file permissions (rwx) and ownership
[ ] Create mount point system
[ ] Implement file descriptors and handles
[ ] Add IndexedDB persistence layer
[ ] Create standard directory structure (/etc, /home, /var, etc.)
[ ] Create a namespace similar as what we have in .net in the System.IO for file management, File.Exist, etc. Ex.: HackOS.System.IO. The class will have to be able to acces scooped services under the UserScope to be able to validate the right. Maybe create a static varriable somewhere with info about the current session so we can use Static methods or utility class like File.Open without the need to pass the services ...  
```

**Key Classes:**
- `VirtualFileSystem.cs` - Main filesystem implementation
- `FileNode.cs` - Base class for files/directories
- `FileDescriptor.cs` - File handle management
- `FilePermissions.cs` - Linux-style permissions
- `IndexedDBStorage.cs` - Persistence layer

**Linux Compatibility:**
- Support for hidden files (dot files)
- Case-sensitive paths
- Symbolic links support
- Standard Unix file attributes

### Phase 2: System Services

#### 2.1 Settings Module (`wasm2/HackerOs/HackerOs/OS/Settings/`)
```
Task Breakdown:
[ ] Create ISettingsService interface
[ ] Implement ConfigFileParser for Linux-style config files
[ ] Create SystemSettings class for /etc/ configs
[ ] Create UserSettings class for ~/.config/ configs
[ ] Implement config file watchers for live updates
[ ] Add settings inheritance (system -> user)
[ ] Create default configuration templates
```

**Configuration File Format:**
```bash
# /etc/hackeros.conf
[System]
hostname=hacker-machine
default_shell=/bin/bash
theme=gothic-hacker

[Network]
enable_networking=true
dns_server=8.8.8.8

# ~/.config/hackeros/user.conf
[Preferences]
desktop_icons=true
terminal_transparency=0.8
start_menu_pinned=terminal,browser,editor
```

#### 2.2 User Module (`wasm2/HackerOs/HackerOs/OS/User/`)
```
Task Breakdown:
[ ] Create User and Group classes
[ ] Implement UserManager with /etc/passwd simulation
[ ] Add a login screen. Generate a token in the the LocalStorage with a refresh technique when the system is in use. We will use the expiration of the token as a delay to lock the session and require to enter the password to resume the session.
[ ] Add session management. The user can switch session.
[ ] Create home directory initialization
[ ] Implement su/sudo functionality
[ ] Add user preferences loading from ~/.config
```

### Phase 3: Shell and Applications

#### 3.1 Shell Module (`wasm2/HackerOs/HackerOs/OS/Shell/`)
```
Task Breakdown:
[ ] Create IShell interface
[ ] Implement CommandParser for parsing user input
[ ] Create CommandRegistry for available commands
[ ] Implement built-in commands (cd, ls, cat, etc.)
[ ] Add pipeline support (|, >, >>)
[ ] Create environment variable management
[ ] Implement command history (~/.bash_history)
[ ] Add tab completion
```

#### 3.2 Applications Module (`wasm2/HackerOs/HackerOs/OS/Applications/`)
```
Task Breakdown:
[ ] Create IApplication interface
[ ] Implement ApplicationManager 
  - There's allredy a window manager in wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager\Services\WindowManagerService.cs. Make sure the 2 work tobether 
  - A Window is an application, but an application don't have to be a Window. It can be a command.
  - An application is a program or software package that provides functionality to users. It's the static definition of what can be executed. A process is a running instance of an application. It's the dynamic execution of the application with its own allocated resources.
[ ] Create application manifest system
[ ] Add sandboxed execution environment
[ ] Implement inter-process communication (IPC)
[ ] Create standard, fully fueatured, applications:
  [ ] Terminal emulators (With tab support)
    - Linus Bash style
    - Windows Cmd style
    - Powershell style
  [ ] File manager
  [ ] Text editor
  [ ] System monitor
  [ ] Settings manager
  [ ] Web browser (will use internal network implementation)
```

### Phase 4: UI Implementation



#### 4.1 Window System (`wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager\BlazorWindowManager.csproj`)
```
This project allredy define the base of window, dislog, desktop and taskbar with theaming. You can modify it, but try to not distorb the way it work.
```

### Phase 5: Security and Networking

#### 5.1 Security Module (`wasm2/HackerOs/HackerOs/OS/Security/`)
```
Task Breakdown:
[ ] Implement permission checking system
[ ] Create application sandboxing
[ ] Add user authentication
[ ] Implement access control lists (ACLs)
[ ] Create security audit logging
```

#### 5.2 Network Module (`wasm2/HackerOs/HackerOs/OS/Network/`)
```
Task Breakdown:
[ ] Create virtual network stack
[ ] Implement DNS resolution simulation
[ ] Add virtual network interfaces
[ ] Create socket simulation
[ ] Implement basic network services
[ ] Create a webServer structure to create webpages to be view vrom the webBrowser or the curl command ...
  - Each host have it's own dirrectory.
  - We will replicate how the Asp.net Mvc work.
  - wwwRoot for static files, Each controller have it's [Route] attribute, default to the controller name. HomeController is the default. It will alsow respond to host.com/home.
  - The Views folder have a dirrectory for each controller. It have cshtml file so we can use as templating with model. In the root of the Views folder, we have _layout.cshtml, the corresponding view will be render where the @body is. So each view dont have to have the full html. It can switch view by changing the property Layout to null to act as it's own html complete page or specify an other layout file. In the controller.
  - In the controller, they can return View, PartialView (That's the view without the layout). This function is the html string returned to the webbrowser or whatever requested the page.
  - We must be able to change the HttpHeaders and return a status code.
  - We will need to return data in json if we are making an api controller
  - Try to reproduce as more features that you can. Create a list of the features before starting the implementation and mark as optional or feature the one you think you will not implement on this task list.
```

## Implementation Guidelines

### 1. File Organization
```
wasm2/HackerOs/HackerOs/OS/
├── Kernel/
│   ├── Core/
│   ├── Process/
│   └── Memory/
├── IO/
│   ├── FileSystem/
│   └── Devices/
├── System/
│   └── Services/
├── Shell/
│   └── Commands/
├── Applications/
│   └── BuiltIn/
├── UI/
│   ├── Components/
│   ├── Desktop/
│   └── Windows/
├── WebServer/
│   └── Example.com/
|          ├── Controllers/
|          ├── Views/
|          └── wwwRoot/
├── Settings/
├── Theme/
├── Security/
├── User/
└── docs/
```

### 2. Service Registration
```csharp
// Program.cs
builder.Services.AddSingleton<IKernel, Kernel>();
builder.Services.AddSingleton<IFileSystem, VirtualFileSystem>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IUserManager, UserManager>();
builder.Services.AddScoped<IShell, BashShell>();
builder.Services.AddScoped<IApplicationManager, ApplicationManager>();
builder.Services.AddScoped<IWindowManager, WindowManager>();
```

### 3. MudBlazor Integration
Use MudBlazor for complex UI components:

- `MudDataGrid` for file listings
- `MudMenu` for context menus
- `MudTabs` for tabbed interfaces

### 4. Example Component Structure
<WindowContent> and @inherits WindowBase is only use on dialog window.
```razor
@* Terminal.razor *@
@implements IApplication
@inherits WindowBase
@inject IShell Shell
@inject IFileSystem FileSystem

<WindowContent>
<div class="terminal-container">
    <div class="terminal-output">
        @foreach (var line in OutputLines)
        {
            <div class="terminal-line">@line</div>
        }
    </div>
    <div class="terminal-input">
        <span class="prompt">@Prompt</span>
        <input @bind="CurrentCommand" @onkeypress="@OnKeyPress" />
    </div>
</div>
</WindowContent>
```

```css
/* Terminal.razor.css */
.terminal-container {
    background: #0a0a0a;
    color: #00ff00;
    font-family: 'Courier New', monospace;
    padding: 10px;
    height: 100%;
}

.terminal-output {
    overflow-y: auto;
    height: calc(100% - 30px);
}

.terminal-line {
    margin: 2px 0;
    white-space: pre-wrap;
}

.prompt {
    color: #00ff00;
    font-weight: bold;
}
```
```c# 
// Terminal.razor.cs
public partial class Terminal : WindowBase
// Component logic here
protected override Task OnInitializedAsync(){

}

```
## Testing Strategy

### 1. Unit Tests
- Test each module in isolation
- Mock kernel interfaces for testing
- Verify file system operations
- Test permission systems

### 2. Integration Tests
- Test module interactions
- Verify system call flows
- Test application lifecycle
- Verify settings persistence

### 3. UI Tests
- Test window management
- Verify keyboard/mouse interactions
- Test responsive design
- Verify theme switching

## Documentation Requirements

Each module MUST include:
1. `README.md` - Module overview and architecture
2. `API.md` - Public interfaces and usage
3. `IMPLEMENTATION.md` - Internal design decisions
4. Unit test coverage report
5. Performance benchmarks

## Performance Considerations

1. **Lazy Loading**: Load applications on-demand
2. **Virtual Scrolling**: For file listings
3. **IndexedDB Caching**: Minimize file system reads
4. **State Management**: Use efficient state updates
5. **Memory Management**: Dispose resources properly

## Security Considerations

1. **Sandboxing**: All applications run in isolated contexts
2. **Permission Checks**: Every file/system access validated
3. **Input Validation**: Sanitize all user inputs
4. **XSS Prevention**: Use Blazor's built-in protections
5. **Resource Limits**: Prevent memory/CPU exhaustion

## Completion Checklist

```
Phase 1: Core Infrastructure
[ ] Kernel implementation complete
[ ] File system with IndexedDB persistence
[ ] Basic process management
[ ] Memory management simulation

Phase 2: System Services  
[ ] Settings service with file-based storage
[ ] User management with sessions
[ ] System initialization

Phase 3: Shell and Applications
[ ] Functional shell with basic commands
[ ] Application framework
[ ] Built-in applications

Phase 4: UI Implementation
[ ] Desktop environment
[ ] Window management
[ ] Theme system (Gothic/Modern)

Phase 5: Security and Networking
[ ] Permission system
[ ] Basic networking simulation
[ ] Security sandboxing

Final Integration
[ ] All modules integrated
[ ] System boots successfully
[ ] Applications launch correctly
[ ] Settings persist across sessions
[ ] UI responsive and themed
```

---

**Remember**: This is a simulation that should convincingly mimic real OS behavior while running entirely in the browser. Focus on creating an immersive, realistic experience that follows Linux conventions and maintains proper architectural boundaries.