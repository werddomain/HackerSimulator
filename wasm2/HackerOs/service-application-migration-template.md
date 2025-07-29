# Service Application Migration Template

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `worksheet.md` FOR PROJECT GUIDELINES**

## Overview

This document provides a step-by-step template for migrating existing service applications to the new unified architecture. The template includes migration steps, code examples, and testing guidelines to ensure consistent implementation.

## Migration Checklist

### Pre-Migration Assessment
- [ ] Review the service's current implementation
- [ ] Identify dependencies and integration points
- [ ] Document current functionality and behavior
- [ ] Verify service complexity against migration inventory

### Code Migration Steps
- [ ] Update file structure (if needed)
- [ ] Convert to ServiceBase inheritance
- [ ] Update dependency injection and service usage
- [ ] Implement background worker pattern
- [ ] Add lifecycle method implementations
- [ ] Implement event handling through ApplicationBridge
- [ ] Add configuration management
- [ ] Implement error handling and logging

### Testing Steps
- [ ] Verify service initialization
- [ ] Test background processing
- [ ] Verify service functionality matches original
- [ ] Test integration with other components
- [ ] Verify process lifecycle (start, stop)
- [ ] Test error scenarios and recovery
- [ ] Performance testing (if applicable)

## Directory Structure

Migrated service applications should follow this directory structure:

```
HackerOs/
└── OS/
    └── Applications/
        └── Services/
            └── {ServiceName}/
                └── {ServiceName}Service.cs
```

