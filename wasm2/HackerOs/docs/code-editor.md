# Code Editor

## Purpose

The Code Editor provides bounded, multi-tab source editing over the app-scoped
virtual filesystem. Editing never grants script execution: that remains behind
the separate `user-scripts.execute` capability defined by ADR 0020.

## Architecture

- `CodeEditorDocument` owns one buffer, clean baseline, VFS path/revision,
  syntax mode, 1 MiB UTF-8 limit, and serializable recovery state.
- `CodeEditorSession` owns tab order, active-tab selection, dirty-close
  decisions, tab isolation, and complete C# recovery snapshots.
- `CodeEditorFileService` performs bounded reads, selected-resource-aware VFS
  access, new-file creation, and atomic optimistic content replacement. Denial,
  missing files, binary content, size limits, and revision conflicts are typed
  recoverable outcomes.
- `CodeEditorWindow` owns dialogs, tabs, status, shortcuts, C# document state,
  VFS-backed recovery, whole-window close confirmation, and JS resource disposal.
- `CodeEditorRecoveryStore` serializes bounded snapshots through the approved
  app-scoped VFS boundary. It returns typed missing, malformed, denied, and
  oversized outcomes rather than relying on browser storage.
- `CodeEditorWindow.razor.js` is the narrow collocated boundary to a local
  CodeMirror 6 bundle. It can create, update, focus, change syntax language, and
  destroy an editor; it cannot access the VFS, app context, or script execution.

The pinned bundle is generated from `package-lock.json` with `npm run build`.
It is 585,094 bytes minified and has no runtime CDN dependency. The project
build fails if the lock file, bundle, or license notice is missing.

## Usage and API

`CodeEditorDocument.TryEdit` rejects content over 1 MiB without corrupting the
previous buffer. `CompleteSave` advances the clean baseline only after a VFS
write commits. `CodeEditorSession.Close` returns `ConfirmationRequired` for a
dirty tab unless the caller explicitly confirms discard. `CaptureRecovery` and
`Restore` keep recovery serialization independent of CodeMirror and the DOM.

Developers rebuilding the checked-in browser asset use:

```powershell
cd Apps/System/HackerOs.Apps.CodeEditor
npm ci
npm run build
```

## Key decisions

- CodeMirror 6 is bundled locally per ADR 0020; all runtime editor packages are
  pinned exactly and npm reported zero vulnerabilities at installation.
- C# is authoritative for files, tabs, permissions, persistence decisions,
  dirty state, recovery data, and lifecycle. JavaScript owns only CodeMirror's
  DOM-specific adapter.
- CodeMirror has no worker in this configuration, avoiding worker authority and
  cleanup ambiguity. `destroy` is still guaranteed and browser-tested.
- Save replaces file content atomically with an expected revision. Save As
  creates an empty VFS entry when needed, then atomically replaces its content.

## Task list and evidence

- [x] Replace the prototype textarea with the local CodeMirror 6 adapter.
- [x] Implement independent tabs, syntax modes, bounded buffers, and dirty-tab
  confirmation.
- [x] Implement app-scoped VFS load, Save, Save As, typed denial, and optimistic
  revision-conflict behavior.
- [x] Add deterministic recovery snapshots, approved VFS persistence, and editor
  disposal.
- [x] Connect dirty state to the platform's whole-window close coordinator.
- [x] Pass 20 focused tests, the Chromium
  `Code_editor_local_bundle_edits_switches_mode_and_disposes_cleanly` test, and
  the representative serious/critical axe scan.
- [ ] Prove recovery in a real rendered Code Editor reload scenario.
- [ ] Exercise the complete window plus real file dialogs/VFS in published,
  offline, lazy-loaded browser output.

`P4-W3-002` and audit item `E-001` remain unchecked until real component reload
and published-browser gaps are closed.
