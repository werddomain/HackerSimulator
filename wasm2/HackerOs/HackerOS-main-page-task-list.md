# HackerOS Main Page Implementation Task List

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `wasm2\HackerOs\worksheet.md` FOR ALL ARCHITECTURAL GUIDELINES AND REQUIREMENTS**

## 📋 Task Tracking Instructions

- Use `[ ]` for incomplete tasks and `[x]` for completed tasks
- When a task is marked complete, add a brief remark or timestamp
- Break down complex tasks into smaller sub-tasks for clarity
- Create Analysis Plans before starting major development tasks
- Reference Analysis Plans in task descriptions for context

## Progress Tracking Legend
- [ ] = Pending task
- [x] = Completed task  
- [~] = In progress task
- [!] = Blocked or needs attention
- [✅] = Verified complete in codebase

## 🎯 CURRENT PRIORITY: Main Page and Desktop Implementation

### Analysis Plan Creation (IMMEDIATE PRIORITY)
- [x] Task 1.1: Create `analysis-plan-main-page.md` - COMPLETED July 3, 2025
  - [x] Analyze existing code structure and components
  - [x] Determine integration points for BlazorWindowManager
  - [x] Plan Desktop implementation approach
  - [x] Design TaskBar integration strategy
  - [x] Outline file/directory linking implementation

### Core Desktop Implementation
- [~] Task 2.1: Create Desktop Component
  - [x] Enhance existing Desktop.razor component
  - [x] Implement desktop background and styling
  - [x] Add desktop icon container/grid
  - [x] Implement desktop context menu
  - [x] Add drag-and-drop support for icons

- [~] Task 2.2: Implement Desktop Icons
  - [x] Create DesktopIconModel class
  - [x] Implement icon rendering with images
  - [x] Add click handlers for application launching
  - [x] Implement icon selection/highlighting
  - [x] Add icon context menu

- [~] Task 2.3: Implement File System Integration
  - [x] Create DesktopFileService to fetch files/directories for desktop
  - [x] Implement directory-to-icon mapping
  - [x] Add file/directory detection and appropriate icons
  - [x] Implement desktop refresh on file system changes
  - [x] Register all desktop UI services in Program.cs - COMPLETED July 3, 2025

### TaskBar & Window Management Integration
- [~] Task 3.1: Integrate BlazorWindowManager
  - [x] Reference BlazorWindowManager project
  - [x] Configure window management services in Program.cs - COMPLETED July 3, 2025
  - [x] Add window container to main layout
  - [x] Implement window z-index management - COMPLETED July 3, 2025

- [~] Task 3.2: Implement TaskBar
  - [x] Update existing TaskBar component
  - [x] Add start menu button
  - [x] Implement running application tracking
  - [x] Add system tray with clock/notifications
  - [x] Implement task switching - COMPLETED July 3, 2025

- [~] Task 3.3: Create Start Menu
  - [x] Design start menu layout - COMPLETED July 3, 2025
  - [x] Implement application list from registry - COMPLETED July 3, 2025
  - [x] Add search functionality - COMPLETED July 3, 2025
  - [x] Implement category grouping - COMPLETED July 3, 2025
  - [x] Add recently used applications section - COMPLETED July 3, 2025
  - [x] Style Start Menu component with CSS - COMPLETED July 3, 2025
  - [x] Integrate with MainLayout and Taskbar - COMPLETED July 3, 2025
  - [~] Implement application pinning functionality
  - [ ] Test all Start Menu functionality with actual applications

- [~] Task 3.4: Implement Notification System
  - [x] Create NotificationToast component - COMPLETED July 3, 2025
  - [x] Enhance NotificationCenter component
  - [x] Implement notification service integration
  - [x] Add toast notifications
  - [x] Implement application launching from notifications
  - [ ] Add notification sound effects

### Application Management
- [ ] Task 4.1: Implement Application Registry
  - Requirements:
    - App is declared using App Attribute. Like this for a built application: [App(Id="builtin.Notepad", Name="Notepad", Icon="/apps/notepad/icon.png"")] [AppDescription("Simple notepad application to read or write plain text document.")]
    - (AppDescription is optional)
    - Icon can also be an icon from a library. Like "fa-note" for the note icon from font awsome.
      -> We will need an icon factory where we can pass a string (Like a filePath or a string in this format: "[fa/iconic/mid]-{name}" -> [icon provider]-{name}  ) and this factory will return a renderFragment to render the iconé
  - [ ] Create ApplicationRegistry service
  - [ ] Implement application discovery
  - [ ] Add metadata parsing (icons, descriptions, etc.)
  - [ ] Implement application launching service

- [ ] Task 4.2: Implement window use from "wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager"
  - [ ] Create documentation on how to use the BlazorWindowManager like in the 'wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager\Components\ThemeSelector.razor'
    -> We need to write content inside a 'WindowContent' component. 
    -> Code behing must be in a separate file. Like for ThemeSelector.razor, the code behind must be in ThemeSelector.razor.cs, style in ThemeSelector.razor.css and if we need javascript, in ThemeSelector.razor.js
  - [ ] Create a simple Notepad application
  - [ ] Implement application lifecycle hooks with the windowManager from "wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager"
  - [ ] Implement window state persistence or use the one in "wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager"

### Main Page Integration
- [~] Task 5.1: Create Main Layout
  - [x] Update MainLayout.razor - COMPLETED July 3, 2025
  - [x] Integrate Desktop component
  - [x] Add TaskBar component
  - [x] Configure window container
  - [ ] Implement global key bindings

- [ ] Task 5.2: Implement User Session Integration
  - [ ] Add or use existing or use existing user authentication check
  - [ ] Implement session-specific desktop
  - [ ] Add or use existing user preferences loading
  - [ ] Implement theme selection

### Testing & Validation
- [ ] Task 6.1: Implement Test Applications
  - [ ] Create simple text editor app
  - [ ] Add file browser application
  - [ ] Implement settings application
  - [ ] Create terminal application

- [ ] Task 6.2: Test Integration Points
  - [ ] Verify window management works
  - [ ] Test application launching
  - [ ] Validate desktop icon functionality
  - [ ] Test taskbar operations

## Next Steps:
1. Create analysis plan
2. Implement core desktop components
3. Integrate window management system
4. Add taskbar and start menu
5. Connect all components in main layout
