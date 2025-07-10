using System;
using System.Collections.Generic;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Defines a permission template that can be applied to files and directories
    /// </summary>
    public class PermissionTemplate
    {
        /// <summary>
        /// Gets or sets the default permissions for new files
        /// </summary>
        public FilePermissions FilePermissions { get; set; } = FilePermissions.FromOctal(644); // rw-r--r--

        /// <summary>
        /// Gets or sets the default permissions for new directories
        /// </summary>
        public FilePermissions DirectoryPermissions { get; set; } = FilePermissions.FromOctal(755); // rwxr-xr-x

        /// <summary>
        /// Gets or sets the default owner for new files and directories
        /// </summary>
        public string Owner { get; set; } = "root";

        /// <summary>
        /// Gets or sets the default group for new files and directories
        /// </summary>
        public string Group { get; set; } = "root";

        /// <summary>
        /// Creates a new permission template with the default values (644 for files, 755 for directories)
        /// </summary>
        public PermissionTemplate()
        {
        }

        /// <summary>
        /// Creates a new permission template with the specified values
        /// </summary>
        public PermissionTemplate(FilePermissions filePermissions, FilePermissions directoryPermissions, string owner, string group)
        {
            FilePermissions = filePermissions;
            DirectoryPermissions = directoryPermissions;
            Owner = owner;
            Group = group;
        }

        /// <summary>
        /// Creates a new permission template using the umask format
        /// </summary>
        public static PermissionTemplate FromUmask(int umask, string owner = "root", string group = "root")
        {
            // Umask works by removing bits from 666 (files) and 777 (directories)
            int fileMode = 0666 & ~umask;
            int dirMode = 0777 & ~umask;

            return new PermissionTemplate(
                FilePermissions.FromOctal(fileMode),
                FilePermissions.FromOctal(dirMode),
                owner,
                group
            );
        }
    }
}
