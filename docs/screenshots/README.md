# Playwright Test Report – Hacker Simulator (JS app)

The web application (`/src`) was built and served locally, then driven end‑to‑end
with Playwright (headless Chrome). Every desktop application, the Start menu and
the terminal command processor were exercised. The bugs found during testing were
fixed; after the fixes the app loads and runs with **no application console/page
errors** (the only remaining console messages are external CDN resources –
Google Fonts and the xterm stylesheet – which fail only in a fully offline
sandbox and degrade gracefully).

## Issues found and fixed

| # | Area | Problem | Fix |
|---|------|---------|-----|
| 1 | Build | `src/styles/main.less` imported Google Fonts via `@import url(...)`, which `less-loader` resolves at **build time** and fails offline, breaking the whole build. | Load the font at runtime with a `<link>` in `index.html`; removed the build‑time `@import`. |
| 2 | Startup | Start Menu read the IndexedDB‑backed filesystem in its constructor (before `fileSystem.init()`), throwing `Database not initialized` and an unhandled page error. | Defer `buildPinedApps()` to the existing `os.Ready()` callback, after the filesystem is initialized. |
| 3 | Code Editor | Monaco loaded its web worker from `https://unpkg.com/monaco-editor@latest/...`, which fails offline and risks a version mismatch with the bundled Monaco. | Use webpack‑bundled workers via `new Worker(new URL('monaco-editor/esm/...', import.meta.url))`. |
| 4 | Theme init | `ThemeManager` used `stat()` (which logs an error for a missing path) to test whether `/etc/themes` exists, logging an error on every startup. | Use `exists()` instead, treating a missing directory as an expected condition. |
| 5 | Page metadata | The app shipped no favicon, so every load produced a `GET /favicon.ico 404`. | Added an inline SVG favicon (no external file, works offline). |
| 6 | Blazor build | Stray extra `}` in `WindowStateTest.razor.cs` broke the `wasm2` Blazor build. | Removed the extra brace. |

## Screenshots

| View | File |
|------|------|
| Desktop | `01-desktop.png` |
| Start menu (pinned app tiles with colors) | `02-start-menu.png` |
| Terminal | `03-terminal.png` |
| File Explorer | `04-file-explorer.png` |
| Browser (in‑game HackerSearch) | `05-browser.png` |
| Code Editor (Monaco, locally bundled) | `06-code-editor.png` |
| System Monitor | `07-system-monitor.png` |
| Terminal running `help` and `ls /` | `08-terminal-commands.png` |
