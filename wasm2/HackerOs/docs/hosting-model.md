# Hosting Model

## Purpose

HackerOS is one set of Razor components, apps, and Platform/Infrastructure
services that can be composed by more than one host process. This document
describes the current hosts, what each one is for, and the long-term direction
for the optional server. It is a reference for orienting new work, not a task
list — it does not add or reopen anything in
`wasm2/HackerOs/docs/integration-task-list.md`.

## Guiding constraint

The browser is the local-first authority. Every component and service must
keep working when only the WebAssembly host is present, offline, with no
server reachable. Hosts other than `HackerOs.Ecosystem` exist to make
development or future deployment easier — none of them may become a required
dependency for ordinary use.

## Current hosts

### 1. `OS/HackerOs.Ecosystem` — the product host

`OS/HackerOs.Ecosystem/HackerOs.Ecosystem.csproj` is the standalone,
self-contained Blazor WebAssembly PWA: its own `wwwroot/index.html`, web app
manifest, and service worker. It has no ASP.NET Core process behind it and no
compile-time dependency on the server. This is the host that ships — it must
build, publish, and run fully offline on its own. See
[`ecosystem-host.md`](ecosystem-host.md).

### 2. `test/test` — the interactive debug harness

`test/test/test.csproj` is a Blazor **Web App** (ASP.NET Core host, `Program.cs`
+ `Components/App.razor`) that renders the exact same `HackerOs.Ecosystem.App`
component tree via `AddInteractiveWebAssemblyRenderMode`. It exists solely
because a Blazor Web App host gives a better local debugging loop (server-side
build/reload, `UseWebAssemblyDebugging`) than debugging the standalone WASM PWA
directly. It references the same app assemblies as `HackerOs.Ecosystem` and
adds MudBlazor's CDN font/CSS/JS for convenience during development.

This host is not a distribution target and is not a dependency of any other
project — it is expected to remain a permanent developer-facing harness, not a
temporary scaffold to delete once the migration is "done."

### 3. `Server/HackerOs.Server` — the optional backend, today

`Server/HackerOs.Server/HackerOs.Server.csproj` is an ASP.NET Core minimal-API
process (EF Core/SQLite) that is entirely optional at runtime. It currently
provides three capabilities, consumed over HTTP by the WASM client when present:

- **Sync** — versioned record push/pull with conflict resolution (ADR 0025).
- **Identity** — account/device registration and token management (ADR 0024).
- **Proxy** — server-validated HTTP/TCP/UDP proxying for authorized apps
  reaching the real network.

It does not host any Razor UI today. See [`server-security.md`](server-security.md)
and [`server-backup-restore.md`](server-backup-restore.md).

## Future direction: Server as a third UI host

It is a stated long-term goal — **not yet implemented, not yet scheduled** —
for `HackerOs.Server` to also become a third way to serve the UI: hosting the
same Razor components the way `test/test` does today, but as a real deployment
target rather than a debug harness, with backend-only contracts and services
(the ones `HackerOs.Server` already implements: sync, identity, proxy) injected
directly into the composition root instead of being reached over HTTP from the
WASM client.

Consequences this implies for any component/service work done in the
meantime, so it doesn't need to be redone later:

- Nothing in `Platform`, `Infrastructure`, or app projects should assume there
  is exactly one way to reach backend capabilities (HTTP call vs. injected
  service). Prefer depending on the existing abstractions/contracts rather than
  concrete HTTP client types where practical.
- The WASM-only composition (`AddHackerOsEcosystem`) must keep working
  unmodified whether or not a server-hosted mode exists — this is additive, not
  a replacement for the standalone PWA.
- No task in `integration-task-list.md` currently tracks this; when work
  actually starts on it, it should get its own ADR (server-hosted composition,
  render-mode strategy, contract injection boundary) before implementation.

## Non-goals

- This document does not change which host is "the" build target — that
  remains `OS/HackerOs.Ecosystem`.
- This document does not authorize adding a `Components/Pages` UI to
  `HackerOs.Server` yet. That is future work requiring its own design pass.
