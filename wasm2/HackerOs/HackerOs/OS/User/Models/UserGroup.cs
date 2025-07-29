using System;
using System.Collections.Generic;
using System.Linq;

namespace HackerOs.OS.User.Models
{
    /// <summary>
    /// Represents a user group in the HackerOS system with Linux-like group properties.
    /// </summary>
    public class UserGroup
    {
        /// <summary>
        /// Gets or sets the group identifier (GID).
        /// 0 is reserved for the root group, 1-999 for system groups, 1000+ for regular groups.
        /// </summary>
        public int Gid { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the group.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of usernames that are members of this group.
        /// </summary>
        public List<string> Members { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the type of the group.
        /// </summary>
        public GroupType Type { get; set; } = GroupType.User;

        /// <summary>
        /// Gets or sets the date when this group was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets additional properties specific to this group.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the permissions associated with this group.
        /// </summary>
        public GroupPermissions Permissions { get; set; } = new GroupPermissions();

        /// <summary>
        /// Gets a value indicating whether this group is an administrative group.
        /// </summary>
        public bool IsAdminGroup => Gid == 0 || GroupName == "admin" || GroupName == "wheel" || GroupName == "sudo";

        /// <summary>
        /// Gets a value indicating whether this group is a system group.
        /// </summary>
        public bool IsSystemGroup => Gid > 0 && Gid < 1000;

        /// <summary>
        /// Adds a user to the group.
        /// </summary>
        /// <param name="username">The username to add.</param>
        /// <returns>True if the user was added, false if the user was already a member.</returns>
        public bool AddMember(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be empty or whitespace.", nameof(username));
            }

            if (IsMember(username))
            {
                return false;
            }

            Members.Add(username);
            return true;
        }

        /// <summary>
        /// Removes a user from the group.
        /// </summary>
        /// <param name="username">The username to remove.</param>
        /// <returns>True if the user was removed, false if the user was not a member.</returns>
        public bool RemoveMember(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be empty or whitespace.", nameof(username));
            }

            return Members.Remove(username);
        }

        /// <summary>
        /// Checks if a user is a member of the group.
        /// </summary>
        /// <param name="username">The username to check.</param>
        /// <returns>True if the user is a member, false otherwise.</returns>
        public bool IsMember(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be empty or whitespace.", nameof(username));
            }

            return Members.Contains(username, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the list of all group members.
        /// </summary>
        /// <returns>A read-only list of usernames.</returns>
        public IReadOnlyList<string> GetMembers()
        {
            return Members.AsReadOnly();
        }

        /// <summary>
        /// Sets the group permission for a specific resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource.</param>
        /// <param name="permission">The permission to set.</param>
        public void SetPermission(string resourcePath, ResourcePermission permission)
        {
            Permissions.SetPermission(resourcePath, permission);
        }

        /// <summary>
        /// Gets the permission for a specific resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource.</param>
        /// <returns>The permission for the resource, or null if no specific permission is set.</returns>
        public ResourcePermission? GetPermission(string resourcePath)
        {
            return Permissions.GetPermission(resourcePath);
        }
    }

    /// <summary>
    /// Represents the type of a group.
    /// </summary>
    public enum GroupType
    {
        /// <summary>
        /// A system group used by the operating system.
        /// </summary>
        System,

        /// <summary>
        /// A regular user group.
        /// </summary>
        User,

        /// <summary>
        /// A special application-specific group.
        /// </summary>
        Special
    }

    /// <summary>
    /// Represents the permissions of a group.
    /// </summary>
    public class GroupPermissions
    {
        private readonly Dictionary<string, ResourcePermission> _permissions = new Dictionary<string, ResourcePermission>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Sets the permission for a specific resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource.</param>
        /// <param name="permission">The permission to set.</param>
        public void SetPermission(string resourcePath, ResourcePermission permission)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                throw new ArgumentException("Resource path cannot be empty or whitespace.", nameof(resourcePath));
            }

            _permissions[resourcePath] = permission;
        }

        /// <summary>
        /// Gets the permission for a specific resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource.</param>
        /// <returns>The permission for the resource, or null if no specific permission is set.</returns>
        public ResourcePermission? GetPermission(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                throw new ArgumentException("Resource path cannot be empty or whitespace.", nameof(resourcePath));
            }

