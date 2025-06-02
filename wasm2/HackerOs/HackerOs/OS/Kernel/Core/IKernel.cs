// Kernel Interface - Public kernel abstraction
using System;
using System.Threading.Tasks;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.Kernel.Memory;

namespace HackerOs.OS.Kernel.Core
{
    /// <summary>
    /// Public kernel interface providing controlled access to kernel services
    /// This interface extends IKernelCore and serves as the main kernel contract
    /// for external modules and applications
    /// </summary>
    public interface IKernel : IKernelCore
    {
        /// <summary>
        /// Kernel version information
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Kernel boot time
        /// </summary>
        DateTime BootTime { get; }

        /// <summary>
        /// Get system uptime
        /// </summary>
        TimeSpan Uptime { get; }

        /// <summary>
        /// Trigger a kernel panic with specified error
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="exception">Optional exception that caused the panic</param>
        Task KernelPanicAsync(string errorMessage, Exception? exception = null);

        /// <summary>
        /// Get kernel statistics and health information
        /// </summary>
        Task<KernelStatistics> GetStatisticsAsync();

        /// <summary>
        /// Check if the kernel is in safe mode
        /// </summary>
        bool IsSafeMode { get; }

        /// <summary>
        /// Get current kernel mode (user/kernel space indicator)
        /// </summary>
        KernelMode CurrentMode { get; }
    }

    /// <summary>
    /// Kernel statistics container
    /// </summary>
    public class KernelStatistics
    {
        public DateTime BootTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public long TotalMemoryUsage { get; set; }
        public int ActiveProcessCount { get; set; }
        public int TotalSystemCalls { get; set; }
        public int FailedSystemCalls { get; set; }
        public double CpuUsagePercent { get; set; }
        public bool IsHealthy { get; set; }
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// Kernel execution mode
    /// </summary>
    public enum KernelMode
    {
        /// <summary>
        /// User mode - restricted access to system resources
        /// </summary>
        User,
        
        /// <summary>
        /// Kernel mode - full access to all system resources
        /// </summary>
        Kernel,
        
        /// <summary>
        /// Safe mode - minimal functionality, emergency operation
        /// </summary>
        Safe
    }
}
