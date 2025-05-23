using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class HelpCommand : CommandBase
    {
        public HelpCommand(ShellService shell, KernelService kernel) : base("help", shell, kernel) { }

        public override string Description => "Display available commands";
        public override string Usage => "help";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            foreach (var cmd in Shell.GetCommands())
            {
                context.Stdout.WriteLine($"{cmd.Name}\t{cmd.Description}");
            }
            return Task.CompletedTask;
        }
    }
}
