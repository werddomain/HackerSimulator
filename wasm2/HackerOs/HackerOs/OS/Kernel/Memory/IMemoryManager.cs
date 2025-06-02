// Memory Manager Interface - Virtual memory management
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Kernel.Memory
{
    /// <summary>
    /// Memory management interface for kernel services
    /// </summary>
    public interface IMemoryManager
    {
        /// <summary>
        /// Allocate memory for a process
        /// </summary>
        Task<MemoryRegion?> AllocateMemoryAsync(int processId, long size, MemoryProtection protection = MemoryProtection.ReadWrite);

        /// <summary>
        /// Deallocate memory
        /// </summary>
        Task<bool> DeallocateMemoryAsync(MemoryRegion region);

        /// <summary>
        /// Get memory usage for a process
        /// </summary>
        MemoryUsage GetProcessMemoryUsage(int processId);

        /// <summary>
        /// Get total system memory usage
        /// </summary>
        SystemMemoryInfo GetSystemMemoryInfo();

        /// <summary>
        /// Get all memory regions for a process
        /// </summary>
        IReadOnlyList<MemoryRegion> GetProcessMemoryRegions(int processId);

        /// <summary>
        /// Check for memory leaks
        /// </summary>
        Task<MemoryLeakReport> CheckForMemoryLeaksAsync();

        /// <summary>
        /// Force garbage collection for a process
        /// </summary>
        Task ForceGarbageCollectionAsync(int processId);

        /// <summary>
        /// Set memory limit for a process
        /// </summary>
        bool SetProcessMemoryLimit(int processId, long limitBytes);

        /// <summary>
        /// Get memory statistics asynchronously
        /// </summary>
        /// <returns>Detailed memory statistics information</returns>
        Task<MemoryStatistics> GetMemoryStatisticsAsync();

        /// <summary>
        /// Memory events
        /// </summary>
        event EventHandler<MemoryEvent> OnMemoryEvent;
    }

    /// <summary>
    /// Memory region representation
    /// </summary>
    public class MemoryRegion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int ProcessId { get; set; }
        public long BaseAddress { get; set; }
        public long Size { get; set; }
        public MemoryProtection Protection { get; set; }
        public MemoryType Type { get; set; }
        public DateTime AllocationTime { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Memory protection flags
    /// </summary>
    [Flags]
    public enum MemoryProtection
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        ReadWrite = Read | Write,
        ReadExecute = Read | Execute,
        All = Read | Write | Execute
    }

    /// <summary>
    /// Memory region types
    /// </summary>
    public enum MemoryType
    {
        Code,
        Data,
        Stack,
        Heap,
        Shared,
        Mapped
    }

    /// <summary>
    /// Process memory usage information
    /// </summary>
    public class MemoryUsage
    {
        public int ProcessId { get; set; }
        public long AllocatedBytes { get; set; }
        public long UsedBytes { get; set; }
        public long PeakUsage { get; set; }
        public int RegionCount { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// System memory information
    /// </summary>
    public class SystemMemoryInfo
    {
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
        public long UsedMemory { get; set; }
        public double UsagePercentage => TotalMemory > 0 ? (double)UsedMemory / TotalMemory * 100 : 0;
        public int TotalProcesses { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Memory leak detection report
    /// </summary>
    public class MemoryLeakReport
    {
        public bool HasLeaks { get; set; }
        public List<MemoryLeak> DetectedLeaks { get; set; } = new();
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
        public TimeSpan ScanDuration { get; set; }
    }

    /// <summary>
    /// Memory leak information
    /// </summary>
    public class MemoryLeak
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public long LeakedBytes { get; set; }
        public TimeSpan LeakAge { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Memory event information
    /// </summary>
    public class MemoryEvent
    {
        public MemoryEventType EventType { get; set; }
        public int ProcessId { get; set; }
        public long Size { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Data { get; set; }
    }

    /// <summary>
    /// Memory event types
    /// </summary>
    public enum MemoryEventType
    {
        Allocated,
        Deallocated,
        OutOfMemory,
        LeakDetected,
        LimitExceeded,
        Error
    }

    /// <summary>
    /// Memory statistics information
    /// </summary>
    public class MemoryStatistics
    {
        /// <summary>
        /// The total physical memory in bytes
        /// </summary>
        public long TotalPhysicalMemory { get; set; }

        /// <summary>
        /// The available physical memory in bytes
        /// </summary>
        public long AvailablePhysicalMemory { get; set; }

        /// <summary>
        /// The used physical memory in bytes
        /// </summary>
        public long UsedPhysicalMemory { get; set; }

        /// <summary>
        /// The total virtual memory in bytes
        /// </summary>
        public long TotalVirtualMemory { get; set; }

        /// <summary>
        /// The available virtual memory in bytes
        /// </summary>
        public long AvailableVirtualMemory { get; set; }

        /// <summary>
        /// The used virtual memory in bytes
        /// </summary>
        public long UsedVirtualMemory { get; set; }

        /// <summary>
        /// The memory usage by process ID
        /// </summary>
        public Dictionary<int, long> ProcessMemoryUsage { get; set; } = new Dictionary<int, long>();

        /// <summary>
        /// The number of memory allocations
        /// </summary>
        public int AllocationCount { get; set; }

        /// <summary>
        /// The number of memory deallocations
        /// </summary>
        public int DeallocationCount { get; set; }

        /// <summary>
        /// The timestamp when the statistics were collected
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
