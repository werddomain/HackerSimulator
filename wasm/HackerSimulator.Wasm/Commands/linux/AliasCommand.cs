using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class AliasCommand : CommandBase
    {
        private readonly AliasService _aliases;
        public AliasCommand(ShellService shell, KernelService kernel, AliasService aliases) : base("alias", shell, kernel)
        {
            _aliases = aliases;
        }

        public override string Description => "List registered aliases";
        public override string Usage => "alias";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            foreach (var (alias, target) in _aliases.GetAll())
            {
                context.Stdout.WriteLine($"{alias} -> {target}");
            }
            return Task.CompletedTask;
        }
    }
}
