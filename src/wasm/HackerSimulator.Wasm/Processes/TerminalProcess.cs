using System.Threading;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Processes
{
    public class TerminalProcess : ProcessBase
    {
        public TerminalProcess() : base("terminal")
        {
        }

        protected override Task RunAsync(string[] args, CancellationToken token)
        {
            // In a real implementation this would start an interactive terminal
            System.Console.WriteLine("Terminal started");
            return Task.CompletedTask;
        }
    }
}
