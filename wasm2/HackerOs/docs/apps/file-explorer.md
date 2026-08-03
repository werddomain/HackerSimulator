# HackerOS File Explorer Application (`org.hackeros.file-explorer`)

## Purpose

`Apps/System/HackerOs.Apps.FileExplorer/` provides the graphical virtual file manager and directory browser for HackerOS v3, implemented as a first-party `AppKind.Window` application.

## Architecture

- **`FileExplorerWindow.razor/.css`**: Primary window UI inheriting `WindowAppBase`. Renders toolbar controls (Back, Forward, Up, Refresh, Home, Address Bar, Search Filter, View Mode Toggle), action buttons (New Folder, New File, Delete, Properties, Open With), sortable list/details view or icon grid view, and status bar.
- **`FileExplorerState.cs`**: State management encapsulating working directory (`CurrentPath`), navigation history stacks (`_backStack`, `_forwardStack`), item selection set, search query, and sort column ordering (`Name`, `Kind`, `Size`, `ModifiedDate`).
- **`OpenWithDialog.razor/.css`**: Modal dialog presenting catalog apps (`AppCatalog`) and allowing the user to select an app to open a file with via `AppIntentDispatcher`.
- **`FilePropertiesDialog.razor/.css`**: Modal dialog displaying entry metadata (Path, Kind, Size, Owner, Permissions, Modified Date).
- **`app.manifest.json`**: Manifest declaring reverse-domain ID `org.hackeros.file-explorer`, multi-instance window policy, and capabilities (`apps.launch`, `filesystem.user-home.read`, `filesystem.user-home.write`, `dialogs.file-open`, `dialogs.file-save`, `dialogs.folder-select`).

## Key Features & User Experience

- **Navigation**: Full directory navigation, breadcrumb editing, keyboard shortcuts (`Enter` on address bar), and history stack traversal.
- **View Modes**: Toggle between detailed list view with sortable table headers and high-density icon grid view.
- **File Operations**: Create folder (`CreateAsync`), create file, rename/move (`MoveAsync`), delete (`DeleteAsync`), and inspect entry properties (`StatAsync`).
- **Intent Dispatch & Open With**: Double-clicking files dispatches `LaunchAppIntent` or presents the "Open With" app selection dialog.
- **Event Auto-Refresh**: Subscribes to `IAppEventGateway` for real-time filesystem updates without polling.

## Task Checklist

- [x] `P2-FILE-001` Create manifest for `org.hackeros.file-explorer` with capabilities and window entry point.
- [x] `P2-FILE-002` Implement toolbar, breadcrumbs, address bar, navigation history, and CWD state.
- [x] `P2-FILE-003` Implement details/grid view, multi-selection, and sort ordering.
- [x] `P2-FILE-004` Implement folder/file creation, rename, delete, and properties dialog.
- [x] `P2-FILE-005` & `P2-FILE-006` Implement intent dispatch for opening files and "Open With" dialog.
- [x] `P2-FILE-007` Implement real-time event subscription auto-refresh.
- [x] `P2-FILE-008` Add unit tests in `Tests/HackerOs.Apps.FileExplorer.Tests/`.
