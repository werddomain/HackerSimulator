using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class TouchCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public TouchCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("touch", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Update file timestamps";
        public override string Usage => "touch FILE";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }
            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            foreach (var file in args)
            {
                var path = _fs.ResolvePath(file, cwd);
                try
                {
                    if (await _fs.Exists(path))
                    {
                        var content = await _fs.ReadFile(path);
                        await _fs.WriteFile(path, content);
                    }
                    else
                    {
                        await _fs.WriteFile(path, string.Empty);
                    }
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"touch: {ex.Message}");
                }
            }
        }
    }
}
