//Moved to \wasm2\HackerOs\HackerOs\OS\IO\FilePermissions.cs

//using System;
//using System.Text;

//namespace HackerOs.OS.IO.FileSystem
//{
//    /// <summary>
//    /// Represents Linux-style file permissions (rwx for owner, group, and others).
//    /// </summary>
//    public class FilePermissions
//    {
//        // Owner permissions
//        public bool OwnerRead { get; set; }
//        public bool OwnerWrite { get; set; }
//        public bool OwnerExecute { get; set; }

//        // Group permissions
//        public bool GroupRead { get; set; }
//        public bool GroupWrite { get; set; }
//        public bool GroupExecute { get; set; }

//        // Other permissions
//        public bool OtherRead { get; set; }
//        public bool OtherWrite { get; set; }
//        public bool OtherExecute { get; set; }

//        // Special permission bits
//        public bool SetUID { get; set; }
//        public bool SetGID { get; set; }
//        public bool Sticky { get; set; }

//        /// <summary>
//        /// Creates default permissions (644 for files, 755 for directories).
//        /// </summary>
//        /// <param name="isDirectory">Whether this is for a directory.</param>
//        public FilePermissions(bool isDirectory = false)
//        {
//            if (isDirectory)
//            {
//                // Default directory permissions: 755 (rwxr-xr-x)
//                OwnerRead = OwnerWrite = OwnerExecute = true;
//                GroupRead = GroupExecute = true;
//                OtherRead = OtherExecute = true;
//                GroupWrite = OtherWrite = false;
//            }
//            else
//            {
//                // Default file permissions: 644 (rw-r--r--)
//                OwnerRead = OwnerWrite = true;
//                GroupRead = OtherRead = true;
//                OwnerExecute = GroupWrite = GroupExecute = OtherWrite = OtherExecute = false;
//            }

//            // Special bits default to false
//            SetUID = SetGID = Sticky = false;
//        }

//        /// <summary>
//        /// Creates permissions from octal notation.
//        /// </summary>
//        /// <param name="octal">Octal permission value (e.g., 644, 755, 4755 for setuid).</param>
//        public FilePermissions(int octal)
//        {
//            SetFromOctal(octal);
//        }

//        /// <summary>
//        /// Creates permissions from rwx string notation.
//        /// </summary>
//        /// <param name="permissionString">Permission string (e.g., "rwxr-xr-x").</param>
//        public FilePermissions(string permissionString)
//        {
//            SetFromString(permissionString);
//        }

//        /// <summary>
//        /// Converts permissions to octal notation.
//        /// </summary>
//        /// <returns>Octal representation of permissions.</returns>
//        public int ToOctal()
//        {
//            int owner = (OwnerRead ? 4 : 0) + (OwnerWrite ? 2 : 0) + (OwnerExecute ? 1 : 0);
//            int group = (GroupRead ? 4 : 0) + (GroupWrite ? 2 : 0) + (GroupExecute ? 1 : 0);
//            int other = (OtherRead ? 4 : 0) + (OtherWrite ? 2 : 0) + (OtherExecute ? 1 : 0);
//            int special = (SetUID ? 4 : 0) + (SetGID ? 2 : 0) + (Sticky ? 1 : 0);

//            return special * 1000 + owner * 100 + group * 10 + other;
//        }

//        /// <summary>
//        /// Sets permissions from octal notation.
//        /// </summary>
//        /// <param name="octal">Octal permission value.</param>
//        public void SetFromOctal(int octal)
//        {
//            int special = (octal / 1000) % 10;
//            int owner = (octal / 100) % 10;
//            int group = (octal / 10) % 10;
//            int other = octal % 10;

//            OwnerRead = (owner & 4) != 0;
//            OwnerWrite = (owner & 2) != 0;
//            OwnerExecute = (owner & 1) != 0;

//            GroupRead = (group & 4) != 0;
//            GroupWrite = (group & 2) != 0;
//            GroupExecute = (group & 1) != 0;

//            OtherRead = (other & 4) != 0;
//            OtherWrite = (other & 2) != 0;
//            OtherExecute = (other & 1) != 0;

//            SetUID = (special & 4) != 0;
//            SetGID = (special & 2) != 0;
//            Sticky = (special & 1) != 0;
//        }

//        /// <summary>
//        /// Sets permissions from rwx string notation.
//        /// </summary>
//        /// <param name="permissionString">Permission string (e.g., "rwxr-xr-x").</param>
//        public void SetFromString(string permissionString)
//        {
//            if (string.IsNullOrEmpty(permissionString) || permissionString.Length != 9)
//            {
//                throw new ArgumentException("Permission string must be exactly 9 characters (rwxrwxrwx format)");
//            }

