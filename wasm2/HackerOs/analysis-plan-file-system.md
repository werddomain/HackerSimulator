# File System Access Module Analysis Plan

## Overview
This document outlines the design and implementation approach for the ProxyServer's File System Access Module, which enables secure access to the host file system from the WebAssembly-based HackerOS simulator.

## Key Requirements

1. **Security**: Implement a robust sandbox that prevents unauthorized access to system files
2. **Configuration**: Create a flexible shared folder configuration system
3. **Abstraction**: Design an API that simplifies file operations across the WebSocket boundary
4. **Mount Points**: Support virtual mounting of host folders in the simulator
5. **Metadata**: Track file system access through hidden metadata files
6. **Permissions**: Implement a permission model that respects host OS restrictions

## Architecture Components

### 1. File System Security Sandbox

#### Design Considerations
- **Path Traversal Prevention**: Implement strict path validation to prevent ".." exploits
- **Root Path Constraints**: Limit access to specifically configured shared folders only
- **Symbolic Link Resolution**: Handle symlinks securely to prevent sandbox escapes
- **File Extension Whitelisting/Blacklisting**: Optional control over accessible file types

#### Implementation Approach
```csharp
public class FileSystemSecurity
{
    // Validate that a path is within the allowed shared folders
    public bool ValidatePath(string requestedPath, out string normalizedPath)
    
    // Check if a file operation is permitted
    public bool IsOperationAllowed(FileOperation operation, string path, string user)
    
    // Audit file access operations
    public void LogAccess(string user, string path, FileOperationType operation, bool success)
}
```

### 2. Shared Folder Configuration System

#### Shared Folder Model
```json
{
  "id": "uuid",
  "hostPath": "C:\\Shared\\Documents",
  "alias": "documents",
  "permissions": "read-write",
  "allowedExtensions": [".txt", ".md", ".pdf"],
  "mountInfo": ".mount_info.json"
}
```

#### Implementation Approach
```csharp
public class SharedFolderManager
{
    // Get all configured shared folders
    public IEnumerable<SharedFolderInfo> GetSharedFolders()
    
    // Add a new shared folder
    public SharedFolderInfo AddSharedFolder(string hostPath, string alias, string permissions)
    
    // Remove a shared folder
    public bool RemoveSharedFolder(string id)
    
    // Save configuration to file
    private void SaveConfiguration()
    
    // Load configuration from file
    private void LoadConfiguration()
}
```

### 3. File Operation Abstraction Layer

#### Operations to Support
- File creation, reading, writing, deletion
- Directory listing, creation, deletion
- File/directory copy and move
- File attributes and properties
- File watching for changes

#### Implementation Approach
```csharp
public class FileSystemOperations
{
    // File operations
    public byte[] ReadFile(string path)
    public bool WriteFile(string path, byte[] content)
    public bool DeleteFile(string path)
    
    // Directory operations
    public IEnumerable<FileSystemEntry> ListDirectory(string path)
    public bool CreateDirectory(string path)
    public bool DeleteDirectory(string path, bool recursive)
    
    // Advanced operations
    public bool CopyFile(string sourcePath, string destinationPath, bool overwrite)
    public bool MoveFile(string sourcePath, string destinationPath)
    public FileAttributes GetAttributes(string path)
}
```

### 4. Mount Point Management

#### Mount Point Configuration
- Virtual path in HackerOS
- Reference to shared folder
- Mount options (read-only, case-sensitivity, etc.)

#### Implementation Approach
```csharp
public class MountPointManager
{
    // Mount a shared folder to a virtual path
    public bool MountSharedFolder(string sharedFolderId, string virtualPath, MountOptions options)
    
    // Unmount a virtual path
    public bool UnmountPath(string virtualPath)
    
    // List all active mount points
    public IEnumerable<MountPoint> ListMountPoints()
    
    // Resolve a virtual path to a real host path
    public string ResolveVirtualPath(string virtualPath, out SharedFolderInfo sharedFolder)
}
```

### 5. Hidden Metadata File Handling

#### Metadata Structure
```json
{
  "created": "2025-05-20T15:30:00Z",
  "lastAccessed": "2025-06-03T10:15:00Z",
  "accessLog": [
    {
      "user": "hackeros-user",
      "timestamp": "2025-06-03T10:15:00Z",
      "operation": "READ"
    }
  ],
  "permissions": {
    "owner": "rwx",
    "group": "r-x",
    "others": "r--"
  }
}
```

