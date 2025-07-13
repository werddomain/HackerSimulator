# Calculator Application Migration Task List

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `worksheet.md` FOR PROJECT GUIDELINES**

## Overview

This task list outlines the steps for migrating the Calculator application to the new unified architecture. The Calculator application has been selected as the first application to migrate due to its relatively low complexity and few dependencies.

## Progress Tracking Legend
- [ ] = Pending task
- [x] = Completed task  
- [~] = In progress task
- [!] = Blocked or needs attention

## Implementation Tasks

### 1. Initial Setup
- [x] Create directory structure for Calculator application
- [ ] Examine existing Calculator application code
- [ ] Identify key functionality and dependencies

### 2. Implement Calculator UI
- [ ] Create CalculatorApp.razor file
  - [ ] Implement WindowBase inheritance
  - [ ] Set up WindowContent with Window="this"
  - [ ] Create calculator display area
  - [ ] Create number pad (0-9, decimal)
  - [ ] Create operation buttons (+, -, *, /)
  - [ ] Create function buttons (clear, equals, etc.)
- [ ] Create CalculatorApp.razor.css file
  - [ ] Style calculator display
  - [ ] Style calculator buttons
  - [ ] Implement responsive design
  - [ ] Add theme integration

### 3. Implement Calculator Logic
- [ ] Create CalculatorApp.razor.cs file
  - [ ] Inject required services
  - [ ] Implement ApplicationBridge integration
  - [ ] Create calculator state properties
  - [ ] Implement number input handling
  - [ ] Implement operation handling
  - [ ] Implement calculation logic
  - [ ] Add error handling (division by zero, etc.)

### 4. Application Lifecycle
- [ ] Implement initialization logic
  - [ ] Register with ApplicationBridge
  - [ ] Set window properties
  - [ ] Initialize calculator state
- [ ] Implement state management
  - [ ] Handle window state changes
  - [ ] Implement SetStateAsync
- [ ] Implement cleanup handling
  - [ ] Properly dispose resources
  - [ ] Unregister from ApplicationBridge

### 5. Testing
- [ ] Test basic calculator operations
  - [ ] Addition
  - [ ] Subtraction
  - [ ] Multiplication
  - [ ] Division
- [ ] Test complex scenarios
  - [ ] Chained operations
  - [ ] Error handling
  - [ ] Edge cases
- [ ] Test window functionality
  - [ ] Minimize, maximize, close
  - [ ] Resize behavior
  - [ ] Window state persistence

### 6. Documentation
- [ ] Add code comments
  - [ ] Document public methods and properties
  - [ ] Explain complex logic
  - [ ] Note any limitations or assumptions
- [ ] Update progress.md with migration details
- [ ] Document lessons learned for future migrations

## Notes
- The Calculator app will serve as a template for other window application migrations
- Focus on clean implementation following architecture guidelines
- Prioritize proper ApplicationBridge integration
