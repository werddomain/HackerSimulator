using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class EchoCommand : CommandBase
    {
        public EchoCommand(ShellService shell, KernelService kernel) : base("echo", shell, kernel) { }

        public override string Description => "Display text";
        public override string Usage => "echo [text]";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            var output = string.Join(' ', args);
            context.Stdout.WriteLine(output);
            return Task.CompletedTask;
        }
    }
}
