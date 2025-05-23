using System;
using System.Collections.Generic;
using System.Linq;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Simple service to manage filesystem path aliases.
    /// This is a lightweight approximation of the JavaScript implementation.
    /// </summary>
    public class AliasService
    {
        private readonly Dictionary<string, string> _aliases = new();

        public void Register(string alias, string target) => _aliases[alias] = target;
        public bool Unregister(string alias) => _aliases.Remove(alias);
        public IEnumerable<(string Alias, string Target)> GetAll() => _aliases.Select(k => (k.Key, k.Value));

        public string Resolve(string path, string? cwd = null)
        {
            if (string.IsNullOrEmpty(path))
                path = string.Empty;

            if (path == "~" || path.StartsWith("~/"))
            {
                if (_aliases.TryGetValue("~", out var home))
                {
                    path = home + path.Substring(1);
                }
            }
            else
            {
                foreach (var kv in _aliases)
                {
                    if (kv.Key == "~")
                        continue;
                    if (path == kv.Key || path.StartsWith(kv.Key + "/"))
                    {
                        path = kv.Value + path.Substring(kv.Key.Length);
                        break;
                    }
                }
            }

            if (!path.StartsWith("/") && !string.IsNullOrEmpty(cwd))
            {
                path = cwd.TrimEnd('/') + "/" + path;
            }

            return Normalize(path);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";
            var parts = new List<string>();
            foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".")
                    continue;
                if (part == "..")
                {
                    if (parts.Count > 0)
                        parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(part);
            }
            return "/" + string.Join('/', parts);
        }
    }
}
