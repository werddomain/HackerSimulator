using System.Collections.Concurrent;
using System.Diagnostics;

namespace BlazorTerminal.Services
{
    /// <summary>
    /// Service for profiling terminal performance
    /// </summary>
    public class PerformanceProfiler
    {
        private readonly Dictionary<string, PerformanceMetric> _metrics = new Dictionary<string, PerformanceMetric>();
        private readonly object _metricsLock = new object();
        private readonly bool _profilingEnabled;
        
        /// <summary>
        /// Creates a new performance profiler
        /// </summary>
        /// <param name="enabled">Whether profiling is enabled</param>
        public PerformanceProfiler(bool enabled = true)
        {
            _profilingEnabled = enabled;
        }
        
        /// <summary>
        /// Starts timing an operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <returns>Disposable timer</returns>
        public IDisposable StartTiming(string operationName)
        {
            if (!_profilingEnabled)
                return new NullTimer();
                
            return new PerformanceTimer(this, operationName);
        }
        
        /// <summary>
        /// Records a timing measurement
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="elapsedMs">Elapsed time in milliseconds</param>
        public void RecordTiming(string operationName, double elapsedMs)
        {
            if (!_profilingEnabled)
                return;
                
            lock (_metricsLock)
            {
                if (!_metrics.TryGetValue(operationName, out var metric))
                {
                    metric = new PerformanceMetric(operationName);
                    _metrics[operationName] = metric;
                }
                
                metric.RecordTiming(elapsedMs);
            }
        }
        
        /// <summary>
        /// Records a counter increment
        /// </summary>
        /// <param name="counterName">Name of the counter</param>
        /// <param name="increment">Amount to increment</param>
        public void IncrementCounter(string counterName, long increment = 1)
        {
            if (!_profilingEnabled)
                return;
                
            lock (_metricsLock)
            {
                if (!_metrics.TryGetValue(counterName, out var metric))
                {
                    metric = new PerformanceMetric(counterName);
                    _metrics[counterName] = metric;
                }
                
                metric.IncrementCounter(increment);
            }
        }
        
        /// <summary>
        /// Gets all recorded metrics
        /// </summary>
        /// <returns>Array of performance metrics</returns>
        public PerformanceMetric[] GetMetrics()
        {
            lock (_metricsLock)
            {
                return _metrics.Values.ToArray();
            }
        }
        
        /// <summary>
        /// Gets a specific metric by name
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <returns>Performance metric or null if not found</returns>
        public PerformanceMetric? GetMetric(string name)
        {
            lock (_metricsLock)
            {
                return _metrics.TryGetValue(name, out var metric) ? metric : null;
            }
        }
        
        /// <summary>
        /// Clears all recorded metrics
        /// </summary>
        public void ClearMetrics()
        {
            lock (_metricsLock)
            {
                _metrics.Clear();
            }
        }
        
        /// <summary>
        /// Gets a performance summary
        /// </summary>
        /// <returns>String summary of performance metrics</returns>
        public string GetPerformanceSummary()
        {
            lock (_metricsLock)
            {
                if (_metrics.Count == 0)
                    return "No performance metrics recorded.";
                    
                var summary = new System.Text.StringBuilder();
                summary.AppendLine("Performance Metrics Summary:");
                summary.AppendLine("===========================");
                
                foreach (var metric in _metrics.Values.OrderBy(m => m.Name))
                {
                    summary.AppendLine(metric.ToString());
                }
                
                return summary.ToString();
            }
        }
    }
    
    /// <summary>
    /// Represents a performance metric
    /// </summary>
    public class PerformanceMetric
    {
        private readonly List<double> _timings = new List<double>();
        private long _counter = 0;
        private readonly object _lock = new object();
        
        /// <summary>
        /// Gets the metric name
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Gets the number of timing recordings
        /// </summary>
        public int TimingCount
        {
            get
            {
                lock (_lock)
                {
                    return _timings.Count;
                }
            }
        }
        
        /// <summary>
        /// Gets the counter value
        /// </summary>
        public long CounterValue
        {
            get
            {
                lock (_lock)
                {
                    return _counter;
                }
            }
        }
        
        /// <summary>
        /// Gets the average timing in milliseconds
        /// </summary>
        public double AverageMs
        {
            get
            {
                lock (_lock)
                {
                    return _timings.Count > 0 ? _timings.Average() : 0.0;
                }
            }
        }
        
        /// <summary>
        /// Gets the minimum timing in milliseconds
        /// </summary>
        public double MinMs
        {
            get
            {
                lock (_lock)
                {
                    return _timings.Count > 0 ? _timings.Min() : 0.0;
                }
            }
        }
        
        /// <summary>
        /// Gets the maximum timing in milliseconds
        /// </summary>
        public double MaxMs
        {
            get
            {
                lock (_lock)
                {
                    return _timings.Count > 0 ? _timings.Max() : 0.0;
                }
            }
        }
        
        /// <summary>
        /// Gets the total timing in milliseconds
        /// </summary>
        public double TotalMs
        {
            get
            {
                lock (_lock)
                {
                    return _timings.Sum();
                }
            }
        }
        
        /// <summary>
        /// Creates a new performance metric
        /// </summary>
        /// <param name="name">Metric name</param>
        public PerformanceMetric(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        
        /// <summary>
        /// Records a timing measurement
        /// </summary>
        /// <param name="elapsedMs">Elapsed time in milliseconds</param>
        public void RecordTiming(double elapsedMs)
        {
            lock (_lock)
            {
                _timings.Add(elapsedMs);
                
                // Limit timing history to prevent memory growth
                if (_timings.Count > 1000)
                {
                    _timings.RemoveRange(0, 500);
                }
            }
        }
        
        /// <summary>
        /// Increments the counter
        /// </summary>
        /// <param name="increment">Amount to increment</param>
        public void IncrementCounter(long increment = 1)
        {
            lock (_lock)
            {
                _counter += increment;
            }
        }
        
        /// <summary>
        /// Returns a string representation of the metric
        /// </summary>
        public override string ToString()
        {
            lock (_lock)
            {
                if (_timings.Count > 0 && _counter > 0)
                {
                    return $"{Name}: Counter={_counter}, Timings: Count={_timings.Count}, Avg={AverageMs:F2}ms, Min={MinMs:F2}ms, Max={MaxMs:F2}ms, Total={TotalMs:F2}ms";
                }
                else if (_timings.Count > 0)
                {
                    return $"{Name}: Timings: Count={_timings.Count}, Avg={AverageMs:F2}ms, Min={MinMs:F2}ms, Max={MaxMs:F2}ms, Total={TotalMs:F2}ms";
                }
                else if (_counter > 0)
                {
                    return $"{Name}: Counter={_counter}";
                }
                else
                {
                    return $"{Name}: No data recorded";
                }
            }
        }
    }
    
    /// <summary>
    /// Performance timer for measuring operation duration
    /// </summary>
    internal class PerformanceTimer : IDisposable
    {
        private readonly PerformanceProfiler _profiler;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed = false;
        
        public PerformanceTimer(PerformanceProfiler profiler, string operationName)
        {
            _profiler = profiler;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _profiler.RecordTiming(_operationName, _stopwatch.Elapsed.TotalMilliseconds);
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Null timer for when profiling is disabled
    /// </summary>
    internal class NullTimer : IDisposable
    {
        public void Dispose()
        {
            // Do nothing
        }
    }
}
