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
        public override string Usage => "cat [-n] [-E] <file>...";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            var files = new System.Collections.Generic.List<string>();
            bool number = false;
            bool showEnds = false;

            foreach (var arg in args)
            {
                if (arg == "-n")
                    number = true;
                else if (arg == "-E")
                    showEnds = true;
                else if (arg.StartsWith("-"))
                {
                    context.Stderr.WriteLine($"cat: invalid option '{arg}'");
                    return;
                }
                else
                    files.Add(arg);
            }

            if (files.Count == 0)
            {
                context.Stderr.WriteLine("cat: missing file operand");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            int lineNo = 1;
            foreach (var file in files)
            {
                var path = _fs.ResolvePath(file, cwd);
                try
                {
                    var content = await _fs.ReadFile(path);
                    var lines = content.Split('\n');
                    foreach (var line in lines)
                    {
                        var output = string.Empty;
                        if (number)
                            output += $"{lineNo++,6}  ";
                        output += line;
                        if (showEnds)
                            output += "$";
                        context.Stdout.WriteLine(output);
                    }
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"cat: {file}: {ex.Message}");
                }
            }
        }
    }
}
