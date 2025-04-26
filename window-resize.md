# Window Manager Refactoring Plan

The current `WindowManager` class is too large and has multiple responsibilities. We need to split it into smaller, more focused modules to improve maintainability and readability.

## Proposed Structure

1. **Core Window Manager** (`window.ts`)
   - Keep core window management logic
   - Delegate DOM manipulation to specialized classes

2. **DOM Manipulation** (`window-dom.ts`)
   - Create a `WindowDomManager` class
   - Handle all DOM creation and manipulation
   - Public methods for window creation operations

3. **Window Resize Functionality** (`window-resize.ts`)
   - Extract resize-related functionality
   - Create a `WindowResizeHandler` class

4. **Window Event System** (`window-events.ts`)
   - Extract event handling functionality
   - Create a `WindowEventManager` class

5. **Window UI Effects** (`window-effects.ts`)
   - Handle animations, notifications, dialogs
   - Create a `WindowEffectsManager` class

## Tasks

### Task 1: Create WindowDomManager
- [ ] Create `window-dom.ts` file
- [ ] Extract DOM creation and manipulation methods
- [ ] Expose public methods for window element creation

### Task 2: Create WindowResizeHandler
- [ ] Create `window-resize.ts` file
- [ ] Extract `makeWindowResizable` method
- [ ] Create a class to handle window resize operations

### Task 3: Create WindowEventManager
- [ ] Create `window-events.ts` file
- [ ] Extract event handling system
- [ ] Create a class to manage window events

### Task 4: Create WindowEffectsManager
- [ ] Create `window-effects.ts` file
- [ ] Extract dialog and notification methods
- [ ] Create a class to handle UI effects

### Task 5: Refactor WindowManager
- [ ] Update `window.ts` to use the new modules
- [ ] Remove extracted code
- [ ] Create instances of the new classes
- [ ] Update method calls

### Task 6: Update Imports and References
- [ ] Update imports in files that use `WindowManager`
- [ ] Ensure all references are updated

## Implementation Example for Window DOM Manager

```typescript
// window-dom.ts
export class WindowDomManager {
  private windowElements: Map<string, HTMLElement> = new Map();
  private windowsContainer: HTMLElement | null = null;
  private taskbarItems: HTMLElement | null = null;
  
  constructor() {
    this.windowsContainer = document.getElementById('windows-container');
    this.taskbarItems = document.getElementById('taskbar-items');
  }
  
  // Methods for creating window elements
  public createWindowElement(id: string, options: WindowOptions): HTMLElement {
    // Implementation...
  }
  
  // Methods for manipulating window elements
  public getWindowElement(id: string): HTMLElement | null {
    return this.windowElements.get(id) || null;
  }
  
  // Other DOM-related methods...
}
```

## Implementation Example for Window Resize Handler

```typescript
// window-resize.ts
export class WindowResizeHandler {
  constructor(private windowDomManager: WindowDomManager) {}
  
  public makeWindowResizable(id: string, emitEvent: (type: string, id: string) => void): void {
    // Implementation of resize functionality
  }
}
```

This refactoring will significantly improve code organization, making it easier to maintain and extend the window management system.
