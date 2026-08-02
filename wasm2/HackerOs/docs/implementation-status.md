# v3 Implementation Status

## Purpose

Track implementation against `doc/wasm/wasm-v3-migration-analyse.md` without
allowing later phases to obscure unfinished platform work.

The exhaustive remaining work, maintenance rules, decisions, problems, and phase
gates are maintained in `docs/integration-task-list.md`. This status file remains
the concise implemented-state summary.

## Current milestone

Phase 1 has started with a headless contracts slice. No Blazor host, browser
storage, window manager, or application UI has been created yet. The complete
Phase 1 in-memory virtual filesystem slice now passes its assembled contract
suite; browser persistence remains a later phase. The manifest/build-profile
validator slice is now also implemented and verified end to end: JSON Schema
conformance, semantic manifest validation, source-generated serializer/
validator coverage, build-profile reference/asset scoping, deterministic
explicit discovery-list assembly, dependency-graph validation,
boot-recovery policy enforcement, and explicit regression coverage for every
build-profile validation error code all pass in the headless solution test suite.
The Phase 1 exit gate (`P1-GATE-001` through `P1-GATE-004`) is now satisfied: a
Release build with the trim analyzer enabled on every shipping project reports
zero warnings, and a new headless kernel integration test
(`Tests/HackerOs.Platform.Core.Tests/HeadlessKernelIntegrationTests.cs`) proves
boot, session login, real assembly-based discovery, app launch/command
execution, capability-checked filesystem and settings gateway use, service
shutdown, app disable, and session logout compose correctly with no Blazor or
browser dependency. Fresh verification: `dotnet test HackerOs.sln --no-restore`
completed with 378 tests passed and 0 failed.

