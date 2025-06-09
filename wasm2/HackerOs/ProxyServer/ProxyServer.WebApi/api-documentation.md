# File System API Documentation

This document describes the HTTP API endpoints for the File System Access Module of the ProxyServer application.

## Base URL

All API endpoints are relative to the base URL: `/api`

## Authentication

*NOTE: Authentication is not yet implemented. This section will be updated when authentication is added.*

## Response Format

Most API responses follow this general structure:

```json
{
  "success": true,
  "errorMessage": null,
  "data": { ... }
}
```

In case of errors:

```json
{
  "success": false,
  "errorMessage": "Error description",
  "data": null
}
```

## Shared Folders API

### List all shared folders

**GET** `/folders`

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "hostPath": "C:\\Shared\\Documents",
      "alias": "documents",
      "permission": "ReadOnly",
      "allowedExtensions": [".txt", ".md", ".pdf"],
      "blockedExtensions": null,
      "metadataFileName": ".mount_info.json",
      "lastAccessed": "2025-06-03T10:15:00Z",
      "createdAt": "2025-05-20T15:30:00Z"
    }
  ]
}
```

### Get a specific shared folder

**GET** `/folders/{id}`

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "hostPath": "C:\\Shared\\Documents",
    "alias": "documents",
    "permission": "ReadOnly",
    "allowedExtensions": [".txt", ".md", ".pdf"],
    "blockedExtensions": null,
    "metadataFileName": ".mount_info.json",
    "lastAccessed": "2025-06-03T10:15:00Z",
    "createdAt": "2025-05-20T15:30:00Z"
  }
}
```

### Create a new shared folder

**POST** `/folders`

**Request Body:**
```json
{
  "hostPath": "C:\\Shared\\Documents",
  "alias": "documents",
  "permission": "read-only",
  "allowedExtensions": [".txt", ".md", ".pdf"],
  "blockedExtensions": null
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "hostPath": "C:\\Shared\\Documents",
    "alias": "documents",
    "permission": "ReadOnly",
    "allowedExtensions": [".txt", ".md", ".pdf"],
    "blockedExtensions": null,
    "metadataFileName": ".mount_info.json",
    "lastAccessed": "2025-06-03T10:15:00Z",
    "createdAt": "2025-06-03T10:15:00Z"
  }
}
```

### Update a shared folder

**PUT** `/folders/{id}`

**Request Body:**
```json
{
  "alias": "new-documents",
  "permission": "read-write",
  "allowedExtensions": [".txt", ".md", ".pdf", ".docx"]
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "hostPath": "C:\\Shared\\Documents",
    "alias": "new-documents",
    "permission": "ReadWrite",
    "allowedExtensions": [".txt", ".md", ".pdf", ".docx"],
    "blockedExtensions": null,
    "metadataFileName": ".mount_info.json",
    "lastAccessed": "2025-06-03T10:15:00Z",
    "createdAt": "2025-05-20T15:30:00Z"
  }
}
```

### Delete a shared folder

**DELETE** `/folders/{id}`

**Response:**
```json
{
  "success": true,
  "data": true
}
```

## Mount Points API

### List all mount points

**GET** `/mounts`

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "virtualPath": "/documents",
      "sharedFolderId": "shared-folder-uuid",
      "sharedFolderAlias": "documents",
      "options": {
        "readOnly": true,
        "caseSensitive": false,
        "trackAccess": true,
        "followSymlinks": false,
        "maxFileSize": 0
      },
      "createdAt": "2025-05-20T15:30:00Z",
      "lastAccessed": "2025-06-03T10:15:00Z",
      "isActive": true
    }
  ]
}
```

### Get a specific mount point

**GET** `/mounts/{id}`

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "virtualPath": "/documents",
    "sharedFolderId": "shared-folder-uuid",
    "sharedFolderAlias": "documents",
    "options": {
      "readOnly": true,
      "caseSensitive": false,
      "trackAccess": true,
      "followSymlinks": false,
      "maxFileSize": 0
    },
    "createdAt": "2025-05-20T15:30:00Z",
    "lastAccessed": "2025-06-03T10:15:00Z",
    "isActive": true
  }
}
```

### Create a new mount point

**POST** `/mounts`

