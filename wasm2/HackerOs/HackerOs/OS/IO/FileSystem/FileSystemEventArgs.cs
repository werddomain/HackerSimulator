using System;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Event arguments for file system operations that provides information about file system changes.
    /// </summary>
    public class FileSystemEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the path of the file or directory that triggered the event.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of change that occurred.
        /// </summary>
        public WatcherChangeTypes ChangeType { get; set; }

        /// <summary>
        /// Gets or sets the name of the file or directory that changed.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the FileSystemEventArgs class.
        /// </summary>
        /// <param name="changeType">The type of file system event that occurred.</param>
        /// <param name="directory">The directory where the change occurred.</param>
        /// <param name="name">The name of the file or directory that changed.</param>
        public FileSystemEventArgs(WatcherChangeTypes changeType, string directory, string name)
        {
            ChangeType = changeType;
            Path = directory;
            Name = name;
        }
    }

    /// <summary>
    /// Describes the changes that might occur to a file or directory.
    /// </summary>
    public enum WatcherChangeTypes
    {
        /// <summary>
        /// The creation of a file or directory.
        /// </summary>
        Created = 1,

        /// <summary>
        /// The deletion of a file or directory.
        /// </summary>
        Deleted = 2,

        /// <summary>
        /// The change of a file or directory.
        /// </summary>
        Changed = 4,

        /// <summary>
        /// The renaming of a file or directory.
        /// </summary>
        Renamed = 8,

        /// <summary>
        /// The attributes of a file or directory were changed.
        /// </summary>
        AttributeChanged = 16
    }
}
