using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands
{
    /// <summary>
    /// Reads from stdin and writes uppercase output.
    /// </summary>
    public class UpperCommand : CommandBase
    {
        public UpperCommand(ShellService shell, KernelService kernel) : base("upper", shell, kernel) { }

        public override string Description => "Convert input to upper case";
        public override string Usage => "upper";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            string input = await context.Stdin.ReadToEndAsync();
            context.Stdout.Write(input.ToUpperInvariant());
        }
    }
}
