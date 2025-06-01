using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HackerOs.IO.Utilities
{
    /// <summary>
    /// Provides path manipulation utilities for the virtual file system, similar to System.IO.Path.
    /// This class offers Linux-style path operations.
    /// </summary>
    public static class Path
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
        /// <param name="extension">The new extension (with or without a leading period)</param>
        /// <returns>The modified path information</returns>
        public static string? ChangeExtension(string? path, string? extension)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var directory = GetDirectoryName(path);
            var filename = GetFileNameWithoutExtension(path);
            
            if (string.IsNullOrEmpty(extension))
                return directory != null ? Combine(directory, filename) : filename;

            if (!extension.StartsWith("."))
                extension = "." + extension;

            return directory != null ? Combine(directory, filename + extension) : filename + extension;
        }

        /// <summary>
        /// Combines an array of strings into a path.
        /// </summary>
        /// <param name="paths">An array of parts of the path</param>
        /// <returns>The combined paths</returns>
        public static string Combine(params string[] paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            if (paths.Length == 0)
                return string.Empty;

            var result = new StringBuilder();
            
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                
                if (string.IsNullOrEmpty(path))
                    continue;

                if (IsPathRooted(path))
                {
                    // Absolute path resets the result
                    result.Clear();
                    result.Append(path);
                }
                else
                {
                    if (result.Length > 0 && !result.ToString().EndsWith("/"))
                        result.Append('/');
                    result.Append(path);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Combines two strings into a path.
        /// </summary>
        /// <param name="path1">The first path to combine</param>
        /// <param name="path2">The second path to combine</param>
        /// <returns>The combined paths</returns>
        public static string Combine(string path1, string path2)
        {
            return Combine(new[] { path1, path2 });
        }

        /// <summary>
        /// Combines three strings into a path.
        /// </summary>
        /// <param name="path1">The first path to combine</param>
        /// <param name="path2">The second path to combine</param>
        /// <param name="path3">The third path to combine</param>
        /// <returns>The combined paths</returns>
        public static string Combine(string path1, string path2, string path3)
        {
            return Combine(new[] { path1, path2, path3 });
        }

        /// <summary>
        /// Returns the directory information for the specified path string.
        /// </summary>
        /// <param name="path">The path of a file or directory</param>
        /// <returns>Directory information for path, or null if path denotes a root directory or is null</returns>
        public static string? GetDirectoryName(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            path = path.TrimEnd('/');
            
            if (string.IsNullOrEmpty(path) || path == "/")
                return null;

            var lastSlash = path.LastIndexOf('/');
            if (lastSlash <= 0)
                return "/";

            return path.Substring(0, lastSlash);
        }

        /// <summary>
        /// Returns the extension of the specified path string.
        /// </summary>
        /// <param name="path">The path string from which to get the extension</param>
        /// <returns>The extension of the specified path (including the period ".")</returns>
        public static string? GetExtension(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var filename = GetFileName(path);
            if (string.IsNullOrEmpty(filename))
                return string.Empty;

            var lastDot = filename.LastIndexOf('.');
            if (lastDot == -1 || lastDot == 0)
                return string.Empty;

            return filename.Substring(lastDot);
        }

        /// <summary>
        /// Returns the file name and extension of the specified path string.
        /// </summary>
        /// <param name="path">The path string from which to obtain the file name and extension</param>
        /// <returns>The characters after the last directory character in path</returns>
        public static string? GetFileName(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var lastSlash = path.LastIndexOf('/');
            return lastSlash == -1 ? path : path.Substring(lastSlash + 1);
        }

        /// <summary>
        /// Returns the file name of the specified path string without the extension.
        /// </summary>
        /// <param name="path">The path of the file</param>
        /// <returns>The string returned by GetFileName, minus the last period (.) and all characters following it</returns>
        public static string? GetFileNameWithoutExtension(string? path)
        {
            var filename = GetFileName(path);
            if (string.IsNullOrEmpty(filename))
                return filename;

            var lastDot = filename.LastIndexOf('.');
            if (lastDot == -1 || lastDot == 0)
                return filename;

            return filename.Substring(0, lastDot);
        }

        /// <summary>
        /// Returns the absolute path for the specified path string.
        /// </summary>
        /// <param name="path">The file or directory for which to obtain absolute path information</param>
        /// <returns>The fully qualified location of path</returns>
        public static string GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            // If already absolute, normalize it
            if (IsPathRooted(path))
                return NormalizePath(path);

            // For relative paths, we need a current directory
            // In a virtual file system context, this would typically come from the file system instance
            // For now, assume root as the current directory
            return NormalizePath(Combine("/", path));
        }

        /// <summary>
        /// Gets a value indicating whether the specified path string contains a root.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if path contains a root; otherwise, false</returns>
        public static bool IsPathRooted(string? path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("/");
        }

        /// <summary>
        /// Determines whether a path includes a file name extension.
        /// </summary>
        /// <param name="path">The path to search for an extension</param>
        /// <returns>true if the characters that follow the last directory separator or volume separator in the path include a period (.) followed by one or more characters; otherwise, false</returns>
        public static bool HasExtension(string? path)
        {
            var extension = GetExtension(path);
            return !string.IsNullOrEmpty(extension);
        }

        /// <summary>
        /// Returns a random folder name or file name.
        /// </summary>
        /// <returns>A random folder name or file name</returns>
        public static string GetRandomFileName()
        {
            var random = new Random();
            var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var result = new StringBuilder(8);
            
            for (int i = 0; i < 8; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Creates a uniquely named, zero-byte temporary file and returns the full path of that file.
        /// </summary>
        /// <returns>The full path of the temporary file</returns>
        public static string GetTempFileName()
        {
            return Combine("/tmp", "tmp" + GetRandomFileName() + ".tmp");
        }

        /// <summary>
        /// Returns the path of the current user's temporary folder.
        /// </summary>
        /// <returns>The path to the temporary folder</returns>
        public static string GetTempPath()
        {
            return "/tmp";
        }

        /// <summary>
        /// Determines whether the specified file name contains invalid characters.
        /// </summary>
        /// <param name="filename">The file name to test</param>
        /// <returns>true if filename contains invalid characters; otherwise, false</returns>
        public static bool ContainsInvalidFileNameChars(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return false;

            var invalidChars = GetInvalidFileNameChars();
            return filename.Any(c => invalidChars.Contains(c));
        }

        /// <summary>
        /// Determines whether the specified path contains invalid characters.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if path contains invalid characters; otherwise, false</returns>
        public static bool ContainsInvalidPathChars(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var invalidChars = GetInvalidPathChars();
            return path.Any(c => invalidChars.Contains(c));
        }

        /// <summary>
        /// Normalizes a path by resolving . and .. components and removing redundant separators.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path</returns>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            // Split path into components
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var stack = new List<string>();

            foreach (var part in parts)
            {
                if (part == ".")
                {
                    continue; // Current directory, skip
                }
                else if (part == "..")
                {
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1); // Go up one directory
                    }
                    // If at root, stay at root
                }
                else
                {
                    stack.Add(part);
                }
            }

            return "/" + string.Join("/", stack);
        }
    }
}
