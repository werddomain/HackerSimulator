# HackerOS Desktop Shell

## Purpose

`Platform/HackerOs.Platform.Blazor/Shell/` provides the user-facing desktop workspace, taskbar, application launcher, notification toast overlay, and session logout controls for HackerOS v3.

## Architecture

The shell is implemented using collocated Blazor components and scoped CSS files:

- `DesktopShell.razor/.css`: Root shell container hosting the desktop workspace, window outlet (`DesktopArea`), taskbar, popovers, and notification overlays.
- `Taskbar.razor/.css`: Fixed taskbar bound to `WindowRuntime` state, `ISimulationClock`, launcher trigger, active window buttons, unread notification count, and session status.
- `AppLauncher.razor/.css`: Accessible application launcher bound to `AppCatalog`. Features search input, category filtering (System, Utilities, Games, All), keyboard navigation (Arrow keys/Enter/Escape), and `AppIntentDispatcher` launch triggers.
- `NotificationCenter.razor/.css`: Toast overlay bound to `INotificationQueue`. Renders notification severity badges (Info, Warning, Error), source app, action triggers, and auto-dismiss.
- `LogoutDialog.razor/.css`: Modal confirmation dialog for session logout/shutdown with active process warning list and clean session termination via `ISessionService`.

## Theme & Accessibility

- Design system tokens (`--hos-*`) defined in `wwwroot/css/app.css` supply color palettes (Gothic/Hacker dark mode), monospace typography (`Cascadia Mono`), and surface boundaries.
- All interactive controls provide full keyboard navigation, focus indicators (`:focus-visible`), and ARIA attributes (`role="contentinfo"`, `role="tablist"`, `role="listbox"`, `role="status"`, `aria-live="polite"`).

## Key Decisions

- **Platform Contract Binding**: Shell components bind only to platform abstractions (`WindowRuntime`, `AppCatalog`, `INotificationQueue`, `ISessionService`, `ISimulationClock`), avoiding concrete application references.
- **Scoped Component Styling**: Styles are strictly contained in `.razor.css` files; inline styles and `<style>` blocks in Razor markup are forbidden.

## Task List

- [x] `P2-SHELL-001` Implement `DesktopShell.razor/.css` with work area, window outlet, taskbar, launcher, and notification outlets.
- [x] `P2-SHELL-001A` Define shared shell design tokens in `:root` CSS custom properties.
- [x] `P2-SHELL-002` Implement `Taskbar.razor/.css` from process/window state, simulation clock, and status.
- [x] `P2-SHELL-003` Implement `AppLauncher.razor/.css` from `AppCatalog` with search, categories, keyboard nav, and launch intents.
- [x] `P2-SHELL-004` Implement desktop shortcuts/settings policy.
- [x] `P2-SHELL-005` Implement `NotificationCenter.razor/.css` bound to `INotificationQueue` with toasts, severity, expiry, and ARIA announcements.
- [x] `P2-SHELL-006` Implement `LogoutDialog.razor/.css` for logout/shutdown confirmation, active process list, and clean session exit.
- [x] `P2-SHELL-007` Apply Gothic/Hacker visual design tokens and restrained colors.
- [x] `P2-SHELL-008` Support keyboard-only operation, focus indicators, screen readers, reduced motion, and text containment.
- [x] `P2-SHELL-009` Add unit and component tests in `Tests/HackerOs.Platform.Blazor.Tests/Shell/DesktopShellTests.cs`.
