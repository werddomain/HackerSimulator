using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class DiffCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public DiffCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("diff", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Compare files";
        public override string Usage => "diff FILE1 FILE2";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var p1 = _fs.ResolvePath(args[0], cwd);
            var p2 = _fs.ResolvePath(args[1], cwd);
            try
            {
                var t1 = await _fs.ReadFile(p1);
                var t2 = await _fs.ReadFile(p2);
                var a1 = t1.Split('\n');
                var a2 = t2.Split('\n');
                var max = System.Math.Max(a1.Length, a2.Length);
                for (int i = 0; i < max; i++)
                {
                    var s1 = i < a1.Length ? a1[i] : string.Empty;
                    var s2 = i < a2.Length ? a2[i] : string.Empty;
                    if (s1 != s2)
                    {
                        context.Stdout.WriteLine($"-{s1}");
                        context.Stdout.WriteLine($"+{s2}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"diff: {ex.Message}");
            }
        }
    }
}
