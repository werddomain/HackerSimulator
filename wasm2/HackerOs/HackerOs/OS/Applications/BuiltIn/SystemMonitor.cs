using HackerOs.OS.Applications;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.Kernel.Memory;
using HackerOs.OS.User;
using System.Text;
using System.Text.Json;

namespace HackerOs.OS.Applications.BuiltIn
{
    /// <summary>
    /// System monitor application for viewing system and process information
    /// </summary>
    public class SystemMonitor : ApplicationBase
    {
        private readonly IProcessManager _processManager;
        private readonly IMemoryManager _memoryManager;
        private Timer? _refreshTimer;
        private readonly object _lockObject = new object();
        
        // Monitoring configuration
        public class MonitorSettings
        {
            public int RefreshIntervalSeconds { get; set; } = 5;
            public bool ShowSystemProcesses { get; set; } = true;
            public bool ShowUserProcesses { get; set; } = true;
            public bool AutoRefresh { get; set; } = true;
            public string SortBy { get; set; } = "Memory"; // Memory, CPU, Name, PID
            public bool SortDescending { get; set; } = true;
        }

        private MonitorSettings _settings;
        private List<ProcessInfo> _currentProcesses;
        private SystemStatistics _currentStats;

        public class SystemStatistics
        {
            public long TotalMemory { get; set; }
            public long UsedMemory { get; set; }
            public long FreeMemory { get; set; }
            public double MemoryUsagePercent { get; set; }
            public int TotalProcesses { get; set; }
            public int RunningProcesses { get; set; }
            public int SleepingProcesses { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        public class ProcessInfo
        {
            public int PID { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public long MemoryUsage { get; set; }
            public string User { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public TimeSpan RunTime { get; set; }
            public int? ParentPID { get; set; }
            public bool IsSystemProcess { get; set; }
        }

        public SystemMonitor(IProcessManager processManager, IMemoryManager memoryManager) : base()
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _settings = new MonitorSettings();
            _currentProcesses = new List<ProcessInfo>();
            _currentStats = new SystemStatistics();
        }

        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            try
            {
                // Initial data refresh
                await RefreshDataAsync();

                // Start auto-refresh timer if enabled
                if (_settings.AutoRefresh)
                {
                    StartRefreshTimer();
                }

                await OnOutputAsync("System Monitor started.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to start system monitor: {ex.Message}");
                return false;
            }
        }        protected override async Task<bool> OnStopAsync()
        {
            try
            {
                // Stop refresh timer
                _refreshTimer?.Dispose();

                await OnOutputAsync("System Monitor closed.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Error stopping system monitor: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Refresh all monitoring data
        /// </summary>
        public async Task RefreshDataAsync()
        {
            try
            {
                await RefreshProcessListAsync();
                await RefreshSystemStatsAsync();
                
                await OnOutputAsync($"System data refreshed at {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to refresh data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current process list
        /// </summary>
        public IEnumerable<ProcessInfo> GetProcessList()
        {
            lock (_lockObject)
            {
                return _currentProcesses.ToList();
            }
        }

        /// <summary>
        /// Get current system statistics
        /// </summary>
        public SystemStatistics GetSystemStatistics()
        {
            return _currentStats;
        }

        /// <summary>
        /// Kill a process by PID
        /// </summary>
        public async Task<bool> KillProcessAsync(int pid)
        {
            try
            {
                // Check if user has permission to kill the process
                var process = _currentProcesses.FirstOrDefault(p => p.PID == pid);
                if (process == null)
                {
                    await OnErrorAsync($"Process with PID {pid} not found.");
                    return false;
                }

                // System processes can only be killed by root
                if (process.IsSystemProcess && Context?.UserSession?.User?.UserId != 0)
                {
                    await OnErrorAsync("Permission denied: Cannot kill system process.");
                    return false;
                }

                await _processManager.TerminateProcessAsync(pid);
                await OnOutputAsync($"Process {pid} ({process.Name}) terminated.");
                
                // Refresh process list
                await RefreshProcessListAsync();
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to kill process {pid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get detailed process information
        /// </summary>
        public async Task<string> GetProcessDetailsAsync(int pid)
        {
            try
            {
                var process = _currentProcesses.FirstOrDefault(p => p.PID == pid);
                if (process == null)
                {
                    return $"Process with PID {pid} not found.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Process Details - PID {pid}");
                sb.AppendLine("================================");
                sb.AppendLine($"Name: {process.Name}");
                sb.AppendLine($"Status: {process.Status}");
                sb.AppendLine($"User: {process.User}");
                sb.AppendLine($"Memory Usage: {FormatBytes(process.MemoryUsage)}");
                sb.AppendLine($"Start Time: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Run Time: {process.RunTime:dd\\.hh\\:mm\\:ss}");
                sb.AppendLine($"Parent PID: {process.ParentPID?.ToString() ?? "N/A"}");
                sb.AppendLine($"System Process: {(process.IsSystemProcess ? "Yes" : "No")}");

                // Try to get additional details from process manager
                try
                {
                    var processState = _processManager.GetProcessState(pid);
                    if (processState != null)
                    {
                        sb.AppendLine($"State: {processState}");
                    }
                }
                catch
                {
                    // Process might have terminated
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting process details: {ex.Message}";
            }
        }

        /// <summary>
        /// Update monitor settings
        /// </summary>
        public async Task UpdateSettingsAsync(MonitorSettings newSettings)
        {
            _settings = newSettings;

            // Update refresh timer
            if (_settings.AutoRefresh && _refreshTimer == null)
            {
                StartRefreshTimer();
            }
            else if (!_settings.AutoRefresh && _refreshTimer != null)
            {
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
            else if (_refreshTimer != null)
            {
                // Restart timer with new interval
                _refreshTimer.Dispose();
                StartRefreshTimer();
            }

            // Re-sort process list with new settings
            SortProcessList();

            await OnOutputAsync("Monitor settings updated.");
        }

        /// <summary>
        /// Get current monitor settings
        /// </summary>
        public MonitorSettings GetSettings()
        {
            return _settings;
        }

        /// <summary>
        /// Export system information to text
        /// </summary>
        public async Task<string> ExportSystemInfoAsync()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("HackerOS System Information");
                sb.AppendLine("===========================");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                // System Statistics
                sb.AppendLine("System Statistics:");
                sb.AppendLine($"  Total Memory: {FormatBytes(_currentStats.TotalMemory)}");
                sb.AppendLine($"  Used Memory: {FormatBytes(_currentStats.UsedMemory)} ({_currentStats.MemoryUsagePercent:F1}%)");
                sb.AppendLine($"  Free Memory: {FormatBytes(_currentStats.FreeMemory)}");
                sb.AppendLine($"  Total Processes: {_currentStats.TotalProcesses}");
                sb.AppendLine($"  Running Processes: {_currentStats.RunningProcesses}");
                sb.AppendLine($"  Sleeping Processes: {_currentStats.SleepingProcesses}");
                sb.AppendLine();

                // Process List
                sb.AppendLine("Process List:");
                sb.AppendLine("PID\tName\t\tStatus\t\tMemory\t\tUser\t\tStart Time");
                sb.AppendLine("---\t----\t\t------\t\t------\t\t----\t\t----------");

                foreach (var process in _currentProcesses)
                {
                    sb.AppendLine($"{process.PID}\t{process.Name.PadRight(15)}\t{process.Status.PadRight(10)}\t{FormatBytes(process.MemoryUsage).PadRight(10)}\t{process.User.PadRight(10)}\t{process.StartTime:HH:mm:ss}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error exporting system info: {ex.Message}";
            }
        }

        private async Task RefreshProcessListAsync()
        {
            try
            {
                var processes = await _processManager.GetAllProcessesAsync();
                var processInfoList = new List<ProcessInfo>();

                foreach (var process in processes)
                {                    var info = new ProcessInfo
                    {
                        PID = process.ProcessId,
                        Name = process.Name,
                        Status = process.State.ToString(),
                        MemoryUsage = process.MemoryUsage,
                        User = process.Owner ?? "system",
                        StartTime = process.StartTime,
                        RunTime = DateTime.Now - process.StartTime,
                        ParentPID = process.ParentProcessId,
                        IsSystemProcess = process.Owner == "root" || process.Owner == "system"
                    };

                    // Filter based on settings
                    if ((!_settings.ShowSystemProcesses && info.IsSystemProcess) ||
                        (!_settings.ShowUserProcesses && !info.IsSystemProcess))
                    {
                        continue;
                    }

                    processInfoList.Add(info);
                }

                lock (_lockObject)
                {
                    _currentProcesses = processInfoList;
                    SortProcessList();
                }
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to refresh process list: {ex.Message}");
            }
        }

        private async Task RefreshSystemStatsAsync()
        {
            try
            {
                var memStats = await _memoryManager.GetMemoryStatisticsAsync();
                var processStats = await _processManager.GetProcessStatisticsAsync();                _currentStats = new SystemStatistics
                {
                    TotalMemory = memStats.TotalPhysicalMemory,
                    UsedMemory = memStats.UsedPhysicalMemory,
                    FreeMemory = memStats.AvailablePhysicalMemory,
                    MemoryUsagePercent = memStats.TotalPhysicalMemory > 0 ? 
                        (double)memStats.UsedPhysicalMemory / memStats.TotalPhysicalMemory * 100 : 0,
                    TotalProcesses = processStats.TotalProcesses,
                    RunningProcesses = processStats.RunningProcesses,
                    SleepingProcesses = processStats.SleepingProcesses,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to refresh system stats: {ex.Message}");
            }
        }

        private void SortProcessList()
        {
            if (_currentProcesses.Count == 0) return;

            switch (_settings.SortBy.ToLower())
            {
                case "memory":
                    _currentProcesses = _settings.SortDescending
                        ? _currentProcesses.OrderByDescending(p => p.MemoryUsage).ToList()
                        : _currentProcesses.OrderBy(p => p.MemoryUsage).ToList();
                    break;

                case "name":
                    _currentProcesses = _settings.SortDescending
                        ? _currentProcesses.OrderByDescending(p => p.Name).ToList()
                        : _currentProcesses.OrderBy(p => p.Name).ToList();
                    break;

                case "pid":
                    _currentProcesses = _settings.SortDescending
                        ? _currentProcesses.OrderByDescending(p => p.PID).ToList()
                        : _currentProcesses.OrderBy(p => p.PID).ToList();
                    break;

                case "user":
                    _currentProcesses = _settings.SortDescending
                        ? _currentProcesses.OrderByDescending(p => p.User).ToList()
                        : _currentProcesses.OrderBy(p => p.User).ToList();
                    break;

                default:
                    // Default sort by PID
                    _currentProcesses = _currentProcesses.OrderBy(p => p.PID).ToList();
                    break;
            }
        }

        private void StartRefreshTimer()
        {
            if (_refreshTimer != null) return;

            _refreshTimer = new Timer(RefreshCallback, null,
                TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds),
                TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds));
        }

        private async void RefreshCallback(object? state)
        {
            try
            {
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Auto-refresh failed: {ex.Message}");
            }
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";
            
            return $"{bytes} B";
        }
    }
}
