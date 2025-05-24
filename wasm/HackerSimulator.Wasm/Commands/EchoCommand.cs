using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands
{
    /// <summary>
    /// Simple echo command used for testing pipeline support.
    /// </summary>
    public class EchoCommand : CommandBase
    {
        public EchoCommand(ShellService shell, KernelService kernel) : base("echo", shell, kernel) { }

        public override string Description => "Echo the input arguments";
        public override string Usage => "echo [text]";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            var text = string.Join(" ", args);
            context.Stdout.WriteLine(text);
            return Task.CompletedTask;
        }
    }
}
