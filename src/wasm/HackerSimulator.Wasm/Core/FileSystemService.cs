using System.Collections.Generic;
using System.Linq;

namespace HackerSimulator.Wasm.Core
{
    public class FsEntry
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public List<FsEntry> Children { get; set; } = new();
    }

    public class FileSystemService
    {
        public FileSystemService()
        {
            _root.Children.Add(new FsEntry { Name = "home", IsDirectory = true });
            _root.Children.Add(new FsEntry { Name = "bin", IsDirectory = true });
        }

        private readonly FsEntry _root = new() { Name = "/", IsDirectory = true };

        public FsEntry GetEntry(string path)
        {
            var parts = path.Trim('/').Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            var current = _root;
            foreach (var part in parts)
            {
                current = current.Children.First(e => e.Name == part);
            }
            return current;
        }

        public IEnumerable<FsEntry> List(string path)
        {
            return GetEntry(path).Children;
        }

        public void CreateDirectory(string path, string name)
        {
            var parent = GetEntry(path);
            parent.Children.Add(new FsEntry { Name = name, IsDirectory = true });
        }

        public void CreateFile(string path, string name)
        {
            var parent = GetEntry(path);
            parent.Children.Add(new FsEntry { Name = name, IsDirectory = false });
        }
    }
}
