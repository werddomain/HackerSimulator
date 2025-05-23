using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class RmCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public RmCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("rm", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Remove files";
        public override string Usage => "rm FILE";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            foreach (var arg in args)
            {
                var path = _fs.ResolvePath(arg, cwd);
                try
                {
                    await _fs.DeleteEntry(path);
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"rm: {ex.Message}");
                }
            }
        }
    }
}
