using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class FindCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public FindCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("find", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Search for files";
        public override string Usage => "find [path]";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var start = args.Length > 0 ? args[0] : ".";
            var root = _fs.ResolvePath(start, cwd);
            await Recurse(root);

            async Task Recurse(string path)
            {
                context.Stdout.WriteLine(path);
                try
                {
                    var entries = await _fs.ReadDirectory(path);
                    foreach (var e in entries)
                    {
                        if (e.IsDirectory)
                            await Recurse(path.TrimEnd('/') + "/" + e.Name);
                        else
                            context.Stdout.WriteLine(path.TrimEnd('/') + "/" + e.Name);
                    }
                }
                catch { }
            }
        }
    }
}
