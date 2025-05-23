using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class CatCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public CatCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("cat", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Concatenate files";
        public override string Usage => "cat <file>...";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine("cat: missing file operand");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            foreach (var file in args)
            {
                var path = _fs.ResolvePath(file, cwd);
                try
                {
                    var content = await _fs.ReadFile(path);
                    context.Stdout.WriteLine(content);
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"cat: {file}: {ex.Message}");
                }
            }
        }
    }
}
