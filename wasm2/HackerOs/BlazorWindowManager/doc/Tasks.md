
We will create a list of tasks to html us organise the work to create the Blazor Component Project in this project: wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager\BlazorWindowManager.csproj
Create a task list in wasm2\HackerOs\BlazorWindowManager\doc\Initial-tasks-list.md

The component is a helper to create Window. This component will be base on what we have on others projects : 
- In 'src/' typescript project 'src\core\window.ts'
- In 'wasm\HackerSimulator.Wasm\HackerSimulator.Wasm.csproj' where windows inherith of WindowBase (wasm\HackerSimulator.Wasm\Windows\WindowBase.razor.cs)

# Blazor Window Manager: Project Overview & Features

This document outlines the plan for creating a comprehensive Blazor Window Manager component, designed to replicate a rich, desktop-like user experience within the browser, akin to Microsoft Windows. The core project will reside in `wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager\BlazorWindowManager.csproj`, with an initial task list maintained in `wasm2\HackerOs\BlazorWindowManager\doc\Initial-tasks-list.md`.

The Blazor Window Manager will provide developers with a powerful helper component to easily create and manage draggable, resizable, and interactive windows. This component will build upon concepts and experiences from existing projects, specifically referencing the TypeScript project `'src/core/window.ts'` and the windowing system in `'wasm/HackerSimulator.Wasm/HackerSimulator.Wasm.csproj'` where windows inherit from `WindowBase` (`wasm/HackerSimulator.Wasm/Windows/WindowBase.razor.cs`).

## Core Component: `WindowBase`

The `WindowBase` component will be the cornerstone of the system, offering features analogous to a native desktop window:

- **Identification:** Each window instance will have a unique `Guid Id` and an optional user-defined `Name`.
- **Content:** Window content will be supplied via a `RenderFragment`.
- **Scoped Services:** Each `WindowBase` instance will expose a protected `IServiceCollection` named `WindowContext`, scoped specifically to that window. Developers can retrieve window-scoped services using `WindowContext.GetRequiredService<TService>()`.
- **Standard Window Actions:**
    - **Title Bar:** Contains the window title and (optional) icon. Dragging is initiated from the title bar.
    - **Icon:** A default icon will be provided. Users can customize this via a `RenderFragment` template.
    - **Resizing:** Windows will be resizable by default via draggable borders/corners. This can be disabled per window by setting a `Resizable` property to `false`. Minimum and maximum size constraints can be defined.
    - **Minimizing, Maximizing, Restoring:** Windows will support standard states:
        - **Minimize:** By default, minimized windows will appear on the `TaskBarComponent`. If the taskbar is not used, developers can specify a `RenderFragment` reference to act as an "Animation Target Area," where they can then handle the visual representation of the minimized state.
        - **Maximize:** Maximized windows will fill their designated container. This container can be an optional `Container` property on the window. Alternatively, a global `viewPort` can be set in the configuration service. By default, maximization will fill the browser screen, respecting the space occupied by the `TaskBarComponent` or other registered "Animation Target Areas."
        - **Restore:** Returns the window to its previous size and position.
    - **Closing:**
        - A `BeforeClose` event will be raised, providing an `EventArgs` with a `Cancel` property. Setting `Cancel` to `true` will halt the closing process.
        - The cancellation can be overridden if a global event `WindowCloseCancelRequested` forces the closure, or if the `Window.Close(force: true)` method is invoked.
        - An `AfterClose` event will be raised just before the window component instance is destroyed.
- **Focus Management:** Clicking anywhere on a window (title bar or content) will bring it to the front (highest `z-index`) and set it as the active window. The previously active window will lose focus. Events will be triggered for both gaining and losing focus.
- **Dragging:**
    - Initiated by dragging the title bar.
    - The entire window will move smoothly (no outline preview during drag).
    - Dragging will be constrained within the boundaries of its parent container (e.g., the "desktop area" or another window if nested).
- **Initial State & Positioning:**
    - By default, new windows will open centered.
    - Subsequent windows will adopt a "Cascaded" (or "Tiled") positioning strategy: offset from the last opened window by the height of a title bar (Y-axis) and double that amount (X-axis). This sequence will reset to the top of the screen once it reaches the container's edge.
- **Snapping:** Windows will support snapping behavior (e.g., to screen edges or other windows).

## Dialog Windows (`DialogBase`)

A specialized `DialogBase` component, inheriting from `WindowBase`, will be available for modal interactions:

