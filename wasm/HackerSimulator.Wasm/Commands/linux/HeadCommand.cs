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
        public override string Usage => "head [-n N] FILE";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            int num = 10;
            var files = new System.Collections.Generic.List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a == "-n" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out var n)) num = n;
                    i++;
                }
                else if (a.StartsWith("-") && int.TryParse(a.Substring(1), out var n))
                {
                    num = n;
                }
                else if (a.StartsWith("-"))
                {
                    context.Stderr.WriteLine($"head: invalid option {a}");
                    return;
                }
                else
                {
                    files.Add(a);
                }
            }

            if (files.Count == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            foreach (var f in files)
            {
                var path = _fs.ResolvePath(f, cwd);
                try
                {
                    var content = await _fs.ReadFile(path);
                    var lines = content.Split('\n');
                    for (int l = 0; l < System.Math.Min(num, lines.Length); l++)
                        context.Stdout.WriteLine(lines[l]);
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"head: {ex.Message}");
                }
            }
        }
    }
}
