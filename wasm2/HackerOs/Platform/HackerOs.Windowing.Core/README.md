# HackerOs.Windowing.Core

A headless, renderer-independent window manager engine: window identity,
geometry, focus, z-order, modality, and lifecycle transitions, applied as
deterministic commands and observed as deterministic events.

This package has no dependency on Blazor rendering, a browser, or any
particular host application model — only the `RenderFragment` delegate type
(for window content) is referenced, via `Microsoft.AspNetCore.Components`.

## Usage

```csharp
WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));

WindowRuntimeState state = new(
    WindowId.FromGuid(Guid.NewGuid()),
    appId: "my-app",
    ownerInstanceId: WindowOwnerId.FromGuid(Guid.NewGuid()),
    title: "My Window",
    iconAssetPath: null,
    new WindowBounds(60, 50, 420, 280),
    restoreBounds: null,
    zOrder: 0,
    WindowVisualState.Normal,
    new WindowConstraints(isResizable: true, minWidth: 260, minHeight: 180));

runtime.Apply(new CreateWindowCommand(state));
```

Pair this package with `HackerOs.Windowing.Blazor` for the Blazor
components that render a `WindowRuntime`'s state, and
`HackerOs.Taskbar.Blazor` for a taskbar over the same runtime.

See `HackerOs.Windowing.SampleHost` in the source repository for a complete,
minimal host that uses only these three packages.
