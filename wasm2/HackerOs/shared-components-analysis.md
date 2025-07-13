# HackerOS Shared Components Analysis

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `worksheet.md` FOR PROJECT GUIDELINES**

## Overview

This document provides a detailed analysis of shared components and utilities across HackerOS applications. Identifying these common patterns helps create reusable abstractions for the migration process, reducing duplication and ensuring consistent behavior across the system.

## Core Shared Components

### 1. File System Interaction

#### Component Description
File system operations are among the most common patterns across applications. Window applications like NotepadApp and TextEditor, services like FileWatchService, and most command-line applications interact with the virtual file system.

#### Implementation Details
```csharp
// Common pattern found in file-related applications
private readonly IVirtualFileSystem _fileSystem;

public async Task<string> ReadFileContentAsync(string path)
{
    if (!await _fileSystem.FileExistsAsync(path))
    {
        throw new FileNotFoundException($"File not found: {path}");
    }
    
    using var stream = await _fileSystem.OpenReadAsync(path);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
}

public async Task WriteFileContentAsync(string path, string content)
{
    using var stream = await _fileSystem.OpenWriteAsync(path);
    using var writer = new StreamWriter(stream);
    await writer.WriteAsync(content);
}
```

#### Abstraction Opportunity
Create a `FileOperationsHelper` class that standardizes these operations and provides consistent error handling, permission checking, and async patterns.

```csharp
public class FileOperationsHelper
{
    private readonly IVirtualFileSystem _fileSystem;
    private readonly IUserManager _userManager;
    private readonly ILogger _logger;

    public FileOperationsHelper(
        IVirtualFileSystem fileSystem, 
        IUserManager userManager,
        ILogger logger)
    {
        _fileSystem = fileSystem;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<string> ReadFileContentAsync(string path, bool checkPermissions = true)
    {
        try
        {
            if (checkPermissions && !await HasReadPermissionAsync(path))
            {
                throw new UnauthorizedAccessException($"No read permission for: {path}");
            }

            if (!await _fileSystem.FileExistsAsync(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            
            using var stream = await _fileSystem.OpenReadAsync(path);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading file: {path}");
            throw;
        }
    }

    // Additional methods for write, append, delete, etc.
    // Permission checking methods
}
```

### 2. Settings Management

#### Component Description
Many applications need to load, save, and apply settings. This pattern is seen in SettingsService, DesktopSettingsService, and various applications with configuration options.

#### Implementation Details
```csharp
// Common pattern found in applications with settings
private readonly string _settingsPath;
private Settings _settings;

public async Task LoadSettingsAsync()
{
    if (await _fileSystem.FileExistsAsync(_settingsPath))
    {
        var content = await ReadFileContentAsync(_settingsPath);
        _settings = JsonSerializer.Deserialize<Settings>(content);
    }
    else
    {
        _settings = new Settings(); // Default settings
    }
}

public async Task SaveSettingsAsync()
{
    var content = JsonSerializer.Serialize(_settings);
    await WriteFileContentAsync(_settingsPath, content);
}
```

#### Abstraction Opportunity
Create a generic `SettingsManager<T>` class that handles loading, saving, and change notification for settings.

```csharp
public class SettingsManager<T> where T : class, new()
{
    private readonly IVirtualFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly string _settingsPath;
    private T _settings;

    public event EventHandler<T> SettingsChanged;

    public SettingsManager(
        IVirtualFileSystem fileSystem,
        ILogger logger,
        string settingsPath)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _settingsPath = settingsPath;
        _settings = new T();
    }

    public T Settings => _settings;

    public async Task LoadAsync()
    {
        try
        {
            if (await _fileSystem.FileExistsAsync(_settingsPath))
            {
                using var stream = await _fileSystem.OpenReadAsync(_settingsPath);
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                _settings = JsonSerializer.Deserialize<T>(content) ?? new T();
            }
            else
            {
                _settings = new T();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading settings from {_settingsPath}");
            _settings = new T();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!await _fileSystem.DirectoryExistsAsync(directory))
            {
                await _fileSystem.CreateDirectoryAsync(directory);
            }

            using var stream = await _fileSystem.OpenWriteAsync(_settingsPath);
            using var writer = new StreamWriter(stream);
            var content = JsonSerializer.Serialize(_settings);
            await writer.WriteAsync(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving settings to {_settingsPath}");
        }
    }

    public void UpdateSettings(Action<T> updateAction)
    {
        updateAction(_settings);
        SettingsChanged?.Invoke(this, _settings);
    }
}
```

