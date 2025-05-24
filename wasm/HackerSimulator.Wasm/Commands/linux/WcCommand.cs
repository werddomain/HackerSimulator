using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class WcCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public WcCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("wc", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Word count";
        public override string Usage => "wc FILE";

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
                var content = await _fs.ReadFile(path);
                int lines = content.Split('\n').Length;
                int words = content.Split((char[])null!, System.StringSplitOptions.RemoveEmptyEntries).Length;
                int bytes = System.Text.Encoding.UTF8.GetByteCount(content);
                context.Stdout.WriteLine($"{lines} {words} {bytes} {args[0]}");
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"wc: {ex.Message}");
            }
        }
    }
}
