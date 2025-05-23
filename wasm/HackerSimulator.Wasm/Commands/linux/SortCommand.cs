using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class SortCommand : CommandBase
    {
        public SortCommand(ShellService shell, KernelService kernel) : base("sort", shell, kernel) { }

        public override string Description => "Sort lines of text";
        public override string Usage => "sort";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            var input = await context.Stdin.ReadToEndAsync();
            var lines = input.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            System.Array.Sort(lines, System.StringComparer.Ordinal);
            foreach (var line in lines)
                context.Stdout.WriteLine(line);
        }
    }
}
