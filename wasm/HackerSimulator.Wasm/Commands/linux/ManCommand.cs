using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class ManCommand : CommandBase
    {
        public ManCommand(ShellService shell, KernelService kernel) : base("man", shell, kernel) { }

        public override string Description => "Display command help";
        public override string Usage => "man <command>";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine("man: missing command");
                return Task.CompletedTask;
            }
            var cmd = Shell.GetCommand(args[0]);
            if (cmd == null)
            {
                context.Stderr.WriteLine($"man: {args[0]}: command not found");
                return Task.CompletedTask;
            }
            context.Stdout.WriteLine($"{cmd.Name} - {cmd.Description}\nUsage: {cmd.Usage}");
            return Task.CompletedTask;
        }
    }
}
