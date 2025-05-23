using System;
using System.Threading;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Apps
{
    public partial class SystemMonitorApp : Windows.WindowBase, IDisposable
    {
        private Timer? _timer;
        private int _cpuUsage;
        private int _memUsage;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "System Monitor";
            _timer = new Timer(UpdateUsage, null, 0, 2000);
        }

        private void UpdateUsage(object? state)
        {
            var rnd = new Random();
            _cpuUsage = rnd.Next(0, 100);
            _memUsage = rnd.Next(0, 100);
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
