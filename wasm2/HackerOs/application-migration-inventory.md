# HackerOS Application Migration Inventory

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `worksheet.md` FOR PROJECT GUIDELINES**

## Overview

This document contains an inventory of existing applications in the HackerOS ecosystem that need to be migrated to the new application architecture. The applications are categorized by type (window, service, command) and include relevant information for migration planning.

## Window Applications

These applications have a visual user interface and interact with the window management system.

### Already Migrated

1. **NotepadApp** - `OS/Applications/UI/Windows/Notepad/NotepadApp.razor`
   - Description: Text editor for creating and editing text files
   - Status: Migrated to new architecture (WindowBase)
   - Dependencies: IVirtualFileSystem, IUserManager, IJSRuntime

### High Priority for Migration

1. **Calculator** - `OS/Applications/BuiltIn/Calculator/CalculatorApplication.cs`
   - Description: Basic calculator application
   - Current Architecture: Custom implementation
   - Dependencies: None identified
   - Migration Complexity: Low

2. **Terminal** - `OS/Applications/BuiltIn/TerminalEmulator.cs`
   - Description: Terminal emulator for command execution
   - Current Architecture: Custom implementation
   - Dependencies: IShell, ICommandProcessor
   - Migration Complexity: Medium

3. **FileManager** - `OS/Applications/BuiltIn/FileManager.cs`
   - Description: File explorer for browsing the virtual file system
   - Current Architecture: Custom implementation
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Medium

### Medium Priority for Migration

1. **TextEditor** - `OS/Applications/BuiltIn/TextEditor.cs`
   - Description: Enhanced text editor with additional features
   - Current Architecture: Custom implementation
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Medium

2. **Calendar** - `OS/Applications/BuiltIn/Calendar/CalendarApplication.cs`
   - Description: Calendar application for scheduling
   - Current Architecture: Custom implementation
   - Dependencies: ICalendarEngineService, ReminderService
   - Migration Complexity: High

3. **WindowStateTest** - `OS/Applications/UI/Windows/Test/WindowStateTest.razor.cs`
   - Description: Test application for window state management
   - Current Architecture: Custom implementation
   - Dependencies: None identified
   - Migration Complexity: Low

## Service Applications

These applications run in the background and provide functionality without a direct user interface.

### Already Migrated

1. **FileWatchService** - `OS/Applications/Services/FileWatch/FileWatchService.cs`
   - Description: Monitors file system changes
   - Status: Migrated to new architecture (ServiceBase)
   - Dependencies: IVirtualFileSystem, ILogger

### High Priority for Migration

1. **ReminderService** - `OS/Applications/BuiltIn/Calendar/ReminderService.cs`
   - Description: Manages reminders for calendar events
   - Current Architecture: IDisposable implementation
   - Dependencies: CalendarEngineService
   - Migration Complexity: Medium

2. **NotificationService** - `OS/UI/Services/NotificationService.cs`
   - Description: Manages system notifications
   - Current Architecture: Custom implementation
   - Dependencies: None identified
   - Migration Complexity: Medium

3. **MainService** - `OS/HSystem/MainService.cs`
   - Description: Core system service
   - Current Architecture: IMainService implementation
   - Dependencies: Multiple system components
   - Migration Complexity: High

### Medium Priority for Migration

1. **SettingsService** - `OS/Settings/SettingsService.cs`
   - Description: Manages system and application settings
   - Current Architecture: Multiple interface implementation
   - Dependencies: ISettingsProvider
   - Migration Complexity: Medium

2. **UserService** - `OS/Security/UserService.cs`
   - Description: Manages user accounts and permissions
   - Current Architecture: IUserService implementation
   - Dependencies: AuthenticationService
   - Migration Complexity: Medium

3. **DesktopSettingsService** - `OS/UI/Services/DesktopSettingsService.cs`
   - Description: Manages desktop appearance and behavior
   - Current Architecture: Custom implementation
   - Dependencies: SettingsService
   - Migration Complexity: Medium

## Command Applications

These applications run in the terminal and provide command-line functionality.

### Already Migrated

