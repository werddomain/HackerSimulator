using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class MkdirCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public MkdirCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("mkdir", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Create directories";
        public override string Usage => "mkdir DIR";

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
                await _fs.CreateDirectory(path);
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"mkdir: {ex.Message}");
            }
        }
    }
}
