# WordPad App

This document describes the basic rich text editor application implemented for the Blazor WebAssembly version of Hacker Simulator.

## Purpose

WordPad provides simple word processing capabilities that sit between the plain text editor and a full office suite. Users can open, edit, and save rich text documents stored in the simulated file system.

## Architecture

- **WordPadApp** – Blazor window-based application using a contenteditable div for editing.
- **wordpad.js** – JavaScript module wrapping `document.execCommand` to apply formatting and to get/set editor HTML.
- **FileSystemService** – Used to load and save document content as HTML.

## Usage

- Toolbar buttons invoke formatting commands (bold, italic, underline, unordered list and text color).
- The path box specifies the file path to open or save.
- Files are stored as HTML text and can use extensions like `.rtf`, `.docx`, `.txt` or `.html`.

## Key Decisions

- Document content is stored as HTML for simplicity.
- Formatting relies on the deprecated but widely supported `execCommand` API.

## Task List

- [x] Create `wordpad.js` helper module.
- [x] Implement `WordPadApp.razor` UI.
- [x] Implement `WordPadApp.razor.cs` logic.
- [x] Add scoped styling in `WordPadApp.razor.css`.
- [x] Document the feature in this file.