Phase 2A has started: `P2-IDB-001` through `P2-IDB-004` are complete. ADR 0015
decides the browser support baseline (Chromium 89+, Firefox 90+, Safari 15+,
evergreen only) and the IndexedDB adapter approach (hand-written minimal
collocated JS module(s) under
`Infrastructure/HackerOs.Infrastructure.Browser/wwwroot/`, batched
transaction-oriented primitives, no third-party IndexedDB NuGet package,
C#-owned schema/migration ordering). The new
`Infrastructure/HackerOs.Infrastructure.Browser` project (with its
`Tests/HackerOs.Infrastructure.Browser.Tests` project) now declares the
concrete `hackeros` database schema version 2: 12 object stores (`users`,
`groups`, `sessions`, `settings`, `fsEntries`, `fsLinks`, `fsContent`, `catalog`,
`grants`, `audit`, `diagnostics`, `syncMetadata`) and 10 named cross-store
transaction boundaries plus the independent `GroupWrite` boundary. Its static web asset now provides declarative database
upgrades and batched transactional operations without embedding domain rules in
JavaScript. The partial `P2-IDB-005` foundation adds an internal, lazily imported
C# interop adapter that accepts only named schema transaction boundaries, sends
ordered batches, propagates cancellation, and never exposes `IJSRuntime` to app
or shared projects. `P-007` is resolved through the ADR 0015 amendment:
user/group persistence contracts use cancellation-aware `ValueTask`, nullable
lookup results represent absence, and mutations acknowledge only committed
durability. `LocalSessionService` consumes the async boundary; memory
repositories retain synchronous test conveniences without sync-over-async.
The first concrete browser repository now persists and retrieves local groups;
the migration plan is now a contiguous C#-owned chain that aborts missing paths
without implicit deletion. Settings definitions now carry their canonical
structured repository key while retaining path-based projection APIs. The
IndexedDB settings service performs idempotent atomic initialization and
revision-checked replacement through generic `addIfAbsent` and `compareAndPut`
browser primitives, while C# remains the owner of authorization, validation,
revision semantics, and rebuildable file-association indexes. Other repositories
still need persistence implementations, including the last-administrator
invariant for user/group mutations. The first `P2-IDB-008` filesystem slice adds
an aborting multi-record revision precondition, stable persisted entry/link
records, an atomic create plan, a required `fsLinks.parentId` enumeration index,
and idempotent initialization of a reserved stable root entry. Its internal
reader now resolves link-based canonical paths, performs indexed ordinal child
enumeration with batched entry loads, and detects dangling-link corruption. A
public provider exposes stat/enumeration and atomic create; created entries use
the acting user as owner and inherit the parent directory group. Atomic
permission changes and stable-link move/rename are also implemented with
revision assertions and parent metadata updates. Recursive delete asserts the
complete observed subtree and removes all links/entries atomically. Recursive
copy assigns fresh IDs while retaining immutable deduplicated content hashes.
Content reads/writes now use bounded incremental SHA-256 hashing, deduplicated
256 KiB chunks, optimistic metadata publication, and integrity verification.
The shared maintenance API performs bounded 30-day orphan collection with an
atomic reference recheck and conservative v1 compatibility. Projection routing
and complete profile seeding reuse the existing Platform Core mount and seeder;
their concrete invocation remains owned by the future host composition. The
first `P2-IDB-009` slice adds async persistent diagnostics and audit contracts.
Browser repositories redact structured properties before interop, retain
diagnostics with atomic bounded eviction, preserve general audit append-only,
and order equal timestamps by auto-incremented primary key. Persistent grants
atomically couple grant state, canonical audit, and policy revision. Catalog
reconciliation replaces authoritative build snapshots while preserving local
enablement and retaining removed apps disabled outside runtime input.
Browser storage status now reports native usage/quota and durable-retention
state, applies the approved 10-percent-or-64-MiB low-space policy, and maps
IndexedDB quota exhaustion to a recoverable non-destructive exception.
Backup format v1 now exports all stores from one consistent transaction with a
SHA-256 integrity digest. Validated restore supports explicit atomic additive
merge or full replacement; differing merge collisions abort without mutation.
`D-010` is accepted in ADR 0018: IndexedDB recovery is non-destructive first,
with export before destructive actions, validated merge/replace restore, and
explicit confirmation for replacement or reset. The renderer-independent
recovery contract now expresses typed failure states, context-sensitive boot
blocking, safe actions, export availability, stable error codes, and correlation
IDs. Browser Infrastructure classifies storage exceptions without mutation;
replacement and reset require exact `REPLACE` or `RESET` confirmation.
`P2-IDB-013` adds a host-independent Blazor WASM harness and xUnit Playwright
driver. Installed headless Chrome now executes native IndexedDB group, settings,
and filesystem contracts, including optimistic conflicts and atomic metadata
mutations. This evidence also corrected inline-key `add`/`put` calls that passed
an invalid separate null key in real browsers. `P2-IDB-014` extends that matrix
to reload persistence, interrupted migration, transaction rollback, native quota
failure and C# recovery mapping, backup replace/idempotent merge, bounded orphan
cleanup, and two-tab revision conflict. Named DOM errors are normalized before
crossing JS interop so recovery classification no longer receives an undefined
message.
See `docs/browser-storage.md`, `docs/indexeddb-filesystem.md`, and
`docs/indexeddb-operational-records.md`. Focused recovery-contract verification
and all 439 solution tests, including the real-Chrome E2E and headless window
runtime tests, pass with 0
failed. The
browser infrastructure project build confirms that `indexedDb.js` and
`storageManager.js` are emitted through the Razor static-web-assets pipeline.

`D-013` is accepted in ADR 0016. MudBlazor 9.7.0 is approved only for complex
menus, grids, tabs, forms, dialogs, and selectors behind Platform-owned wrappers.
Native Blazor/scoped CSS remains authoritative for desktop, window chrome,
taskbar, launcher layout, and simple controls; public SDK/domain contracts remain
free of MudBlazor types. The retained wrapper publishes in trimmed Release with
no warning; direct MudBlazor runtime payload is 1,363,359 raw / 251,505 Brotli
bytes. Playwright proves desktop/mobile containment, complex interactions,
validation, accessible roles/labels, screenshot output, and clean console/
network behavior. Wrapper conventions are documented for `P2-UI-003`.

The Platform window runtime is complete through `P2-WIN-014`: immutable C#
state, geometry persistence, dynamic rendering, lifecycle setup, Pointer Events,
constraints, close coordination and owner modality are implemented. Chrome
proves all resize edges, touch/pen, keyboard controls, taskbar restore, viewport
changes, focus/z-order, close and clean module loading. File-dialog work has
advanced through the reusable browser, Save/Folder components, typed selected
resources, media-aware lazy enumeration, short-lived handle issuance and
lifecycle revocation. Active requests project into authoritative owner-modal
Windows with focus return and ordinary Escape/token/close cancellation. Chrome
now renders Open, Save, and Folder flows and proves filters, visible dotfiles,
multi-selection, overwrite, folder creation, filesystem denial, modality, focus
return, cancellation, and clean console/network behavior. `P2-DLG-001` through
`P2-DLG-010` are complete.

The standalone `OS/HackerOs.Ecosystem` .NET 10 Blazor WebAssembly PWA now
provides the Phase 2 host boundary. It is included in `HackerOs.sln`, contains no
template sample pages/assets, retains only boot-critical global styles, and
builds with repository trimming analysis and warnings-as-errors. Composition
references now point only to Platform Core/Blazor and Browser Infrastructure;
there are no concrete app references in the current empty build profile. DI
composition remains under `P2-HOST-003` and `P2-HOST-004`.

## Implemented architecture

```text
HackerOs.App.Abstractions
  <- HackerOs.AppSdk

HackerOs.App.Abstractions
  <- HackerOs.App.Abstractions.Tests

HackerOs.AppSdk
  <- HackerOs.AppSdk.Tests

HackerOs.App.Abstractions
  <- HackerOs.Simulation.Abstractions
  <- HackerOs.Platform.Core
  <- HackerOs.Platform.Core.Tests
```

The abstractions projects have no UI, browser, dependency injection, or concrete
storage dependency. The App SDK adds renderer-independent terminal execution and
active-session service execution. Platform Core now proves canonical settings,
authorization, revisions, validation, audit events, and filesystem projection in
memory before IndexedDB is introduced. The headless app catalog validates package
graphs and computes deterministic activation/deactivation order. The Blazor SDK
now defines the window app lifecycle and file dialog boundary without providing a
window manager or dialog renderer yet. Simulation abstractions now define stable
filesystem entry identity, normalized names, permissions, timestamps, and
file/directory/symbolic-link metadata plus validated operation requests, stable
errors, generic results, and transaction outcomes without introducing storage
dependencies. Platform Core implements deterministic in-memory files and
transactions, mounted authorization, bounded symbolic-link traversal,
clean-profile seeding, and canonical settings projections. App abstractions now
also define immutable exact capability grants with closed path/host/port
constraints.
Policy decisions now use a deny-by-default immutable result with stable granted,
missing, revoked, constrained, and authority-denied reasons plus policy/grant
revision evidence. An in-memory grant repository evaluates those decisions
against structured resource candidates, requires Administrator/System authority
to grant or revoke, detects broader re-grants as expansions, and records a
chronological audit log. Canonical settings now have a structured scope key
(app/user, app/device, app/roaming-user, OS/admin), a deterministic projection
path factory, a scope-authorization policy that never lets app kind alone
elevate scope, a typed schema/sensitivity model, and a Linux-like `.config`
parser/serializer feeding a schema-driven document validator. Capability grant
and other OS policy changes are themselves stored as an ordinary protected
canonical settings document under `/etc/hackeros/policy.config`.

## Key decisions

- Target .NET 10 and permit roll-forward within installed .NET 10 feature bands.
- Validate manifests before loading or executing application code.
- Assign Administrator/System authority through trusted OS policy, never through
  an app's self-authored manifest.
- Keep terminal commands independent of xterm.js and any concrete renderer.
- Treat service cancellation as session shutdown, with no resume-state contract.
- Defer `WindowAppBase` until the Blazor SDK project and window lifecycle contract
  are designed together.
- Match capabilities exactly and case-sensitively; policy constraints, not
  wildcard capability names, restrict resources.
- Store settings once and expose the same canonical records through virtual
  filesystem projections.
- Reject duplicate, missing, incompatible, or cyclic app dependencies before
  activation and order valid graphs deterministically by app ID.
- Seal the framework-owned Blazor lifecycle and expose app-specific hooks so a
  derived component cannot skip framework post-render setup.
- Reject inline CSS and JavaScript in every Razor project at build time.
- Use immutable filesystem entry IDs, ordinal case-sensitive paths, separate
  metadata/content, projection-first mounts, bounded symbolic-link traversal,
  and optimistic provider transactions as defined by ADR 0008.
- Use a purpose-built C# window runtime with Platform-owned chrome, isolated
  Pointer Events interop, and a mandatory published-Release proof per ADR 0009.
- Use one strict versioned `app.manifest.json` with source-generated canonical
  serialization, rejected unknown/duplicate fields, and hashed relative assets.
  The serializer is now implemented and pinned by a golden canonical fixture.
- Use structured settings keys with Linux-like `.config` projections supporting
  `#` comments and optional sections; retain protected association JSON.
- Use monotonic PIDs, platform-owned cancellation, deterministic simulation
  clocks/random streams, and virtual hardware resource profiles per ADR 0012.
- Require first-run Release Administrator creation with no default credentials;
  keep local elevation operation-scoped and provision homes through the VFS.
- Use a small deterministic shell grammar with quoted tokens, environment
  expansion, command-owned flags, and deferred pipes/redirection/jobs/scripts.
- Grant and revoke mutations require Administrator/System acting authority;
  they never succeed from ordinary User authority regardless of app kind.
- Persist policy and other protected OS configuration through the same
  canonical settings revision, validation, and audit path as every other
  document rather than a parallel storage mechanism.
- Gate settings scope usage on explicit manifest declaration plus, for roaming
  and OS/admin scopes, an explicit granted capability and sufficient authority.

## Task list

- [x] Create the .NET 10 solution under `wasm2/HackerOs/`.
- [x] Create app abstractions and headless App SDK projects.
- [x] Define app kinds and `System > Administrator > User` ordering.
- [x] Define the initial canonical app manifest.
- [x] Validate identity, semantic versions, SDK ranges, duplicates, and app-kind
  constraints.
- [x] Define renderer-independent `TerminalAppBase`.
- [x] Define active-session `ServiceAppBase` and stop reasons.
- [x] Add focused automated tests.
- [x] Decide the initial capability identifier catalog and exact matching semantics.
- [x] Define typed core intents and app intent requests.
- [x] Define canonical virtual paths and reject traversal above root.
- [x] Define settings/filesystem projection contracts and operation context.
- [x] Implement protected in-memory settings with capability plus authority checks.
- [x] Validate file-association JSON, atomic replacement, revision conflicts, and
  change audit events.
- [x] Implement the manifest catalog and deterministic dependency graph.
- [x] Implement Semantic Version 2.0.0 precedence for dependency ranges.
- [x] Create `HackerOs.AppSdk.Blazor` and define `WindowAppBase`.
- [x] Seal framework lifecycle methods and expose app lifecycle hooks.
- [x] Define typed file-open, file-save, and folder-selection contracts.
- [x] Enforce no inline CSS/JavaScript in Razor builds.
- [x] Accept ADR 0008 for the virtual filesystem model.
- [x] Define immutable filesystem IDs, names, permissions, timestamps, metadata,
  and directory links.
- [x] Define filesystem operation requests, stable errors, generic results, and
  transaction outcomes.
- [x] Define owned binary/text streaming content contracts.
- [x] Define trusted filesystem authorization and selected-resource handles.
- [x] Define provider contracts and longest-segment mount routing.
- [x] Define normalized, bounded, mount-aware symbolic-link traversal.
- [x] Implement the deterministic in-memory repository and mounted service.
- [x] Implement idempotent clean-profile system and per-user seeding.
- [x] Mount canonical settings documents with shared revisions.
- [x] Complete the assembled filesystem contract suite.
- [x] Accept ADR 0009 for the purpose-built window runtime strategy.
- [x] Accept ADR 0010 for canonical manifest JSON and schema evolution.
- [x] Accept ADR 0011 for settings scopes, keys, paths, and `.config` syntax.
- [x] Accept ADR 0012 for deterministic process, clock, and resource simulation.
- [x] Accept ADR 0013 for local users, sessions, credentials, and elevation.
- [x] Accept ADR 0014 for the first-slice shell grammar boundary.
- [x] Define immutable exact capability grants and structured path/host/port
  constraints.
- [x] Define deny-by-default capability evaluation with explicit stable reasons.
- [x] Define policy changes as a protected revisioned settings document
  requiring Administrator/System write authority.
- [x] Implement the in-memory capability grant repository with revocation,
  expansion detection, and an audit log.
- [x] Define app/user, app/device, roaming-user, and OS/admin settings document
  keys and a deterministic projection path factory.
- [x] Define settings-scope authorization so app kind alone never elevates
  scope and roaming/OS-admin require capability plus authority.
- [x] Implement schema-driven setting declarations, sensitivity classes, and the
  `.config` parser/serializer/validator.
- [x] Prove a system app operated by a User does not gain System authority
  without an explicit audited system context.
- [x] Expand the capability catalog with process, notification, window,
  clipboard, and service capabilities; reject window-only capabilities
  declared by non-window apps.
- [x] Seed clean-profile default capability grants from a manifest's declared
  capabilities and prove System authority never implies an undeclared one.
- [x] Define user/group/session identity records and implement `ISessionService`
  login/logout/shutdown with in-memory repositories and last-administrator
  protection (ADR 0013).
- [x] Define process identity/state/resource-profile contracts and implement an
  in-memory process manager with singleton lookup, bounded stop, kill/cascade,
  and bounded history (ADR 0012).
- [x] Define and implement a deterministic simulation clock, scheduler, and
  seeded random source used by process management and resource simulation.
- [x] Implement deterministic CPU/RAM/storage/network resource simulation with
  hardware-capacity clamping and per-process seeded jitter.
- [x] Define and implement a typed in-memory event bus with ordering, disposal,
  and fault isolation.
- [x] Define and implement bounded diagnostic/audit logging with sensitive-key
  redaction.
- [x] Add cross-cutting Section 7 tests proving session, process, event, and
  resource subsystems compose end to end with no sleeps.
- [x] Expand `IAppExecutionContext` with identity, cancellation, and seven
  narrow app-scoped gateways (filesystem, settings, events, notifications,
  logging, clock, process/job); implement a trusted
  `AppExecutionContextFactory` as the sole constructor; issue/revoke
  short-lived selected-resource handles with expiry and event-driven
  auto-revocation (see [`app-execution-context.md`](app-execution-context.md)).
- [x] Implement referenced-assembly app entry-point discovery, app descriptors,
  and lifecycle orchestration for Terminal/Service/Window apps including
  singleton focus and dependency-cascade enable/disable; implement
  capability-gated typed intent dispatch (launch, open/edit/reveal file,
  execute command, show settings) and canonical, protected file-association
  resolution (explicit target, configured default, sole candidate,
  chooser-required, no-handler) (see
  [`app-intents-and-associations.md`](app-intents-and-associations.md)).
- [x] Expand the canonical app manifest with Presentation, OS compatibility,
  Localizations, Settings schema, Intents, Assets, Update, and Service-only
  AutoStart fields per migration analysis section 7.2; publish a versioned
  Draft 2020-12 JSON Schema (`Schema/manifest.schema.v1.json`, embedded as a
  resource) enforcing unknown-field rejection and app-kind-specific structural
  rules, with one valid fixture per app kind and nine invalid fixtures under
  `Schema/Fixtures/` (see [`build-profile.md`](build-profile.md)).
- [x] Define the first build-profile contract for selected packages, default
  enables, grants, associations, locales, themes, and optional server features
  with serializer and validator coverage.
- [x] Add explicit invalid-fixture regression coverage for every build-profile
  validation error code (`P1-BLD-008`).
- [x] Enable the trim analyzer on every shipping project and resolve/justify
  every reflection warning at the discovery/instantiation boundary
  (`P1-GATE-004`).
- [x] Add a headless kernel integration test proving boot, session, discovery,
  app launch, command execution, capability-checked filesystem/settings
  gateway use, service shutdown, disable, and logout compose end to end
  (`P1-GATE-003`).
- [ ] Implement the platform window runtime and modal file dialog renderer.
- [ ] Scaffold the PWA host only after the headless Phase 1 gate passes.

## Validation

The implementation is validated by solution build plus 344 focused tests:

- 75 manifest, SemVer, authority, capability, grant, policy evaluation, intent,
  build-profile, and path tests, including 12 manifest JSON Schema conformance
  tests (one valid fixture per app kind, nine structural-violation fixtures)
  and explicit invalid-fixture regression cases for every build-profile
  validation error code.
- 4 App SDK lifecycle tests.
- 10 Blazor App SDK lifecycle and dialog tests.
- 255 Platform Core tests: catalog, settings authorization, filesystem
  projection, metadata, operation, streaming, authorization, routing,
  traversal, repository, mounted-service/seed/projection/assembled-contract,
  settings-scope, `.config` format, schema, protected policy document,
  capability grant repository/expansion/audit, clean-profile default grant
  seeder, Section 7 (session/process/clock/event/diagnostics/resource-
  simulation), Section 7.1 (app execution context and scoped gateways),
  Section 8 (entry-point discovery, lifecycle orchestration, file-association
  resolution, capability-gated intent dispatch), and the Phase 1 exit-gate
  headless kernel integration test.

Validation command:

```powershell
dotnet test HackerOs.sln --no-restore
```

Warnings are treated as errors for every project under the solution directory,
and the trim analyzer runs on every shipping (non-test) project so trimming/AOT
reflection issues surface as ordinary build errors well before any host publish
step exists.