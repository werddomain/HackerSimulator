# HackerOs.Taskbar.Blazor

A taskbar Razor component (`Taskbar`) driven entirely by host-supplied
contracts instead of a fixed set of injected services: `ITaskbarWindowSource`,
`ITaskbarCommandDispatcher`, `ITaskbarLauncher`, `ITaskbarStatusSource`,
`ITaskbarClockPanelSource`, `ITaskbarSessionCommands`. Every contract
parameter is optional — the corresponding taskbar region (launcher trigger,
clock, session control) simply does not render when its contract isn't
supplied.

The clock is the taskbar's one extensible surface: `ITaskbarClockPanelSource`
controls whether its panel is open, and the `ClockPanelContent` `RenderFragment`
parameter supplies what's inside it. This package owns only the trigger button
and the panel's anchoring/positioning — it never inspects `ClockPanelContent`,
so the panel's actual design (notifications, calendar, a platform-mode toggle,
anything else) lives entirely in the host application, outside this package.

## Usage

```razor
<Taskbar WindowSource="_windowSource"
         Commands="_commands"
         Launcher="_launcher"
         StatusSource="_status"
         ClockPanelSource="_clockPanel"
         ClockPanelContent="@ClockPanelFragment"
         Options="_options" />
```

This example omits `SessionCommands` — a host with no session concept just
doesn't provide it, and the taskbar renders without that region. `TaskbarOptions`
carries only the labels a host would otherwise be forced to see hardcoded (the
launcher trigger's mark and text); which taskbar zones appear is controlled
entirely by which contracts you supply.

Pair this package with `HackerOs.Windowing.Blazor` for the window chrome that
these contracts typically sit alongside. See `HackerOs.Windowing.SampleHost`
in the source repository for a complete, minimal host that uses only these
packages, including sample `ITaskbarWindowSource`/`ITaskbarCommandDispatcher`/
`ITaskbarLauncher`/`ITaskbarStatusSource` implementations.
