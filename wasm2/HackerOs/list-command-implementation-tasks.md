# ListCommand Implementation Task List

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `worksheet.md` FOR PROJECT GUIDELINES**

## Overview

This task list outlines the steps needed to implement Task 3.1.3 (Create Sample Command Application - ListCommand) from the HackerOS Application Architecture task list. We will create a command-line application that lists directory contents and demonstrates the command application lifecycle using the new architecture.

## Progress Tracking Legend
- [ ] = Pending task
- [x] = Completed task  
- [~] = In progress task
- [!] = Blocked or needs attention

## Task 3.1.3: Create ListCommand Sample Command Application

### Task 1: Create Basic Command Structure
- [x] Create ListCommand.cs class
  - [x] Inherit from CommandBase
  - [x] Add AppAttribute with appropriate settings
  - [x] Inject necessary services (IVirtualFileSystem, ILogger, etc.)
  - [x] Implement proper constructor with dependency injection

### Task 2: Implement Command Functionality
- [x] Add directory listing functionality
  - [x] Create methods to list files and directories
  - [x] Implement formatting options for different views
  - [x] Add sorting capabilities
- [x] Implement command options
  - [x] Add -l for detailed listing
  - [x] Add -a to show hidden files
  - [x] Add -r for recursive listing
  - [x] Add -s for sorting options

### Task 3: Implement Command Lifecycle
- [x] Implement command lifecycle methods
  - [x] Add ExecuteAsync method for command execution
  - [x] Implement argument parsing
  - [x] Add help text generation
  - [x] Handle command termination

### Task 4: Enhance Output Formatting
- [x] Implement formatted output
  - [x] Add color-coded output for different file types
  - [x] Create tabular output for detailed view
  - [x] Add file size formatting
  - [x] Implement permissions display

### Task 5: Testing and Documentation
- [ ] Test ListCommand functionality
  - [ ] Verify directory listing works correctly
  - [ ] Test all command options
  - [ ] Ensure proper error handling
- [ ] Update documentation
  - [ ] Document ListCommand usage
  - [ ] Create command reference
  - [ ] Update HackerOS-ApplicationArchitecture-task-list.md

## Implementation Notes

1. **Follow the Command Application Architecture**
   - Use CommandBase as the foundation
   - Implement proper command lifecycle
   - Follow terminal I/O patterns

2. **Code Organization**
   - Keep command logic separate from display logic
   - Use dependency injection
   - Follow C# best practices

3. **Error Handling**
   - Implement robust error handling for file operations
   - Provide user-friendly error messages
   - Handle invalid arguments gracefully

4. **User Experience**
   - Create consistent, readable output formatting
   - Implement intuitive command-line arguments
   - Follow terminal command conventions
