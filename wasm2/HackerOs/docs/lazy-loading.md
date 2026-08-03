# HackerOS Build-Known Lazy Loading Architecture

## Overview

To optimize initial WebAssembly boot time and minimize initial PWA download payload, HackerOS implements **Build-Known Lazy Assembly Loading**.
Core platform framework assemblies load eagerly during boot, while non-critical application assemblies load on-demand when launched.

---

## 1. Assembly Classification

| Assembly | Loading Strategy | Justification |
|---|---|---|
| `HackerOs.App.Abstractions.dll` | **Eager** | Required for type system, manifests, and capability checking. |
| `HackerOs.AppSdk.dll` / `HackerOs.AppSdk.Blazor.dll` | **Eager** | Base execution context & component contracts. |
| `HackerOs.Platform.Core.dll` / `HackerOs.Platform.Blazor.dll` | **Eager** | Desktop shell, process manager, VFS router, event bus. |
| `HackerOs.Ecosystem.dll` | **Eager** | Host composition root & PWA shell. |
| `HackerOs.Apps.Terminal.dll` | **Eager** | Primary OS entry point and shell command handler. |
| `HackerOs.Apps.FileExplorer.dll` | **Lazy** | Desktop file manager application loaded on first intent. |
| `HackerOs.Apps.TextEditor.dll` | **Lazy** | Desktop text editor window app loaded on file open intent. |
| `HackerOs.Samples.WindowApp.dll` | **Lazy** | Optional sample window application. |
| `HackerOs.Samples.ServiceApp.dll` | **Eager** | AutoStart background ticker service. |

---

## 2. MSBuild Configuration (`HackerOs.Ecosystem.csproj`)

Lazy assemblies are declared in the WebAssembly host project file:

```xml
<ItemGroup>
  <BlazorWebAssemblyLazyLoad Include="HackerOs.Apps.FileExplorer.dll" />
  <BlazorWebAssemblyLazyLoad Include="HackerOs.Apps.TextEditor.dll" />
  <BlazorWebAssemblyLazyLoad Include="HackerOs.Samples.WindowApp.dll" />
</ItemGroup>
```

---

## 3. Dynamic Runtime Loading & Discovery

When an app launch intent is dispatched (`LaunchAppIntent` / `OpenFileIntent`):
1. `AppLifecycleOrchestrator` checks if the application assembly is loaded.
2. If lazy, `LazyAssemblyLoader.LoadAssembliesAsync(["HackerOs.Apps.TextEditor.dll"])` fetches the assembly asynchronously.
3. Upon completion, `AppEntryPointDiscovery` resolves the descriptor and instantiates the Blazor component or process.

---

## 4. Offline Failure Handling & Recovery UI

If an assembly fails to load (e.g. network timeout or offline without cache):
- The orchestrator displays a non-modal error toast or fallback error dialog: `"Failed to load application 'Text Editor'. Please check network connection."`
- The operating system shell remains 100% active and functional.
- Cached assemblies continue operating normally.
