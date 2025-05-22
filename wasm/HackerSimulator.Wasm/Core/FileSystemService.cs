using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// A very small persistent file system backed by browser localStorage.
    /// It mimics a subset of the JavaScript IndexedDB implementation.
    /// </summary>
    public class FileSystemService
    {
        private const string StorageKey = "hacker-os-fs";
        private readonly IJSRuntime _js;
        private Dictionary<string, EntryRecord> _entries = new();

        public FileSystemService(IJSRuntime js)
        {
            _js = js;
        }

        #region Entry definitions
        private class EntryRecord
        {
            public string Path { get; set; } = string.Empty;
            public string Parent { get; set; } = string.Empty;
            public FileSystemEntry Entry { get; set; } = new FileSystemEntry();
        }

        public class FileSystemEntry
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = "file"; // file or directory
            public string? Content { get; set; }
            public string? BinaryContent { get; set; }
            public bool IsBinary { get; set; }
            public MetaData Metadata { get; set; } = new MetaData();
            public bool IsDirectory => Type == "directory";
        }

        public class MetaData
        {
            public long Created { get; set; }
            public long Modified { get; set; }
            public long Size { get; set; }
            public string Permissions { get; set; } = "-rw-r--r--";
            public string Owner { get; set; } = "user";
        }
        #endregion

        public async Task InitAsync()
        {
            var json = await _js.InvokeAsync<string>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _entries = JsonSerializer.Deserialize<Dictionary<string, EntryRecord>>(json) ?? new();
            }

            if (!_entries.ContainsKey("/"))
            {
                _entries["/"] = new EntryRecord
                {
                    Path = "/",
                    Parent = string.Empty,
                    Entry = new FileSystemEntry
                    {
                        Name = "/",
                        Type = "directory",
                        Metadata = new MetaData
                        {
                            Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            Modified = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            Permissions = "drwxr-xr-x",
                            Owner = "root"
                        }
                    }
                };
                await SaveAsync();
            }
        }

        private async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(_entries);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";
            if (!path.StartsWith("/")) path = "/" + path;
            if (path.Length > 1 && path.EndsWith("/")) path = path.TrimEnd('/');
            return path;
        }

        public async Task<bool> Exists(string path)
        {
            return _entries.ContainsKey(Normalize(path));
        }

        public async Task<IEnumerable<FileSystemEntry>> ReadDirectory(string path)
        {
            path = Normalize(path);
            return _entries.Values.Where(e => e.Parent == path).Select(e => e.Entry);
        }

        public async Task<string> ReadFile(string path)
        {
            path = Normalize(path);
            if (!_entries.TryGetValue(path, out var rec) || rec.Entry.Type != "file")
                throw new Exception($"Not a file: {path}");
            if (rec.Entry.IsBinary && rec.Entry.BinaryContent != null)
                return rec.Entry.BinaryContent;
            return rec.Entry.Content ?? string.Empty;
        }

        public async Task WriteFile(string path, string content)
        {
            path = Normalize(path);
            var name = path.Split('/').Last();
            var parent = path.Substring(0, path.LastIndexOf('/'));
            if (string.IsNullOrEmpty(parent)) parent = "/";
            if (!_entries.ContainsKey(parent)) throw new Exception($"Parent does not exist: {parent}");

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_entries.TryGetValue(path, out var rec))
            {
                rec.Entry.Content = content;
                rec.Entry.IsBinary = false;
                rec.Entry.Metadata.Modified = now;
                rec.Entry.Metadata.Size = content.Length;
            }
            else
            {
                rec = new EntryRecord
                {
                    Path = path,
                    Parent = parent,
                    Entry = new FileSystemEntry
                    {
                        Name = name,
                        Type = "file",
                        Content = content,
                        Metadata = new MetaData
                        {
                            Created = now,
                            Modified = now,
                            Size = content.Length
                        }
                    }
                };
                _entries[path] = rec;
            }

            await SaveAsync();
        }

        public async Task CreateDirectory(string path)
        {
            path = Normalize(path);
            if (_entries.ContainsKey(path)) throw new Exception($"Path exists: {path}");
            var name = path.Split('/').Last();
            var parent = path.Substring(0, path.LastIndexOf('/'));
            if (string.IsNullOrEmpty(parent)) parent = "/";
            if (!_entries.ContainsKey(parent)) throw new Exception($"Parent does not exist: {parent}");

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _entries[path] = new EntryRecord
            {
                Path = path,
                Parent = parent,
                Entry = new FileSystemEntry
                {
                    Name = name,
                    Type = "directory",
                    Metadata = new MetaData
                    {
                        Created = now,
                        Modified = now,
                        Permissions = "drwxr-xr-x",
                        Owner = "user"
                    }
                }
            };
            await SaveAsync();
        }

        public async Task DeleteEntry(string path)
        {
            path = Normalize(path);
            if (!_entries.ContainsKey(path)) return;
            if (_entries.Values.Any(e => e.Parent == path))
                throw new Exception("Directory not empty");
            _entries.Remove(path);
            await SaveAsync();
        }

        public async Task MoveEntry(string oldPath, string newPath)
        {
            oldPath = Normalize(oldPath);
            newPath = Normalize(newPath);
            if (!_entries.TryGetValue(oldPath, out var rec))
                throw new Exception($"Source does not exist: {oldPath}");
            if (_entries.ContainsKey(newPath))
                throw new Exception($"Destination exists: {newPath}");

            var parent = newPath.Substring(0, newPath.LastIndexOf('/'));
            if (string.IsNullOrEmpty(parent)) parent = "/";
            if (!_entries.ContainsKey(parent))
                throw new Exception($"Parent does not exist: {parent}");

            _entries.Remove(oldPath);
            rec.Path = newPath;
            rec.Parent = parent;
            rec.Entry.Name = newPath.Split('/').Last();
            rec.Entry.Metadata.Modified = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _entries[newPath] = rec;
            await SaveAsync();
        }

        public Task Copy(string sourcePath, string destPath)
        {
            sourcePath = Normalize(sourcePath);
            if (!_entries.TryGetValue(sourcePath, out var rec))
                throw new Exception($"Source does not exist: {sourcePath}");
            var clone = JsonSerializer.Deserialize<EntryRecord>(JsonSerializer.Serialize(rec))!;
            clone.Path = Normalize(destPath);
            clone.Parent = clone.Path.Substring(0, clone.Path.LastIndexOf('/')); 
            clone.Entry.Name = clone.Path.Split('/').Last();
            _entries[clone.Path] = clone;
            return SaveAsync();
        }

        public Task Move(string sourcePath, string destPath) => MoveEntry(sourcePath, destPath);
        public Task Remove(string path) => DeleteEntry(path);

        public Task<FileStats> Stat(string path)
        {
            path = Normalize(path);
            if (!_entries.TryGetValue(path, out var rec))
                throw new Exception($"Path does not exist: {path}");

            var stats = new FileStats
            {
                IsDirectory = rec.Entry.Type == "directory",
                Size = rec.Entry.Metadata.Size,
                CreatedTime = DateTimeOffset.FromUnixTimeMilliseconds(rec.Entry.Metadata.Created).DateTime,
                ModifiedTime = DateTimeOffset.FromUnixTimeMilliseconds(rec.Entry.Metadata.Modified).DateTime,
                Permissions = rec.Entry.Metadata.Permissions,
                Owner = rec.Entry.Metadata.Owner
            };
            return Task.FromResult(stats);
        }

        public async Task WriteBinaryFile(string path, byte[] content)
        {
            var base64 = Convert.ToBase64String(content);
            path = Normalize(path);
            var name = path.Split('/').Last();
            var parent = path.Substring(0, path.LastIndexOf('/'));
            if (string.IsNullOrEmpty(parent)) parent = "/";
            if (!_entries.ContainsKey(parent)) throw new Exception($"Parent does not exist: {parent}");

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _entries[path] = new EntryRecord
            {
                Path = path,
                Parent = parent,
                Entry = new FileSystemEntry
                {
                    Name = name,
                    Type = "file",
                    BinaryContent = base64,
                    IsBinary = true,
                    Metadata = new MetaData
                    {
                        Created = now,
                        Modified = now,
                        Size = content.Length
                    }
                }
            };
            await SaveAsync();
        }

        public Task<byte[]> ReadBinaryFile(string path)
        {
            path = Normalize(path);
            if (!_entries.TryGetValue(path, out var rec) || !rec.Entry.IsBinary || rec.Entry.BinaryContent == null)
                throw new Exception($"Binary file not found: {path}");
            return Task.FromResult(Convert.FromBase64String(rec.Entry.BinaryContent));
        }

        public Task<bool> IsBinaryFile(string path)
        {
            path = Normalize(path);
            return Task.FromResult(_entries.TryGetValue(path, out var rec) && rec.Entry.IsBinary);
        }

        public class FileStats
        {
            public bool IsDirectory { get; set; }
            public long? Size { get; set; }
            public DateTime? CreatedTime { get; set; }
            public DateTime? ModifiedTime { get; set; }
            public string? Permissions { get; set; }
            public string? Owner { get; set; }
        }
    }
}
