using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class TailCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public TailCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("tail", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Output end of files";
        public override string Usage => "tail FILE";

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
                int start = System.Math.Max(lines.Length - 10, 0);
                for (int i = start; i < lines.Length; i++)
                    context.Stdout.WriteLine(lines[i]);
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"tail: {ex.Message}");
            }
        }
    }
}
