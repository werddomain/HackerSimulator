// Process Manager Interface - Process lifecycle management
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Applications;

namespace HackerOs.OS.Kernel.Process
{
    /// <summary>
    /// Process management interface for kernel services
    /// </summary>
    public interface IProcessManager
    {
        /// <summary>
        /// Create a new process
        /// </summary>
        Task<IProcess> CreateProcessAsync(ProcessStartInfo startInfo);

        /// <summary>
        /// Terminate a process
        /// </summary>
        Task<bool> TerminateProcessAsync(int processId, int exitCode = 0);

        /// <summary>
        /// Get process by ID
        /// </summary>
        IProcess? GetProcess(int processId);

        /// <summary>
        /// Get all processes
        /// </summary>
        IReadOnlyList<IProcess> GetAllProcesses();

        /// <summary>
        /// Get processes by user
        /// </summary>
        IReadOnlyList<IProcess> GetProcessesByUser(string username);

        /// <summary>
        /// Allocate new process ID
        /// </summary>
        int AllocateProcessId();

        /// <summary>
        /// Get current process ID
        /// </summary>
        int GetCurrentProcessId();

        /// <summary>
        /// Process events
        /// </summary>
        event EventHandler<ProcessEvent> OnProcessEvent;

        /// <summary>
        /// Get the state of a process by ID
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        /// <returns>The current state of the process, or null if the process doesn't exist</returns>
        ProcessState? GetProcessState(int processId);

        /// <summary>
        /// Get all processes asynchronously
        /// </summary>
        /// <returns>A list of all processes</returns>
        Task<IReadOnlyList<IProcess>> GetAllProcessesAsync();

        /// <summary>
        /// Get process statistics asynchronously
        /// </summary>
        /// <returns>Process statistics information</returns>
        Task<ProcessStatistics> GetProcessStatisticsAsync();
    }

    /// <summary>
    /// Process interface representing a running application
    /// </summary>
    public interface IProcess
    {
        /// <summary>
        /// Process identifier
        /// </summary>
        int ProcessId { get; }

        /// <summary>
        /// Parent process ID
        /// </summary>
        int ParentProcessId { get; }

        /// <summary>
        /// Process name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// User who owns this process
        /// </summary>
        string Owner { get; }

        /// <summary>
        /// Current process state
        /// </summary>
        ProcessState State { get; }

        /// <summary>
        /// Process start time
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// Memory usage in bytes
        /// </summary>
        long MemoryUsage { get; }

        /// <summary>
        /// CPU time used
        /// </summary>
        TimeSpan CpuTime { get; }

        /// <summary>
        /// Process command line
        /// </summary>
        string CommandLine { get; }

        /// <summary>
        /// Process working directory
        /// </summary>
        string WorkingDirectory { get; set; }

        /// <summary>
        /// Environment variables
        /// </summary>
        Dictionary<string, string> Environment { get; }

        /// <summary>
        /// Send signal to process
        /// </summary>
        Task SendSignalAsync(ProcessSignal signal);

        /// <summary>
        /// Wait for process to exit
        /// </summary>
        Task<int> WaitForExitAsync();
    }

    /// <summary>
    /// Process state enumeration
    /// </summary>
    public enum ProcessState
    {
        Creating,
        Running,
        Sleeping,
        Waiting,
        Stopped,
        Zombie,
        Terminated
    }

    /// <summary>
    /// Process signals (Unix-like)
    /// </summary>
    public enum ProcessSignal
    {
        SIGTERM = 15,  // Termination signal
        SIGKILL = 9,   // Kill signal
        SIGSTOP = 19,  // Stop signal
        SIGCONT = 18,  // Continue signal
        SIGINT = 2,    // Interrupt signal
        SIGHUP = 1     // Hangup signal
    }

    /// <summary>
    /// Process start information
    /// </summary>
    public class ProcessStartInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string[] Arguments { get; set; } = Array.Empty<string>();
        public string WorkingDirectory { get; set; } = "/";
        public string Owner { get; set; } = "root";
        public int ParentProcessId { get; set; } = 0;
        public Dictionary<string, string> Environment { get; set; } = new();
        public bool CreateWindow { get; set; } = true;
        public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;
        public bool IsBackground { get; set; } = false;
    }

    /// <summary>
    /// Process event information
    /// </summary>
    public class ProcessEvent
    {
        public ProcessEventType EventType { get; set; }
        public int ProcessId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Data { get; set; }
    }

    /// <summary>
    /// Process event types
    /// </summary>
    public enum ProcessEventType
    {
        Created,
        Started,
        Exited,
        Terminated,
        StateChanged,
        Error
    }

    /// <summary>
    /// Process statistics information
    /// </summary>
    public class ProcessStatistics
    {
        /// <summary>
        /// The total number of processes
        /// </summary>
        public int TotalProcesses { get; set; }

        /// <summary>
        /// The number of running processes
        /// </summary>
        public int RunningProcesses { get; set; }

        /// <summary>
        /// The number of sleeping processes
        /// </summary>
        public int SleepingProcesses { get; set; }

        /// <summary>
        /// The number of stopped processes
        /// </summary>
        public int StoppedProcesses { get; set; }

        /// <summary>
        /// The number of zombie processes
        /// </summary>
        public int ZombieProcesses { get; set; }

        /// <summary>
        /// The total CPU time used by all processes
        /// </summary>
        public TimeSpan TotalCpuTime { get; set; }

        /// <summary>
        /// The total memory used by all processes
        /// </summary>
        public long TotalMemoryUsage { get; set; }

        /// <summary>
        /// The timestamp when the statistics were collected
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
