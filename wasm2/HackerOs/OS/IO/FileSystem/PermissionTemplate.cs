namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Predefined permission templates for common file and directory scenarios
    /// </summary>
    public enum PermissionTemplate
    {
        /// <summary>
        /// Private to the user (700 for directories, 600 for files)
        /// </summary>
        UserPrivate,
        
        /// <summary>
        /// User read/write, group read (750 for directories, 640 for files)
        /// </summary>
        UserReadGroupRead,
        
        /// <summary>
        /// User read/write/execute, other read/execute (755 for directories, 644 for files)
        /// </summary>
        UserReadable,
        
        /// <summary>
        /// User read/write/execute, other read/execute (755 for all)
        /// </summary>
        UserExecutable,
        
        /// <summary>
        /// User and group read/write/execute (770 for directories, 660 for files)
        /// </summary>
        GroupShared,
        
        /// <summary>
        /// User and group read/write/execute, other read/execute (775 for all)
        /// </summary>
        GroupExecutable,
        
        /// <summary>
        /// World readable (755 for directories, 644 for files)
        /// </summary>
        WorldReadable,
        
        /// <summary>
        /// World executable (755 for all)
        /// </summary>
        WorldExecutable,
        
        /// <summary>
        /// World writable (777 for directories, 666 for files)
        /// </summary>
        WorldWritable,
        
        /// <summary>
        /// Set user ID (4755)
        /// </summary>
        SetUID,
        
        /// <summary>
        /// Set group ID (2775 for directories, 2755 for files)
        /// </summary>
        SetGID,
        
        /// <summary>
        /// Directory with sticky bit (1755)
        /// </summary>
        StickyDir,
        
        /// <summary>
        /// Temporary directory (1777 - world writable with sticky bit)
        /// </summary>
        TempDir,
        
        /// <summary>
        /// Configuration file (640 - user read/write, group read)
        /// </summary>
        ConfigFile,
        
        /// <summary>
        /// Log file (640 - user read/write, group read)
        /// </summary>
        LogFile
    }
}
