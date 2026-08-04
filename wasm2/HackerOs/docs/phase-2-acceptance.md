# Phase 2 Acceptance and Exit Gate Report

## Overview

This document records currently verified Phase 2 evidence. The 2026-08-03 audit
reopened criteria whose prior entries referenced implementation or design files
instead of executable acceptance evidence.

---

## Acceptance Criteria & Evidence Matrix

| ID | Criteria Summary | Role & Capabilities | Test Location & Automated Evidence | Status |
|---|---|---|---|---|
| `P2-ACC-001` | Clean profile initializes root (`/`, `/home`, `/etc`, `/tmp`, `/var`) and user home (`/home/{userId}`) | User / Admin | `Phase2AcceptanceTests.P2_ACC_001_CleanProfile_InitializesLinuxLikeRoot_AndUserHome` | **PASSED** |
| `P2-ACC-002` | Reload retains committed files, settings, grants, defaults, and catalog state | User / Admin | `IndexedDbBrowserContractTests.Reload_retains_committed_entries` | **PASSED** |
| `P2-ACC-003` | Desktop and launcher open Terminal/File Explorer through typed intents (`LaunchAppIntent`, `OpenFileIntent`) | User | `Phase2AcceptanceTests.P2_ACC_003_P2_ACC_005_TypedIntents_And_SingletonFocus` | **PASSED** |
| `P2-ACC-004` | Move, resize, focus, minimize, maximize, restore, taskbar, and close work by pointer/touch/keyboard | User | `IndexedDbBrowserContractTests.Window_runtime_handles_every_resize_edge_in_real_browser`; `Window_runtime_renders_and_handles_mouse_and_touch_pointer_gestures`; `Window_runtime_handles_keyboard_modality_close_and_viewport_changes`; three consecutive Release repetitions | **PASSED** |
| `P2-ACC-005` | Singleton launch restores/focuses existing instance without a second process | User | `Phase2AcceptanceTests.P2_ACC_003_P2_ACC_005_TypedIntents_And_SingletonFocus` | **PASSED** |
| `P2-ACC-006` | Every app launch creates a process; close/kill removes it and cancels its token | User / Admin | `Phase2AcceptanceTests.P2_ACC_006_AppLaunch_CreatesProcess_AndClose_RemovesProcess` | **PASSED** |
| `P2-ACC-007` | Core commands (`pwd`, `ls`, `cd`, `cat`, `echo`) execute through `TerminalAppBase`, streams, working directory, and exit status | User | `CoreCommandsTests` | **PASSED** |
| `P2-ACC-008` | Files created/edited in one app (e.g., Text Editor) appear in others (File Explorer / Terminal) and persist after reload | User | `TextEditorDocumentTests`, `FileExplorerWindowTests` | **PASSED** |
| `P2-ACC-009` | File opening honors explicit app, protected default, sole handler, Open With, and no-handler outcomes | User | `AppIntentResolverTests` | **PASSED** |
| `P2-ACC-010` | An app denied broad filesystem permission cannot obtain broad or selected-resource handles | User | `Phase2AcceptanceTests.P2_ACC_010_AppDeniedPermission_CannotAccessVfs` | **PASSED** |
| `P2-ACC-011` | User can inspect but not modify `/etc/hackeros/file-associations.json`; authorized Admin edit is validated and atomic | User / Admin | `Phase2AcceptanceTests.P2_ACC_011_ProtectedSettings_UserRead_AdminWrite` | **PASSED** |
| `P2-ACC-012` | File/folder dialogs enforce filters, access, overwrite, modality, handles, and cancellation | User | `FileDialogServiceTests` | **PASSED** |
| `P2-ACC-013` | Disabling an optional app removes launcher/association availability and cancels active instances | Admin | `Phase2AcceptanceTests.P2_ACC_013_P2_ACC_014_DisablingApp_And_ShutdownServiceCancellation` | **PASSED** |
| `P2-ACC-014` | Shutdown cancels sample background service and restart creates fresh volatile state | System | `Phase2AcceptanceTests.P2_ACC_013_P2_ACC_014_DisablingApp_And_ShutdownServiceCancellation` | **PASSED** |
| `P2-ACC-015` | Published PWA works after online install with server stopped and browser offline | User | Published-browser matrix not yet implemented | **REOPENED** |
| `P2-ACC-016` | PWA update preserves compatible data and never mixes release assets | User | Two-release published-browser update evidence not yet implemented | **REOPENED** |
| `P2-ACC-017` | Unit/contract tests remain browser-free where designed; browser E2E runs in automated CI | Developer / CI | Active CI still requires .NET 10 solution replacement and a passing run | **REOPENED** |

---

## Exit Gate Verification (`P2-GATE-001` through `P2-GATE-005`)

1. **`P2-GATE-001` — PASSED:** after `dotnet build HackerOs.sln --configuration Release --no-restore` completed with 0 warnings and 0 errors, `dotnet test HackerOs.sln --configuration Release --no-build` passed 615 tests with 0 failed and 0 skipped on 2026-08-03.
2. **`P2-GATE-002` — REOPENED:** final published PWA/static-asset verification remains outstanding.
3. **`P2-GATE-003` — REOPENED:** complete automated accessibility and manual assistive-technology evidence remains outstanding.
4. **`P2-GATE-004` — REOPENED:** criteria 015–017 do not yet have passing published/CI evidence.
5. **`P2-GATE-005` — AWAITING APPROVAL:** request explicit user approval only after gates 002–004 are supported by evidence.
