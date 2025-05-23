using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class GrepCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public GrepCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("grep", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Search text";
        public override string Usage => "grep PATTERN FILE";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }
            var pattern = args[0];
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            for (int i = 1; i < args.Length; i++)
            {
                var file = _fs.ResolvePath(args[i], cwd);
                try
                {
                    var text = await _fs.ReadFile(file);
                    var lines = text.Split('\n');
                    for (int ln = 0; ln < lines.Length; ln++)
                    {
                        if (lines[ln].Contains(pattern))
                            context.Stdout.WriteLine($"{ln + 1}:{lines[ln]}");
                    }
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"grep: {args[i]}: {ex.Message}");
                }
            }
        }
    }
}
