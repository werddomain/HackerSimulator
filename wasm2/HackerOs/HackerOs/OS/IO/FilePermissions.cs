using HackerOs.OS.IO.FileSystem;
using System.Text;

namespace HackerOs.OS.IO;

/// <summary>
/// Represents file system permissions
/// </summary>
public class FilePermissions
{
    /// <summary>
    /// Owner read permission
    /// </summary>
    public bool OwnerRead { get; set; }

    /// <summary>
    /// Owner write permission
    /// </summary>
    public bool OwnerWrite { get; set; }

    /// <summary>
    /// Owner execute permission
    /// </summary>
    public bool OwnerExecute { get; set; }

    /// <summary>
    /// Group read permission
    /// </summary>
    public bool GroupRead { get; set; }

    /// <summary>
    /// Group write permission
    /// </summary>
    public bool GroupWrite { get; set; }

    /// <summary>
    /// Group execute permission
    /// </summary>
    public bool GroupExecute { get; set; }

    /// <summary>
    /// Others read permission
    /// </summary>
    public bool OthersRead { get; set; }

    /// <summary>
    /// Others write permission
    /// </summary>
    public bool OthersWrite { get; set; }

    /// <summary>
    /// Others execute permission
    /// </summary>
    public bool OthersExecute { get; set; }

    /// <summary>
    /// Is the file sticky
    /// </summary>
    public bool IsSticky { get; set; }

    /// <summary>
    /// Is SETUID enabled
    /// </summary>
    public bool IsSUID { get; set; }

    /// <summary>
    /// Is SETGID enabled
    /// </summary>
    public bool IsSGID { get; set; }

    /// <summary>
    /// Default constructor - gives read/write to owner only
    /// </summary>
    public FilePermissions()
    {
        OwnerRead = true;
        OwnerWrite = true;
    }

    /// <summary>
    /// Creates permissions from octal notation.
    /// </summary>
    /// <param name="octal">Octal permission value (e.g., 644, 755, 4755 for setuid).</param>
    public FilePermissions(int octal)
    {
        SetFromOctal(octal);
    }

    /// <summary>
    /// Sets permissions from octal notation.
    /// </summary>
    /// <param name="octal">Octal permission value.</param>
    public void SetFromOctal(int octal)
    {
        int special = (octal / 1000) % 10;
        int owner = (octal / 100) % 10;
        int group = (octal / 10) % 10;
        int other = octal % 10;

        OwnerRead = (owner & 4) != 0;
        OwnerWrite = (owner & 2) != 0;
        OwnerExecute = (owner & 1) != 0;

        GroupRead = (group & 4) != 0;
        GroupWrite = (group & 2) != 0;
        GroupExecute = (group & 1) != 0;

        OthersRead = (other & 4) != 0;
        OthersWrite = (other & 2) != 0;
        OthersExecute = (other & 1) != 0;

        IsSUID = (special & 4) != 0;
        IsSGID = (special & 2) != 0;
        IsSticky = (special & 1) != 0;
    }

    /// <summary>
    /// Create from octal mode string
    /// </summary>
    public static FilePermissions FromOctalString(string mode)
    {
        if (!uint.TryParse(mode, System.Globalization.NumberStyles.HexNumber, null, out uint octalMode))
        {
            throw new ArgumentException("Invalid octal mode string", nameof(mode));
        }

        return FromOctal(octalMode);
    }

    /// <summary>
    /// Create from octal mode value
    /// </summary>
    public static FilePermissions FromOctal(uint mode)
    {
        var perms = new FilePermissions
        {
            IsSticky = (mode & 0x1000) != 0,
            IsSUID = (mode & 0x800) != 0,
            IsSGID = (mode & 0x400) != 0,

            OwnerRead = (mode & 0x100) != 0,
            OwnerWrite = (mode & 0x080) != 0,
            OwnerExecute = (mode & 0x040) != 0,

            GroupRead = (mode & 0x020) != 0,
            GroupWrite = (mode & 0x010) != 0,
            GroupExecute = (mode & 0x008) != 0,

            OthersRead = (mode & 0x004) != 0,
            OthersWrite = (mode & 0x002) != 0,
            OthersExecute = (mode & 0x001) != 0
        };

        return perms;
    }

    /// <summary>
    /// Get the octal mode string (e.g. "0755")
    /// </summary>
    public string ToOctalString()
    {
        return ToOctal().ToString("X4");
    }

