# Hacker Simulator

A simulator game that recreates the experience of being a hacker, featuring multiple applications including a terminal, code editor, and system monitoring tools.

View the current working version [(Here)](https://werddomain.github.io/HackerSimulator/)

## Project Overview

Hacker Simulator is an interactive web-based application that simulates various hacking scenarios and computer environments. Users can interact with multiple applications including a terminal, text editor, code editor, system monitor, file explorer, and more.

## Getting Started

### Prerequisites

- Node.js (v12 or higher)
- npm (v6 or higher)

### Installation

1. Clone the repository
2. Install dependencies:
   ```
   npm install
   ```

### Development

```bash
npm run dev         # Start the dev server with hot reloading
npm run tsc:debug   # Compile all TS files individually with source maps
```

### Build

```bash
npm run build:prod  # Creates app.js and app.min.js in the dist folder
```

## Project Structure

- `/src`: Main source code
  - `/apps`: Individual applications (terminal, code editor, etc.)
  - `/commands`: Command processing for terminal
  - `/core`: Core functionality and application management
  - `/missions`: Game missions and scenarios
  - `/server`: Server-side code
  - `/websites`: Web content for in-game browsing

## Features

- Multiple simulated applications:
  - Terminal with command-line interface
  - Code editor with syntax highlighting
  - Text editor for managing documents
  - File explorer for navigating the virtual file system
  - System monitor to track resources
  - Browser for in-game web content
  - Calculator for basic calculations

## Contributing

Please read our [contribution guidelines](CONTRIBUTING.md) before submitting pull requests.

## License

This project is licensed under the terms found in [LICENSE.md](LICENSE.md).


## Blazor WebAssembly Version

A new Blazor WebAssembly implementation targeting .NET 9 is located in [`src/wasm`](src/wasm). This version aims to recreate the JavaScript functionality of the original project purely in C#.
This implementation includes a basic shell capable of launching components like the terminal, calculator, and file explorer using C# processes.
