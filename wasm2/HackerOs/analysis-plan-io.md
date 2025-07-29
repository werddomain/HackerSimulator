# HackerOS IO Module - Analysis Plan

## Overview

The IO Module provides a comprehensive virtual file system (VFS) implementation that simulates Linux-like file system behavior within the Blazor WebAssembly environment. This module is crucial for providing realistic file system operations while maintaining data persistence through IndexedDB.

## Core Components

### 1. Virtual File System Foundation (`IO/FileSystem/`)

#### IVirtualFileSystem Interface
```csharp
public interface IVirtualFileSystem
{
    Task<bool> InitializeAsync();
    Task<VirtualFileSystemNode?> GetNodeAsync(string path);
    Task<bool> CreateFileAsync(string path, byte[] content = null);
    Task<bool> CreateDirectoryAsync(string path);
    Task<bool> DeleteAsync(string path, bool recursive = false);
    Task<bool> ExistsAsync(string path);
    Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(string path);
    Task<byte[]?> ReadFileAsync(string path);
    Task<bool> WriteFileAsync(string path, byte[] content);
    Task<bool> MoveAsync(string sourcePath, string destinationPath);
    Task<bool> CopyAsync(string sourcePath, string destinationPath);
    event EventHandler<FileSystemEvent> OnFileSystemEvent;
}
```

#### Key Classes Design
```csharp
public abstract class VirtualFileSystemNode
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public DateTime AccessedAt { get; set; }
    public FilePermissions Permissions { get; set; }
    public string Owner { get; set; }
    public string Group { get; set; }
    public long Size { get; set; }
    public VirtualDirectory? Parent { get; set; }
}

public class VirtualFile : VirtualFileSystemNode
{
    public byte[] Content { get; set; }
    public string MimeType { get; set; }
    public bool IsSymbolicLink { get; set; }
    public string? SymbolicLinkTarget { get; set; }
}

public class VirtualDirectory : VirtualFileSystemNode
{
    public Dictionary<string, VirtualFileSystemNode> Children { get; set; }
    public bool IsMountPoint { get; set; }
    public string? MountedFileSystemType { get; set; }
}
```

### 2. Linux-Style File System Features

#### File Permissions System
```csharp
public class FilePermissions
{
    public bool OwnerRead { get; set; }
    public bool OwnerWrite { get; set; }
    public bool OwnerExecute { get; set; }
    public bool GroupRead { get; set; }
    public bool GroupWrite { get; set; }
    public bool GroupExecute { get; set; }
    public bool OtherRead { get; set; }
    public bool OtherWrite { get; set; }
    public bool OtherExecute { get; set; }
    
    public int ToOctal() { /* Convert to rwxrwxrwx format */ }
    public static FilePermissions FromOctal(int octal) { /* Parse from octal */ }
    public override string ToString() { /* Return rwxrwxrwx string */ }
}
```

#### Path Resolution
- **Absolute paths**: `/home/user/file.txt`
- **Relative paths**: `./file.txt`, `../parent/file.txt`
- **Home directory**: `~/file.txt` expands to `/home/username/file.txt`
- **Current directory**: `.` and parent directory `..`
- **Case sensitivity**: Linux-style case-sensitive paths
- **Hidden files**: Support for dot files (`.bashrc`, `.config`)

#### Symbolic Links
- Support for symbolic links pointing to files and directories
- Proper link resolution with cycle detection
- Link creation and management through standard file operations

### 3. File System Operations

#### File Descriptors
```csharp
public class FileDescriptor
{
    public int Id { get; set; }
    public string FilePath { get; set; }
    public FileAccessMode AccessMode { get; set; }
    public int ProcessId { get; set; }
    public long Position { get; set; }
    public bool IsLocked { get; set; }
    public DateTime OpenedAt { get; set; }
}

public enum FileAccessMode
{
    Read,
    Write,
    ReadWrite,
    Append
}
```

#### Mount Point System
```csharp
public class MountPoint
{
    public string MountPath { get; set; }
    public string FileSystemType { get; set; }
    public Dictionary<string, object> Options { get; set; }
    public bool IsReadOnly { get; set; }
    public DateTime MountedAt { get; set; }
}
```