1. **ListCommand** - `OS/Applications/Commands/FileSystem/ListCommand.cs`
   - Description: Lists files and directories
   - Status: Migrated to new architecture (CommandBase)
   - Dependencies: IVirtualFileSystem, ILogger

### High Priority for Migration

1. **CdCommand** - `OS/Shell/Commands/CdCommand.cs`
   - Description: Changes the current directory
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Low

2. **LsCommand** - `OS/Shell/Commands/LsCommand.cs`
   - Description: Lists directory contents
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Low

3. **CatCommand** - `OS/Shell/Commands/CatCommand.cs`
   - Description: Displays file contents
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Low

### Medium Priority for Migration

1. **FindCommand** - `OS/Shell/Commands/FindCommand.cs`
   - Description: Searches for files matching criteria
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Medium

2. **GrepCommand** - `OS/Shell/Commands/GrepCommand.cs`
   - Description: Searches for patterns in files
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Medium

3. **CpCommand** - `OS/Shell/Commands/CpCommand.cs`
   - Description: Copies files and directories
   - Current Architecture: IShellCommandBase implementation
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Medium

4. **MvCommand** - `OS/Shell/Commands/MvCommand.cs`
   - Description: Moves files and directories
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Medium

5. **RmCommand** - `OS/Shell/Commands/RmCommand.cs`
   - Description: Removes files and directories
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Medium

6. **MkdirCommand** - `OS/Shell/Commands/MkdirCommand.cs`
   - Description: Creates directories
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Low

7. **TouchCommand** - `OS/Shell/Commands/TouchCommand.cs`
   - Description: Creates empty files
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: IVirtualFileSystem
   - Migration Complexity: Low

8. **EchoCommand** - `OS/Shell/Commands/EchoCommand.cs`
   - Description: Outputs text
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: None identified
   - Migration Complexity: Low

9. **PwdCommand** - `OS/Shell/Commands/PwdCommand.cs`
   - Description: Displays current directory
   - Current Architecture: CommandBase (old implementation)
   - Dependencies: None identified
   - Migration Complexity: Low

10. **ShCommand** - `OS/Shell/Commands/ShCommand.cs`
    - Description: Shell command execution
    - Current Architecture: CommandBase (old implementation)
    - Dependencies: IShell
    - Migration Complexity: Medium

11. **InstallCommand** - `OS/Shell/Commands/Applications/InstallCommands.cs`
    - Description: Installs applications
    - Current Architecture: CommandBase (old implementation)
    - Dependencies: Multiple application services
    - Migration Complexity: High

12. **UninstallCommand** - `OS/Shell/Commands/Applications/InstallCommands.cs`
    - Description: Uninstalls applications
    - Current Architecture: CommandBase (old implementation)
    - Dependencies: Multiple application services
    - Migration Complexity: High

13. **ListAppsCommand** - `OS/Shell/Commands/Applications/InstallCommands.cs`
    - Description: Lists installed applications
    - Current Architecture: CommandBase (old implementation)
    - Dependencies: ApplicationManager
    - Migration Complexity: Medium

## Migration Complexity Factors

The complexity assessment for each application is based on the following factors:

1. **Code Size and Complexity**
   - Low: < 200 lines of code, simple logic
   - Medium: 200-500 lines of code, moderate complexity
   - High: > 500 lines of code, complex logic

2. **Dependencies**
   - Low: 0-2 external dependencies
   - Medium: 3-5 external dependencies
   - High: > 5 external dependencies

3. **Integration Points**
   - Low: Self-contained, minimal integration
   - Medium: Moderate integration with other components
   - High: Deeply integrated with multiple system components

4. **UI Complexity** (for window applications)
   - Low: Simple interface, minimal controls
   - Medium: Moderate interface complexity
   - High: Complex interface with many controls and interactions

5. **State Management**
   - Low: Minimal state management
   - Medium: Moderate state complexity
   - High: Complex state management with persistence

## Next Steps

1. Finalize migration priority based on:
   - System importance
   - User impact
   - Technical dependencies
   - Implementation complexity

2. Develop migration templates for each application type

3. Begin migration with high-priority applications

4. Develop comprehensive testing plans for migrated applications
