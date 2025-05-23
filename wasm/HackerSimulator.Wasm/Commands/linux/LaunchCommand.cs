using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class LaunchCommand : CommandBase
    {
        public LaunchCommand(ShellService shell, KernelService kernel) : base("launch", shell, kernel) { }

        public override string Description => "Launch a process";
        public override string Usage => "launch <process> [args...]";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }
            var name = args[0];
            var pArgs = args.Length > 1 ? args[1..] : System.Array.Empty<string>();
            await Shell.Run(name, pArgs, null);
        }
    }
}
