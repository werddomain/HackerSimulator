# HackerOS v3

This directory contains the clean Blazor WebAssembly v3 migration. The legacy
TypeScript implementation in `src/` remains a behavioral reference and is not a
dependency of these projects.

## Current projects

- `Shared/HackerOs.App.Abstractions`: stable app kinds, manifests, validation,
  and authority contracts.
- `Shared/HackerOs.AppSdk`: headless terminal and session-service base classes.
- `Shared/HackerOs.AppSdk.Blazor`: window app base and typed virtual file dialogs.
- `Shared/HackerOs.Simulation.Abstractions`: settings and virtual filesystem
    projection contracts.
- `Platform/HackerOs.Platform.Core`: headless app catalog, policy, and canonical
    settings behavior.
- `Tests/HackerOs.App.Abstractions.Tests`: manifest and authority contract tests.
- `Tests/HackerOs.AppSdk.Tests`: terminal and service lifecycle tests.
- `Tests/HackerOs.AppSdk.Blazor.Tests`: sealed window lifecycle and dialog tests.
- `Tests/HackerOs.Platform.Core.Tests`: settings authorization and projection
    tests.

## Build

```powershell
dotnet build HackerOs.sln
dotnet test HackerOs.sln
```

The solution targets .NET 10. New implementation code must remain under this
directory. Blazor components will use collocated `.razor`, `.razor.css`, and
`.razor.js` assets with no inline CSS or JavaScript.

## Documentation

[`docs/README.md`](docs/README.md) is the documentation index. Start with
[`docs/ecosystem-rebuild-plan.md`](docs/ecosystem-rebuild-plan.md), the
validated plan (2026-10-10, in French) that now sets the ecosystem's direction.
Documents written before that plan are archived in `docs/old/` and indexed by
[`docs/old/README.md`](docs/old/README.md); they remain useful technical
background:

- `docs/old/implementation-status.md`: migration progress before the rebuild plan.
- `docs/old/integration-task-list.md`: the original integration plan.
- `docs/old/app-contracts.md`: current manifest and lifecycle contracts.
- `docs/old/settings-system.md`: current settings and filesystem projection.
- `docs/old/app-catalog.md`: deterministic manifest and dependency catalog.
- `docs/old/blazor-app-sdk.md`: window lifecycle, dialogs, and scoped assets.
- `docs/old/hosting-model.md`: the three host projects (Ecosystem, test debug
    harness, optional Server) and the future Server-as-host direction.
- `docs/adr/`: accepted architecture decisions.