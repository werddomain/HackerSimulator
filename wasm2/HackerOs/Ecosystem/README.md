# HackerOS Ecosystem

A modular Blazor WebAssembly framework for building a self-registering
application desktop. Write a component that inherits `WindowAppBase` or
`TerminalAppBase`, decorate it with `[App]`, and it appears in the START menu and
taskbar automatically.

## Projects

- **`HackerOs.AppFramework`** &mdash; the reusable framework (Razor Class Library).
- **`HackerOs.Ecosystem`** &mdash; the runnable host app with sample modules.
- **`tests`** &mdash; Playwright end-to-end tests.

## Run

```bash
cd HackerOs.Ecosystem
dotnet run
```

## Test

```bash
cd tests
npm install
npx playwright test
```

See [`docs/app-framework.md`](docs/app-framework.md) for the full architecture,
API reference and how to add your own applications.
