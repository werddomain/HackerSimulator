# Hacker Simulator Blazor WebAssembly

This directory contains a Blazor WebAssembly version of the Hacker Simulator project. The goal is to reimplement all of the existing JavaScript-based functionality using .NET 9 and C# without any custom JavaScript.

Core services including a simple kernel, shell, and in-memory file system are provided. Applications are implemented as processes that the shell can launch via dependency injection.


Additional OS emulation layers are being ported from the TypeScript version. A basic network stack and DNS server have been implemented in C# under `Core/NetworkService.cs`.

The WebAssembly frontend uses [MudBlazor](https://mudblazor.com/) components and several icon fonts including FontAwesome, Google Material Icons, Lucide and Iconscout.


To build and run the application (once .NET 9 SDK is installed):

```bash
cd src/wasm/HackerSimulator.Wasm
dotnet run
```

This will launch the WebAssembly app and serve it locally.
