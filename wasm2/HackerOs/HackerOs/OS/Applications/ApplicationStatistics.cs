namespace HackerOs.OS.Applications;

/// <summary>
/// Runtime statistics for an application
/// </summary>
public class ApplicationStatistics
{
    /// <summary>
    /// Application ID
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// Process ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// When the application was started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Total running time
    /// </summary>
    public TimeSpan RunningTime => DateTime.UtcNow - StartTime;

    /// <summary>
    /// Current memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Peak memory usage in bytes
    /// </summary>
    public long PeakMemoryUsageBytes { get; set; }

    /// <summary>
    /// Total CPU time used
    /// </summary>
    public TimeSpan CpuTime { get; set; }

    /// <summary>
    /// Current CPU usage percentage (0-100)
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Number of threads
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Number of file handles open
    /// </summary>
    public int FileHandleCount { get; set; }

    /// <summary>
    /// Number of network connections
    /// </summary>
    public int NetworkConnectionCount { get; set; }

    /// <summary>
    /// Bytes read from disk
    /// </summary>
    public long DiskReadBytes { get; set; }

    /// <summary>
    /// Bytes written to disk
    /// </summary>
    public long DiskWriteBytes { get; set; }

    /// <summary>
    /// Bytes sent over network
    /// </summary>
    public long NetworkSentBytes { get; set; }

    /// <summary>
    /// Bytes received over network
    /// </summary>
    public long NetworkReceivedBytes { get; set; }

    /// <summary>
    /// Number of times the application has been paused
    /// </summary>
    public int PauseCount { get; set; }

    /// <summary>
    /// Total time the application has been paused
    /// </summary>
    public TimeSpan TotalPausedTime { get; set; }

    /// <summary>
    /// Number of errors encountered
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of warnings encountered
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Application priority
    /// </summary>
    public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;

    /// <summary>
    /// Current state of the application
    /// </summary>
    public ApplicationState State { get; set; } = ApplicationState.Stopped;

    /// <summary>
    /// Additional custom metrics
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = new();

    /// <summary>
    /// Get memory usage in megabytes
    /// </summary>
    public double MemoryUsageMB => MemoryUsageBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Get peak memory usage in megabytes
    /// </summary>
    public double PeakMemoryUsageMB => PeakMemoryUsageBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Check if the application is idle (no activity for specified time)
    /// </summary>
    /// <param name="idleThreshold">Time threshold for considering idle</param>
    /// <returns>True if application is idle</returns>
    public bool IsIdle(TimeSpan idleThreshold)
    {
        return DateTime.UtcNow - LastActivity > idleThreshold;
    }

    /// <summary>
    /// Update last activity timestamp
    /// </summary>
    public void UpdateActivity()
    {
        LastActivity = DateTime.UtcNow;
    }
}

/// <summary>
/// Statistics for the application manager
/// </summary>
public class ApplicationManagerStatistics
{
    /// <summary>
    /// Total number of registered applications
    /// </summary>
    public int RegisteredApplicationCount { get; set; }

    /// <summary>
    /// Total number of running applications
    /// </summary>
    public int RunningApplicationCount { get; set; }

    /// <summary>
    /// Total number of applications launched since system start
    /// </summary>
    public int TotalApplicationsLaunched { get; set; }

    /// <summary>
    /// Total number of applications terminated since system start
    /// </summary>
    public int TotalApplicationsTerminated { get; set; }

    /// <summary>
    /// Number of application crashes
    /// </summary>
    public int CrashCount { get; set; }

    /// <summary>
    /// Total memory used by all applications
    /// </summary>
    public long TotalMemoryUsageBytes { get; set; }

    /// <summary>
    /// Total CPU time used by all applications
    /// </summary>
    public TimeSpan TotalCpuTime { get; set; }

    /// <summary>
    /// System uptime
    /// </summary>
    public TimeSpan SystemUptime { get; set; }

    /// <summary>
    /// Applications by type
    /// </summary>
    public Dictionary<ApplicationType, int> ApplicationsByType { get; set; } = new();

    /// <summary>
    /// Applications by state
    /// </summary>
    public Dictionary<ApplicationState, int> ApplicationsByState { get; set; } = new();

    /// <summary>
    /// Average application startup time
    /// </summary>
    public TimeSpan AverageStartupTime { get; set; }

    /// <summary>
    /// Get total memory usage in megabytes
    /// </summary>
    public double TotalMemoryUsageMB => TotalMemoryUsageBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Get application crash rate (crashes per hour)
    /// </summary>
    public double CrashRate => SystemUptime.TotalHours > 0 ? CrashCount / SystemUptime.TotalHours : 0;

    /// <summary>
    /// Get applications launched per hour
    /// </summary>
    public double LaunchRate => SystemUptime.TotalHours > 0 ? TotalApplicationsLaunched / SystemUptime.TotalHours : 0;
}