## Code Template

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.Core;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Services.{ServiceName}
{
    /// <summary>
    /// {ServiceName}Service provides [brief description of service functionality].
    /// </summary>
    public class {ServiceName}Service : ServiceBase
    {
        private readonly ILogger<{ServiceName}Service> _logger;
        // Add other required service dependencies
        
        private CancellationTokenSource _cancellationTokenSource;
        private Task _backgroundTask;
        private readonly int _processingIntervalMs = 1000; // Default interval
        
        /// <summary>
        /// Initializes a new instance of the <see cref="{ServiceName}Service"/> class.
        /// </summary>
        /// <param name="applicationBridge">The application bridge for process and event management.</param>
        /// <param name="logger">The logger for service logging.</param>
        public {ServiceName}Service(
            IApplicationBridge applicationBridge,
            ILogger<{ServiceName}Service> logger
            /* Add other required services */)
            : base(applicationBridge)
        {
            _logger = logger;
            // Initialize other dependencies
            
            // Set application information
            Name = "{ServiceName}Service";
            Description = "Provides [brief description of service functionality]";
            ApplicationType = ApplicationType.Service;
        }
        
        /// <summary>
        /// Starts the service and begins background processing.
        /// </summary>
        public override async Task StartAsync()
        {
            try
            {
                _logger.LogInformation($"Starting {Name}...");
                
                // Load configuration
                await LoadConfigurationAsync();
                
                // Initialize background processing
                _cancellationTokenSource = new CancellationTokenSource();
                _backgroundTask = Task.Run(() => BackgroundProcessingAsync(_cancellationTokenSource.Token));
                
                // Register with system services or perform other startup tasks
                
                await base.StartAsync();
                
                _logger.LogInformation($"{Name} started successfully");
                await RaiseOutputAsync($"{Name} started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting {Name}");
                await RaiseErrorAsync($"Error starting {Name}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Stops the service and cancels background processing.
        /// </summary>
        public override async Task StopAsync()
        {
            try
            {
                _logger.LogInformation($"Stopping {Name}...");
                
                // Cancel background processing
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    if (_backgroundTask != null)
                    {
                        await Task.WhenAny(_backgroundTask, Task.Delay(5000)); // Wait with timeout
                    }
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
                
                // Perform cleanup operations
                
                await base.StopAsync();
                
                _logger.LogInformation($"{Name} stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping {Name}");
                await RaiseErrorAsync($"Error stopping {Name}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Performs the background processing for the service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop processing.</param>
        private async Task BackgroundProcessingAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Perform service-specific background processing
                        await ProcessWorkItemsAsync();
                        
                        // Wait for next processing interval
                        await Task.Delay(_processingIntervalMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during cancellation
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background processing");
                        await RaiseErrorAsync($"Error in background processing: {ex.Message}");
                        
                        // Wait longer after an error to prevent tight error loops
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in background task");
                await RaiseErrorAsync($"Unhandled exception in background task: {ex.Message}");
            }
            finally
            {
                _logger.LogInformation("Background processing stopped");
            }
        }
        
        /// <summary>
        /// Processes work items in the background task.
        /// Implement service-specific functionality here.
        /// </summary>
        private async Task ProcessWorkItemsAsync()
        {
            // Implement service-specific processing logic
            // Example:
            // 1. Check for new data
            // 2. Process data
            // 3. Update status
            // 4. Notify interested parties
            
            await Task.CompletedTask; // Replace with actual implementation
        }
        
        /// <summary>
        /// Loads service configuration from the file system.
        /// </summary>
        private async Task LoadConfigurationAsync()
        {
            // Implement configuration loading
            // Example:
            // 1. Check for config file
            // 2. Load and parse configuration
            // 3. Apply configuration settings
            
            // Example config path: /etc/hackeros/services/{serviceName}.conf
            // or /home/user/.config/{serviceName}/settings.json
            
            await Task.CompletedTask; // Replace with actual implementation
        }
        
        /// <summary>
        /// Implements the SetStateAsync method from IApplication.
        /// </summary>
        /// <param name="state">The new application state.</param>
        public override async Task SetStateAsync(ApplicationState state)
        {
            try
            {
                // Handle state changes appropriately for service
                await base.SetStateAsync(state);
                
                if (state == ApplicationState.Running && !IsRunning)
                {
                    await StartAsync();
                }
                else if (state == ApplicationState.Stopped && IsRunning)
                {
                    await StopAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting state for {Name}");
                await RaiseErrorAsync($"Error setting state: {ex.Message}");
            }
        }
        
        // Service-specific methods and properties
    }
}
```

## Configuration Management

For services that need configuration, use this pattern:

```csharp
public class ServiceConfiguration
{
    public int ProcessingIntervalMs { get; set; } = 1000;
    public bool EnableNotifications { get; set; } = true;
    public string[] WatchedDirectories { get; set; } = new string[] { "/home/user" };
    // Add other configuration properties
}

private ServiceConfiguration _configuration = new();
private readonly string _configPath = "/etc/hackeros/services/{serviceName}.conf";

private async Task LoadConfigurationAsync()
{
    try
    {
        if (await _fileSystem.FileExistsAsync(_configPath))
        {
            var content = await _fileSystem.ReadAllTextAsync(_configPath);
            _configuration = System.Text.Json.JsonSerializer.Deserialize<ServiceConfiguration>(content) 
                ?? new ServiceConfiguration();
            
            // Apply configuration
            _processingIntervalMs = _configuration.ProcessingIntervalMs;
            // Apply other settings
            
            _logger.LogInformation($"Configuration loaded from {_configPath}");
        }
        else
        {
            _logger.LogInformation($"No configuration found at {_configPath}, using defaults");
            _configuration = new ServiceConfiguration();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error loading configuration from {_configPath}");
        _configuration = new ServiceConfiguration(); // Use defaults
    }
}
```

## Event Publishing

For services that publish events for other applications:

```csharp
public class ServiceEvent
{
    public string Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string Data { get; set; }
}

private async Task PublishEventAsync(string type, string data)
{
    try
    {
        var serviceEvent = new ServiceEvent
        {
            Type = type,
            Timestamp = DateTime.UtcNow,
            Data = data
        };
        
        var eventJson = System.Text.Json.JsonSerializer.Serialize(serviceEvent);
        await RaiseOutputAsync(eventJson);
        
        // Optional: Store event in event log
        // await AppendToEventLogAsync(eventJson);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error publishing event {type}");
    }
}
```

## Common Migration Patterns

### Resource Cleanup

Ensure proper resource cleanup in the StopAsync method:

```csharp
public override async Task StopAsync()
{
    try
    {
        _logger.LogInformation($"Stopping {Name}...");
        
        // Cancel background processing
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            try
            {
                if (_backgroundTask != null)
                {
                    // Wait with timeout to prevent hanging
                    if (await Task.WhenAny(_backgroundTask, Task.Delay(5000)) != _backgroundTask)
                    {
                        _logger.LogWarning("Background task did not complete within timeout");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for background task to complete");
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
        
        // Close any open resources
        await CloseResourcesAsync();
        
        await base.StopAsync();
        
        _logger.LogInformation($"{Name} stopped successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error stopping {Name}");
        await RaiseErrorAsync($"Error stopping {Name}: {ex.Message}");
        throw;
    }
}

private async Task CloseResourcesAsync()
{
    // Close any open resources like file handles, network connections, etc.
    await Task.CompletedTask; // Replace with actual implementation
}
```

### Status Reporting

For services that need to report status:

```csharp
private Timer _statusReportTimer;
private ServiceStatus _currentStatus = ServiceStatus.Idle;

private void InitializeStatusReporting()
{
    _statusReportTimer = new Timer(async _ => await ReportStatusAsync(), null, 
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
}

private async Task ReportStatusAsync()
{
    try
    {
        var status = new
        {
            Service = Name,
            Status = _currentStatus.ToString(),
            Timestamp = DateTime.UtcNow,
            ActiveTasks = _activeTaskCount,
            MemoryUsage = Environment.WorkingSet / 1024 / 1024 // MB
        };
        
        var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
        await RaiseOutputAsync(statusJson);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error reporting status");
    }
}

private enum ServiceStatus
{
    Idle,
    Processing,
    Error,
    Maintenance
}
```

## Troubleshooting Common Issues

### Service Not Starting

Check:
- Verify ApplicationBridge initialization
- Check for exceptions in the service logs
- Ensure all dependencies are properly injected

### Background Processing Issues

Check:
- Verify cancellation token is being respected
- Check for infinite loops or deadlocks
- Ensure proper error handling in the processing loop

### Resource Leaks

Check:
- Verify all disposable resources are properly disposed
- Check for unhandled exceptions in cleanup code
- Ensure background tasks are properly cancelled and joined

## Examples

For complete examples of migrated service applications, refer to:

1. `FileWatchService.cs` - File system monitoring service example
2. `ReminderService.cs` - Reminder management service example (after migration)

## Testing Guidelines

1. **Unit Testing**
   - Test service initialization and configuration
   - Test individual processing methods
   - Mock dependencies for controlled testing

2. **Integration Testing**
   - Test interaction with system services
   - Verify event publishing and subscription
   - Test error recovery mechanisms

3. **Performance Testing**
   - Monitor CPU and memory usage
   - Test with high processing loads
   - Verify resource cleanup

4. **Stability Testing**
   - Run for extended periods
   - Test recovery from simulated crashes
   - Verify proper cleanup on shutdown

## Final Validation Checklist

- [ ] Service starts correctly
- [ ] Background processing works as expected
- [ ] All functionality from original service works
- [ ] Service responds to system events
- [ ] Service cleans up resources on stop
- [ ] Error handling and recovery work as expected
- [ ] Service follows the new architecture guidelines
- [ ] Service properly reports status and errors
