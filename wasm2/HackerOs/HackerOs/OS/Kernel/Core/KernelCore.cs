// Main Kernel Implementation
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.Kernel.Memory;

namespace HackerOs.OS.Kernel.Core
{
    /// <summary>
    /// Main kernel implementation for HackerOS
    /// </summary>
    public class KernelCore : IKernelCore
    {
        private readonly ILogger<KernelCore> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Type, object> _kernelServices = new();
        private readonly ConcurrentDictionary<string, Func<object[], Task<object?>>> _systemCalls = new();
        
        private bool _isInitialized = false;
        
        public event EventHandler<KernelEvent>? OnKernelEvent;
        
        public IProcessManager ProcessManager { get; private set; } = null!;
        public IMemoryManager MemoryManager { get; private set; } = null!;
        public bool IsInitialized => _isInitialized;

        public KernelCore(ILogger<KernelCore> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Starting HackerOS Kernel initialization...");
                
                FireKernelEvent("KERNEL_BOOT", "Kernel boot sequence started");

                // Initialize memory manager first
                MemoryManager = _serviceProvider.GetRequiredService<IMemoryManager>();
                await RegisterServiceAsync(MemoryManager);
                _logger.LogDebug("Memory manager initialized");

                // Initialize process manager
                ProcessManager = _serviceProvider.GetRequiredService<IProcessManager>();
                await RegisterServiceAsync(ProcessManager);
                _logger.LogDebug("Process manager initialized");

                // Register core system calls
                await RegisterCoreSystemCallsAsync();
                
                // Subscribe to subsystem events
                SubscribeToSubsystemEvents();

                _isInitialized = true;
                
                FireKernelEvent("KERNEL_READY", "Kernel initialization completed successfully");
                _logger.LogInformation("HackerOS Kernel initialized successfully");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "KERNEL PANIC: Failed to initialize kernel");
                FireKernelEvent("KERNEL_PANIC", $"Kernel initialization failed: {ex.Message}", ex);
                return false;
            }
        }

        public async Task ShutdownAsync()
        {
            try
            {
                _logger.LogInformation("Starting kernel shutdown sequence...");
                FireKernelEvent("KERNEL_SHUTDOWN", "Kernel shutdown sequence started");

                // Terminate all processes
                if (ProcessManager != null)
                {
                    var allProcesses = ProcessManager.GetAllProcesses();
                    foreach (var process in allProcesses)
                    {
                        await ProcessManager.TerminateProcessAsync(process.ProcessId);
                    }
                }

                // Clean up memory
                if (MemoryManager != null)
                {
                    var leakReport = await MemoryManager.CheckForMemoryLeaksAsync();
                    if (leakReport.HasLeaks)
                    {
                        _logger.LogWarning("Memory leaks detected during shutdown: {LeakCount} leaks", 
                            leakReport.DetectedLeaks.Count);
                    }
                }

                _isInitialized = false;
                FireKernelEvent("KERNEL_SHUTDOWN_COMPLETE", "Kernel shutdown completed");
                _logger.LogInformation("Kernel shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during kernel shutdown");
                FireKernelEvent("KERNEL_ERROR", $"Shutdown error: {ex.Message}", ex);
            }
        }

        public async Task<SystemCallResult> ExecuteSystemCallAsync(SystemCall call)
        {
            try
            {
                if (!_isInitialized)
                {
                    return new SystemCallResult
                    {
                        Success = false,
                        ErrorMessage = "Kernel not initialized",
                        ErrorCode = -1
                    };
                }

                _logger.LogTrace("Executing system call: {CallName} from {Caller}", call.Name, call.CallerContext);

                if (!_systemCalls.TryGetValue(call.Name, out var handler))
                {
                    return new SystemCallResult
                    {
                        Success = false,
                        ErrorMessage = $"Unknown system call: {call.Name}",
                        ErrorCode = -2
                    };
                }

                var result = await handler(call.Parameters);
                
                return new SystemCallResult
                {
                    Success = true,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing system call {CallName}", call.Name);
                return new SystemCallResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ErrorCode = -3
                };
            }
        }

        public async Task<bool> RegisterServiceAsync<T>(T service) where T : class
        {
            try
            {
                _kernelServices[typeof(T)] = service;
                _logger.LogDebug("Registered kernel service: {ServiceType}", typeof(T).Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register kernel service: {ServiceType}", typeof(T).Name);
                return false;
            }
        }

        public async Task<T?> GetServiceAsync<T>() where T : class
        {
            if (_kernelServices.TryGetValue(typeof(T), out var service))
            {
                return service as T;
            }
            
            // Try to get from DI container
            try
            {
                var diService = _serviceProvider.GetService<T>();
                if (diService != null)
                {
                    await RegisterServiceAsync(diService);
                    return diService;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get service from DI container: {ServiceType}", typeof(T).Name);
            }

            return null;
        }

        private async Task RegisterCoreSystemCallsAsync()
        {
            // Process management system calls
            _systemCalls["create_process"] = async (args) =>
            {
                if (args.Length > 0 && args[0] is ProcessStartInfo startInfo)
                {
                    return await ProcessManager.CreateProcessAsync(startInfo);
                }
                throw new ArgumentException("Invalid arguments for create_process");
            };

            _systemCalls["terminate_process"] = async (args) =>
            {
                if (args.Length > 0 && args[0] is int processId)
                {
                    var exitCode = args.Length > 1 && args[1] is int code ? code : 0;
                    return await ProcessManager.TerminateProcessAsync(processId, exitCode);
                }
                throw new ArgumentException("Invalid arguments for terminate_process");
            };

            _systemCalls["get_process"] = async (args) =>
            {
                if (args.Length > 0 && args[0] is int processId)
                {
                    return ProcessManager.GetProcess(processId);
                }
                throw new ArgumentException("Invalid arguments for get_process");
            };

            // Memory management system calls
            _systemCalls["allocate_memory"] = async (args) =>
            {
                if (args.Length >= 2 && args[0] is int processId && args[1] is long size)
                {
                    var protection = args.Length > 2 && args[2] is MemoryProtection prot ? prot : MemoryProtection.ReadWrite;
                    return await MemoryManager.AllocateMemoryAsync(processId, size, protection);
                }
                throw new ArgumentException("Invalid arguments for allocate_memory");
            };

            _systemCalls["deallocate_memory"] = async (args) =>
            {
                if (args.Length > 0 && args[0] is MemoryRegion region)
                {
                    return await MemoryManager.DeallocateMemoryAsync(region);
                }
                throw new ArgumentException("Invalid arguments for deallocate_memory");
            };

            _systemCalls["get_memory_info"] = async (args) =>
            {
                return MemoryManager.GetSystemMemoryInfo();
            };

            _logger.LogDebug("Registered {Count} core system calls", _systemCalls.Count);
        }

        private void SubscribeToSubsystemEvents()
        {
            // Subscribe to process manager events
            if (ProcessManager != null)
            {
                ProcessManager.OnProcessEvent += (sender, e) =>
                {
                    FireKernelEvent("PROCESS_EVENT", e.Message, e);
                };
            }

            // Subscribe to memory manager events
            if (MemoryManager != null)
            {
                MemoryManager.OnMemoryEvent += (sender, e) =>
                {
                    FireKernelEvent("MEMORY_EVENT", e.Message, e);
                };
            }
        }

        private void FireKernelEvent(string eventType, string message, object? data = null)
        {
            var kernelEvent = new KernelEvent
            {
                EventType = eventType,
                Message = message,
                Data = data
            };

            OnKernelEvent?.Invoke(this, kernelEvent);
            _logger.LogTrace("Kernel event: {EventType} - {Message}", eventType, message);
        }
    }
}
