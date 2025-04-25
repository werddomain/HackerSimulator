# Mobile Application TypeScript Errors Task List

## Overview
This task list addresses TypeScript compilation errors in the mobile implementation of the HackerSimulator project. The errors are primarily related to missing methods, incorrect type definitions, and interface implementation issues across various mobile components.

## Main Categories of Issues

1. Missing methods in classes and interfaces
2. Type definition errors (implicit any types)
3. Constructor accessibility issues
4. Property access errors (private/public)
5. DOM API type errors
6. Platform type detection issues
7. EventEmitter integration issues
8. Incompatible class extensions

## Task List

### 1. MobileFileExplorerApp Issues

- [x] Implement missing `getCurrentPath` method in `MobileFileExplorerApp` class
- [x] Fix WindowManager interface to include `showNotification` method
- [x] Fix WindowManager interface to include `showPrompt` method
- [x] Add type definition for `newName` parameter in rename method
- [x] Add missing `rename` method to FileSystem interface
- [x] Fix WindowManager interface to include `showConfirm` method
- [x] Add type definition for `confirmed` parameter in delete confirmation method
- [x] Add missing `rmdir` method to FileSystem interface
- [x] Add missing `unlink` method to FileSystem interface
- [x] Implement `getSelectedItems` method in `MobileFileExplorerApp` class or fix reference to `deleteSelectedItems`

### 2. MobileSettingsApp Issues

- [x] Fix Promise comparison issues in settings app (lines 394, 416, 456, 485-487)
- [x] Correct argument count in function calls (lines 394, 416, 431, 456, 476, 520, 599)
- [x] Add proper type definition for `theme` parameter (line 601)
- [x] Add null checks for possibly null objects (lines 641, 663)
- [x] Fix touch event handling types (lines 708, 709, 713, 714, 723, 725)
- [x] Implement `getCurrentMode` method in `PlatformDetector` class
- [x] Implement `setMode` method in `PlatformDetector` class
- [x] Fix arguments count in function call at line 852
- [x] Implement `handleError` method in `ErrorHandler` class
- [x] Change `getAvailableThemes` to `getAllThemes` or implement the missing method

### 3. MobileSystemMonitorApp Issues

- [x] Fix accessibility of `PerformanceMonitor` constructor
- [x] Fix DOM element style access (lines 204, 205, 215, 216, 226, 227)
- [x] Add type definition for `suggestion` parameter (line 248)
- [x] Fix export of `MobileSystemMonitorApp` in system-monitor-factory.ts

### 4. MobileTerminalApp Issues

- [x] Fix class inheritance issues with `TerminalApp`
  - [x] Make `handleResize` method in `MobileTerminalApp` private or protected to match parent class
  - [x] Fix access to private method `handleResize` from `TerminalApp`
  - [x] Fix access to private method `closeWindow` from `GuiApplication`
- [x] Implement missing `getPlatformType` method in `PlatformDetector` class
- [x] Fix `PlatformType.Mobile` reference to use `PlatformType.MOBILE`

### 5. SystemMonitor Issues

- [ ] Fix import of `PerformanceMetrics` (should be `PerformanceMetric`)
- [ ] Fix accessibility of `PerformanceMonitor` constructor
- [ ] Add null checks for performance monitor operations
- [ ] Implement missing `onUpdate` method in `PerformanceMonitor` class
- [ ] Add type definition for `metrics` parameter
- [ ] Implement missing `updateRenderingTab` method in `SystemMonitorApp` class
- [ ] Implement missing `start` method in `PerformanceMonitor` class

### 6. Core Components Issues

- [ ] Fix `EventEmitter` module import issues
- [ ] Implement missing methods in `PlatformDetector`
- [ ] Fix DOM element type casting issues in mobile optimizers
- [ ] Fix missing methods in `AppManager` interface:
  - [ ] `getInstalledApps`
  - [ ] `getAppInfo`
  - [ ] `getRecentApps`
  - [ ] `getRunningApps`
- [ ] Fix missing method in `Desktop` class: `showDesktop`
- [ ] Add type definitions for parameters in mobile navigation and mobile start menu
- [ ] Fix touch event handling in mobile components
- [ ] Fix `WeakMap` entries and values access methods compatibility
- [ ] Fix initialization of `itemContainer` property in `VirtualizedList` class
- [ ] Fix CSS style declaration issues in virtual components
- [ ] Correct `WindowManager` implementation according to `IWindowManager` interface

### 7. Special Fixes

- [ ] Fix syntax errors in dom-optimizer.ts (lines 424-426)
- [ ] Fix constant reassignment in dom-optimizer.ts (line 569)
- [ ] Fix incorrect event handler type in dom-performance-observer.ts (line 140)
- [ ] Fix possibly undefined access in dom-performance-observer.ts (lines 202, 513)
- [ ] Fix private property access in dom-performance-observer.ts (lines 794, 795, 797, 798)

## Implementation Strategy

1. First fix core component issues that many mobile apps depend on
2. Fix interface definitions and method signatures
3. Implement missing methods in platform-specific classes
4. Address type definition issues
5. Fix class inheritance and accessibility issues
6. Test components individually and in integration

## Testing

- After each set of related fixes, compile the code to check for resolved errors
- Test mobile functionality on appropriate devices/emulators
- Verify that fixes don't introduce regressions in desktop functionality