#### Implementation Approach
```csharp
public class MetadataFileManager
{
    // Get metadata for a file or directory
    public FileMetadata GetMetadata(string path)
    
    // Update access information
    public void UpdateAccessInfo(string path, string user, FileOperationType operation)
    
    // Set permissions
    public void SetPermissions(string path, FilePermissions permissions)
    
    // Load metadata from .mount_info.json
    private FileMetadata LoadMetadataFile(string path)
    
    // Save metadata to .mount_info.json
    private void SaveMetadataFile(string path, FileMetadata metadata)
}
```

### 6. File Permission Validation

#### Permission Model
- Follow Linux-like rwx permission system
- Map to host OS permissions where possible
- Implement additional permission layers for shared folders

#### Implementation Approach
```csharp
public class FilePermissionManager
{
    // Check if operation is allowed based on permissions
    public bool CheckPermission(string user, string path, FileOperationType operation)
    
    // Apply new permissions to a file
    public bool ApplyPermissions(string path, FilePermissions permissions)
    
    // Map between HackerOS permissions and host OS permissions
    private bool MapAndApplyHostPermissions(string path, FilePermissions permissions)
}
```

## API Design for HTTP Endpoints

### Folder Management Endpoints
- `GET /api/folders` - List all shared folders
- `GET /api/folders/{id}` - Get details of a specific shared folder
- `POST /api/folders` - Add a new shared folder
- `DELETE /api/folders/{id}` - Remove a shared folder
- `PUT /api/folders/{id}` - Update a shared folder configuration

### Mount Points Endpoints
- `GET /api/mounts` - List all mount points
- `POST /api/mounts` - Create a new mount point
- `DELETE /api/mounts/{virtualPath}` - Remove a mount point

### File Operations Endpoints
- `GET /api/files` - List directory contents
- `GET /api/files/content` - Download a file
- `POST /api/files/content` - Upload a file
- `DELETE /api/files` - Delete a file or directory
- `POST /api/files/copy` - Copy files or directories
- `POST /api/files/move` - Move files or directories
- `POST /api/files/mkdir` - Create a directory

## Security Considerations

1. **Access Control**:
   - Validate all paths before any file operation
   - Check permissions for each operation
   - Audit all file access attempts

2. **Path Security**:
   - Normalize all paths to prevent directory traversal
   - Verify paths are within configured shared folders
   - Handle symlinks securely

3. **Rate Limiting**:
   - Implement limits on file operations to prevent abuse
   - Add size limits for file uploads
   - Monitor and restrict high-frequency operations

4. **Data Integrity**:
   - Validate file content for uploads
   - Implement checksums for data transfer
   - Handle conflicts when multiple clients access the same files

## Implementation Roadmap

1. **Phase 1**: Core Security Model
   - Implement FileSystemSecurity class
   - Create SharedFolderManager
   - Develop path validation logic
   
2. **Phase 2**: Configuration and Operations
   - Build shared folder configuration UI
   - Implement HTTP API endpoints
   - Create basic file operations

3. **Phase 3**: Mount Points and Metadata
   - Develop mount point management
   - Create metadata tracking system
   - Implement permission validation
   
4. **Phase 4**: Advanced Features
   - Add file watching for live updates
   - Implement batch operations
   - Enhance security and performance

## Integration with WebAssembly HackerOS

The File System Access Module will interact with the HackerOS simulator through:

1. **WebSocket-based Protocol**:
   - Realtime file system events (file changes, etc.)
   - Status updates and notifications

2. **HTTP API Endpoints**:
   - CRUD operations for files and folders
   - Configuration management
   
3. **Mount Point Integration**:
   - Virtual file system mapping in HackerOS
   - Path translation between host and virtual paths

## Testing Strategy

1. **Unit Testing**:
   - Path validation logic
   - Permission checks
   - Configuration management

2. **Integration Testing**:
   - Full file operations workflow
   - Mount point management
   - API endpoint functionality

3. **Security Testing**:
   - Path traversal attack prevention
   - Permission bypass attempts
   - Rate limit effectiveness
   
4. **Performance Testing**:
   - Large file handling
   - Concurrent operations
   - Memory usage under load
