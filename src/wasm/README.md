# Hacker Simulator Blazor WebAssembly

This directory contains a Blazor WebAssembly version of the Hacker Simulator project. The goal is to reimplement all of the existing JavaScript-based functionality using .NET 9 and C# without any custom JavaScript.

This project now includes a simple shell that can launch calculator, terminal and file explorer components.

To build and run the application (once .NET 9 SDK is installed):

```bash
cd src/wasm/HackerSimulator.Wasm
dotnet run
```

This will launch the WebAssembly app and serve it locally.
