
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;


namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// A very small persistent file system backed by browser IndexedDB.
    /// It mimics a subset of the JavaScript IndexedDB implementation.
    /// </summary>
    public class FileSystemService
    {
        private const string StoreName = "fs";
        private readonly DatabaseService _db;
        private readonly AliasService _aliases;
        private Dictionary<string, EntryRecord> _entries = new();

        public FileSystemService(DatabaseService db, AliasService aliases)
        {
            _db = db;
            _aliases = aliases;
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
            public string? MimeType { get; set; }
            public string? Description { get; set; }
            public string? DefaultAppId { get; set; }
            public string? Icon { get; set; }
            public bool? Extractable { get; set; }
            public bool? Compressible { get; set; }
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
            await _db.InitTable<EntryRecord>(StoreName, 1, null);
            var all = await _db.GetAll<EntryRecord>(StoreName);
            _entries = all.ToDictionary(e => e.Path, e => e);

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
            await _db.Clear(StoreName);
            foreach (var rec in _entries.Values)
                await _db.Set(StoreName, rec.Path, rec);
        }

        public string ResolvePath(string path, string? cwd = null)
        {
            return _aliases.Resolve(path, cwd);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";
            if (!path.StartsWith("/")) path = "/" + path;
            if (path.Length > 1 && path.EndsWith("/")) path = path.TrimEnd('/');
            return path;
        }

        public Task<bool> Exists(string path)
        {
            return Task.FromResult(_entries.ContainsKey(Normalize(path)));
        }

        public Task<IEnumerable<FileSystemEntry>> ReadDirectory(string path)
        {
            path = Normalize(path);
            return Task.FromResult(_entries.Values.Where(e => e.Parent == path).Select(e => e.Entry));
        }

        public  Task<string> ReadFile(string path)
        {
            path = Normalize(path);
            if (!_entries.TryGetValue(path, out var rec) || rec.Entry.Type != "file")
                throw new Exception($"Not a file: {path}");
            if (rec.Entry.IsBinary && rec.Entry.BinaryContent != null)
                return Task.FromResult(rec.Entry.BinaryContent);
            return Task.FromResult(rec.Entry.Content ?? string.Empty);
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

        public async Task<byte[]> ReadFileBytes(string path)
        {
            path = Normalize(path);
            if (!_entries.TryGetValue(path, out var rec) || rec.Entry.Type != "file")
                throw new Exception($"Not a file: {path}");
            if (rec.Entry.IsBinary && rec.Entry.BinaryContent != null)
                return Convert.FromBase64String(rec.Entry.BinaryContent);
            return System.Text.Encoding.UTF8.GetBytes(rec.Entry.Content ?? string.Empty);
        }

        public async Task<byte[]> ZipEntry(string path)
        {
            path = Normalize(path);
            if (!_entries.TryGetValue(path, out var rec))
                throw new Exception($"Path does not exist: {path}");
            using var ms = new System.IO.MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                await AddEntryToArchive(path, rec.Entry.Name, archive);
            }
            return ms.ToArray();
        }

        public async Task<byte[]> ZipEntries(IEnumerable<string> paths)
        {
            using var ms = new System.IO.MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var p in paths)
                {
                    var norm = Normalize(p);
                    if (_entries.TryGetValue(norm, out var rec))
                        await AddEntryToArchive(norm, rec.Entry.Name, archive);
                }
            }
            return ms.ToArray();
        }

        private async Task AddEntryToArchive(string path, string zipPath, System.IO.Compression.ZipArchive archive)
        {
            if (!_entries.TryGetValue(path, out var rec)) return;
            if (rec.Entry.IsDirectory)
            {
                foreach (var child in _entries.Values.Where(e => e.Parent == path))
                {
                    var childPath = child.Path;
                    var childZip = string.IsNullOrEmpty(zipPath) ? child.Entry.Name : $"{zipPath}/{child.Entry.Name}";
                    await AddEntryToArchive(childPath, childZip, archive);
                }
            }
            else
            {
                var entry = archive.CreateEntry(zipPath);
                using var stream = entry.Open();
                byte[] data = rec.Entry.IsBinary && rec.Entry.BinaryContent != null
                    ? Convert.FromBase64String(rec.Entry.BinaryContent)
                    : System.Text.Encoding.UTF8.GetBytes(rec.Entry.Content ?? string.Empty);
                await stream.WriteAsync(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Search for files and directories containing the given term.
        /// Returns full paths for all matches.
        /// </summary>
        public Task<IEnumerable<string>> Search(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Task.FromResult(Enumerable.Empty<string>());

            var results = _entries.Values
                .Where(e => e.Entry.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Path);

            return Task.FromResult(results);
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
