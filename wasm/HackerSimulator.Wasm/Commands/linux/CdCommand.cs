using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class CdCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public CdCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("cd", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Change directory";
        public override string Usage => "cd <dir>";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var target = args.Length > 0 ? args[0] : "~";
            var path = _fs.ResolvePath(target, cwd);
            try
            {
                var stat = await _fs.Stat(path);
                if (!stat.IsDirectory)
                {
                    context.Stderr.WriteLine("cd: not a directory");
                    return;
                }
                context.Env["PWD"] = path;
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"cd: {ex.Message}");
            }
        }
    }
}
