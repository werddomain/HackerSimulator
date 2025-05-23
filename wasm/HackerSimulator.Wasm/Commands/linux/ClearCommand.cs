using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class ClearCommand : CommandBase
    {
        public ClearCommand(ShellService shell, KernelService kernel) : base("clear", shell, kernel) { }

        public override string Description => "Clear the screen";
        public override string Usage => "clear";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            context.Stdout.Write("\x1B[2J\x1B[0f");
            return Task.CompletedTask;
        }
    }
}