    /// <summary>
    /// Get the octal mode value
    /// </summary>
    public uint ToOctal()
    {
        uint mode = 0;

        if (IsSticky) mode |= 0x1000;
        if (IsSUID) mode |= 0x800;
        if (IsSGID) mode |= 0x400;

        if (OwnerRead) mode |= 0x100;
        if (OwnerWrite) mode |= 0x080;
        if (OwnerExecute) mode |= 0x040;

        if (GroupRead) mode |= 0x020;
        if (GroupWrite) mode |= 0x010;
        if (GroupExecute) mode |= 0x008;

        if (OthersRead) mode |= 0x004;
        if (OthersWrite) mode |= 0x002;
        if (OthersExecute) mode |= 0x001;

        return mode;
    }
    /// <summary>
    /// Sets permissions from rwx string notation.
    /// </summary>
    /// <param name="permissionString">Permission string (e.g., "rwxr-xr-x").</param>
    public void SetFromString(string permissionString)
    {
        if (string.IsNullOrEmpty(permissionString) || permissionString.Length != 9)
        {
            throw new ArgumentException("Permission string must be exactly 9 characters (rwxrwxrwx format)");
        }

        OwnerRead = permissionString[0] == 'r';
        OwnerWrite = permissionString[1] == 'w';
        OwnerExecute = permissionString[2] == 'x';

        GroupRead = permissionString[3] == 'r';
        GroupWrite = permissionString[4] == 'w';
        GroupExecute = permissionString[5] == 'x';

        OthersRead = permissionString[6] == 'r';
        OthersWrite = permissionString[7] == 'w';
        OthersExecute = permissionString[8] == 'x';
    }
    /// Creates permissions from octal notation (static factory method).
    /// </summary>
    /// <param name="octal">Octal permission value (e.g., 644, 755).</param>
    /// <returns>A new FilePermissions instance with the specified permissions.</returns>
    public static FilePermissions FromOctal(int octal)
    {
        return new FilePermissions(octal);
    }
    /// <summary>
    /// Get the symbolic mode string (e.g. "rwxr-xr-x")
    /// </summary>
    /// 


    /// <summary>
    /// Checks if a user can access the file/directory with the specified access mode.
    /// </summary>
    /// <param name="fileOwner">The owner of the file.</param>
    /// <param name="fileGroup">The group of the file.</param>
    /// <param name="userId">The user requesting access.</param>
    /// <param name="userGroupId">The primary group of the user.</param>
    /// <param name="accessMode">The type of access required.</param>
    /// <returns>True if access is granted; otherwise, false.</returns>
    public bool CanAccess(string fileOwner, string fileGroup, string userId, string userGroupId, FileAccessMode accessMode)
    {
        // Root user has all permissions
        if (userId == "root")
            return true;

        bool read, write, execute;

        // Check if user is the owner
        if (userId == fileOwner)
        {
            read = OwnerRead;
            write = OwnerWrite;
            execute = OwnerExecute;
        }
        // Check if user is in the file's group
        else if (userGroupId == fileGroup)
        {
            read = GroupRead;
            write = GroupWrite;
            execute = GroupExecute;
        }
        // User is "other"
        else
        {
            read = OthersRead;
            write = OthersWrite;
            execute = OthersExecute;
        }

        return accessMode switch
        {
            FileAccessMode.Read => read,
            FileAccessMode.Write => write,
            FileAccessMode.Execute => execute,
            FileAccessMode.ReadWrite => read && write,
            _ => false
        };
    }

