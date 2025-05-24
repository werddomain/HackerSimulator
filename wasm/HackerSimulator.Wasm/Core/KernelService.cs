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

        public ProcessInfo RegisterProcess(ProcessBase process)
        {
            var cts = new CancellationTokenSource();
            var info = new ProcessInfo(process, cts);
            _processes.Add(info);
            return info;
        }

        public void UnregisterProcess(System.Guid id)
        {
            var info = _processes.FirstOrDefault(p => p.Process.Id == id);
            if (info != null)
            {
                _processes.Remove(info);
            }
        }

        public Task RunProcess(ProcessBase process, string[] args)
        {
            return process.StartAsync(args);
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
