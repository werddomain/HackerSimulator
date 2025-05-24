# Notepad App

This document outlines the plain text editor implemented using Blazor and MudBlazor.

## Purpose
Provide a lightweight editor similar to Windows Notepad for editing plain text files within the simulated operating system.

## Architecture
- **NotepadApp** – window based application using a `<textarea>` for content.
- **notepad.js** – JavaScript helper used to run clipboard commands via `document.execCommand`.
- **FilePickerDialog** – reused for selecting files when opening or saving.
- **FileSystemService** – persists file contents.

## Usage
- Use the File menu to create, open or save documents.
- Edit menu contains basic clipboard actions and undo.
- View menu toggles word wrap.
- Files with `.txt` or `.md` extensions open with Notepad by default.

## Key Decisions
- Menus are implemented with MudBlazor for a modern look.
- Editing commands rely on `document.execCommand` for browser compatibility.
- Word wrap simply toggles the `wrap` attribute on the textarea.

## Task List
- [x] Create `NotepadApp.razor` UI with MudBlazor menus.
- [x] Implement `NotepadApp.razor.cs` logic for file operations and editing commands.
- [x] Add scoped styling in `NotepadApp.razor.css`.
- [x] Provide `notepad.js` helper module.
- [x] Register `NotepadApp` via `FileTypeService` and class attributes.
- [x] Document the app in this file.
