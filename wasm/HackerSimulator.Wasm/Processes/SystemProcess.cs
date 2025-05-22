using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Processes
{
    /// <summary>
    /// Process used by the desktop UI to launch other processes.
    /// </summary>
    public class SystemProcess : ProcessBase
    {
        private readonly ShellService _shell;

        public SystemProcess(ShellService shell) : base("system")
        {
            _shell = shell;
        }

        protected override async Task RunAsync(string[] args, CancellationToken token)
        {
            if (args.Length == 0)
                return;

            var target = args[0];
            var remaining = args.Skip(1).ToArray();
            await _shell.Run(target, remaining, this);
        }
    }
}
