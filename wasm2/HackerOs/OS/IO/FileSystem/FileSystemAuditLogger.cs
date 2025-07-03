using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Severity level for audit log entries
    /// </summary>
    public enum AuditSeverity
    {
        /// <summary>
        /// Informational events that don't require attention
        /// </summary>
        Information,
        
        /// <summary>
        /// Warning events that might require attention
        /// </summary>
        Warning,
        
        /// <summary>
        /// Error events that require attention
        /// </summary>
        Error,
        
        /// <summary>
        /// Critical security events that require immediate attention
        /// </summary>
        Critical
    }
    
    /// <summary>
    /// Types of events that can be audited
    /// </summary>
    public enum AuditEventType
    {
        /// <summary>
        /// File system access (read/write/execute)
        /// </summary>
        Access,
        
        /// <summary>
        /// File system modification (create/delete/move)
        /// </summary>
        Modification,
        
        /// <summary>
        /// Permission change (chmod/chown)
        /// </summary>
        PermissionChange,
        
        /// <summary>
        /// Authentication event
        /// </summary>
        Authentication,
        
        /// <summary>
        /// Quota event
        /// </summary>
        Quota,
        
        /// <summary>
        /// Policy event
        /// </summary>
        Policy,
        
        /// <summary>
        /// System configuration change
        /// </summary>
        Configuration,
        
        /// <summary>
        /// Administrative action
        /// </summary>
        Administrative
    }
    
    /// <summary>
    /// Represents a single audit log entry
    /// </summary>
    public class AuditLogEntry
    {
        /// <summary>
        /// Gets or sets the timestamp of the event
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the user who performed the action
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// Gets or sets the user ID who performed the action
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// Gets or sets the event type
        /// </summary>
        public AuditEventType EventType { get; set; }
        
        /// <summary>
        /// Gets or sets the severity level
        /// </summary>
        public AuditSeverity Severity { get; set; }
        
        /// <summary>
        /// Gets or sets the path of the file or directory
        /// </summary>
        public string Path { get; set; }
        
        /// <summary>
        /// Gets or sets the operation being performed
        /// </summary>
        public string Operation { get; set; }
        
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the reason for failure if the operation failed
        /// </summary>
        public string FailureReason { get; set; }
        
        /// <summary>
        /// Gets or sets additional context for the event
        /// </summary>
        public Dictionary<string, object> Context { get; set; }
        
        /// <summary>
        /// Creates a new audit log entry
        /// </summary>
        public AuditLogEntry()
        {
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
            Username = "unknown";
            Path = string.Empty;
            Operation = string.Empty;
            FailureReason = string.Empty;
        }
        
        /// <summary>
        /// Creates a new audit log entry with the specified parameters
        /// </summary>
        /// <param name="user">The user performing the action</param>
        /// <param name="eventType">The type of event</param>
        /// <param name="severity">The severity level</param>
        /// <param name="path">The file or directory path</param>
        /// <param name="operation">The operation being performed</param>
        /// <param name="success">Whether the operation was successful</param>
        public AuditLogEntry(User user, AuditEventType eventType, AuditSeverity severity, string path, string operation, bool success)
            : this()
        {
            Username = user?.Username ?? "unknown";
            UserId = user?.Id ?? -1;
            EventType = eventType;
            Severity = severity;
            Path = path ?? string.Empty;
            Operation = operation ?? string.Empty;
            Success = success;
        }
        
        /// <summary>
        /// Adds additional context to the audit log entry
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="value">The context value</param>
        public void AddContext(string key, object value)
        {
            if (key != null)
            {
                Context[key] = value;
            }
        }
        
        /// <summary>
        /// Converts the audit log entry to a JSON string
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        
        /// <summary>
        /// Returns a string representation of the audit log entry
        /// </summary>
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Severity}] [{EventType}] User: {Username} ({UserId}) | Path: {Path} | Operation: {Operation} | Success: {Success}{(Success ? "" : $" | Failure: {FailureReason}")}";
        }
    }
    
    /// <summary>
    /// Logger for file system security audit events
    /// </summary>
    public class FileSystemAuditLogger
    {
        private readonly VirtualFileSystem _fileSystem;
        private readonly List<AuditLogEntry> _memoryLog;
        private readonly int _maxMemoryEntries;
        private bool _loggingEnabled;
        private string _auditLogPath;
        private AuditSeverity _minimumSeverity;
        private bool _logToConsole;
        
        /// <summary>
        /// Event fired when a new audit log entry is created
        /// </summary>
        public event EventHandler<AuditLogEntry> AuditLogEntryCreated;
        
        /// <summary>
        /// Gets or sets whether logging is enabled
        /// </summary>
        public bool LoggingEnabled 
        { 
            get => _loggingEnabled; 
            set => _loggingEnabled = value; 
        }
        
        /// <summary>
        /// Gets or sets the minimum severity level to log
        /// </summary>
        public AuditSeverity MinimumSeverity 
        { 
            get => _minimumSeverity; 
            set => _minimumSeverity = value; 
        }
        
        /// <summary>
        /// Gets or sets whether to log to console
        /// </summary>
        public bool LogToConsole 
        { 
            get => _logToConsole; 
            set => _logToConsole = value; 
        }
        
        /// <summary>
        /// Creates a new instance of the FileSystemAuditLogger
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="auditLogPath">Path to the audit log file</param>
        /// <param name="maxMemoryEntries">Maximum number of entries to keep in memory</param>
        public FileSystemAuditLogger(VirtualFileSystem fileSystem, string auditLogPath = "/var/log/audit.log", int maxMemoryEntries = 1000)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _memoryLog = new List<AuditLogEntry>();
            _maxMemoryEntries = maxMemoryEntries;
            _auditLogPath = auditLogPath;
            _loggingEnabled = true;
            _minimumSeverity = AuditSeverity.Information;
            _logToConsole = false;
        }
        
        /// <summary>
        /// Logs an audit event
        /// </summary>
        /// <param name="user">The user performing the action</param>
        /// <param name="eventType">The type of event</param>
        /// <param name="severity">The severity level</param>
        /// <param name="path">The file or directory path</param>
        /// <param name="operation">The operation being performed</param>
        /// <param name="success">Whether the operation was successful</param>
        /// <param name="failureReason">The reason for failure if the operation failed</param>
        /// <returns>The created audit log entry</returns>
        public async Task<AuditLogEntry> LogAsync(
            User user,
            AuditEventType eventType,
            AuditSeverity severity,
            string path,
            string operation,
            bool success,
            string failureReason = null)
        {
            if (!_loggingEnabled || severity < _minimumSeverity)
                return null;
                
            var entry = new AuditLogEntry(user, eventType, severity, path, operation, success)
            {
                FailureReason = failureReason ?? string.Empty
            };
            
            // Add to in-memory log
            _memoryLog.Add(entry);
            
            // Trim memory log if needed
            if (_memoryLog.Count > _maxMemoryEntries)
            {
                _memoryLog.RemoveRange(0, _memoryLog.Count - _maxMemoryEntries);
            }
            
            // Write to log file
            try
            {
                await WriteToLogFileAsync(entry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to audit log: {ex.Message}");
            }
            
            // Log to console if enabled
            if (_logToConsole)
            {
                Console.WriteLine(entry.ToString());
            }
            
            // Raise event
            AuditLogEntryCreated?.Invoke(this, entry);
            
            return entry;
        }
        
        /// <summary>
        /// Logs a security event
        /// </summary>
        /// <param name="user">The user performing the action</param>
        /// <param name="path">The file or directory path</param>
        /// <param name="operation">The operation being performed</param>
        /// <param name="securityResult">The security check result</param>
        /// <returns>The created audit log entry</returns>
        public async Task<AuditLogEntry> LogSecurityEventAsync(
            User user,
            string path,
            string operation,
            SecurityCheckResult securityResult)
        {
            AuditEventType eventType;
            AuditSeverity severity;
            
            // Determine event type based on operation
            if (operation.StartsWith("read", StringComparison.OrdinalIgnoreCase))
            {
                eventType = AuditEventType.Access;
            }
            else if (operation.StartsWith("write", StringComparison.OrdinalIgnoreCase) ||
                     operation.StartsWith("create", StringComparison.OrdinalIgnoreCase) ||
                     operation.StartsWith("delete", StringComparison.OrdinalIgnoreCase) ||
                     operation.StartsWith("move", StringComparison.OrdinalIgnoreCase))
            {
                eventType = AuditEventType.Modification;
            }
            else if (operation.StartsWith("chmod", StringComparison.OrdinalIgnoreCase) ||
                     operation.StartsWith("chown", StringComparison.OrdinalIgnoreCase))
            {
                eventType = AuditEventType.PermissionChange;
            }
            else
            {
                eventType = AuditEventType.Access;
            }
            
            // Determine severity based on result
            if (securityResult.Success)
            {
                severity = AuditSeverity.Information;
            }
            else
            {
                // Permission denials are warnings
                if (securityResult.DenialReason == SecurityDenialReason.PermissionDenied)
                {
                    severity = AuditSeverity.Warning;
                }
                // Policy denials are errors
                else if (securityResult.DenialReason == SecurityDenialReason.PolicyDenied)
                {
                    severity = AuditSeverity.Error;
                }
                // Other failures are errors
                else
                {
                    severity = AuditSeverity.Error;
                }
            }
            
            var entry = await LogAsync(
                user,
                eventType,
                severity,
                path,
                operation,
                securityResult.Success,
                securityResult.DenialReason.ToString());
                
            if (entry != null)
            {
                // Add security-specific context
                entry.AddContext("SecurityCheckType", securityResult.CheckType);
                
                if (!securityResult.Success)
                {
                    entry.AddContext("DenialReason", securityResult.DenialReason.ToString());
                    entry.AddContext("DenialMessage", securityResult.DenialMessage);
                }
            }
            
            return entry;
        }
        
        /// <summary>
        /// Gets all audit log entries from memory
        /// </summary>
        /// <returns>The list of audit log entries</returns>
        public List<AuditLogEntry> GetMemoryLog()
        {
            return new List<AuditLogEntry>(_memoryLog);
        }
        
        /// <summary>
        /// Gets filtered audit log entries from memory
        /// </summary>
        /// <param name="filter">The filter function</param>
        /// <returns>The filtered list of audit log entries</returns>
        public List<AuditLogEntry> GetFilteredMemoryLog(Func<AuditLogEntry, bool> filter)
        {
            return _memoryLog.FindAll(e => filter(e));
        }
        
        /// <summary>
        /// Clears the memory log
        /// </summary>
        public void ClearMemoryLog()
        {
            _memoryLog.Clear();
        }
        
        /// <summary>
        /// Sets the path for the audit log file
        /// </summary>
        /// <param name="path">The new path</param>
        public void SetAuditLogPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                _auditLogPath = path;
            }
        }
        
        /// <summary>
        /// Reads the audit log file
        /// </summary>
        /// <returns>The content of the audit log file</returns>
        public async Task<string> ReadAuditLogAsync()
        {
            try
            {
                var bytes = await _fileSystem.ReadFileAsync(_auditLogPath);
                return bytes != null ? System.Text.Encoding.UTF8.GetString(bytes) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Writes an entry to the audit log file
        /// </summary>
        /// <param name="entry">The entry to write</param>
        private async Task WriteToLogFileAsync(AuditLogEntry entry)
        {
            try
            {
                // Create log entry line
                string logLine = $"{entry}\n";
                
                // Append to the log file
                byte[] content = System.Text.Encoding.UTF8.GetBytes(logLine);
                
                // Check if file exists
                bool exists = await _fileSystem.ExistsAsync(_auditLogPath);
                
                if (exists)
                {
                    // Read existing content
                    byte[] existingContent = await _fileSystem.ReadFileAsync(_auditLogPath);
                    
                    if (existingContent != null)
                    {
                        // Append new content
                        byte[] newContent = new byte[existingContent.Length + content.Length];
                        Array.Copy(existingContent, 0, newContent, 0, existingContent.Length);
                        Array.Copy(content, 0, newContent, existingContent.Length, content.Length);
                        
                        // Write back
                        await _fileSystem.WriteFileAsync(_auditLogPath, newContent);
                    }
                }
                else
                {
                    // Create directory if needed
                    string directory = System.IO.Path.GetDirectoryName(_auditLogPath).Replace("\\", "/");
                    if (!await _fileSystem.ExistsAsync(directory))
                    {
                        await _fileSystem.CreateDirectoryAsync(directory);
                    }
                    
                    // Create new file
                    await _fileSystem.CreateFileAsync(_auditLogPath, content);
                    
                    // Set permissions - root:root, 0640 (rw-r-----)
                    var node = await _fileSystem.GetNodeAsync(_auditLogPath);
                    if (node != null)
                    {
                        node.OwnerId = 0; // root
                        node.GroupId = 0; // root
                        node.Permissions = new FilePermissions(6, 4, 0); // rw-r-----
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to audit log file: {ex.Message}");
            }
        }
    }
}
