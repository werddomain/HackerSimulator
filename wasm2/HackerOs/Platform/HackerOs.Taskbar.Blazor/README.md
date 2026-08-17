# HackerOs.Taskbar.Blazor

A taskbar Razor component (`Taskbar`) driven entirely by host-supplied
contracts instead of a fixed set of injected services: `ITaskbarWindowSource`,
`ITaskbarCommandDispatcher`, `ITaskbarLauncher`, `ITaskbarStatusSource`,
`ITaskbarNotificationSource`, `ITaskbarSessionCommands`. Every contract
parameter is optional — the corresponding taskbar region (launcher trigger,
notification bell, clock, session control) simply does not render when its
contract isn't supplied.

## Usage

```razor
<Taskbar WindowSource="_windowSource"
         Commands="_commands"
         Launcher="_launcher"
         StatusSource="_status"
         Options="_options" />
```

This example omits `NotificationSource` and `SessionCommands` — a host with
no notification center or session concept just doesn't provide them, and the
taskbar renders without those regions. `TaskbarOptions` carries only the
labels a host would otherwise be forced to see hardcoded (the launcher
trigger's mark and text); which taskbar zones appear is controlled entirely
by which contracts you supply.

Pair this package with `HackerOs.Windowing.Blazor` for the window chrome that
these contracts typically sit alongside. See `HackerOs.Windowing.SampleHost`
in the source repository for a complete, minimal host that uses only these
packages, including sample `ITaskbarWindowSource`/`ITaskbarCommandDispatcher`/
`ITaskbarLauncher`/`ITaskbarStatusSource` implementations.