### 3. Command Argument Parsing

#### Component Description
Command-line applications share a common pattern for parsing arguments and options. This is seen in all command applications.

#### Implementation Details
```csharp
// Common pattern found in command applications
public async Task ExecuteAsync(string[] args)
{
    if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
    {
        ShowHelp();
        return;
    }

    string path = args[0];
    bool recursive = args.Contains("-r") || args.Contains("--recursive");
    // Parse other options
    
    // Execute command logic
}
```

#### Abstraction Opportunity
Create a `CommandArgumentParser` class that standardizes argument parsing and help generation.

```csharp
public class CommandArgumentParser
{
    private readonly Dictionary<string, string> _options = new();
    private readonly List<string> _positionalArgs = new();
    private readonly Dictionary<string, (bool Required, string Description, string[] Aliases)> _optionDefinitions = new();

    public CommandArgumentParser AddOption(string name, string description, bool required = false, params string[] aliases)
    {
        _optionDefinitions[name] = (required, description, aliases);
        return this;
    }

    public bool Parse(string[] args, out string errorMessage)
    {
        _positionalArgs.Clear();
        _options.Clear();
        errorMessage = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith("--"))
            {
                // Handle long option
                string optionName = arg.Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    _options[optionName] = args[++i];
                }
                else
                {
                    _options[optionName] = "true";
                }
            }
            else if (arg.StartsWith("-"))
            {
                // Handle short option
                string shortOption = arg.Substring(1);
                
                // Find the full option name from the alias
                string fullOption = null;
                foreach (var def in _optionDefinitions)
                {
                    if (def.Value.Aliases.Contains(shortOption))
                    {
                        fullOption = def.Key;
                        break;
                    }
                }

                if (fullOption == null)
                {
                    errorMessage = $"Unknown option: {arg}";
                    return false;
                }

                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    _options[fullOption] = args[++i];
                }
                else
                {
                    _options[fullOption] = "true";
                }
            }
            else
            {
                // Handle positional argument
                _positionalArgs.Add(arg);
            }
        }

        // Check required options
        foreach (var def in _optionDefinitions)
        {
            if (def.Value.Required && !_options.ContainsKey(def.Key))
            {
                errorMessage = $"Missing required option: --{def.Key}";
                return false;
            }
        }

        return true;
    }

    public string GetOption(string name, string defaultValue = null)
    {
        return _options.TryGetValue(name, out var value) ? value : defaultValue;
    }

    public bool HasOption(string name)
    {
        return _options.ContainsKey(name);
    }

    public bool GetBoolOption(string name, bool defaultValue = false)
    {
        if (!_options.TryGetValue(name, out var value))
        {
            return defaultValue;
        }

        return value.ToLower() == "true";
    }

    public string GetPositionalArg(int index, string defaultValue = null)
    {
        return index < _positionalArgs.Count ? _positionalArgs[index] : defaultValue;
    }

    public int PositionalArgCount => _positionalArgs.Count;

    public string GenerateHelp(string commandName, string description)
    {
        var help = new StringBuilder();
        help.AppendLine($"Usage: {commandName} [options] [arguments]");
        help.AppendLine();
        help.AppendLine(description);
        help.AppendLine();
        help.AppendLine("Options:");

        foreach (var def in _optionDefinitions)
        {
            var aliases = string.Join(", ", def.Value.Aliases.Select(a => $"-{a}"));
            if (!string.IsNullOrEmpty(aliases))
            {
                aliases = ", " + aliases;
            }
            
            help.AppendLine($"  --{def.Key}{aliases}: {def.Value.Description}" + 
                            (def.Value.Required ? " (Required)" : ""));
        }

        return help.ToString();
    }
}
```

### 4. Background Processing

#### Component Description
Services and some window applications use background processing for long-running tasks. This pattern is seen in ReminderService, FileWatchService, and others.

#### Implementation Details
```csharp
// Common pattern found in service applications
private CancellationTokenSource _cancellationTokenSource;
private Task _backgroundTask;

public async Task StartAsync()
{
    _cancellationTokenSource = new CancellationTokenSource();
    _backgroundTask = Task.Run(() => BackgroundProcessingAsync(_cancellationTokenSource.Token));
}

public async Task StopAsync()
{
    if (_cancellationTokenSource != null)
    {
        _cancellationTokenSource.Cancel();
        if (_backgroundTask != null)
        {
            await _backgroundTask;
        }
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }
}

private async Task BackgroundProcessingAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            // Do background work
            await Task.Delay(1000, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in background processing");
        }
    }
}
```