### 4. Persistence Layer

#### IndexedDB Storage
```csharp
public interface IIndexedDBStorage
{
    Task<bool> InitializeAsync();
    Task<bool> SaveFileSystemAsync(VirtualFileSystem fileSystem);
    Task<VirtualFileSystem?> LoadFileSystemAsync();
    Task<bool> SaveFileAsync(string path, byte[] content);
    Task<byte[]?> LoadFileAsync(string path);
    Task<bool> DeleteFileAsync(string path);
    Task<IEnumerable<string>> ListFilesAsync(string directoryPath);
}
```

#### Data Serialization
- Efficient serialization of file system structure
- Incremental saves for large file systems
- Compression for binary files
- Metadata preservation during serialization

### 5. HackerOS.System.IO Namespace

#### Static File Utilities
```csharp
namespace HackerOS.System.IO
{
    public static class File
    {
        public static Task<bool> ExistsAsync(string path);
        public static Task<string> ReadAllTextAsync(string path);
        public static Task<byte[]> ReadAllBytesAsync(string path);
        public static Task WriteAllTextAsync(string path, string content);
        public static Task WriteAllBytesAsync(string path, byte[] content);
        public static Task<bool> DeleteAsync(string path);
        public static Task<bool> CopyAsync(string source, string destination);
        public static Task<bool> MoveAsync(string source, string destination);
        public static Task<DateTime> GetCreationTimeAsync(string path);
        public static Task<DateTime> GetLastWriteTimeAsync(string path);
        public static Task<long> GetSizeAsync(string path);
    }
    
    public static class Directory
    {
        public static Task<bool> ExistsAsync(string path);
        public static Task<bool> CreateDirectoryAsync(string path);
        public static Task<bool> DeleteAsync(string path, bool recursive = false);
        public static Task<IEnumerable<string>> GetFilesAsync(string path);
        public static Task<IEnumerable<string>> GetDirectoriesAsync(string path);
        public static Task<string> GetCurrentDirectoryAsync();
        public static Task SetCurrentDirectoryAsync(string path);
    }
    
    public static class Path
    {
        public static string Combine(params string[] paths);
        public static string GetDirectoryName(string path);
        public static string GetFileName(string path);
        public static string GetFileNameWithoutExtension(string path);
        public static string GetExtension(string path);
        public static bool IsPathRooted(string path);
        public static string GetFullPath(string path);
    }
}
```

#### Session Context Integration
```csharp
public class FileSystemSessionContext
{
    public static IServiceProvider? ServiceProvider { get; set; }
    public static string? CurrentUser { get; set; }
    public static string? CurrentWorkingDirectory { get; set; }
    
    internal static async Task<bool> ValidatePermissions(string path, FileAccessMode mode)
    {
        var securityService = ServiceProvider?.GetService<ISecurityService>();
        return await securityService?.CheckFilePermissionAsync(CurrentUser, path, mode) ?? false;
    }
}
```

## Standard Directory Structure

### Unix-Like Directory Hierarchy
```
/
├── bin/            # Essential command binaries
├── boot/           # Boot loader files
├── dev/            # Device files
├── etc/            # Host-specific system configuration
│   ├── hackeros.conf
│   ├── passwd
│   ├── group
│   └── fstab
├── home/           # User home directories
│   └── [username]/
│       ├── .bashrc
│       ├── .config/
│       └── Documents/
├── lib/            # Essential shared libraries
├── media/          # Mount point for removable media
├── mnt/            # Temporary mount point
├── opt/            # Optional application software
├── proc/           # Virtual filesystem for process information
├── root/           # Root user home directory
├── sbin/           # Essential system binaries
├── srv/            # Service data
├── sys/            # Virtual filesystem for system information
├── tmp/            # Temporary files
├── usr/            # Secondary hierarchy
│   ├── bin/        # Non-essential command binaries
│   ├── lib/        # Libraries for binaries in /usr/bin/
│   ├── local/      # Local hierarchy
│   └── share/      # Architecture-independent data
└── var/            # Variable data files
    ├── log/        # Log files
    ├── mail/       # User mailbox files
    └── tmp/        # Temporary files preserved between reboots
```

