using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HackerOs.OS.HSystem.IO
{
    /// <summary>
    /// Provides path manipulation utilities for the virtual file system, similar to System.IO.Path.
    /// This class offers Linux-style path operations.
    /// </summary>
    public static class HPath
    {
        /// <summary>
        /// Provides a platform-specific character used to separate directory levels in a path string.
        /// </summary>
        public static readonly char DirectorySeparatorChar = '/';

        /// <summary>
        /// Provides a platform-specific alternate character used to separate directory levels in a path string.
        /// </summary>
        public static readonly char AltDirectorySeparatorChar = '/';

        /// <summary>
        /// A platform-specific separator character used to separate path strings in environment variables.
        /// </summary>
        public static readonly char PathSeparator = ':';

        /// <summary>
        /// Gets an array containing the characters that are not allowed in file names.
        /// </summary>
        /// <returns>An array containing the characters that are not allowed in file names</returns>
        public static char[] GetInvalidFileNameChars()
        {
            return new char[] { '\0', '/' };
        }

        /// <summary>
        /// Gets an array containing the characters that are not allowed in path names.
        /// </summary>
        /// <returns>An array containing the characters that are not allowed in path names</returns>
        public static char[] GetInvalidPathChars()
        {
            return new char[] { '\0' };
        }

        /// <summary>
        /// Changes the extension of a path string.
        /// </summary>
        /// <param name="path">The path information to modify</param>
        /// <param name="extension">The extension to use (with or without leading period)</param>
        /// <returns>Modified path with the new extension</returns>
        public static string ChangeExtension(string path, string? extension)
        {
            if (path == null)
                return null;

            var i = path.LastIndexOf('.');
            if (i < 0)
                return extension == null ? path : path + (extension.StartsWith('.') ? extension : "." + extension);

            return extension == null ? path.Substring(0, i) : path.Substring(0, i) + (extension.StartsWith('.') ? extension : "." + extension);
        }

        /// <summary>
        /// Combines two path strings.
        /// </summary>
        public static string Combine(string path1, string path2)
        {
            if (path1 == null || path2 == null)
                throw new ArgumentNullException(path1 == null ? nameof(path1) : nameof(path2));

            if (path2.Length == 0)
                return path1;

            if (path1.Length == 0)
                return path2;

            if (IsPathRooted(path2))
                return path2;

            var ch = path1[path1.Length - 1];
            if (ch != DirectorySeparatorChar && ch != AltDirectorySeparatorChar)
                return path1 + DirectorySeparatorChar + path2;
            
            return path1 + path2;
        }

        /// <summary>
        /// Combines multiple path strings.
        /// </summary>
        public static string Combine(string path1, string path2, string path3)
            => Combine(Combine(path1, path2), path3);

        /// <summary>
        /// Combines multiple path strings.
        /// </summary>
        public static string Combine(string path1, string path2, string path3, string path4)
            => Combine(Combine(Combine(path1, path2), path3), path4);

        /// <summary>
        /// Combines multiple path strings.
        /// </summary>
        public static string Combine(params string[] paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            if (paths.Length == 0)
                return string.Empty;

            var result = paths[0];
            for (int i = 1; i < paths.Length; i++)
                result = Combine(result, paths[i]);

            return result;
        }

        /// <summary>
        /// Returns the directory information for the specified path.
        /// </summary>
        public static string? GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // Normalize slashes
            path = path.Replace('\\', '/');

            // Find the last directory separator
            var lastSeparatorPos = path.LastIndexOf('/');
            if (lastSeparatorPos < 0)
                return string.Empty;

            // Handle root directory
            if (lastSeparatorPos == 0)
                return "/";

            // Return the directory part
            return path.Substring(0, lastSeparatorPos);
        }

        /// <summary>
        /// Returns the file name and extension of the specified path string.
        /// </summary>
        public static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Normalize slashes
            path = path.Replace('\\', '/');

            // Find the last directory separator
            var lastSeparatorPos = path.LastIndexOf('/');
            if (lastSeparatorPos < 0)
                return path;

            // Return the filename part
            return path.Substring(lastSeparatorPos + 1);
        }

        /// <summary>
        /// Returns the file name of the specified path string without the extension.
        /// </summary>
        public static string GetFileNameWithoutExtension(string path)
        {
            var fileName = GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            var extensionPos = fileName.LastIndexOf('.');
            if (extensionPos < 0)
                return fileName;

            return fileName.Substring(0, extensionPos);
        }

        /// <summary>
        /// Returns the extension of the specified path string.
        /// </summary>
        public static string GetExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var fileName = GetFileName(path);
            var extensionPos = fileName.LastIndexOf('.');
            if (extensionPos < 0)
                return string.Empty;

            return fileName.Substring(extensionPos);
        }

        /// <summary>
        /// Gets the root directory information from the path.
        /// </summary>
        public static string GetPathRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Normalize slashes
            path = path.Replace('\\', '/');

            // Unix-style paths
            if (path.StartsWith("/"))
                return "/";

            return string.Empty;
        }

        /// <summary>
        /// Returns a value indicating whether the specified path string contains a root.
        /// </summary>
        public static bool IsPathRooted(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Normalize slashes
            path = path.Replace('\\', '/');

            // Unix-style paths
            return path.StartsWith("/");
        }

        /// <summary>
        /// Returns a random folder name or file name.
        /// </summary>
        public static string GetRandomFileName()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Gets a full path from a relative path.
        /// </summary>
        public static string GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // Normalize slashes
            path = path.Replace('\\', '/');

            // Already a full path
            if (path.StartsWith("/"))
                return path;

            throw new InvalidOperationException("Relative paths require a base directory context");
        }

        /// <summary>
        /// Gets a full path from a relative path using a specified base directory.
        /// </summary>
        public static string GetFullPath(string path, string basePath)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            if (string.IsNullOrEmpty(basePath))
                throw new ArgumentException("Base path cannot be null or empty", nameof(basePath));

            // Normalize slashes
            path = path.Replace('\\', '/');
            basePath = basePath.Replace('\\', '/');

            // Already a full path
            if (path.StartsWith("/"))
                return path;

            // Ensure basePath ends with directory separator
            if (!basePath.EndsWith("/"))
                basePath += "/";

            return basePath + path;
        }

        /// <summary>
        /// Normalizes the specified path by removing redundant directory separators and up-level directories.
        /// </summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Normalize slashes
            path = path.Replace('\\', '/');

            // Split the path
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();

            // Process each part
            foreach (var part in parts)
            {
                if (part == ".")
                    continue;

                if (part == "..")
                {
                    if (result.Count > 0 && result[result.Count - 1] != "..")
                        result.RemoveAt(result.Count - 1);
                    else if (!path.StartsWith("/"))
                        result.Add("..");
                }
                else
                {
                    result.Add(part);
                }
            }

            // Join the parts
            var normalizedPath = string.Join("/", result);

            // Ensure leading slash if original path had one
            if (path.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            // Handle empty result
            if (string.IsNullOrEmpty(normalizedPath) && path.StartsWith("/"))
                return "/";

            return normalizedPath;
        }
    }
}
