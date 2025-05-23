using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class AddAliasCommand : CommandBase
    {
        private readonly AliasService _aliases;
        public AddAliasCommand(ShellService shell, KernelService kernel, AliasService aliases) : base("addalias", shell, kernel)
        {
            _aliases = aliases;
        }

        public override string Description => "Create a filesystem alias";
        public override string Usage => "addalias <alias> <path>";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return Task.CompletedTask;
            }

            var alias = args[0];
            var target = args[1];
            _aliases.Register(alias, target);
            context.Stdout.WriteLine($"Alias {alias} -> {target} added");
            return Task.CompletedTask;
        }
    }
}