//            OwnerRead = permissionString[0] == 'r';
//            OwnerWrite = permissionString[1] == 'w';
//            OwnerExecute = permissionString[2] == 'x';

//            GroupRead = permissionString[3] == 'r';
//            GroupWrite = permissionString[4] == 'w';
//            GroupExecute = permissionString[5] == 'x';

//            OtherRead = permissionString[6] == 'r';
//            OtherWrite = permissionString[7] == 'w';
//            OtherExecute = permissionString[8] == 'x';
//        }        /// <summary>
//        /// Creates permissions from octal notation (static factory method).
//        /// </summary>
//        /// <param name="octal">Octal permission value (e.g., 644, 755).</param>
//        /// <returns>A new FilePermissions instance with the specified permissions.</returns>
//        public static FilePermissions FromOctal(int octal)
//        {
//            return new FilePermissions(octal);
//        }

//        /// <summary>
//        /// Checks if a user can access the file/directory with the specified access mode.
//        /// </summary>
//        /// <param name="fileOwner">The owner of the file.</param>
//        /// <param name="fileGroup">The group of the file.</param>
//        /// <param name="userId">The user requesting access.</param>
//        /// <param name="userGroupId">The primary group of the user.</param>
//        /// <param name="accessMode">The type of access required.</param>
//        /// <returns>True if access is granted; otherwise, false.</returns>
//        public bool CanAccess(string fileOwner, string fileGroup, string userId, string userGroupId, FileAccessMode accessMode)
//        {
//            // Root user has all permissions
//            if (userId == "root")
//                return true;

//            bool read, write, execute;

//            // Check if user is the owner
//            if (userId == fileOwner)
//            {
//                read = OwnerRead;
//                write = OwnerWrite;
//                execute = OwnerExecute;
//            }
//            // Check if user is in the file's group
//            else if (userGroupId == fileGroup)
//            {
//                read = GroupRead;
//                write = GroupWrite;
//                execute = GroupExecute;
//            }
//            // User is "other"
//            else
//            {
//                read = OtherRead;
//                write = OtherWrite;
//                execute = OtherExecute;
//            }

//            return accessMode switch
//            {
//                FileAccessMode.Read => read,
//                FileAccessMode.Write => write,
//                FileAccessMode.Execute => execute,
//                FileAccessMode.ReadWrite => read && write,
//                _ => false
//            };
//        }

//        /// <summary>
//        /// Enhanced permission check that considers setuid, setgid, and sticky bits
//        /// </summary>
//        /// <param name="user">The user requesting access</param>
//        /// <param name="fileOwnerId">The owner ID of the file</param>
//        /// <param name="fileGroupId">The group ID of the file</param>
//        /// <param name="accessMode">The type of access required</param>
//        /// <param name="isDirectory">Whether the node is a directory</param>
//        /// <returns>True if access is granted; otherwise, false</returns>
//        public bool CanAccess(User.User user, int fileOwnerId, int fileGroupId, FileAccessMode accessMode, bool isDirectory = false)
//        {
//            // Root always has all permissions
//            if (user.UserId == 0)
//                return true;

//            bool read, write, execute;

//            // Determine base permissions
//            if (user.UserId == fileOwnerId)
//            {
//                // User is the owner
//                read = OwnerRead;
//                write = OwnerWrite;
//                execute = OwnerExecute;
//            }
//            else if (user.PrimaryGroupId == fileGroupId || user.AdditionalGroups.Contains(fileGroupId))
//            {
//                // User is in the file's group
//                read = GroupRead;
//                write = GroupWrite;
//                execute = GroupExecute;
//            }
//            else
//            {
//                // User is "other"
//                read = OtherRead;
//                write = OtherWrite;
//                execute = OtherExecute;
//            }

//            // Handle special bits
//            if (accessMode == FileAccessMode.Execute && !isDirectory)
//            {
//                // SetUID: If the file is executable by the user and has the setuid bit,
//                // execution happens with the privileges of the file owner
//                if (SetUID && execute)
//                {
//                    // The execution will proceed with elevated privileges
//                    // We still need to return true based on the user's execute permission
//                    return true;
//                }

//                // SetGID: If the file is executable by the user and has the setgid bit,
//                // execution happens with the privileges of the file's group
//                if (SetGID && execute)
//                {
//                    // The execution will proceed with the group's privileges
//                    // We still need to return true based on the user's execute permission
//                    return true;
//                }
//            }
//            else if (isDirectory)
//            {
//                // For directories, SetGID means new files created in the directory
//                // inherit the directory's group rather than the user's primary group
//                // This doesn't affect access permissions check

