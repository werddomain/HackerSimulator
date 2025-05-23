using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class HeadCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public HeadCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("head", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Output start of files";
        public override string Usage => "head FILE";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var path = _fs.ResolvePath(args[0], cwd);
            try
            {
                var content = await _fs.ReadFile(path);
                var lines = content.Split('\n');
                for (int i = 0; i < System.Math.Min(10, lines.Length); i++)
                    context.Stdout.WriteLine(lines[i]);
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"head: {ex.Message}");
            }
        }
    }
}
