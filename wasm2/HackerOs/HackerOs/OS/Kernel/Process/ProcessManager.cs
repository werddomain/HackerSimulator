// Process Manager Implementation
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Kernel.Process
{
    /// <summary>
    /// Process manager implementation for HackerOS kernel
    /// </summary>
    public class ProcessManager : IProcessManager
    {
        private readonly ILogger<ProcessManager> _logger;
        private readonly ConcurrentDictionary<int, OSProcess> _processes = new();
        private int _nextProcessId = 1;
        private readonly object _pidLock = new();

        public event EventHandler<ProcessEvent>? OnProcessEvent;

        public ProcessManager(ILogger<ProcessManager> logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// Get the next available process ID
        /// </summary>
        private int getNextProcessId()
        {
            return Interlocked.Increment(ref _nextProcessId);
        }
        public async Task<IProcess> CreateProcessAsync(ProcessStartInfo startInfo)
        {
            var processId = AllocateProcessId();
            var process = new OSProcess(processId, startInfo, _logger);
            
            _processes[processId] = process;
            
            // Subscribe to process events
            process.OnStateChanged += (sender, e) => 
            {
                OnProcessEvent?.Invoke(this, new ProcessEvent
                {
                    EventType = ProcessEventType.StateChanged,
                    ProcessId = processId,
                    Message = $"Process {processId} state changed to {e.NewState}",
                    Data = e
                });
            };

            await process.StartAsync();
            
            OnProcessEvent?.Invoke(this, new ProcessEvent
            {
                EventType = ProcessEventType.Created,
                ProcessId = processId,
                Message = $"Process {processId} ({startInfo.Name}) created"
            });

            _logger.LogInformation("Created process {ProcessId} ({Name}) for user {Owner}", 
                processId, startInfo.Name, startInfo.Owner);
            
            return process;
        }

        public async Task<bool> TerminateProcessAsync(int processId, int exitCode = 0)
        {
            if (!_processes.TryGetValue(processId, out var process))
            {
                _logger.LogWarning("Attempted to terminate non-existent process {ProcessId}", processId);
                return false;
            }

            await process.TerminateAsync(exitCode);
            
            OnProcessEvent?.Invoke(this, new ProcessEvent
            {
                EventType = ProcessEventType.Terminated,
                ProcessId = processId,
                Message = $"Process {processId} terminated with exit code {exitCode}"
            });

            _logger.LogInformation("Terminated process {ProcessId} with exit code {ExitCode}", processId, exitCode);
            return true;
        }

        public IProcess? GetProcess(int processId)
        {
            return _processes.TryGetValue(processId, out var process) ? process : null;
        }

        public IReadOnlyList<IProcess> GetAllProcesses()
        {
            return _processes.Values.Cast<IProcess>().ToList().AsReadOnly();
        }

        public IReadOnlyList<IProcess> GetProcessesByUser(string username)
        {
            return _processes.Values
                .Where(p => p.Owner.Equals(username, StringComparison.OrdinalIgnoreCase))
                .Cast<IProcess>()
                .ToList()
                .AsReadOnly();
        }

        public int AllocateProcessId()
        {
            return getNextProcessId();
        }

        public int GetCurrentProcessId()
        {
            // In a real OS, this would return the actual current process ID
            // For simulation, we'll return the first running process or 1
            var currentProcess = _processes.Values.FirstOrDefault(p => p.State == ProcessState.Running);
            return currentProcess?.ProcessId ?? 1;
        }

        /// <summary>
        /// Get the state of a process by ID
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        /// <returns>The current state of the process, or null if the process doesn't exist</returns>
        public ProcessState? GetProcessState(int processId)
        {
            if (_processes.TryGetValue(processId, out var process))
            {
                return process.State;
            }
            
            return null;
        }

        /// <summary>
        /// Get all processes asynchronously
        /// </summary>
        /// <returns>A list of all processes</returns>
        public Task<IReadOnlyList<IProcess>> GetAllProcessesAsync()
        {
            var processList = _processes.Values
                .Cast<IProcess>()
                .ToList()
                .AsReadOnly();
                
            return Task.FromResult<IReadOnlyList<IProcess>>(processList);
        }        /// <summary>
        /// Get process statistics asynchronously
        /// </summary>
        /// <returns>Process statistics information</returns>
        public Task<ProcessStatistics> GetProcessStatisticsAsync()
        {
            var stats = new ProcessStatistics
            {
                TotalProcesses = _processes.Count,
                RunningProcesses = _processes.Values.Count(p => p.State == ProcessState.Running),
                SleepingProcesses = _processes.Values.Count(p => p.State == ProcessState.Sleeping),
                ZombieProcesses = _processes.Values.Count(p => p.State == ProcessState.Zombie),
                StoppedProcesses = _processes.Values.Count(p => p.State == ProcessState.Stopped),
                TotalCpuTime = TimeSpan.FromSeconds(_processes.Values.Sum(p => (DateTime.UtcNow - p.StartTime).TotalSeconds)),
                TotalMemoryUsage = 0, // Could calculate based on actual memory usage if available
                Timestamp = DateTime.UtcNow
            };
            
            return Task.FromResult(stats);
        }
    }

    /// <summary>
    /// OS Process implementation
    /// </summary>
    internal class OSProcess : IProcess
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private DateTime _startTime = DateTime.UtcNow;
        private ProcessState _state = ProcessState.Creating;
        private long _memoryUsage = 0;
        private TimeSpan _cpuTime = TimeSpan.Zero;

        public event EventHandler<ProcessStateChangedEventArgs>? OnStateChanged;

        public int ProcessId { get; }
        public int ParentProcessId { get; }
        public string Name { get; }
        public string Owner { get; }
        public ProcessState State 
        { 
            get => _state;
            private set
            {
                var oldState = _state;
                _state = value;
                OnStateChanged?.Invoke(this, new ProcessStateChangedEventArgs(oldState, value));
            }
        }
        public DateTime StartTime => _startTime;
        public long MemoryUsage => _memoryUsage;
        public TimeSpan CpuTime => _cpuTime;
        public string CommandLine { get; }
        public string WorkingDirectory { get; set; }
        public Dictionary<string, string> Environment { get; }

        public OSProcess(int processId, ProcessStartInfo startInfo, ILogger logger)
        {
            ProcessId = processId;
            ParentProcessId = startInfo.ParentProcessId;
            Name = startInfo.Name;
            Owner = startInfo.Owner;
            CommandLine = $"{startInfo.ExecutablePath} {string.Join(" ", startInfo.Arguments)}";
            WorkingDirectory = startInfo.WorkingDirectory;
            Environment = new Dictionary<string, string>(startInfo.Environment);
            _logger = logger;

            // Simulate initial memory allocation
            _memoryUsage = Random.Shared.Next(1024 * 1024, 10 * 1024 * 1024); // 1-10 MB
        }

        public async Task StartAsync()
        {
            State = ProcessState.Running;
            _startTime = DateTime.UtcNow;
            
            // Start background CPU time simulation
            _ = Task.Run(SimulateCpuUsage, _cancellationTokenSource.Token);
            
            _logger.LogDebug("Process {ProcessId} ({Name}) started", ProcessId, Name);
        }

        public async Task<int> WaitForExitAsync()
        {
            // Wait for cancellation (process termination)
            try
            {
                await Task.Delay(-1, _cancellationTokenSource.Token);
                return 0;
            }
            catch (OperationCanceledException)
            {
                return State == ProcessState.Terminated ? 0 : -1;
            }
        }

        public async Task SendSignalAsync(ProcessSignal signal)
        {
            _logger.LogDebug("Process {ProcessId} received signal {Signal}", ProcessId, signal);
            
            switch (signal)
            {
                case ProcessSignal.SIGTERM:
                case ProcessSignal.SIGKILL:
                    await TerminateAsync(signal == ProcessSignal.SIGKILL ? -9 : 0);
                    break;
                case ProcessSignal.SIGSTOP:
                    State = ProcessState.Stopped;
                    break;
                case ProcessSignal.SIGCONT:
                    if (State == ProcessState.Stopped)
                        State = ProcessState.Running;
                    break;
                case ProcessSignal.SIGINT:
                    await TerminateAsync(130); // 128 + 2 (SIGINT)
                    break;
            }
        }

        public async Task TerminateAsync(int exitCode)
        {
            State = ProcessState.Terminated;
            _cancellationTokenSource.Cancel();
            
            _logger.LogDebug("Process {ProcessId} ({Name}) terminated with exit code {ExitCode}", 
                ProcessId, Name, exitCode);
        }

        private async Task SimulateCpuUsage()
        {
            var startTime = DateTime.UtcNow;
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                    
                    // Simulate CPU time accumulation
                    var elapsed = DateTime.UtcNow - startTime;
                    _cpuTime = TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds * Random.Shared.NextDouble() * 0.1);
                    
                    // Simulate memory usage fluctuation
                    _memoryUsage += Random.Shared.Next(-1024, 1024);
                    if (_memoryUsage < 1024 * 1024) _memoryUsage = 1024 * 1024; // Min 1MB
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Process state change event arguments
    /// </summary>
    public class ProcessStateChangedEventArgs : EventArgs
    {
        public ProcessState OldState { get; }
        public ProcessState NewState { get; }

        public ProcessStateChangedEventArgs(ProcessState oldState, ProcessState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }}