//                // Sticky bit on directories: if set, only the owner of a file can delete or rename it,
//                // even if other users have write permissions on the directory
//                if (Sticky && accessMode == FileAccessMode.Write && write)
//                {
//                    // The sticky bit affects deletion/renaming, but we can't know the target operation here
//                    // The complete check must be done at the operation level (e.g., Delete, Move methods)
//                    // Here we just return the basic write permission
//                    return write;
//                }
//            }

//            // Return permission based on the access mode
//            return accessMode switch
//            {
//                FileAccessMode.Read => read,
//                FileAccessMode.Write => write,
//                FileAccessMode.Execute => execute,
//                FileAccessMode.ReadWrite => read && write,
//                _ => false
//            };
//        }

//        /// <summary>
//        /// Checks if a user has specific access to the file/directory.
//        /// </summary>
//        /// <param name="user">The user requesting access.</param>
//        /// <param name="owner">The owner of the file.</param>
//        /// <param name="group">The group of the file.</param>
//        /// <param name="userGroups">Groups the user belongs to.</param>
//        /// <param name="accessType">The type of access required.</param>
//        /// <returns>True if access is granted; otherwise, false.</returns>
//        public bool HasAccess(string user, string owner, string group, string[] userGroups, FileAccessType accessType)
//        {
//            // Root user has all permissions
//            if (user == "root")
//                return true;

//            bool read, write, execute;

//            // Check if user is the owner
//            if (user == owner)
//            {
//                read = OwnerRead;
//                write = OwnerWrite;
//                execute = OwnerExecute;
//            }
//            // Check if user is in the file's group
//            else if (userGroups.Contains(group))
//            {
//                read = GroupRead;
//                write = GroupWrite;
//                execute = GroupExecute;
//            }
//            // User is "other"
//            else
//            {
//                read = OtherRead;
//                write = OtherWrite;
//                execute = OtherExecute;
//            }

//            return accessType switch
//            {
//                FileAccessType.Read => read,
//                FileAccessType.Write => write,
//                FileAccessType.Execute => execute,
//                FileAccessType.ReadWrite => read && write,
//                FileAccessType.ReadExecute => read && execute,
//                FileAccessType.WriteExecute => write && execute,
//                FileAccessType.ReadWriteExecute => read && write && execute,
//                _ => false
//            };
//        }

//        /// <summary>
//        /// Creates a copy of the current permissions.
//        /// </summary>
//        /// <returns>A new FilePermissions instance with the same values.</returns>
//        public FilePermissions Clone()
//        {
//            return new FilePermissions
//            {
//                OwnerRead = OwnerRead,
//                OwnerWrite = OwnerWrite,
//                OwnerExecute = OwnerExecute,
//                GroupRead = GroupRead,
//                GroupWrite = GroupWrite,
//                GroupExecute = GroupExecute,
//                OtherRead = OtherRead,
//                OtherWrite = OtherWrite,
//                OtherExecute = OtherExecute
//            };
//        }

//        /// <summary>
//        /// Returns the string representation of permissions in rwxrwxrwx format.
//        /// </summary>
//        /// <returns>Permission string.</returns>
//        public override string ToString()
//        {
//            var sb = new StringBuilder(9);

//            sb.Append(OwnerRead ? 'r' : '-');
//            sb.Append(OwnerWrite ? 'w' : '-');
//            sb.Append(OwnerExecute ? 'x' : '-');

//            sb.Append(GroupRead ? 'r' : '-');
//            sb.Append(GroupWrite ? 'w' : '-');
//            sb.Append(GroupExecute ? 'x' : '-');

//            sb.Append(OtherRead ? 'r' : '-');
//            sb.Append(OtherWrite ? 'w' : '-');
//            sb.Append(OtherExecute ? 'x' : '-');

//            return sb.ToString();
//        }

//        /// <summary>
//        /// Returns a detailed string representation including octal notation.
//        /// </summary>
//        /// <returns>Detailed permission information.</returns>
//        public string ToDetailedString()
//        {
//            return $"{ToString()} ({ToOctal():D3})";
//        }

//        /// <summary>
//        /// Creates an enhanced string representation of permissions including special bits.
//        /// </summary>
//        /// <returns>Detailed permission string.</returns>
//        public string ToDetailedPermissionString()
//        {
//            var sb = new StringBuilder(10);

//            // First character indicates the type and special bits
//            if (SetUID)
//                sb.Append(OwnerExecute ? 's' : 'S');
//            else
//                sb.Append(OwnerExecute ? 'x' : '-');

