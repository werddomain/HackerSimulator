// Interrupt Handler Interface - System interrupt and system call processing
using System;
using System.Threading.Tasks;

namespace HackerOs.OS.Kernel.Core
{
    /// <summary>
    /// Interface for handling system interrupts and system calls
    /// Provides the mechanism for controlled kernel access through interrupts
    /// </summary>
    public interface IInterruptHandler
    {
        /// <summary>
        /// Event fired when an interrupt is processed
        /// </summary>
        event EventHandler<InterruptEventArgs>? OnInterruptProcessed;

        /// <summary>
        /// Handle a system call interrupt
        /// </summary>
        /// <param name="interrupt">System call interrupt to process</param>
        Task<InterruptResult> HandleSystemCallAsync(SystemCallInterrupt interrupt);

        /// <summary>
        /// Handle a hardware interrupt simulation
        /// </summary>
        /// <param name="interrupt">Hardware interrupt to process</param>
        Task<InterruptResult> HandleHardwareInterruptAsync(HardwareInterrupt interrupt);

        /// <summary>
        /// Register a system call handler
        /// </summary>
        /// <param name="callNumber">System call number</param>
        /// <param name="handler">Handler function</param>
        Task RegisterSystemCallHandlerAsync(int callNumber, Func<SystemCallInterrupt, Task<object?>> handler);

        /// <summary>
        /// Register a hardware interrupt handler
        /// </summary>
        /// <param name="interruptNumber">Interrupt number</param>
        /// <param name="handler">Handler function</param>
        Task RegisterHardwareInterruptHandlerAsync(int interruptNumber, Func<HardwareInterrupt, Task<object?>> handler);

        /// <summary>
        /// Enable interrupts
        /// </summary>
        Task EnableInterruptsAsync();

        /// <summary>
        /// Disable interrupts (for critical sections)
        /// </summary>
        Task DisableInterruptsAsync();

        /// <summary>
        /// Check if interrupts are enabled
        /// </summary>
        bool InterruptsEnabled { get; }

        /// <summary>
        /// Get interrupt statistics
        /// </summary>
        Task<InterruptStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// System call interrupt representation
    /// </summary>
    public class SystemCallInterrupt
    {
        public int CallNumber { get; set; }
        public string CallName { get; set; } = string.Empty;
        public object[] Parameters { get; set; } = Array.Empty<object>();
        public string CallerContext { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int ProcessId { get; set; }
        public string UserId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Hardware interrupt representation
    /// </summary>
    public class HardwareInterrupt
    {
        public int InterruptNumber { get; set; }
        public InterruptType Type { get; set; }
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public InterruptPriority Priority { get; set; } = InterruptPriority.Normal;
    }

    /// <summary>
    /// Interrupt processing result
    /// </summary>
    public class InterruptResult
    {
        public bool Success { get; set; }
        public object? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public int ErrorCode { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Interrupt event arguments
    /// </summary>
    public class InterruptEventArgs : EventArgs
    {
        public InterruptType Type { get; set; }
        public int InterruptNumber { get; set; }
        public bool Success { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Interrupt statistics
    /// </summary>
    public class InterruptStatistics
    {
        public long TotalInterrupts { get; set; }
        public long SystemCallInterrupts { get; set; }
        public long HardwareInterrupts { get; set; }
        public long FailedInterrupts { get; set; }
        public double AverageProcessingTime { get; set; }
        public DateTime LastInterrupt { get; set; }
        public bool InterruptsEnabled { get; set; }
    }

    /// <summary>
    /// Types of interrupts
    /// </summary>
    public enum InterruptType
    {
        /// <summary>
        /// System call interrupt (software interrupt)
        /// </summary>
        SystemCall = 0x80,
        
        /// <summary>
        /// Timer interrupt
        /// </summary>
        Timer = 0x01,
        
        /// <summary>
        /// Keyboard interrupt
        /// </summary>
        Keyboard = 0x02,
        
        /// <summary>
        /// Network interrupt
        /// </summary>
        Network = 0x03,
        
        /// <summary>
        /// Disk I/O interrupt
        /// </summary>
        DiskIO = 0x04,
        
        /// <summary>
        /// Page fault interrupt
        /// </summary>
        PageFault = 0x05,
        
        /// <summary>
        /// General protection fault
        /// </summary>
        ProtectionFault = 0x06
    }

    /// <summary>
    /// Interrupt priority levels
    /// </summary>
    public enum InterruptPriority
    {
        /// <summary>
        /// Low priority interrupt
        /// </summary>
        Low = 0,
        
        /// <summary>
        /// Normal priority interrupt
        /// </summary>
        Normal = 1,
        
        /// <summary>
        /// High priority interrupt
        /// </summary>
        High = 2,
        
        /// <summary>
        /// Critical priority interrupt (non-maskable)
        /// </summary>
        Critical = 3
    }
}