- **Modality:** Dialogs can be modal to a specific parent window, preventing interaction with that parent until the dialog is closed. They can also be application-modal.
- **Restricted Interaction:** When a dialog is active and modal to a parent window, the parent window cannot be moved, resized, or have its state changed (e.g., minimized/maximized).
- **Awaitable Results:** Dialogs will support awaitable `DialogResult<T>` for common patterns (e.g., `MessageBox`, `Prompt`).
    - Example Declaration: `public partial class FileDialog : Dialog<FileOpenResult>;`
    - Example Invocation:
        ```csharp
        // 'owner' can be a RenderFragment reference or a WindowBase instance
        var fileDialog = DialogService.Create<FileDialog>(container: owner);
        var result = await fileDialog.ShowDialogAsync();
        if (result.Success) { /* ... */ }
        ```
- **Closing Behavior:**
    - If a parent window of an active dialog is closed *without* `Force: true`, the dialog will remain open (and the parent might not close depending on its `BeforeClose` logic).
    - If a parent window is closed with `Force: true`, the dialog and any of its own child dialogs will also be forcibly closed.

## Window Manager Service (`WindowManagerService`)

A central `WindowManagerService` will orchestrate window management:

- **Window Tracking:** Maintains a list of all open windows, including their state (`WindowState`), position, `Name`, `Title`, and `Id`.
- **Z-Index Management:** Dynamically manages the `z-index` of windows to ensure the active window is always on top.
- **Global Events:** Exposes global events for:
    - `WindowCreated`
    - `WindowBeforeClose` (cancellable)
    - `WindowCloseCancelled`
    - `WindowAfterClose`
    - `WindowStateChanged` (providing old and new state)
    - `WindowActiveChanged` (providing newly activated and deactivated window instances)
    - `WindowTitleChanged`
- **Inter-Window Communication:** Provides a `SendMessage(targetWindowId, messagePayload)` method and a corresponding `OnMessageReceived(sourceWindowId, messagePayload)` event per window instance.

## Taskbar Component (`TaskBarComponent`)

An optional `TaskBarComponent` will be offered:

- **Templating:** Fully templatable, with optional left (e.g., for a Start Menu) and right (e.g., for tray icons or a clock) container slots for custom `RenderFragment` content.
- **Window Representation:** Displays buttons for open windows, showing their icon and title.
- **Grouping:** By default, multiple instances of the same window type will be grouped. This behavior will be configurable via settings, even at runtime.
- **Context Menus:** Taskbar items will feature right-click context menus for actions like minimize, maximize, restore, and close.
- **Activation:** Clicking a taskbar button will focus and restore/bring to front the corresponding window.
- **(Bonus Feature Idea):** Window previews on hover (not in initial scope).
- **(User Implemented):** Pinning applications/windows to the taskbar will be a feature developers can build on top, not a built-in function of this component.

## Desktop Area Component

A designated "Desktop Area" component will be provided where all `WindowBase` instances are rendered. This component will typically define the main interaction surface and boundaries for window operations.

## Styling and Theming

- **Full Customization:** Developers will have complete control over the appearance via CSS. Individual windows can have custom CSS classes applied.
- **Predefined Themes:** A selection of built-in themes will be provided:
    - Windows 98
    - Windows XP
    - Windows Vista
    - Windows 7
    - Windows 10
    - macOS
    - Linux (a generic modern style)
    - A "Hacker/Matrix" theme (CRT style, black and green, futuristic).

## Events (Per-Window)

Beyond global events, each `WindowBase` instance will expose a rich set of events:

- `OnMoved`, `OnMoving`
- `OnResized`, `OnResizing`
- `OnContentLoaded` (when the `RenderFragment` content is ready)
- `OnFocus`, `OnBlur`
- `OnStateChanged`
- `OnTitleChanged`
- And others deemed beneficial for fine-grained developer control.
- **Event Payloads:** Event arguments will contain only relevant information for that specific event.

## Advanced Features & Considerations

- **State Persistence:** (User Implemented) The component itself will not persist window states across sessions. Developers can implement this using `localStorage` or other mechanisms by leveraging the `WindowManagerService` data and events.
- **Virtual Desktops:** (Future Consideration) The design will aim to be extensible enough to potentially support virtual desktops in a future release.
- **Accessibility (A11y):**
    - Keyboard navigation for window management (moving, resizing, switching states).
    - An Alt-Tab-like application switcher will be a mandatory feature.
    - ARIA attributes and screen reader compatibility will be considered.
