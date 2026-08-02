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

- `docs/integration-task-list.md`: exhaustive remaining integration plan and
    execution source of truth.
- `docs/implementation-status.md`: migration progress and next gate.
- `docs/app-contracts.md`: implemented manifest and lifecycle contracts.
- `docs/settings-system.md`: canonical settings and filesystem projection.
- `docs/app-catalog.md`: deterministic manifest and dependency catalog.
- `docs/blazor-app-sdk.md`: window lifecycle, dialogs, and scoped assets.
- `docs/adr/`: accepted architecture decisions.