    /// <summary>
    /// Enhanced permission check that considers setuid, setgid, and sticky bits
    /// </summary>
    /// <param name="user">The user requesting access</param>
    /// <param name="fileOwnerId">The owner ID of the file</param>
    /// <param name="fileGroupId">The group ID of the file</param>
    /// <param name="accessMode">The type of access required</param>
    /// <param name="isDirectory">Whether the node is a directory</param>
    /// <returns>True if access is granted; otherwise, false</returns>
    public bool CanAccess(User.User user, int fileOwnerId, int fileGroupId, FileAccessMode accessMode, bool isDirectory = false)
    {
        // Root always has all permissions
        if (user.UserId == 0)
            return true;

        bool read, write, execute;

        // Determine base permissions
        if (user.UserId == fileOwnerId)
        {
            // User is the owner
            read = OwnerRead;
            write = OwnerWrite;
            execute = OwnerExecute;
        }
        else if (user.PrimaryGroupId == fileGroupId || user.AdditionalGroups.Contains(fileGroupId))
        {
            // User is in the file's group
            read = GroupRead;
            write = GroupWrite;
            execute = GroupExecute;
        }
        else
        {
            // User is "other"
            read = OthersRead;
            write = OthersWrite;
            execute = OthersExecute;
        }

        // Handle special bits
        if (accessMode == FileAccessMode.Execute && !isDirectory)
        {
            // SetUID: If the file is executable by the user and has the setuid bit,
            // execution happens with the privileges of the file owner
            if (IsSUID && execute)
            {
                // The execution will proceed with elevated privileges
                // We still need to return true based on the user's execute permission
                return true;
            }

            // SetGID: If the file is executable by the user and has the setgid bit,
            // execution happens with the privileges of the file's group
            if (IsSGID && execute)
            {
                // The execution will proceed with the group's privileges
                // We still need to return true based on the user's execute permission
                return true;
            }
        }
        else if (isDirectory)
        {
            // For directories, SetGID means new files created in the directory
            // inherit the directory's group rather than the user's primary group
            // This doesn't affect access permissions check

            // Sticky bit on directories: if set, only the owner of a file can delete or rename it,
            // even if other users have write permissions on the directory
            if (IsSticky && accessMode == FileAccessMode.Write && write)
            {
                // The sticky bit affects deletion/renaming, but we can't know the target operation here
                // The complete check must be done at the operation level (e.g., Delete, Move methods)
                // Here we just return the basic write permission
                return write;
            }
        }

        // Return permission based on the access mode
        return accessMode switch
        {
            FileAccessMode.Read => read,
            FileAccessMode.Write => write,
            FileAccessMode.Execute => execute,
            FileAccessMode.ReadWrite => read && write,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a user has specific access to the file/directory.
    /// </summary>
    /// <param name="user">The user requesting access.</param>
    /// <param name="owner">The owner of the file.</param>
    /// <param name="group">The group of the file.</param>
    /// <param name="userGroups">Groups the user belongs to.</param>
    /// <param name="accessType">The type of access required.</param>
    /// <returns>True if access is granted; otherwise, false.</returns>
    public bool HasAccess(string user, string owner, string group, string[] userGroups, FileAccessType accessType)
    {
        // Root user has all permissions
        if (user == "root")
            return true;

        bool read, write, execute;

        // Check if user is the owner
        if (user == owner)
        {
            read = OwnerRead;
            write = OwnerWrite;
            execute = OwnerExecute;
        }
        // Check if user is in the file's group
        else if (userGroups.Contains(group))
        {
            read = GroupRead;
            write = GroupWrite;
            execute = GroupExecute;
        }
        // User is "other"
        else
        {
            read = OthersRead;
            write = OthersWrite;
            execute = OthersExecute;
        }

        return accessType switch
        {
            FileAccessType.Read => read,
            FileAccessType.Write => write,
            FileAccessType.Execute => execute,
            FileAccessType.ReadWrite => read && write,
            FileAccessType.ReadExecute => read && execute,
            FileAccessType.WriteExecute => write && execute,
            FileAccessType.ReadWriteExecute => read && write && execute,
            _ => false
        };
    }



    /// <summary>
    /// Returns the string representation of permissions in rwxrwxrwx format.
    /// </summary>
    /// <returns>Permission string.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder(9);

        sb.Append(OwnerRead ? 'r' : '-');
        sb.Append(OwnerWrite ? 'w' : '-');
        sb.Append(OwnerExecute ? (IsSUID ? "s" : "x") : (IsSUID ? "S" : "-"));

        sb.Append(GroupRead ? 'r' : '-');
        sb.Append(GroupWrite ? 'w' : '-');
        sb.Append(GroupExecute ? (IsSGID ? "s" : "x") : (IsSGID ? "S" : "-"));

        sb.Append(OthersRead ? 'r' : '-');
        sb.Append(OthersWrite ? 'w' : '-');
        sb.Append(OthersExecute ? (IsSticky ? "t" : "x") : (IsSticky ? "T" : "-"));

        return sb.ToString();
    }
    /// <summary>
    /// Returns a detailed string representation including octal notation.
    /// </summary>
    /// <returns>Detailed permission information.</returns>
    public string ToDetailedString()
    {
        return $"{ToString()} ({ToOctal():D3})";
    }

    /// <summary>
    /// Creates an enhanced string representation of permissions including special bits.
    /// </summary>
    /// <returns>Detailed permission string.</returns>
    public string ToDetailedPermissionString()
    {
        var sb = new StringBuilder(10);

        // First character indicates the type and special bits
        if (IsSUID)
            sb.Append(OwnerExecute ? 's' : 'S');
        else
            sb.Append(OwnerExecute ? 'x' : '-');

        if (IsSGID)
            sb.Append(GroupExecute ? 's' : 'S');
        else
            sb.Append(GroupExecute ? 'x' : '-');

        if (IsSticky)
            sb.Append(OthersExecute ? 't' : 'T');
        else
            sb.Append(OthersExecute ? 'x' : '-');

        // Standard permissions
        sb.Append(OwnerRead ? 'r' : '-');
        sb.Append(OwnerWrite ? 'w' : '-');

        sb.Append(GroupRead ? 'r' : '-');
        sb.Append(GroupWrite ? 'w' : '-');

        sb.Append(OthersRead ? 'r' : '-');
        sb.Append(OthersWrite ? 'w' : '-');

        return sb.ToString();
    }

    /// <summary>
    /// Sets permissions from enhanced string notation including special bits.
    /// </summary>
    /// <param name="permissionString">Enhanced permission string (e.g., "rwsrwxrwt").</param>
    public void SetFromDetailedString(string permissionString)
    {
        if (string.IsNullOrEmpty(permissionString) || permissionString.Length != 9)
        {
            throw new ArgumentException("Permission string must be exactly 9 characters");
        }

        OwnerRead = permissionString[0] == 'r';
        OwnerWrite = permissionString[1] == 'w';

        // Special handling for setuid bit ('s', 'S')
        if (permissionString[2] == 's' || permissionString[2] == 'S')
        {
            IsSUID = true;
            OwnerExecute = permissionString[2] == 's';
        }
        else
        {
            IsSUID = false;
            OwnerExecute = permissionString[2] == 'x';
        }

        GroupRead = permissionString[3] == 'r';
        GroupWrite = permissionString[4] == 'w';

        // Special handling for setgid bit ('s', 'S')
        if (permissionString[5] == 's' || permissionString[5] == 'S')
        {
            IsSGID = true;
            GroupExecute = permissionString[5] == 's';
        }
        else
        {
            IsSGID = false;
            GroupExecute = permissionString[5] == 'x';
        }

        OthersRead = permissionString[6] == 'r';
        OthersWrite = permissionString[7] == 'w';

        // Special handling for sticky bit ('t', 'T')
        if (permissionString[8] == 't' || permissionString[8] == 'T')
        {
            IsSticky = true;
            OthersExecute = permissionString[8] == 't';
        }
        else
        {
            IsSticky = false;
            OthersExecute = permissionString[8] == 'x';
        }

    }


    public override bool Equals(object? obj)
    {
        if (obj is not FilePermissions other)
            return false;

        return OwnerRead == other.OwnerRead &&
               OwnerWrite == other.OwnerWrite &&
               OwnerExecute == other.OwnerExecute &&
               GroupRead == other.GroupRead &&
               GroupWrite == other.GroupWrite &&
               GroupExecute == other.GroupExecute &&
               OthersRead == other.OthersRead &&
               OthersWrite == other.OthersWrite &&
               OthersExecute == other.OthersExecute;

    }

    public override int GetHashCode()
    {
        return Convert.ToInt32(ToOctal());
    }

    /// <summary>
    /// Static method to create common permission sets.
    /// </summary>
    public static class Common
    {
        /// <summary>644 - rw-r--r-- (readable by all, writable by owner)</summary>
        public static FilePermissions FileDefault => new(644);

        /// <summary>755 - rwxr-xr-x (executable by all, writable by owner)</summary>
        public static FilePermissions DirectoryDefault => new(755);

        /// <summary>600 - rw------- (readable/writable by owner only)</summary>
        public static FilePermissions OwnerOnly => new(600);



        /// <summary>700 - rwx------ (full access by owner only)</summary>
        public static FilePermissions OwnerOnlyExecutable => new(700);

        /// <summary>777 - rwxrwxrwx (full access by everyone)</summary>
        public static FilePermissions FullAccess => new(777);

        /// <summary>000 - --------- (no access for anyone)</summary>
        public static FilePermissions NoAccess => new(000);
        
    }

    
}
