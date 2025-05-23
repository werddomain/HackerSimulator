using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class CpCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public CpCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("cp", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Copy files";
        public override string Usage => "cp SOURCE DEST";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var dest = _fs.ResolvePath(args[^1], cwd);
            for (int i = 0; i < args.Length - 1; i++)
            {
                var src = _fs.ResolvePath(args[i], cwd);
                var target = dest;
                if (args.Length > 2)
                    target = dest.TrimEnd('/') + "/" + System.IO.Path.GetFileName(src);
                try
                {
                    await _fs.Copy(src, target);
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"cp: {args[i]}: {ex.Message}");
                }
            }
        }
    }
}
