using System;
using System.Collections.Generic;
using System.Linq;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Service storing associations between file extensions, icons and
    /// default applications.
    /// </summary>
    public class FileTypeService
    {
        public record FileTypeInfo(string App, string Icon);

        private readonly Dictionary<string, FileTypeInfo> _types = new(StringComparer.OrdinalIgnoreCase);
        private readonly FileTypeInfo _default = new("texteditorapp", "📄");

        public FileTypeService()
        {
            RegisterDefaults();
        }

        public void Register(string extension, string defaultApp, string icon = "📄")
        {
            if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(defaultApp))
                return;
            extension = extension.TrimStart('.').ToLowerInvariant();
            _types[extension] = new FileTypeInfo(defaultApp.ToLowerInvariant(), icon);
        }

        public FileTypeInfo GetInfo(string filename)
        {
            var ext = GetExtension(filename);
            if (ext != null && _types.TryGetValue(ext, out var info))
                return info;
            return _default;
        }

        public string GetDefaultApp(string filename) => GetInfo(filename).App;
        public string GetIcon(string filename) => GetInfo(filename).Icon;

        private static string? GetExtension(string filename)
        {
            var idx = filename.LastIndexOf('.');
            return idx < 0 ? null : filename[(idx + 1)..].ToLowerInvariant();
        }

        private void RegisterDefaults()
        {
            Register("txt", "texteditorapp", "📝");
            Register("md", "texteditorapp", "📝");
            Register("js", "codeeditorapp", "📜");
            Register("ts", "codeeditorapp", "📜");
            Register("json", "codeeditorapp", "📊");
            Register("html", "codeeditorapp", "🌐");
            Register("css", "codeeditorapp", "🎨");
            Register("png", "fileexplorerapp", "🖼️");
            Register("jpg", "fileexplorerapp", "🖼️");
            Register("jpeg", "fileexplorerapp", "🖼️");
            Register("gif", "fileexplorerapp", "🖼️");
        }

        public void RegisterFromAttributes()
        {
            var assembly = typeof(FileTypeService).Assembly;
            foreach (var type in assembly.GetTypes())
            {
                var attrs = type.GetCustomAttributes(typeof(OpenFileTypeAttribute), false)
                    .Cast<OpenFileTypeAttribute>();
                foreach (var attr in attrs)
                {
                    foreach (var ext in attr.Extensions)
                    {
                        Register(ext, type.Name.ToLowerInvariant());
                    }
                }
            }
        }
    }
}
