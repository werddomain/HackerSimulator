# Settings Service

This service stores configuration for applications using the virtual file system.  
Settings are saved in JSON files with a `.config` extension. Each application has
machine level settings under `/etc/{app}/` and user settings under
`/home/{user}/.config/`. User settings override machine settings.

## Architecture
- **SettingsService** – Reads and writes configuration files using
  `FileSystemService`.
- Automatically creates directories if they do not exist.
- Machine settings require the user to belong to the admin group.

## Usage
```
var settings = await Settings.Load("terminalapp");
var userOnly = await Settings.LoadUser("terminalapp");
await Settings.SaveUser("terminalapp", userOnly);
```

## Key Decisions
- Linux like directory structure is enforced when the application starts.
- `/usr/bin` is used to represent installed apps.
- Startup service ensures `/etc`, `/usr/bin` and user config directories exist.

## Task List
- [x] Implement `SettingsService` for file-based configs.
- [x] Ensure Linux style directories during startup.
- [x] Create modern `SettingsApp` for editing configs.
- [x] Document the service.
