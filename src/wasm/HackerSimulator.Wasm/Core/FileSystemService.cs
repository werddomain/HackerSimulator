using System.Collections.Generic;

namespace HackerSimulator.Wasm.Core
{
    public class FileSystemService
    {
        private readonly Dictionary<string, List<string>> _directories = new();

        public FileSystemService()
        {
            _directories["/"] = new List<string>();
        }

        public IEnumerable<string> List(string path)
        {
            if (_directories.TryGetValue(path, out var list))
                return list;
            return new List<string>();
        }

        public void CreateDirectory(string path, string name)
        {
            var dir = Combine(path, name);
            _directories[dir] = new List<string>();
            _directories[path].Add(dir);
        }

        private static string Combine(string path, string name)
        {
            if (path.EndsWith("/"))
                return path + name;
            return path + "/" + name;
        }
    }
}
