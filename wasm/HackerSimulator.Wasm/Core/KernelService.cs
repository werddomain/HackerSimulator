using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Core
{
    public class KernelService
    {
        private readonly List<ProcessInfo> _processes = new();

        public IReadOnlyList<ProcessInfo> Processes => _processes;

        public Task RunProcess(ProcessBase process, string[] args)
        {
            var cts = new CancellationTokenSource();
            var info = new ProcessInfo(process, cts);
            _processes.Add(info);
            info.Start(args);
            return Task.CompletedTask;
        }

        public bool KillProcess(System.Guid id)
        {
            var info = _processes.FirstOrDefault(p => p.Process.Id == id);
            if (info == null)
                return false;

            info.CancellationTokenSource.Cancel();
            return true;
        }
    }
}
