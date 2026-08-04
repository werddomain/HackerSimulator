# HackerOS WebAssembly Debug Host Setup

## Overview

This document describes how WebAssembly debugging, Razor Class Library static web assets, MudBlazor integration, and browser stability optimizations are configured using `wasm2/HackerOs/test/test/test.csproj` as the ASP.NET Core WebAssembly debug host for `HackerOs.Ecosystem.csproj`.

## Architecture & Configuration

1. **Host Project (`test.csproj`):**
   - Uses `Microsoft.NET.Sdk.Web` and references `Microsoft.AspNetCore.Components.WebAssembly.Server`.
   - Calls `app.UseWebAssemblyDebugging()` in development mode.
   - Maps razor components with `AddInteractiveWebAssemblyRenderMode()`.
   - Maps static assets via `app.MapStaticAssets()`.
   - References `HackerOs.Ecosystem.csproj`.

2. **Client Project (`HackerOs.Ecosystem.csproj`):**
   - Uses `Microsoft.NET.Sdk.BlazorWebAssembly`.
   - Registers all required client DI services (`AddHackerOsEcosystem`, `AddMudServices`, `IBuildKnownAssemblyTransport`, `BuildKnownAssemblyLoaderRegistry`, `BuildKnownLazyAppDescriptorRegistry`) in `Program.cs` and `EcosystemServiceCollectionExtensions.cs`.
   - Exposes static web assets under `_content/HackerOs.Ecosystem/` (including `css/app.css`, `manifest.webmanifest`, and icons).

3. **Static Web Assets, MudBlazor & Scoped Bundles (`test/Components/App.razor`):**
   - **MudBlazor UI Library:** Registers `services.AddMudServices()` in DI and links MudBlazor CSS (`_content/MudBlazor/MudBlazor.min.css`) and JS (`_content/MudBlazor/MudBlazor.min.js`).
   - **Platform UI Scoped CSS Bundle:** Links `_content/HackerOs.Platform.Blazor/HackerOs.Platform.Blazor.bundle.scp.css` for desktop shell, window chrome, and taskbar styles.
   - **HackerOS Global CSS:** Links `_content/HackerOs.Ecosystem/css/app.css` for HSL theme tokens and reset styles.
   - **Ecosystem Scoped CSS:** Links `_content/HackerOs.Ecosystem/HackerOs.Ecosystem.styles.css`.
   - **Host Styles:** Links `@Assets["app.css"]` and `@Assets["test.styles.css"]`.
   - **Dynamic Component Tags:** Supports dynamic style and script tag injection per Razor component via `<HeadOutlet />` and `<HeadContent>`.

4. **Remediation for `STATUS_ACCESS_VIOLATION` in Local Development:**
   - Automatically bypasses Service Worker registration on `localhost` / `127.0.0.1` and unregisters any stale Service Worker instances on boot in dev mode.

## Task List

- [x] Configure `HackerOs.Ecosystem/Program.cs` client DI service registration for WebAssembly runtime execution.
- [x] Register `AddMudServices()` in `EcosystemServiceCollectionExtensions.cs`.
- [x] Link MudBlazor CSS (`_content/MudBlazor/MudBlazor.min.css`) and JS (`_content/MudBlazor/MudBlazor.min.js`) in `App.razor` and `index.html`.
- [x] Link `HackerOs.Platform.Blazor` scoped CSS bundle (`_content/HackerOs.Platform.Blazor/HackerOs.Platform.Blazor.bundle.scp.css`) in `App.razor` and `index.html`.
- [x] Link `HackerOs.Ecosystem` static web assets and CSS design tokens in `App.razor`.
- [x] Enable component-level script and style tag support via `<HeadOutlet />`.
- [x] Prevent Chromium `STATUS_ACCESS_VIOLATION` crashes by unregistering Service Workers on `localhost` during development.
