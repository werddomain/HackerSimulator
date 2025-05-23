using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class LsCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public LsCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("ls", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "List directory contents";
        public override string Usage => "ls [-a] [-l] [-h] [path]";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            bool showAll = false;
            bool longFormat = false;
            bool human = false;
            string? target = null;

            foreach (var a in args)
            {
                if (a == "-a" || a == "--all") showAll = true;
                else if (a == "-l") longFormat = true;
                else if (a == "-h") human = true;
                else if (a.StartsWith("-"))
                {
                    context.Stderr.WriteLine($"ls: invalid option {a}");
                    return;
                }
                else target = a;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            var path = target != null ? _fs.ResolvePath(target, cwd) : cwd;
            try
            {
                var entries = await _fs.ReadDirectory(path);
                foreach (var e in entries)
                {
                    if (!showAll && e.Name.StartsWith("."))
                        continue;
                    if (longFormat)
                    {
                        var perms = e.Metadata?.Permissions ?? (e.IsDirectory ? "drwxr-xr-x" : "-rw-r--r--");
                        var owner = e.Metadata?.Owner ?? "user";
                        var sizeVal = e.Metadata?.Size ?? 0;
                        var size = human ? FormatSize(sizeVal) : sizeVal.ToString();
                        var date = e.Metadata != null ? System.DateTimeOffset.FromUnixTimeMilliseconds(e.Metadata.Modified).ToLocalTime().ToString("MMM dd HH:mm") : string.Empty;
                        context.Stdout.WriteLine($"{perms} 1 {owner} {owner} {size,8} {date} {e.Name}{(e.IsDirectory ? "/" : string.Empty)}");
                    }
                    else
                    {
                        context.Stdout.WriteLine(e.Name + (e.IsDirectory ? "/" : string.Empty));
                    }
                }
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"ls: {ex.Message}");
            }
        }

        private string FormatSize(long bytes)
        {
            double size = bytes;
            string[] units = new[] { "B", "K", "M", "G", "T" };
            int index = 0;
            while (size >= 1024 && index < units.Length - 1)
            {
                size /= 1024;
                index++;
            }
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#}{1}", size, units[index]);
        }
    }
}
