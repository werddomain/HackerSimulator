# HackerOs.Windowing.Blazor

Razor components that render a `HackerOs.Windowing.Core` `WindowRuntime`:
`DesktopArea` (the workspace surface), `WindowHost` (one window's chrome
frame), and `WindowChrome` (title bar, minimize/maximize/close, and the
pointer-driven move/resize gesture). No MudBlazor dependency.

## Usage

```razor
<DesktopArea Windows="Runtime.Windows"
             OnFocus="HandleFocus"
             OnMinimize="HandleMinimize"
             OnToggleMaximize="HandleToggleMaximize"
             OnClose="HandleClose"
             OnGesture="HandleGesture">
    <WindowContent Context="window">
        @window.Content
    </WindowContent>
</DesktopArea>
```

`window.Content` is the `RenderFragment` the host supplied when it created
that window's `WindowRuntimeState` — this package never needs to know what
a window's content actually is.

Pair this package with `HackerOs.Taskbar.Blazor` for a taskbar over the same
runtime. See `HackerOs.Windowing.SampleHost` in the source repository for a
complete, minimal host that uses only these packages.
