using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Core
{
    public class KernelService
    {
        private readonly List<ProcessBase> _processes = new();

        public IReadOnlyList<ProcessBase> Processes => _processes;

        public async Task RunProcess(ProcessBase process, string[] args)
        {
            _processes.Add(process);
            await process.StartAsync(args);
        }
    }
}