#### Abstraction Opportunity
Create a `BackgroundWorker` class that standardizes background processing with progress reporting and error handling.

```csharp
public class BackgroundWorker
{
    private readonly ILogger _logger;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _backgroundTask;
    private readonly Func<CancellationToken, IProgress<int>, Task> _workFunc;
    private readonly int _delayMs;

    public event EventHandler<Exception> ErrorOccurred;
    public event EventHandler<int> ProgressChanged;

    public BackgroundWorker(
        ILogger logger,
        Func<CancellationToken, IProgress<int>, Task> workFunc,
        int delayMs = 1000)
    {
        _logger = logger;
        _workFunc = workFunc;
        _delayMs = delayMs;
    }

    public async Task StartAsync()
    {
        if (_backgroundTask != null && !_backgroundTask.IsCompleted)
        {
            return; // Already running
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var progress = new Progress<int>(value => ProgressChanged?.Invoke(this, value));
        _backgroundTask = Task.Run(() => BackgroundProcessingAsync(_cancellationTokenSource.Token, progress));
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while stopping background worker");
                }
            }
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async Task BackgroundProcessingAsync(CancellationToken cancellationToken, IProgress<int> progress)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _workFunc(cancellationToken, progress);
                await Task.Delay(_delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background processing");
                ErrorOccurred?.Invoke(this, ex);
                
                // Add a delay to prevent tight error loops
                try
                {
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public bool IsRunning => _backgroundTask != null && !_backgroundTask.IsCompleted;
}
```

### 5. UI Component Library

#### Component Description
Window applications share common UI components such as dialogs, forms, and data grids. Creating a reusable library of these components would ensure consistency and reduce duplication.

#### Implementation Details
Many applications implement their own versions of common UI components:

```csharp
// Common pattern found in window applications
private async Task ShowDialogAsync(string title, string message)
{
    // Each application implements similar dialog functionality
    var parameters = new DialogParameters
    {
        Title = title,
        Message = message,
        OkText = "OK",
        ShowCancel = false
    };
    
    var result = await DialogService.ShowAsync<DialogComponent>(parameters);
    // Handle result
}
```

#### Abstraction Opportunity
Create a `UIComponentLibrary` with reusable components that follow the same visual style and behavior.

```csharp
// Example component library (conceptual)
public class UIComponentLibrary
{
    private readonly IDialogService _dialogService;
    
    public UIComponentLibrary(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }
    
    public async Task<bool> ShowConfirmDialogAsync(string title, string message, 
        string okText = "OK", string cancelText = "Cancel")
    {
        var parameters = new DialogParameters
        {
            Title = title,
            Message = message,
            OkText = okText,
            CancelText = cancelText,
            ShowCancel = true
        };
        
        var result = await _dialogService.ShowAsync<DialogComponent>(parameters);
        return result.Confirmed;
    }
    
    public async Task ShowAlertAsync(string title, string message, string okText = "OK")
    {
        var parameters = new DialogParameters
        {
            Title = title,
            Message = message,
            OkText = okText,
            ShowCancel = false
        };
        
        await _dialogService.ShowAsync<DialogComponent>(parameters);
    }
    
    // File pickers, input forms, data grids, etc.
}
```

## Implementation Strategy

### Phase 1: Foundation Classes
1. Implement `FileOperationsHelper` and `CommandArgumentParser` first
2. These utilities will be used across most migrated applications

### Phase 2: Service-Level Abstractions
1. Implement `SettingsManager<T>` and `BackgroundWorker`
2. Test with the sample applications already migrated

### Phase 3: UI Components
1. Create core dialog components
2. Develop form input controls
3. Build data visualization components

### Phase 4: Integration
1. Update migration templates to use these shared components
2. Apply to initial migration efforts
3. Refine based on feedback and lessons learned

## Conclusion

Implementing these shared components before proceeding with the bulk of application migration will significantly reduce duplication and ensure consistency across the system. Each utility addresses common patterns found in the existing codebase and provides enhanced functionality while following best practices for the new architecture.
