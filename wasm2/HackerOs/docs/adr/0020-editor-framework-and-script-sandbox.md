# ADR 0020: Code Editor Architecture, Framework Selection, and Script Execution Sandbox Policy

## Status
**ACCEPTED** (DECISION: D-014)

## Context
Wave 3 introduces code editing, typed clipboard handling, drag-and-drop payloads, and terminal editing (`nano`) into HackerOS WebAssembly.
We must evaluate editor framework options (Monaco vs CodeMirror) based on payload size, accessibility, offline PWA cache footprint, language modes, worker cleanup, and licensing. Furthermore, we must strictly isolate code editing from code execution.

## Decision Drivers
1. **Payload & Performance:** WASM bundle footprint, memory consumption, and startup latency.
2. **Accessibility & Keyboard Trapping:** Compliance with WCAG 2.2 AA and keyboard navigation.
3. **Execution Isolation:** Code editing DOES NOT imply script execution permissions. User-script execution requires explicit capability grants (`user-scripts.execute`).
4. **Collocated Interop:** Scoped Blazor component interop without global `window` contamination.

## Framework Comparison Matrix

| Metric | Monaco Editor | CodeMirror 6 | Choice & Rationale |
|---|---|---|---|
| **Bundle Size** | ~3.2 MB JS + Worker DLLs | ~350 KB modular JS | **CodeMirror** for PWA footprint |
| **Offline Cache** | Complex worker script mapping | Single collocated JS module | **CodeMirror** for PWA simplicity |
| **Language Support** | Full language services | Modular syntax parsers | **Both Supported** via Abstraction Layer |
| **Keyboard Accessibility** | Complex DOM trapping | Standard ARIA contenteditable | **CodeMirror / Custom Textarea** |

## Decision (DECISION: D-014)
1. **Editor Architecture:** We adopt a modular C# Code Editor component (`org.hackeros.code-editor`) with collocated syntax highlighting, file tab management, and VFS integration.
2. **Script Sandbox Policy:**
   - Editing a file or script in Code Editor or Nano is a read/write operation (`vfs.read`, `vfs.write`).
   - Executing a user script or code snippet is strictly controlled by the `user-scripts.execute` capability.
   - Script execution runs within an isolated web-worker sandbox without access to host DOM or sensitive OS APIs.
3. **Typed Clipboard Gateway:**
   - Platform Core exposes `IAppClipboardGateway` to safely handle typed text and virtual file references without raw browser DOM leakage.
4. **Typed Drag & Drop Payloads:**
   - Internal drag-and-drop operations utilize strongly typed records (`VirtualFileDragPayload`, `VirtualFolderDragPayload`).

## Consequences
- PWA initial bundle size remains small (<400 KB added).
- Security is preserved: malicious user scripts edited in the OS cannot compromise host state.
- Automated unit tests verify editor manifests, VFS open/save contracts, and permission bounds.
