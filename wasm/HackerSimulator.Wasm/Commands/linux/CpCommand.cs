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
        public override string Usage => "cp [-r] [-v] SOURCE... DEST";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            bool recursive = false;
            bool verbose = false;
            var paths = new System.Collections.Generic.List<string>();

            foreach (var a in args)
            {
                if (a == "-r" || a == "-R" || a == "--recursive")
                    recursive = true;
                else if (a == "-v" || a == "--verbose")
                    verbose = true;
                else if (a.StartsWith("-"))
                {
                    context.Stderr.WriteLine($"cp: invalid option {a}");
                    return;
                }
                else
                    paths.Add(a);
            }

            if (paths.Count < 2)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var dest = _fs.ResolvePath(paths[^1], cwd);
            FileSystemService.FileStats? destStat = null;
            try { destStat = await _fs.Stat(dest); } catch { }

            for (int i = 0; i < paths.Count - 1; i++)
            {
                var src = _fs.ResolvePath(paths[i], cwd);
                var target = destStat != null && destStat.IsDirectory ? dest.TrimEnd('/') + "/" + System.IO.Path.GetFileName(src) : dest;
                try
                {
                    await CopyEntry(src, target, recursive);
                    if (verbose)
                        context.Stdout.WriteLine($"'{paths[i]}' -> '{target}'");
                }
                catch (System.Exception ex)
                {
                    context.Stderr.WriteLine($"cp: {paths[i]}: {ex.Message}");
                }
            }
        }

        private async Task CopyEntry(string source, string dest, bool recursive)
        {
            var stats = await _fs.Stat(source);
            if (stats.IsDirectory)
            {
                if (!recursive)
                    throw new System.Exception("omitting directory");

                try { await _fs.CreateDirectory(dest); } catch { }
                var entries = await _fs.ReadDirectory(source);
                foreach (var e in entries)
                {
                    await CopyEntry(source.TrimEnd('/') + "/" + e.Name, dest.TrimEnd('/') + "/" + e.Name, true);
                }
            }
            else
            {
                var content = await _fs.ReadFile(source);
                await _fs.WriteFile(dest, content);
            }
        }
    }
}
