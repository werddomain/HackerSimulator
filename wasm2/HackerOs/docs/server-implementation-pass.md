# Server Implementation Roadmap

## Purpose

This is the standing checklist for what remains to connect HackerOS's browser-local
runtime to the optional `Server/HackerOs.Server` backend. Read this first before
starting any new server-integration work — it tracks what has shipped and exactly
what the next pass is, so scope doesn't need to be re-derived from scratch each
time. **Update this doc at the end of every pass that touches server integration**:
move the finished item into "Done" with a pointer to the ADR/commit that did it,
and adjust the next pass's description if reality diverged from the plan. This
is a living roadmap, not a one-time status snapshot — treat edits to it as part of
"done" for any server-integration change, the same way `AGENTS.md`'s
documentation-maintenance rule already requires for other docs under
`wasm2/HackerOs/docs/`.

Guiding constraint carried through every pass below: browser-local IndexedDB
storage remains the source of truth. The optional server is a bridge (real
network access via proxy) and a backup/reconciliation target (sync across an
offline-then-reconnected session, or across two computers) — never a
requirement for normal operation. See `docs/hosting-model.md`.

## Done

- **Third UI host** (ADR 0027, `docs/adr/0027-server-hosted-blazor-ui.md`) —
  `Server/HackerOs.Server` hosts the same `HackerOs.Ecosystem.App` component tree
  as the two WASM hosts, via Interactive Server render mode, single-tenant/
  single-active-circuit only for this phase. Existing sync/identity/proxy API
  endpoints unchanged.
- **Client-side server connection + proxy bridge** (ADR 0028,
  `docs/adr/0028-client-side-server-connection.md`) — per-device opt-in
  connection to the optional server (`IServerConnectionRepository`,
  `IAccountClient`, `IProxyClient`, `IServerConnectionService`, in
  `Platform/HackerOs.Platform.Core/ServerConnection/`, browser-independent by
  construction), a Settings UI panel to connect/disconnect, and a real-network
  fallback wired into `ping` only (an unknown host now attempts an HTTP HEAD
  proxy round-trip through the connected server before reporting unreachable).
  **`curl`/`nmap`/`cat` were named as candidates but not wired in this pass** —
  scoped down deliberately rather than spread thin; see Pass N+1a below.
- **Settings domain sync** (ADR 0029, `docs/adr/0029-settings-sync.md`) — the
  first real sync client for any domain. Explicit per-document `SyncEligible`
  opt-in (only `AppearanceSettingsDocuments` opted in so far), deterministic
  `RecordId` derivation, two new domain-agnostic IndexedDB stores
  (`syncCursors`/`syncRecordState`, schema v4) reused by every future sync
  pass, `ISyncClient`, `ISettingsSyncService`, and a "Sync now" button plus
  an on-connect trigger in the Settings panel. Conflicts resolve automatically
  in the server's favor (not surfaced to the user) — an explicit, recorded
  simplification for this low-stakes first domain, not a general policy.
