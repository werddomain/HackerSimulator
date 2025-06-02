// Kernel Core Interface - Main kernel service contract
using System;
using System.Threading.Tasks;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.Kernel.Memory;

namespace HackerOs.OS.Kernel.Core
{
    /// <summary>
    /// Main kernel interface providing core OS services
    /// </summary>
    public interface IKernelCore
    {
        /// <summary>
        /// Initialize the kernel and all subsystems
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Shutdown the kernel and cleanup resources
        /// </summary>
        Task ShutdownAsync();

        /// <summary>
        /// Execute a system call
        /// </summary>
        Task<SystemCallResult> ExecuteSystemCallAsync(SystemCall call);

        /// <summary>
        /// Register a kernel service
        /// </summary>
        Task<bool> RegisterServiceAsync<T>(T service) where T : class;

        /// <summary>
        /// Get a kernel service
        /// </summary>
        Task<T?> GetServiceAsync<T>() where T : class;

        /// <summary>
        /// Kernel event notifications
        /// </summary>
        event EventHandler<KernelEvent> OnKernelEvent;

        /// <summary>
        /// Get process manager instance
        /// </summary>
        IProcessManager ProcessManager { get; }

        /// <summary>
        /// Get memory manager instance
        /// </summary>
        IMemoryManager MemoryManager { get; }

        /// <summary>
        /// Check if kernel is initialized
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// System call result container
    /// </summary>
    public class SystemCallResult
    {
        public bool Success { get; set; }
        public object? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public int ErrorCode { get; set; }
    }

    /// <summary>
    /// System call definition
    /// </summary>
    public class SystemCall
    {
        public string Name { get; set; } = string.Empty;
        public object[] Parameters { get; set; } = Array.Empty<object>();
        public string CallerContext { get; set; } = string.Empty;
    }

    /// <summary>
    /// Kernel event information
    /// </summary>
    public class KernelEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Data { get; set; }
    }
}
