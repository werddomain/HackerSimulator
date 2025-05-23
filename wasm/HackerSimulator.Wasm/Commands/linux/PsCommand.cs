using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class PsCommand : CommandBase
    {
        public PsCommand(ShellService shell, KernelService kernel) : base("ps", shell, kernel) { }

        public override string Description => "List processes";
        public override string Usage => "ps";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            foreach (var p in Kernel.Processes)
            {
                context.Stdout.WriteLine($"{p.Process.Id}\t{p.Process.Name}\t{p.Process.State}");
            }
            return Task.CompletedTask;
        }
    }
}
