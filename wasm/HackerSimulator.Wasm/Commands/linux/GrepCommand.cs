using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class GrepCommand : CommandBase
    {
        private readonly FileSystemService _fs;
        public GrepCommand(ShellService shell, KernelService kernel, FileSystemService fs) : base("grep", shell, kernel)
        {
            _fs = fs;
        }

        public override string Description => "Search text";
        public override string Usage => "grep [options] PATTERN [FILE...]";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            bool ignoreCase = false;
            bool invert = false;
            bool showLine = false;
            bool countOnly = false;
            bool onlyMatch = false;
            bool recursive = false;

            string? pattern = null;
            var files = new System.Collections.Generic.List<string>();

            foreach (var a in args)
            {
                switch (a)
                {
                    case "-i":
                    case "--ignore-case":
                        ignoreCase = true; break;
                    case "-v":
                    case "--invert-match":
                        invert = true; break;
                    case "-n":
                    case "--line-number":
                        showLine = true; break;
                    case "-c":
                    case "--count":
                        countOnly = true; break;
                    case "-o":
                    case "--only-matching":
                        onlyMatch = true; break;
                    case "-r":
                    case "-R":
                    case "--recursive":
                        recursive = true; break;
                    default:
                        if (pattern == null)
                            pattern = a;
                        else
                            files.Add(a);
                        break;
                }
            }

            if (string.IsNullOrEmpty(pattern))
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }

            var cwd = context.Env.TryGetValue("PWD", out var c) ? c : "/";
            if (files.Count == 0)
            {
                context.Stderr.WriteLine("grep: no files specified");
                return;
            }

            var options = ignoreCase ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None;
            var regex = new System.Text.RegularExpressions.Regex(pattern, options);

            foreach (var f in files)
            {
                var path = _fs.ResolvePath(f, cwd);
                await ProcessPath(path, f, regex, context, invert, showLine, countOnly, onlyMatch, recursive, files.Count > 1);
            }
        }

        private async Task ProcessPath(string path, string display, System.Text.RegularExpressions.Regex regex, CommandContext context, bool invert, bool showLine, bool countOnly, bool onlyMatch, bool recursive, bool prefix)
        {
            FileSystemService.FileStats stats;
            try { stats = await _fs.Stat(path); } catch (System.Exception ex) { context.Stderr.WriteLine($"grep: {display}: {ex.Message}"); return; }
            if (stats.IsDirectory)
            {
                if (!recursive)
                {
                    context.Stderr.WriteLine($"grep: {display}: Is a directory");
                    return;
                }
                var entries = await _fs.ReadDirectory(path);
                foreach (var e in entries)
                    await ProcessPath(path.TrimEnd('/') + "/" + e.Name, display + "/" + e.Name, regex, context, invert, showLine, countOnly, onlyMatch, true, prefix);
                return;
            }

            string text;
            try { text = await _fs.ReadFile(path); } catch (System.Exception ex) { context.Stderr.WriteLine($"grep: {display}: {ex.Message}"); return; }
            var lines = text.Split('\n');
            int matchCount = 0;
            for (int ln = 0; ln < lines.Length; ln++)
            {
                bool match = regex.IsMatch(lines[ln]);
                if (invert) match = !match;
                if (!match) continue;
                matchCount++;
                if (countOnly) continue;
                if (onlyMatch)
                {
                    var matches = regex.Matches(lines[ln]);
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        var outLine = string.Empty;
                        if (prefix) outLine += display + ":";
                        if (showLine) outLine += (ln + 1) + ":";
                        outLine += m.Value;
                        context.Stdout.WriteLine(outLine);
                    }
                }
                else
                {
                    var outLine = string.Empty;
                    if (prefix) outLine += display + ":";
                    if (showLine) outLine += (ln + 1) + ":";
                    outLine += lines[ln];
                    context.Stdout.WriteLine(outLine);
                }
            }
            if (countOnly)
            {
                var prefixStr = prefix ? display + ":" : string.Empty;
                context.Stdout.WriteLine(prefixStr + matchCount.ToString());
            }
        }
    }
}
