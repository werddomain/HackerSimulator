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

### 3. `Server/HackerOs.Server` — the optional backend, and a third UI host

`Server/HackerOs.Server/HackerOs.Server.csproj` is an ASP.NET Core process
(EF Core/SQLite) that is entirely optional at runtime. It provides three
backend capabilities, consumed over HTTP by the WASM client when present:

- **Sync** — versioned record push/pull with conflict resolution (ADR 0025).
- **Identity** — account/device registration and token management (ADR 0024).
- **Proxy** — server-validated HTTP/TCP/UDP proxying for authorized apps
  reaching the real network.

Per ADR 0027, it also hosts the same `HackerOs.Ecosystem.App` Razor component
tree the other two hosts render, via Interactive Server render mode
(`Components/App.razor`, mapped in `Program.cs` alongside the unchanged API
endpoints). This mirrors how `test/test` wires the shared component tree in,
but as a real deployment target rather than a debug harness. **This host is
single-tenant / single-active-circuit for this phase**: `AddHackerOsEcosystem`
is reused completely unmodified, and nearly every service it registers is a
process-wide singleton — correct for exactly one connected browser circuit
(the same assumption `test/test` already makes), not for concurrent
multi-user access. See ADR 0027 for the full reasoning and the enumerated
singleton registrations this constraint rests on.

See [`server-security.md`](server-security.md) and
[`server-backup-restore.md`](server-backup-restore.md) for the backend
capabilities, and ADR 0027 for the UI-hosting addition.

## Future direction: multi-tenant server hosting and direct service injection

Two extensions to the server-hosted UI added by ADR 0027 remain future work,
not yet scheduled:

- **Multi-tenant concurrent access.** Converting the composition root's
  singleton registrations to circuit-scoped lifetimes, and replacing every
  `IndexedDb*`-backed repository with an EF Core/SQLite-backed equivalent (browser
  storage cannot be shared across users the way a database can). This needs
  its own ADR once undertaken.
- **Direct service injection instead of HTTP.** Consuming
  `IAccountService`/`ISyncService`/`IProxyService` directly from UI code when
  hosted in `HackerOs.Server`, instead of over HTTP as the WASM hosts must.
  This needs a shared client abstraction (e.g. an `IAccountClient`-shaped
  interface with an HTTP-backed implementation for the WASM hosts and a
  direct-injection implementation for the server-hosted UI) so the same UI
  component code compiles and runs across all three hosts without
  `HackerOs.Server`'s EF/SQLite-specific types leaking into shared UI code.
  This also needs its own ADR.

Consequences this implies for any component/service work done in the
meantime, so it doesn't need to be redone later:

- Nothing in `Platform`, `Infrastructure`, or app projects should assume there
  is exactly one way to reach backend capabilities (HTTP call vs. injected
  service). Prefer depending on the existing abstractions/contracts rather than
  concrete HTTP client types where practical.
- The WASM-only composition (`AddHackerOsEcosystem`) must keep working
  unmodified whether or not a server-hosted mode exists — this is additive, not
  a replacement for the standalone PWA.

## Non-goals

- This document does not change which host is "the" build target — that
  remains `OS/HackerOs.Ecosystem`.
- The server-hosted UI added by ADR 0027 does not support concurrent
  multi-user circuits — see "Future direction" above. Treat it as a
  single-operator deployment target, not a multi-tenant service, until that
  gap is closed by a future ADR.
