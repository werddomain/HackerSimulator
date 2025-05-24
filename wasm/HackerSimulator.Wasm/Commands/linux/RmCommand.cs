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
        public override string Usage => "rm [-r] [-f] [-v] FILE...";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            bool recursive = false;
            bool force = false;
            bool verbose = false;
            var files = new System.Collections.Generic.List<string>();

            foreach (var a in args)
            {
                if (a == "-r" || a == "-R" || a == "--recursive")
                    recursive = true;
                else if (a == "-f" || a == "--force")
                    force = true;
                else if (a == "-v" || a == "--verbose")
                    verbose = true;
                else if (a.StartsWith("-"))
                {
                    context.Stderr.WriteLine($"rm: invalid option {a}");
                    return;
                }
                else
                    files.Add(a);
            }

            if (files.Count == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            foreach (var f in files)
            {
                var path = _fs.ResolvePath(f, cwd);
                try
                {
                    await RemoveEntry(path, recursive);
                    if (verbose)
                        context.Stdout.WriteLine($"removed '{f}'");
                }
                catch (System.Exception ex)
                {
                    if (!force)
                        context.Stderr.WriteLine($"rm: {ex.Message}");
                }
            }
        }

        private async Task RemoveEntry(string path, bool recursive)
        {
            var stat = await _fs.Stat(path);
            if (stat.IsDirectory)
            {
                if (!recursive)
                    throw new System.Exception("is a directory");
                var entries = await _fs.ReadDirectory(path);
                foreach (var e in entries)
                {
                    await RemoveEntry(path.TrimEnd('/') + "/" + e.Name, true);
                }
            }
            await _fs.DeleteEntry(path);
        }
    }
}