**Request Body:**
```json
{
  "sharedFolderId": "shared-folder-uuid",
  "virtualPath": "/documents",
  "options": {
    "readOnly": true,
    "caseSensitive": false,
    "trackAccess": true,
    "followSymlinks": false,
    "maxFileSize": 0
  }
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "virtualPath": "/documents",
    "sharedFolderId": "shared-folder-uuid",
    "sharedFolderAlias": "documents",
    "options": {
      "readOnly": true,
      "caseSensitive": false,
      "trackAccess": true,
      "followSymlinks": false,
      "maxFileSize": 0
    },
    "createdAt": "2025-06-03T10:15:00Z",
    "lastAccessed": "2025-06-03T10:15:00Z",
    "isActive": true
  }
}
```

### Update a mount point

**PUT** `/mounts/{id}`

**Request Body:**
```json
{
  "options": {
    "readOnly": false,
    "caseSensitive": true,
    "trackAccess": true,
    "followSymlinks": false,
    "maxFileSize": 104857600
  }
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "virtualPath": "/documents",
    "sharedFolderId": "shared-folder-uuid",
    "sharedFolderAlias": "documents",
    "options": {
      "readOnly": false,
      "caseSensitive": true,
      "trackAccess": true,
      "followSymlinks": false,
      "maxFileSize": 104857600
    },
    "createdAt": "2025-05-20T15:30:00Z",
    "lastAccessed": "2025-06-03T10:15:00Z",
    "isActive": true
  }
}
```

### Delete a mount point

**DELETE** `/mounts/{id}?permanently=true`

**Query Parameters:**
- `permanently`: (optional, boolean) Whether to permanently delete the mount point or just deactivate it.

**Response:**
```json
{
  "success": true,
  "data": true
}
```

## Files API

### List directory contents

**GET** `/files?path=/documents/folder`

**Query Parameters:**
- `path`: (required) Virtual path of the directory to list.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "name": "file.txt",
      "virtualPath": "/documents/folder/file.txt",
      "isDirectory": false,
      "size": 1024,
      "lastModified": "2025-06-03T10:15:00Z",
      "lastAccessed": "2025-06-03T10:15:00Z",
      "creationTime": "2025-05-20T15:30:00Z",
      "attributes": "Archive"
    },
    {
      "name": "subfolder",
      "virtualPath": "/documents/folder/subfolder",
      "isDirectory": true,
      "size": 0,
      "lastModified": "2025-06-03T10:15:00Z",
      "lastAccessed": "2025-06-03T10:15:00Z",
      "creationTime": "2025-05-20T15:30:00Z",
      "attributes": "Directory"
    }
  ]
}
```

### Get file content

**GET** `/files/content?path=/documents/folder/file.txt`

**Query Parameters:**
- `path`: (required) Virtual path of the file to read.

**Response:**
- Returns the file content with appropriate content type header.

### Write file content

**POST** `/files/content`

**Request Body:**
```json
{
  "virtualPath": "/documents/folder/file.txt",
  "content": "SGVsbG8gd29ybGQh",  // Base64 encoded content
  "overwrite": true
}
```

**Response:**
```json
{
  "success": true,
  "data": true
}
```

### Delete a file or directory

**DELETE** `/files?path=/documents/folder/file.txt&recursive=false`

**Query Parameters:**
- `path`: (required) Virtual path of the file or directory to delete.
- `recursive`: (optional, boolean) Whether to recursively delete directories (default: false).

**Response:**
```json
{
  "success": true,
  "data": true
}
```

### Create a directory

**POST** `/files/mkdir`

**Request Body:**
```json
{
  "virtualPath": "/documents/new-folder",
  "createParents": true
}
```

**Response:**
```json
{
  "success": true,
  "data": true
}
```

### Copy files or directories

**POST** `/files/copy`

**Request Body:**
```json
{
  "sourcePath": "/documents/folder/file.txt",
  "destinationPath": "/documents/folder/file-copy.txt",
  "overwrite": false,
  "recursive": true
}
```

**Response:**
```json
{
  "success": true,
  "data": true
}
```

### Move files or directories

**POST** `/files/move`

**Request Body:**
```json
{
  "sourcePath": "/documents/folder/file.txt",
  "destinationPath": "/documents/other-folder/file.txt",
  "overwrite": false
}
```

**Response:**
```json
{
  "success": true,
  "data": true
}
```

## Error Codes

- 400 Bad Request - Invalid input or parameters
- 401 Unauthorized - Authentication required
- 403 Forbidden - Permission denied
- 404 Not Found - Resource not found
- 409 Conflict - Resource already exists or is in use
- 500 Internal Server Error - Server error
- 501 Not Implemented - Feature not yet implemented
