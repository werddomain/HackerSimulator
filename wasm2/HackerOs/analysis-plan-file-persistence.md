# Analysis Plan: File System Persistence for User Management

## Overview
This plan outlines the implementation strategy for enhancing the file system persistence capabilities of the UserManager class to fully support Unix-like user and group file formats.

## Goals
1. Implement proper parsing of `/etc/passwd` and `/etc/group` files
2. Implement robust error handling for file format issues
3. Implement atomic file writing to prevent corruption
4. Ensure full bi-directional compatibility between in-memory and file representations

## Current Status
- UserManager already has placeholders for `LoadUsersFromFileAsync()` and `LoadGroupsFromFileAsync()`
- Basic file writing is implemented in `SaveUsersToFileAsync()` and `SaveGroupsToFileAsync()`
- File structures are already created in the expected locations (`/etc/passwd` and `/etc/group`)
- The code currently writes the files but doesn't fully parse them on loading

## Implementation Plan

### 1. Enhance LoadUsersFromFileAsync

#### File Format
Standard `/etc/passwd` format:
```
username:x:uid:gid:full_name:home_directory:shell
```

Where:
- `username`: User login name
- `x`: Password placeholder (actual hashes stored in `/etc/shadow`)
- `uid`: Numeric user ID
- `gid`: Primary group ID
- `full_name`: User's full name or comment field
- `home_directory`: User's home directory path
- `shell`: User's default shell

#### Implementation Steps
1. Read file content line by line
2. Parse each line according to the format
3. Create User objects and add to dictionaries
4. Handle malformed lines with appropriate error handling
5. Update _nextUserId based on maximum UID found

### 2. Enhance LoadGroupsFromFileAsync

#### File Format
Standard `/etc/group` format:
```
groupname:x:gid:member1,member2,...
```

Where:
- `groupname`: Group name
- `x`: Password placeholder (historical)
- `gid`: Numeric group ID
- `member1,member2,...`: Comma-separated list of usernames in the group

#### Implementation Steps
1. Read file content line by line
2. Parse each line according to the format
3. Create Group objects and add to dictionaries
4. Handle malformed lines with appropriate error handling
5. Update _nextGroupId based on maximum GID found
6. Associate users with their groups

### 3. Implement Atomic File Writing

To prevent file corruption during saves, we'll implement atomic file writing:

1. Write to a temporary file in the same directory
2. Ensure the temporary file is fully written and flushed to storage
3. Use atomic rename operation to replace the original file
4. Add error handling to clean up temporary files if the operation fails

### 4. Error Handling Improvements

We'll enhance error handling in multiple ways:

1. Add specific error types for file format issues
2. Log detailed error information for debugging
3. Implement recovery mechanisms for malformed files
4. Add validation before writing files
5. Implement backup/restore capability for critical system files

### 5. Testing Strategy

We need to verify:

1. Correct parsing of standard format files
2. Graceful handling of malformed files
3. Proper reconstruction of user-group relationships
4. Atomic file writing under concurrent access
5. Performance with large user/group databases

## Implementation Notes

- Use `FileInfo` and `FileStream` classes for proper file handling
- Consider using a transactional approach for file updates
- Use a parser-combinator approach for flexible format handling
- Keep compatibility with existing code
- Document any file format extensions specific to HackerOS

## Dependencies

- `IVirtualFileSystem` for file operations
- `OS.User.User` and `OS.User.Group` classes
- Logging facilities for error reporting

## Next Steps

After implementing file persistence, we will proceed with creating default user configuration files.