- **Performance:**
    - Event debouncing for actions like resizing and moving.
    - Optimized rendering strategies.
    - Consideration for content virtualization within windows if performance with many complex windows becomes an issue.

## Project Structure and Naming Conventions

- **Main Project:** `wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager\BlazorWindowManager.csproj`
- **Test Project:** `wasm2\HackerOs\BlazorWindowManager\BlazorWindowManager.Test\BlazorWindowManager.Test.csproj`
- **Core Components:** `WindowBase.razor`, `DialogBase.razor`
- **Service:** `WindowManagerService.cs`
- **Taskbar:** `TaskBarComponent.razor`

---

### Example Tasks for `Initial-tasks-list.md`:

- **Core `WindowBase`:**
    - Implement `WindowBase.razor` basic structure (title bar, content area).
    - Add `Id` and `Name` properties to `WindowBase`.
    - Implement `RenderFragment` for window content.
    - Implement basic dragging logic for the title bar.
    - Implement basic resizing logic via borders.
    - Add `Resizable` property.
    - Implement `WindowContext` (`IServiceCollection`) scoping.
    - Implement `Close()` method and `BeforeClose`/`AfterClose` events.
    - ...
- **`WindowManagerService`:**
    - Create `WindowManagerService` class.
    - Implement window registration/unregistration.
    - Implement Z-index management logic.
    - Implement `WindowCreated` global event.
    - ...
- **Taskbar:**
    - Create `TaskBarComponent.razor` basic structure.
    - Implement logic to display buttons for open windows.
    - Implement taskbar button click to focus window.
    - ...
- **Theming:**
    - Design CSS structure for `WindowBase` theming.
    - Implement Windows 98 theme.
    - ...
- **Dialogs:**
    - Create `DialogBase.razor` inheriting from `WindowBase`.
    - Implement modality logic for dialogs.
    - Create `DialogService` for creating dialog instances.
    - Implement `ShowDialogAsync()` and `DialogResult<T>`.
    - ...

---

### Example Tasks for `Initial-tasks-list.md` (Continued):

- **Core `WindowBase` (Continued):**
    - Implement `WindowBase.razor` basic structure (title bar, content area, borders).
    - Add `Id` (auto-generated Guid) and user-settable `Name` properties to `WindowBase`.
    - Implement `Title` property and display it in the title bar.
    - Implement `RenderFragment` for window content projection.
    - Implement `Icon` property (accepting `RenderFragment`) for the title bar.
    - Implement basic dragging logic for the title bar, constrained by parent container.
    - Implement basic resizing logic via draggable borders/corners.
    - Add `Resizable` boolean property to enable/disable resizing.
    - Add `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight` properties.
    - Implement `WindowContext` (`IServiceCollection`) for scoped service injection.
    - Implement `Close()` method (with `force` parameter).
    - Implement `BeforeClose` event (cancellable) and `AfterClose` event.
    - Implement `WindowState` property (Normal, Minimized, Maximized).
    - Implement minimize, maximize, and restore button functionality in the title bar.
    - Implement logic for initial window positioning (centered, then cascaded).
    - Implement focus management (bringing window to front on click, setting active state).
    - Expose `OnFocus` and `OnBlur` events.
    - Expose `OnMoved` and `OnResized` events (potentially with `OnMoving` and `OnResizing` for continuous feedback).
    - Expose `OnStateChanged` event.
    - Expose `OnTitleChanged` event.
    - Expose `OnContentLoaded` event.

- **`WindowManagerService` (Continued):**
    - Create `WindowManagerService.cs` class.
    - Implement window registration (`RegisterWindow`) and unregistration (`UnregisterWindow`).
    - Maintain a list of active windows with their properties (Id, Name, Title, State, Position, Size, Z-index).
    - Implement Z-index management logic for bringing windows to front.
    - Implement global event: `WindowCreated`.
    - Implement global event: `WindowBeforeClose` (and mechanism to respect cancellation or force close).
    - Implement global event: `WindowCloseCancelled`.
    - Implement global event: `WindowAfterClose`.
    - Implement global event: `WindowStateChanged` (payload: window, old state, new state).
    - Implement global event: `WindowActiveChanged` (payload: newly active window, previously active window).
    - Implement global event: `WindowTitleChanged` (payload: window, new title).
    - Design and implement inter-window communication:
        - `SendMessage(targetWindowId, messagePayload)` method.
        - `OnMessageReceived(sourceWindowId, messagePayload)` event on `WindowBase`.

