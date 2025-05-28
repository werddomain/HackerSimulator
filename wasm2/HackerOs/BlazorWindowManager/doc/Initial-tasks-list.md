# Blazor Window Manager - Initial Task List

This document tracks the implementation progress of the Blazor Window Manager component project. Each task should be marked as completed when finished, with notes and remarks added as needed.

## Project Setup & Infrastructure

- [x] **Create main project structure**
  - [x] Set up `BlazorWindowManager.csproj` with proper dependencies (Added MudBlazor, DI)
  - [x] Set up `BlazorWindowManager.Test.csproj` for unit testing (Already exists)
  - [x] Configure project references and dependencies (MudBlazor, etc.)
  - [x] Create basic folder structure for components, services, and themes (Components/, Services/, Themes/, Models/)

## Core WindowBase Component

- [x] **WindowBase.razor - Basic Structure**
  - [x] Create `WindowBase.razor` component with title bar, content area, and borders
  - [x] Implement basic HTML structure with proper CSS classes for theming
  - [x] Add scoped CSS file `WindowBase.razor.css` with base styling (Modern/Hacker theme)
  - [x] Implement `RenderFragment` for window content projection

- [x] **WindowBase - Core Properties**
  - [x] Add `Id` property (auto-generated Guid)
  - [x] Add user-settable `Name` property
  - [x] Add `Title` property with display in title bar
  - [x] Add `Icon` property (accepting `RenderFragment`) for title bar
  - [x] Add `WindowState` enum and property (Normal, Minimized, Maximized)
  - [x] Add `Resizable` boolean property to enable/disable resizing
  - [x] Add `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight` properties

- [x] **WindowBase - Window Context & Services**
  - [x] Implement `WindowContext` (`IServiceCollection`) for scoped service injection
  - [x] Create method `GetWindowService<TService>()` for retrieving window-scoped services
  - [x] Implement proper service scope lifecycle management

- [x] **WindowBase - Movement & Dragging**
  - [x] Implement dragging logic for title bar
  - [x] Ensure dragging is constrained by parent container boundaries
  - [x] Implement smooth movement without outline preview
  - [x] Add `OnMoved` and `OnMoving` events
  - [x] Implement initial positioning logic (centered, then cascaded)

- [x] **WindowBase - Resizing**
  - [x] Implement resizing logic via draggable borders/corners
  - [x] Respect `MinWidth/Height` and `MaxWidth/Height` constraints
  - [x] Add `OnResized` and `OnResizing` events
  - [x] Ensure resizing respects `Resizable` property

- [x] **WindowBase - Focus Management**
  - [x] Implement focus management (bringing window to front on click)
  - [x] Manage z-index for active window state
  - [x] Expose `OnFocus` and `OnBlur` events
  - [x] Integrate with WindowManagerService for global focus tracking

- [x] **WindowBase - Window State Management**
  - [x] Implement minimize functionality with animation target area support
  - [x] Implement maximize functionality (full container or custom bounds)
  - [x] Implement restore functionality
  - [x] Add minimize/maximize/restore buttons in title bar
  - [x] Expose `OnStateChanged` event

- [x] **WindowBase - Closing Logic**
  - [x] Implement `Close()` method with optional `force` parameter
  - [x] Implement `BeforeClose` event (cancellable with EventArgs.Cancel)
  - [x] Implement `AfterClose` event
  - [x] Add close button in title bar
  - [x] Handle cleanup of window-scoped services

- [x] **WindowBase - Additional Events**
  - [x] Expose `OnTitleChanged` event
  - [x] Expose `OnContentLoaded` event
  - [x] Implement proper event argument classes for all events

## WindowManagerService

- [x] **WindowManagerService - Core Structure**
  - [x] Create `WindowManagerService.cs` class
  - [x] Implement dependency injection registration
  - [x] Design internal data structures for tracking windows

- [x] **WindowManagerService - Window Registration**
  - [x] Implement `RegisterWindow(WindowBase window)` method
  - [x] Implement `UnregisterWindow(Guid windowId)` method
  - [x] Maintain list of active windows with properties (Id, Name, Title, State, Position, Size, Z-index)

- [x] **WindowManagerService - Z-Index Management**
  - [x] Implement z-index management logic for bringing windows to front
  - [x] Create `BringToFront(Guid windowId)` method
  - [x] Implement `GetTopWindow()` method

- [x] **WindowManagerService - Global Events**
  - [x] Implement `WindowCreated` global event
  - [x] Implement `WindowBeforeClose` global event (with cancellation support)
  - [x] Implement `WindowCloseCancelled` global event
  - [x] Implement `WindowAfterClose` global event
  - [x] Implement `WindowStateChanged` global event (old state, new state)
  - [x] Implement `WindowActiveChanged` global event (newly active, previously active)
  - [x] Implement `WindowTitleChanged` global event

- [x] **WindowManagerService - Inter-Window Communication**
  - [x] Design message payload structure
  - [x] Implement `SendMessage(Guid targetWindowId, object messagePayload)` method
  - [x] Implement `OnMessageReceived` event on WindowBase
  - [x] Create message routing and delivery system

## DialogBase Component

- [x] **DialogBase - Basic Structure**
  - [x] Create `DialogBase.razor` inheriting from `WindowBase`
  - [x] Add modal overlay functionality
  - [x] Implement parent window relationship tracking

- [x] **DialogBase - Modality Logic**
  - [x] Implement modal behavior (blocking parent window interaction)
  - [x] Support for application-modal vs parent-modal dialogs
  - [x] Prevent parent window movement/resizing when dialog is active

