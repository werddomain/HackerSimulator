using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class MvCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public MvCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("mv", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Move/rename files";
        public override string Usage => "mv SOURCE DEST";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var src = _fs.ResolvePath(args[0], cwd);
            var dest = _fs.ResolvePath(args[1], cwd);
            try
            {
                await _fs.Move(src, dest);
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"mv: {ex.Message}");
            }
        }
    }
}
