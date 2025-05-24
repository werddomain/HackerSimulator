using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class ChmodCommand : CommandBase
    {
        public ChmodCommand(ShellService shell, KernelService kernel) : base("chmod", shell, kernel) { }

        public override string Description => "Change file mode bits";
        public override string Usage => "chmod MODE FILE";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            context.Stderr.WriteLine("chmod: operation not supported");
            return Task.CompletedTask;
        }
    }
}
