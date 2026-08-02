# Terminal and Shell

## Purpose

Provide a windowed terminal emulator backed by a browser-independent shell and
independently versioned `TerminalAppBase` command apps.

## Status

The first-slice grammar was accepted on 2026-08-01 in ADR 0014
(`docs/adr/0014-shell-grammar-boundary.md`). The terminal emulator and shell are
not implemented.

## Proposed boundary

The headless shell owns tokenization, environment expansion, command resolution,
working directory, history state, cancellation, streams, and exit status. The
terminal window owns xterm integration, line editing, key handling, completion
UI, ANSI display, and resize.

Commands receive raw ordered tokens and parse their own flags. They never receive
xterm.js or mutate a global current directory.

## First-slice grammar

- Unicode whitespace separates unquoted tokens.
- Single quotes are literal.
- Double quotes support environment expansion and small explicit escapes.
- `$NAME` and `${NAME}` expand outside single quotes.
- Unsupported pipes, redirects, jobs, lists, substitutions, and scripts return
  structured syntax errors unless quoted or escaped.
- Syntax, not-found, cancellation, fault, and command statuses remain distinct.

## Exclusions

- Pipelines, redirection, jobs, scripting, and command substitution.
- Dynamic user aliases.
- Persistent history/environment startup files.
- Renderer dependencies in parser or command apps.

## Task list

- [x] Draft ADR 0014 for the first-slice grammar boundary.
- [x] Obtain Product + SDK approval for D-007.
- [ ] Implement session state and tokenizer/parser.
- [ ] Resolve enabled command manifests and aliases.
- [ ] Integrate the renderer through isolated lifecycle-managed interop.
- [ ] Add headless and browser tests.