            if (_permissions.TryGetValue(resourcePath, out var permission))
            {
                return permission;
            }

            // Try to find a parent directory permission
            var parentPath = GetParentPath(resourcePath);
            while (!string.IsNullOrEmpty(parentPath))
            {
                if (_permissions.TryGetValue(parentPath, out var parentPermission))
                {
                    return parentPermission;
                }
                parentPath = GetParentPath(parentPath);
            }

            return null;
        }

        /// <summary>
        /// Gets all resource paths with specific permissions.
        /// </summary>
        /// <returns>A dictionary of resource paths and their permissions.</returns>
        public IReadOnlyDictionary<string, ResourcePermission> GetAllPermissions()
        {
            return _permissions;
        }

        /// <summary>
        /// Removes the permission for a specific resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource.</param>
        /// <returns>True if the permission was removed, false if no permission was set.</returns>
        public bool RemovePermission(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                throw new ArgumentException("Resource path cannot be empty or whitespace.", nameof(resourcePath));
            }

            return _permissions.Remove(resourcePath);
        }

        /// <summary>
        /// Gets the parent path of a resource path.
        /// </summary>
        /// <param name="path">The resource path.</param>
        /// <returns>The parent path, or an empty string if there is no parent.</returns>
        private static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var lastSeparatorIndex = path.LastIndexOf('/');
            if (lastSeparatorIndex <= 0)
            {
                return string.Empty;
            }

            return path.Substring(0, lastSeparatorIndex);
        }
    }

    /// <summary>
    /// Represents the permission for a resource.
    /// </summary>
    public class ResourcePermission
    {
        /// <summary>
        /// Gets or sets whether the group can read the resource.
        /// </summary>
        public bool CanRead { get; set; }

        /// <summary>
        /// Gets or sets whether the group can write to the resource.
        /// </summary>
        public bool CanWrite { get; set; }

        /// <summary>
        /// Gets or sets whether the group can execute the resource.
        /// </summary>
        public bool CanExecute { get; set; }

        /// <summary>
        /// Gets or sets whether the permission applies to subdirectories.
        /// </summary>
        public bool ApplyToSubdirectories { get; set; }

        /// <summary>
        /// Gets or sets additional permission flags.
        /// </summary>
        public Dictionary<string, bool> Flags { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Creates a new resource permission.
        /// </summary>
        public ResourcePermission()
        {
        }

        /// <summary>
        /// Creates a new resource permission with the specified values.
        /// </summary>
        /// <param name="canRead">Whether the group can read the resource.</param>
        /// <param name="canWrite">Whether the group can write to the resource.</param>
        /// <param name="canExecute">Whether the group can execute the resource.</param>
        /// <param name="applyToSubdirectories">Whether the permission applies to subdirectories.</param>
        public ResourcePermission(bool canRead, bool canWrite, bool canExecute, bool applyToSubdirectories = false)
        {
            CanRead = canRead;
            CanWrite = canWrite;
            CanExecute = canExecute;
            ApplyToSubdirectories = applyToSubdirectories;
        }

        /// <summary>
        /// Creates a permission from a Unix-style permission string (e.g., "rwx").
        /// </summary>
        /// <param name="permission">The permission string.</param>
        /// <returns>A new ResourcePermission object.</returns>
        public static ResourcePermission FromUnixString(string permission)
        {
            if (string.IsNullOrEmpty(permission))
            {
                return new ResourcePermission(false, false, false);
            }

            var result = new ResourcePermission
            {
                CanRead = permission.Contains('r'),
                CanWrite = permission.Contains('w'),
                CanExecute = permission.Contains('x')
            };

            return result;
        }

        /// <summary>
        /// Converts the permission to a Unix-style permission string (e.g., "rwx").
        /// </summary>
        /// <returns>The permission string.</returns>
        public string ToUnixString()
        {
            return string.Concat(
                CanRead ? "r" : "-",
                CanWrite ? "w" : "-",
                CanExecute ? "x" : "-"
            );
        }
    }
}
