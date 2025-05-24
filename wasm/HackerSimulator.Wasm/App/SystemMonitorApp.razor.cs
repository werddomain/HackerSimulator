using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HackerSimulator.Wasm.Core;
using MudBlazor;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:chart-line")]
    public partial class SystemMonitorApp : Windows.WindowBase, IDisposable
    {

        private Timer? _timer;
        private readonly Dictionary<Guid, ProcessMetrics> _metrics = new();
        private readonly List<double> _cpuHistory = new();
        private readonly List<double> _memHistory = new();
        private readonly List<double> _diskHistory = new();
        private readonly List<double> _netHistory = new();
        private const int MaxHistory = 30;
        private readonly Random _rand = new();

        private double _cpuUsage;
        private double _memUsage;
        private double _diskUsage;
        private double _networkUsage;

        private List<ProcessItem> _processList = new();
        private List<DiskActivityItem> _diskActivity = new();
        private List<ConnectionItem> _connections = new();

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "System Monitor";
            _timer = new Timer(Update, null, 0, 1000);
        }

        private void Update(object? state)
        {
            var processes = Kernel.Processes;

            foreach (var p in processes)
            {
                if (!_metrics.TryGetValue(p.Process.Id, out var m))
                {
                    m = new ProcessMetrics(p.Process.Id, p.Process.Name)
                    {
                        Cpu = _rand.NextDouble() * 10,
                        Memory = _rand.NextDouble() * 10,
                        State = p.Process.State.ToString()
                    };
                    _metrics[p.Process.Id] = m;
                }
                else
                {
                    m.Cpu = Clamp(m.Cpu + (_rand.NextDouble() - 0.5) * 5, 0, 50);
                    m.Memory = Clamp(m.Memory + (_rand.NextDouble() - 0.5) * 2, 0, 50);
                    m.State = p.Process.State.ToString();
                }
            }

            var ids = processes.Select(p => p.Process.Id).ToHashSet();
            foreach (var id in _metrics.Keys.Where(k => !ids.Contains(k)).ToList())
                _metrics.Remove(id);

            _cpuUsage = _metrics.Values.Sum(x => x.Cpu);
            if (_cpuUsage > 100) _cpuUsage = 100;
            _memUsage = _metrics.Values.Sum(x => x.Memory);
            if (_memUsage > 100) _memUsage = 100;

            _diskUsage = Clamp(_diskUsage + (_rand.NextDouble() - 0.5) * 5, 0, 100);
            _networkUsage = Clamp(_networkUsage + (_rand.NextDouble() - 0.5) * 5, 0, 100);

            _cpuHistory.Add(_cpuUsage);
            _memHistory.Add(_memUsage);
            _diskHistory.Add(_diskUsage);
            _netHistory.Add(_networkUsage);
            if (_cpuHistory.Count > MaxHistory)
            {
                _cpuHistory.RemoveAt(0);
                _memHistory.RemoveAt(0);
                _diskHistory.RemoveAt(0);
                _netHistory.RemoveAt(0);
            }

            _processList = _metrics.Values.Select(m => new ProcessItem
            {
                Id = m.Id,
                Name = m.Name,
                State = m.State,
                Cpu = m.Cpu,
                Memory = m.Memory
            }).ToList();

            _diskActivity = _processList
                .OrderByDescending(p => p.Cpu)
                .Take(5)
                .Select(p => new DiskActivityItem
                {
                    Name = p.Name,
                    ReadKB = Math.Abs(p.Cpu * 20 * _rand.NextDouble()),
                    WriteKB = Math.Abs(p.Cpu * 15 * _rand.NextDouble())
                }).ToList();

            _connections = _processList
                .OrderBy(_ => _rand.NextDouble())
                .Take(5)
                .Select(p => new ConnectionItem
                {
                    Process = p.Name,
                    Protocol = _rand.Next(2) == 0 ? "TCP" : "UDP",
                    Local = $"127.0.0.1:{_rand.Next(1024,65000)}",
                    Remote = $"{_rand.Next(1,255)}.{_rand.Next(1,255)}.{_rand.Next(1,255)}.{_rand.Next(1,255)}:{_rand.Next(20,9000)}"
                }).ToList();

            InvokeAsync(StateHasChanged);
        }

        private static double Clamp(double val, double min, double max)
            => val < min ? min : val > max ? max : val;

        private void EndProcess(Guid id)
        {
            Kernel.KillProcess(id);
        }

        private string[] _chartLabels => Enumerable.Range(0, _cpuHistory.Count)
            .Select(i => i.ToString())
            .ToArray();

        private ChartSeries[] _chartData => new[]
        {
            new ChartSeries { Name = "CPU", Data = _cpuHistory.ToArray() },
            new ChartSeries { Name = "Memory", Data = _memHistory.ToArray() },
            new ChartSeries { Name = "Disk", Data = _diskHistory.ToArray() },
            new ChartSeries { Name = "Network", Data = _netHistory.ToArray() }
        };

        private ChartSeries[] _networkChartData => new[]
        {
            new ChartSeries { Name = "Network", Data = _netHistory.ToArray() }
        };

        public new void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }

        private record ProcessMetrics(Guid Id, string Name)
        {
            public double Cpu { get; set; }
            public double Memory { get; set; }
            public string State { get; set; } = string.Empty;
        }

        private class ProcessItem
        {
            public Guid Id { get; set; }
            public string Pid => Id.ToString()[..8];
            public string Name { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public double Cpu { get; set; }
            public double Memory { get; set; }
        }

        private class DiskActivityItem
        {
            public string Name { get; set; } = string.Empty;
            public double ReadKB { get; set; }
            public double WriteKB { get; set; }
        }

        private class ConnectionItem
        {
            public string Process { get; set; } = string.Empty;
            public string Protocol { get; set; } = string.Empty;
            public string Local { get; set; } = string.Empty;
            public string Remote { get; set; } = string.Empty;
        }
    }
}