## Implementation Strategy

### Phase 1.2.1: Virtual File System Foundation
1. **Create core interfaces** - IVirtualFileSystem, IFileSystemNode
2. **Implement base classes** - VirtualFileSystemNode, VirtualFile, VirtualDirectory
3. **Create VirtualFileSystem service** - Main filesystem implementation
4. **Add basic file operations** - Create, read, write, delete

### Phase 1.2.2: Linux-Style Features
1. **Implement FilePermissions class** - rwx permissions for owner/group/other
2. **Add path resolution** - Support for ., .., ~, absolute and relative paths
3. **Create ownership system** - User and group ownership tracking
4. **Add symbolic links** - Link creation and resolution with cycle detection

### Phase 1.2.3: File System Operations
1. **Implement file descriptors** - File handle management with process tracking
2. **Add mount point system** - Support for mounting different filesystem types
3. **Create file locking** - Prevent concurrent access conflicts
4. **Add metadata management** - Timestamps, file types, extended attributes

### Phase 1.2.4: Persistence Layer
1. **Create IndexedDB storage** - Browser-based persistent storage
2. **Implement serialization** - Efficient filesystem state persistence
3. **Add data integrity** - Checksums and corruption detection
4. **Create backup/restore** - Filesystem snapshot functionality

### Phase 1.2.5: HackerOS.System.IO Namespace
1. **Create static utility classes** - File, Directory, Path utilities
2. **Add session context** - Current user and permission validation
3. **Implement permission checks** - Integration with security module
4. **Create async/await patterns** - Non-blocking file operations

## Performance Considerations

### Browser Limitations
1. **IndexedDB Performance**: Optimize for large file operations
2. **Memory Usage**: Implement streaming for large files
3. **Async Operations**: Use proper async/await patterns for all I/O

### Optimization Strategies
1. **Lazy Loading**: Load directory contents only when accessed
2. **Caching**: Cache frequently accessed files and metadata
3. **Compression**: Compress large files in storage
4. **Batching**: Batch multiple file operations for better performance

## Security Model

### Access Control
1. **Permission Validation**: Check rwx permissions before file operations
2. **User Context**: Validate operations against current user context
3. **Path Traversal Protection**: Prevent access outside allowed directories
4. **Sandbox Isolation**: Isolate processes from each other's files

### Integration Points
1. **Kernel Module**: System calls for file operations (open, read, write, close)
2. **Security Module**: Permission checking and user authentication
3. **User Module**: Current user context and home directory management
4. **Shell Module**: Command execution and path resolution

## Testing Strategy

### Unit Tests
1. **File Operations**: Test all CRUD operations for files and directories
2. **Permission System**: Test rwx permissions and ownership
3. **Path Resolution**: Test all path formats and edge cases
4. **Serialization**: Test filesystem persistence and restoration

### Integration Tests
1. **Kernel Integration**: Test system call integration
2. **Security Integration**: Test permission validation
3. **Cross-Platform**: Test path handling across different environments

### Performance Tests
1. **Large File Handling**: Test with files of various sizes
2. **Directory Performance**: Test with directories containing many files
3. **Persistence Performance**: Test save/load times for large filesystems

## Success Criteria

### Functional Requirements
- [ ] Virtual filesystem can be created, saved, and restored
- [ ] All basic file operations work correctly (CRUD)
- [ ] Linux-style permissions and ownership function properly
- [ ] Path resolution handles all standard Unix path formats
- [ ] Symbolic links work with proper cycle detection
- [ ] Standard directory structure is created and maintained
- [ ] HackerOS.System.IO namespace provides convenient static methods
- [ ] Integration with kernel module system calls

### Performance Requirements
- [ ] File operations complete within acceptable time limits
- [ ] Large files (>10MB) can be handled without memory issues
- [ ] Directory listings with 1000+ files perform adequately
- [ ] Filesystem persistence doesn't block UI

### Security Requirements
- [ ] Permission system prevents unauthorized access
- [ ] Path traversal attacks are prevented
- [ ] User isolation is maintained between processes
- [ ] File operations validate user permissions correctly