//            if (SetGID)
//                sb.Append(GroupExecute ? 's' : 'S');
//            else
//                sb.Append(GroupExecute ? 'x' : '-');

//            if (Sticky)
//                sb.Append(OtherExecute ? 't' : 'T');
//            else
//                sb.Append(OtherExecute ? 'x' : '-');

//            // Standard permissions
//            sb.Append(OwnerRead ? 'r' : '-');
//            sb.Append(OwnerWrite ? 'w' : '-');

//            sb.Append(GroupRead ? 'r' : '-');
//            sb.Append(GroupWrite ? 'w' : '-');

//            sb.Append(OtherRead ? 'r' : '-');
//            sb.Append(OtherWrite ? 'w' : '-');

//            return sb.ToString();
//        }

//        /// <summary>
//        /// Sets permissions from enhanced string notation including special bits.
//        /// </summary>
//        /// <param name="permissionString">Enhanced permission string (e.g., "rwsrwxrwt").</param>
//        public void SetFromDetailedString(string permissionString)
//        {
//            if (string.IsNullOrEmpty(permissionString) || permissionString.Length != 9)
//            {
//                throw new ArgumentException("Permission string must be exactly 9 characters");
//            }

//            OwnerRead = permissionString[0] == 'r';
//            OwnerWrite = permissionString[1] == 'w';

//            // Special handling for setuid bit ('s', 'S')
//            if (permissionString[2] == 's' || permissionString[2] == 'S')
//            {
//                SetUID = true;
//                OwnerExecute = permissionString[2] == 's';
//            }
//            else
//            {
//                SetUID = false;
//                OwnerExecute = permissionString[2] == 'x';
//            }

//            GroupRead = permissionString[3] == 'r';
//            GroupWrite = permissionString[4] == 'w';

//            // Special handling for setgid bit ('s', 'S')
//            if (permissionString[5] == 's' || permissionString[5] == 'S')
//            {
//                SetGID = true;
//                GroupExecute = permissionString[5] == 's';
//            }
//            else
//            {
//                SetGID = false;
//                GroupExecute = permissionString[5] == 'x';
//            }

//            OtherRead = permissionString[6] == 'r';
//            OtherWrite = permissionString[7] == 'w';

//            // Special handling for sticky bit ('t', 'T')
//            if (permissionString[8] == 't' || permissionString[8] == 'T')
//            {
//                Sticky = true;
//                OtherExecute = permissionString[8] == 't';
//            }
//            else
//            {
//                Sticky = false;
//                OtherExecute = permissionString[8] == 'x';
//            }
//        }

//        /// <summary>
//        /// Static method to create common permission sets.
//        /// </summary>
//        public static class Common
//        {
//            /// <summary>644 - rw-r--r-- (readable by all, writable by owner)</summary>
//            public static FilePermissions FileDefault => new(644);

//            /// <summary>755 - rwxr-xr-x (executable by all, writable by owner)</summary>
//            public static FilePermissions DirectoryDefault => new(755);

//            /// <summary>600 - rw------- (readable/writable by owner only)</summary>
//            public static FilePermissions OwnerOnly => new(600);



//            /// <summary>700 - rwx------ (full access by owner only)</summary>
//            public static FilePermissions OwnerOnlyExecutable => new(700);

//            /// <summary>777 - rwxrwxrwx (full access by everyone)</summary>
//            public static FilePermissions FullAccess => new(777);

//            /// <summary>000 - --------- (no access for anyone)</summary>
//            public static FilePermissions NoAccess => new(000);
//        }

//        public override bool Equals(object? obj)
//        {
//            if (obj is not FilePermissions other)
//                return false;

//            return OwnerRead == other.OwnerRead &&
//                   OwnerWrite == other.OwnerWrite &&
//                   OwnerExecute == other.OwnerExecute &&
//                   GroupRead == other.GroupRead &&
//                   GroupWrite == other.GroupWrite &&
//                   GroupExecute == other.GroupExecute &&
//                   OtherRead == other.OtherRead &&
//                   OtherWrite == other.OtherWrite &&
//                   OtherExecute == other.OtherExecute;
//        }

//        public override int GetHashCode()
//        {
//            return ToOctal();
//        }
//    }

//    /// <summary>
//    /// Types of file access operations.
//    /// </summary>
//    public enum FileAccessType
//    {
//        Read,
//        Write,
//        Execute,
//        ReadWrite,
//        ReadExecute,
//        WriteExecute,
//        ReadWriteExecute
//    }
//}
