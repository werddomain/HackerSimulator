using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class KillCommand : CommandBase
    {
        public KillCommand(ShellService shell, KernelService kernel) : base("kill", shell, kernel) { }

        public override string Description => "Terminate a process";
        public override string Usage => "kill <pid>";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0 || !System.Guid.TryParse(args[0], out var pid))
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return Task.CompletedTask;
            }
            if (Kernel.KillProcess(pid))
                context.Stdout.WriteLine("Process terminated");
            else
                context.Stderr.WriteLine("Process not found");
            return Task.CompletedTask;
        }
    }
}
