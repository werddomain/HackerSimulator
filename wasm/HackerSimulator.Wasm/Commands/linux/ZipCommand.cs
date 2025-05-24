using System;
using System.IO;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class ZipCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public ZipCommand(ShellService shell, KernelService kernel, FileSystemService fs)
            : base("zip", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Create zip archive";
        public override string Usage => "zip SOURCE [DEST]";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var source = _fs.ResolvePath(args[0], cwd);
            var dest = args.Length > 1 ? _fs.ResolvePath(args[1], cwd) : null;

            try
            {
                var bytes = await _fs.ZipEntry(source);
                if (dest != null)
                {
                    await _fs.WriteBinaryFile(dest, bytes);
                }
                else
                {
                    var b64 = Convert.ToBase64String(bytes);
                    context.Stdout.WriteLine(b64);
                }
            }
            catch (Exception ex)
            {
                context.Stderr.WriteLine($"zip: {ex.Message}");
            }
        }
    }
}
