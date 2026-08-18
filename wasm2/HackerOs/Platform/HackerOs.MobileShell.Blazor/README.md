# HackerOs.MobileShell.Blazor

Mobile shell chrome Razor components, driven by host-supplied contracts the
same way `HackerOs.Taskbar.Blazor` drives the Desktop taskbar. Mobile has no
taskbar (docs/mobile-interface-platform-plan.md §7.2), so this is a sibling
package rather than a modification of `HackerOs.Taskbar.Blazor`.

## `MobileSystemNavigationBar`

An Android-inspired bottom bar with three buttons — Triangle/Back,
Circle/Home, Square/Recent — each at least 44×44 CSS pixels and respecting
`env(safe-area-inset-*)`. The bar owns only the trigger buttons; what
Back/Home/Recent actually do is entirely up to the host's
`IMobileNavigationCommands` implementation.

```razor
<MobileSystemNavigationBar Commands="_navigationCommands" Options="_options" />
```

Unlike `HackerOs.Taskbar.Blazor`'s optional contract parameters, `Commands` is
required — the Mobile shell has no floating-chrome equivalent for these three
actions, so a bar with nothing to dispatch to is a host configuration error,
not a legitimately reduced state.

`MobileSystemNavigationBarOptions` carries only the three buttons' accessible
labels, mirroring `TaskbarOptions`.

This is `MOB-010` from the mobile platform plan. It does not yet implement
Back's ordered semantics (dialog → app handler → nav stack → Home, plan §7.3)
or the Recent surface itself (`MOB-011`/`MOB-012`) — those are a host
concern the dispatched `IMobileNavigationCommands` methods fulfill.