- **`TaskBarComponent` (Continued):**
    - Create `TaskBarComponent.razor` basic structure.
    - Implement `RenderFragment` slots for `LeftContainer` (e.g., Start Menu) and `RightContainer` (e.g., Tray Icons).
    - Subscribe to `WindowManagerService` events (`WindowCreated`, `WindowClosed`, `WindowTitleChanged`, `WindowActiveChanged`, `WindowStateChanged`).
    - Implement logic to display buttons for open (non-minimized or all, depending on settings) windows.
    - Display window icon and title on taskbar buttons.
    - Implement taskbar button click to focus, restore, or minimize the corresponding window.
    - Implement visual indication for active window on the taskbar.
    - Implement window grouping setting (on/off).
        - If grouping is on, display grouped windows under a single button or a representative button.
    - Implement context menu for taskbar items (Minimize, Maximize, Restore, Close).
    - Allow full templating for taskbar items.
    - Implement logic for handling minimized windows (if taskbar is the target).

- **`DialogBase` and `DialogService`:**
    - Create `DialogBase.razor` inheriting from `WindowBase`.
    - Add specific properties/methods for dialog behavior (e.g., `DialogResult`).
    - Implement modality:
        - Application-modal (blocks all other windows).
        - Parent-modal (blocks a specific owner window).
    - Prevent interaction with parent window when a modal dialog is active.
    - Prevent parent window state changes when a modal dialog is active.
    - Create `DialogService` for creating and showing dialogs.
        - `Create<TDialog>(owner)` method where `TDialog` : `DialogBase`.
        - `ShowDialogAsync()` method on dialog instances, returning `Task<DialogResult<T>>`.
    - Implement basic dialogs:
        - `MessageBox` (Ok, OkCancel, YesNo, YesNoCancel).
        - `PromptDialog` (for text input).
    - Ensure dialogs close if their parent is force-closed.

- **Theming (Continued):**
    - Define base CSS structure and variables for `WindowBase`, `DialogBase`, and `TaskBarComponent` to allow theming.
    - Implement Theme 1: Windows 98.
        - CSS for window borders, title bar, buttons (min, max, close).
        - CSS for taskbar appearance.
        - CSS for dialogs.
    - Implement Theme 2: Windows XP.
    - Implement Theme 3: Windows Vista.
    - Implement Theme 4: Windows 7.
    - Implement Theme 5: Windows 10.
    - Implement Theme 6: macOS.
    - Implement Theme 7: Linux (generic modern).
    - Implement Theme 8: Hacker/Matrix (CRT style).
    - Provide a mechanism for users to easily switch themes or apply custom CSS classes.

- **Snapping Functionality:**
    - Design snapping logic (screen edges, other windows).
    - Implement visual cues for snapping targets.
    - Implement window position adjustment on snap.
    - Add configuration options for snapping (enable/disable, sensitivity).

- **Desktop Area Component:**
    - Create a `DesktopArea.razor` component.
    - This component will act as the primary container for `WindowBase` instances.
    - Define how windows are rendered within this area (e.g., using `RenderFragment` or direct child components managed by `WindowManagerService`).
    - Manage boundaries for window movement and maximization (if not using full viewport).

- **Accessibility (A11y):**
    - Implement keyboard navigation for switching between open windows (e.g., Alt-Tab like "App Switcher" UI).
    - Allow keyboard control for window actions (move, resize, minimize, maximize, close) for the active window.
    - Ensure ARIA attributes are appropriately used for windows, title bars, buttons, and taskbar items.
    - Test with screen readers.
    - Ensure sufficient color contrast in default themes.

- **Performance Optimizations:**
    - Implement event debouncing for frequent events like `OnMoving` and `OnResizing`.
    - Profile and optimize rendering performance, especially with many windows.
    - Investigate and implement content virtualization for windows if needed (for off-screen or complex content).

- **Documentation & Examples:**
    - Write basic usage documentation for `WindowBase`, `WindowManagerService`, `TaskBarComponent`, and `DialogService`.
    - Create example Blazor pages showcasing different features.
    - Document all public APIs, events, and theming capabilities.

- **Project Setup & Build:**
    - Finalize `.csproj` settings for `BlazorWindowManager`.
    - Set up the `BlazorWindowManager.Test` project.
    - Implement initial unit tests for key service logic and component behaviors.