- [x] **DialogBase - Dialog Results**
  - [x] Create `DialogResult<T>` class
  - [x] Implement `ShowDialogAsync()` method
  - [x] Support awaitable dialog patterns
  - [x] Create example dialogs (MessageBox, FileDialog, etc.)

- [x] **DialogBase - Parent Window Integration**
  - [x] Ensure dialogs close when parent is force-closed
  - [x] Handle dialog lifecycle with parent window state changes
  - [x] Implement proper cleanup when parent closes

## TaskBarComponent

- [x] **TaskBarComponent - Basic Structure**
  - [x] Create `TaskBarComponent.razor` basic structure
  - [x] Add scoped CSS file `TaskBarComponent.razor.css`
  - [x] Implement templating with left/right container slots

- [x] **TaskBarComponent - Window Integration**
  - [x] Implement logic to display buttons for open windows
  - [x] Show window icon and title in taskbar buttons
  - [x] Implement taskbar button click to focus/restore window
  - [x] Handle minimized window representation

- [x] **TaskBarComponent - Grouping & Context Menus**
  - [x] Implement window grouping by type (configurable)
  - [x] Add right-click context menus for taskbar items
  - [x] Support actions: minimize, maximize, restore, close
  - [x] Make grouping behavior configurable at runtime

## Desktop Area Component

- [x] **DesktopArea Component**
  - [x] Create `DesktopArea.razor` component
  - [x] Define boundaries for window movement and operations
  - [x] Integrate with WindowManagerService for container management
  - [x] Handle window positioning within desktop bounds

## Snapping Functionality

- [ ] **Window Snapping**
  - [ ] Design snapping logic for screen edges
  - [ ] Implement snapping to other windows
  - [ ] Add configuration options (enable/disable, sensitivity)
  - [ ] Create visual feedback during snapping operations
  - [ ] Create SnappingService for managing snap behavior
  - [ ] Integrate snapping with WindowBase dragging
  - [ ] Add snap zones (left half, right half, maximize)
  - [ ] Implement magnetic snapping to window edges

## Theming System

- [ ] **Base Theming Infrastructure**
  - [ ] Design CSS variable system for theming
  - [ ] Create base theme structure for all components
  - [ ] Implement theme switching mechanism

- [ ] **Predefined Themes**
  - [ ] Windows 98 theme
  - [ ] Windows XP theme
  - [ ] Windows Vista theme
  - [ ] Windows 7 theme
  - [ ] Windows 10 theme
  - [ ] macOS theme
  - [ ] Linux (modern) theme
  - [ ] Hacker/Matrix theme (CRT style, black and green)

## Accessibility (A11y)

- [ ] **Keyboard Navigation**
  - [ ] Implement Alt-Tab like application switcher
  - [ ] Add keyboard shortcuts for window management
  - [ ] Support arrow keys for window movement/resizing
  - [ ] Implement tab order management within windows

- [ ] **Screen Reader Support**
  - [ ] Add proper ARIA attributes to all components
  - [ ] Ensure sufficient color contrast in themes
  - [ ] Test with screen readers and improve as needed

## Performance Optimizations

- [ ] **Event Optimization**
  - [ ] Implement event debouncing for `OnMoving` and `OnResizing`
  - [ ] Optimize z-index updates and DOM manipulations
  - [ ] Minimize unnecessary re-renders

- [ ] **Content Virtualization**
  - [ ] Investigate content virtualization for off-screen windows
  - [ ] Implement performance monitoring for many windows scenario
  - [ ] Optimize window state management for large numbers of windows

## Testing & Documentation

- [ ] **Unit Tests**
  - [ ] Create unit tests for WindowManagerService
  - [ ] Create unit tests for core WindowBase functionality
  - [ ] Create unit tests for DialogBase behavior
  - [ ] Set up test infrastructure and CI

- [ ] **Documentation**
  - [ ] Write usage documentation for WindowBase
  - [ ] Write usage documentation for WindowManagerService
  - [ ] Write usage documentation for TaskBarComponent
  - [ ] Write theming guide
  - [ ] Create example projects and demos
  - [ ] Document all public APIs with XML comments

## Integration & Polish

- [ ] **Final Integration**
  - [ ] Test complete system with multiple windows
  - [ ] Verify all themes work correctly
  - [ ] Performance testing with many windows
  - [ ] Cross-browser compatibility testing

- [ ] **Polish & Refinement**
  - [ ] Fine-tune animations and transitions
  - [ ] Improve visual feedback for all interactions
  - [ ] Final accessibility review and improvements
  - [ ] Code review and refactoring

---

## Progress Notes

*Use this section to add remarks, discoveries, and notes as tasks are completed.*

**Completed Tasks:**
- [x] Project setup with MudBlazor dependencies and folder structure
- [x] Core models and enums (WindowState, WindowBounds, WindowEventArgs, WindowInfo)
- [x] WindowManagerService complete with all required functionality
- [x] WindowBase component with full functionality including:
  - Dragging and resizing with JavaScript interop
  - Window state management (minimize, maximize, restore)
  - Focus management and z-index handling
  - Event system for all window operations
  - Scoped service injection support
  - Modern/hacker aesthetic styling with CSS
- [x] DesktopArea component for window containment
- [x] Service registration extensions for DI
- [x] JavaScript module for mouse interaction handling
- [x] TaskBarComponent with full functionality including:
  - Window button display with icons and titles
  - Window grouping support (configurable)
  - Context menus for window actions
  - Real-time clock display
  - Modern/hacker themed styling
  - Responsive design support

**Current Focus:**
Ready to work on DialogBase component implementation

**Issues & Blockers:**
None identified so far. Core architecture is solid and components are well-integrated.

**Next Priorities:**
1. TaskBarComponent implementation
2. DialogBase component
3. Theming system
4. Testing and documentation
