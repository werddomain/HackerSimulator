using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class RmAliasCommand : CommandBase
    {
        private readonly AliasService _aliases;
        public RmAliasCommand(ShellService shell, KernelService kernel, AliasService aliases) : base("rmalias", shell, kernel)
        {
            _aliases = aliases;
        }

        public override string Description => "Remove an alias";
        public override string Usage => "rmalias <alias>";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return Task.CompletedTask;
            }
            if (_aliases.Unregister(args[0]))
                context.Stdout.WriteLine("Alias removed");
            else
                context.Stderr.WriteLine("Alias not found");
            return Task.CompletedTask;
        }
    }
}
