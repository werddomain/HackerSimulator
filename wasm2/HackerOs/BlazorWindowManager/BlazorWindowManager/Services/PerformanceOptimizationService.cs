using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace BlazorWindowManager.Services;

/// <summary>
/// Service for performance optimizations including event debouncing and throttling
/// </summary>
public class PerformanceOptimizationService : IAsyncDisposable
{
    private readonly Dictionary<string, CancellationTokenSource> _debouncers = new();
    private readonly Dictionary<string, DateTime> _throttlers = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Debounces an async action by the specified delay
    /// </summary>
    /// <param name="key">Unique key for the debounce operation</param>
    /// <param name="action">Action to execute after debounce</param>
    /// <param name="delay">Delay in milliseconds</param>
    public async Task DebounceAsync(string key, Func<Task> action, int delay = 300)
    {
        CancellationTokenSource? previousCts;
        CancellationTokenSource newCts;
        
        lock (_lock)
        {
            // Cancel any existing debouncer for this key
            _debouncers.TryGetValue(key, out previousCts);
            previousCts?.Cancel();
            
            // Create new cancellation token source
            newCts = new CancellationTokenSource();
            _debouncers[key] = newCts;
        }
        
        try
        {
            // Wait for the delay
            await Task.Delay(delay, newCts.Token);
            
            // Execute the action if not cancelled
            await action();
        }
        catch (OperationCanceledException)
        {
            // Debounce was cancelled, do nothing
        }
        finally
        {
            lock (_lock)
            {
                // Clean up if this is still the current debouncer
                if (_debouncers.TryGetValue(key, out var currentCts) && currentCts == newCts)
                {
                    _debouncers.Remove(key);
                }
            }
            newCts.Dispose();
        }
    }
    
    /// <summary>
    /// Debounces a synchronous action by the specified delay
    /// </summary>
    /// <param name="key">Unique key for the debounce operation</param>
    /// <param name="action">Action to execute after debounce</param>
    /// <param name="delay">Delay in milliseconds</param>
    public async Task DebounceAsync(string key, Action action, int delay = 300)
    {
        await DebounceAsync(key, () =>
        {
            action();
            return Task.CompletedTask;
        }, delay);
    }
    
    /// <summary>
    /// Throttles an action to execute at most once per interval
    /// </summary>
    /// <param name="key">Unique key for the throttle operation</param>
    /// <param name="action">Action to execute</param>
    /// <param name="intervalMs">Minimum interval between executions in milliseconds</param>
    /// <returns>True if the action was executed, false if throttled</returns>
    public bool Throttle(string key, Action action, int intervalMs = 16)
    {
        var now = DateTime.UtcNow;
        
        lock (_lock)
        {
            if (_throttlers.TryGetValue(key, out var lastExecution))
            {
                if ((now - lastExecution).TotalMilliseconds < intervalMs)
                {
                    return false; // Throttled
                }
            }
            
            _throttlers[key] = now;
        }
        
        action();
        return true;
    }
    
    /// <summary>
    /// Throttles an async action to execute at most once per interval
    /// </summary>
    /// <param name="key">Unique key for the throttle operation</param>
    /// <param name="action">Async action to execute</param>
    /// <param name="intervalMs">Minimum interval between executions in milliseconds</param>
    /// <returns>True if the action was executed, false if throttled</returns>
    public async Task<bool> ThrottleAsync(string key, Func<Task> action, int intervalMs = 16)
    {
        var now = DateTime.UtcNow;
        
        lock (_lock)
        {
            if (_throttlers.TryGetValue(key, out var lastExecution))
            {
                if ((now - lastExecution).TotalMilliseconds < intervalMs)
                {
                    return false; // Throttled
                }
            }
            
            _throttlers[key] = now;
        }
        
        await action();
        return true;
    }
    
    /// <summary>
    /// Cancels all pending debounced operations
    /// </summary>
    public void CancelAllDebounces()
    {
        lock (_lock)
        {
            foreach (var cts in _debouncers.Values)
            {
                cts.Cancel();
            }
            _debouncers.Clear();
        }
    }
    
    /// <summary>
    /// Cancels a specific debounced operation
    /// </summary>
    /// <param name="key">Key of the debounced operation to cancel</param>
    public void CancelDebounce(string key)
    {
        lock (_lock)
        {
            if (_debouncers.TryGetValue(key, out var cts))
            {
                cts.Cancel();
                _debouncers.Remove(key);
            }
        }
    }
    
    /// <summary>
    /// Clears throttle history for all operations
    /// </summary>
    public void ClearThrottleHistory()
    {
        lock (_lock)
        {
            _throttlers.Clear();
        }
    }
    
    /// <summary>
    /// Clears throttle history for a specific operation
    /// </summary>
    /// <param name="key">Key of the throttle operation to clear</param>
    public void ClearThrottleHistory(string key)
    {
        lock (_lock)
        {
            _throttlers.Remove(key);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        CancelAllDebounces();
        ClearThrottleHistory();
    }
}

/// <summary>
/// Extension methods for EventArgs to support performance optimization
/// </summary>
public static class PerformanceEventExtensions
{
    /// <summary>
    /// Creates a debounced version of an event callback
    /// </summary>
    /// <typeparam name="T">Event args type</typeparam>
    /// <param name="callback">Original callback</param>
    /// <param name="performanceService">Performance optimization service</param>
    /// <param name="key">Unique key for debouncing</param>
    /// <param name="delay">Debounce delay in milliseconds</param>
    /// <returns>Debounced event callback</returns>
    public static EventCallback<T> Debounced<T>(
        this EventCallback<T> callback,
        PerformanceOptimizationService performanceService,
        string key,
        int delay = 300)
    {        return new EventCallback<T>(null, (Func<T, Task>)(async (args) =>
        {
            await performanceService.DebounceAsync(key, async () =>
            {
                if (callback.HasDelegate)
                    await callback.InvokeAsync(args);
            }, delay);
        }));
    }
    
    /// <summary>
    /// Creates a throttled version of an event callback
    /// </summary>
    /// <typeparam name="T">Event args type</typeparam>
    /// <param name="callback">Original callback</param>
    /// <param name="performanceService">Performance optimization service</param>
    /// <param name="key">Unique key for throttling</param>
    /// <param name="intervalMs">Throttle interval in milliseconds</param>
    /// <returns>Throttled event callback</returns>
    public static EventCallback<T> Throttled<T>(
        this EventCallback<T> callback,
        PerformanceOptimizationService performanceService,
        string key,
        int intervalMs = 16)
    {        return new EventCallback<T>(null, (Func<T, Task>)(async (args) =>
        {
            await performanceService.ThrottleAsync(key, async () =>
            {
                if (callback.HasDelegate)
                    await callback.InvokeAsync(args);
            }, intervalMs);
        }));
    }
}
