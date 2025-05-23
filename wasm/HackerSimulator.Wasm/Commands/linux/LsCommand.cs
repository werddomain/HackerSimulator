using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class LsCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public LsCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("ls", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "List directory contents";
        public override string Usage => "ls [path]";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var path = args.Length > 0 ? _fs.ResolvePath(args[0], cwd) : cwd;
            try
            {
                var entries = await _fs.ReadDirectory(path);
                foreach (var e in entries)
                {
                    context.Stdout.WriteLine(e.Name + (e.IsDirectory ? "/" : string.Empty));
                }
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"ls: {ex.Message}");
            }
        }
    }
}