- **FileSystem domain sync** (ADR 0030, `docs/adr/0030-filesystem-sync.md`) —
  push/pull of every entry under the active user's `/home/{userId}` (recursive
  walk, not the whole filesystem). Fixed a real server-side gap along the way:
  `ContentBlobService.GetChunkAsync` was a stub returning zero bytes, so
  content download was completely broken — now content-addressed and
  session-free (`GET /api/sync/content/download/{contentHash}/chunks/{index}`),
  covered by the first server-side content-blob tests
  (`Tests/HackerOs.Server.Tests/ContentBlobServiceTests.cs`). New
  `IContentTransferClient`/`HttpContentTransferClient` (chunked upload/download,
  browser-independent, mirrors `ISyncClient`'s placement) and
  `IFileSystemSyncService`/`FileSystemSyncService` (the domain adapter —
  metadata via `SyncRecordEnvelope`, content via the separate chunked
  protocol, tied together only by `ContentHash`). Unlike Settings, a push
  conflict is **never** auto-resolved in either direction (ADR 0030 Decision
  5) — and a pull-side guard added during implementation prevents the
  matching gap: without it, a pull immediately following a conflicted push
  would have silently reapplied the server's copy over the very edit that
  just failed to push. Wired into the same "Sync now" button and a
  "N files have unresolved sync conflicts" indicator in the Settings panel.
  Known, deliberate simplifications carried forward rather than solved here:
  no deletion propagation (a file removed on one device isn't removed on
  another), and pull always re-downloads file content rather than checking
  for a local dedup hit first (the plan's "check local `fsContent` by hash"
  optimization needs an abstraction that doesn't exist yet — correctness was
  prioritized over that optimization for this pass).

## Pass N+1a: Wire `curl -I`/`nmap`/`cat` into the same proxy bridge

Extend the pattern already proven end-to-end by `ping` (see `PingCommand.cs`'s
`PingRealHostAsync`) to the other three commands ADR 0023/0028 originally
named. Concretely:
- `curl -I` (headers-only): fully achievable today — `IProxyClient.ExecuteHttpRequestAsync`
  already returns real status/headers, no body needed.
- Normal `curl` (full body) and `cat` (reading a URL as content): blocked on
  the same server-side gap noted below (`ProxyHttpResponse` is metadata-only).
- `nmap`: port-scanning doesn't map onto a single HTTP proxy call at all;
  needs either a new non-HTTP proxy contract shape or stays simulation-only
  indefinitely — see the open question below.

## Pass N+3: Sync — Grants domain

Capability grants are server-authoritative per ADR 0025 (`ClientWins`/`Merge`
are not applicable to this domain; tombstones are blocked). Simplest conflict
story of the five remaining domains, but highest security sensitivity — needs
explicit test coverage proving a compromised/buggy client can never widen its
own grants via a crafted push. Depends on Pass N+1's scaffolding.

## Pass N+4: Sync — AppCatalog + FileAssociations domains

The remaining two `SyncDomain` values. Smallest scope of the five; do together
in one pass once N+1 through N+3 have proven the pattern across three
meaningfully different domains (simple document, chunked content, server-
authoritative).

## Pass N+5: Direct service injection for the server-hosted host

ADR 0027's other explicitly deferred item. Once `IAccountClient`/`IProxyClient`
(and the `ISyncClient` sibling this roadmap implies once Pass N+1 exists) are
proven over HTTP, give `Server/HackerOs.Server`'s own circuits a
direct-injection implementation of the same interfaces that calls
`IAccountService`/`ISyncService`/`IProxyService` in-process instead of over
HTTP — the interfaces were deliberately kept ASP.NET-free in ADR 0028 so this
doesn't require redesigning them. Needs its own ADR for the composition-root
wiring (how a request picks HTTP vs. direct implementation) before
implementation, per ADR 0027 Consequences.

## Pass N+6: Multi-tenant concurrent circuits

ADR 0027's other explicitly deferred item — converting the shared composition
root's per-process singletons to per-circuit scoped lifetimes so
`Server/HackerOs.Server` could serve multiple simultaneous browser users with
correct isolation. **Only revisit this if the product actually needs live
concurrent server-hosted UI sessions.** Per the direction set in ADR 0028,
browser-local IndexedDB remains the permanent source of truth and the
proxy/sync bridge model is the intended path for offline-reconnect and
multi-device use — this pass may never be needed. If it is, note (from prior
research) that a full server-side EF/SQLite reimplementation of the entire
IndexedDB-equivalent schema is *not* required for correctness — each Blazor
Server circuit already gets its own correctly-scoped `IJSRuntime` pointing at
that circuit's own connected browser's IndexedDB, so a lifetime-only
conversion (`AddSingleton` → `AddScoped` for the ~20 per-user-state services in
`AddHackerOsEcosystem`) is sufficient for isolation; EF-backed storage would
only be needed for a different, unrelated goal (server-persisted state
independent of any one browser).

## Open questions carried forward

- **Server-side gap found during ADR 0028 implementation**: `POST /api/proxy/http`
  (`ProxyEndpoints.ExecuteHttpAsync`) only ever returns `ProxyHttpResponse`
  metadata (status, headers, a content hash) — it does not stream the actual
  request or response body. `ProxyHttpRequest`/`ProxyHttpResponse`'s own doc
  comments in `ProxyContracts.cs` already say "Body is transmitted as a
  separate binary payload; this contract carries metadata only," but no
  endpoint for that separate binary payload exists yet. This blocks any
  command needing fetched content (`curl` without `-I`, `cat`) from working
  over the real proxy until a body-transfer endpoint is added server-side —
  needs its own small ADR/design pass (chunked, per the existing
  `ContentTransferContracts.cs` precedent from sync, or a simpler direct
  streaming response).
- Does `ping`/`nmap`'s "real" mode need a TCP/UDP proxy call shape beyond
  `IProxyService.ExecuteHttpRequestAsync`'s HTTP-only contract? If the existing
  proxy contract can't support it, either extend `IProxyService` (server-side
  change, needs its own security review per the SSRF/redirect-limit work
  already tracked under `P5-PROXY-*`) or scope those two commands' "real" mode
  down to what HTTP proxying actually supports.
- ~~Does real network access need its own capability ID...~~ **Resolved**:
  `AppCapabilities.NetworkRealAccess` (`network.real.access`) was added during
  ADR 0028 implementation and is declared by `ping`'s manifest.
