using System;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;
// Aliases to avoid conflicts with System.IO
using VFile = HackerOs.IO.Utilities.File;
using VDirectory = HackerOs.IO.Utilities.Directory;
using VPath = HackerOs.IO.Utilities.Path;

namespace HackerOs.IO.Tests 
{
    /// <summary>
    /// Test class to verify the IO module functionality including file system operations,
    /// standard directory structure, mount points, and utility functions.
    /// </summary>
    public static class IOModuleTests
    {
        /// <summary>
        /// Runs comprehensive tests for the IO module.
        /// </summary>
        /// <returns>True if all tests pass; otherwise, false</returns>
        public static async Task<bool> RunAllTestsAsync()
        {
            Console.WriteLine("Starting IO Module Tests...");

            try
            {
                var fileSystem = new VirtualFileSystem();
                
                // Test 1: Initialization and standard directory structure
                if (!await TestInitializationAsync(fileSystem))
                {
                    Console.WriteLine("❌ Initialization test failed");
                    return false;
                }
                Console.WriteLine("✅ Initialization test passed");

                // Test 2: Basic file operations
                if (!await TestBasicFileOperationsAsync(fileSystem))
                {
                    Console.WriteLine("❌ Basic file operations test failed");
                    return false;
                }
                Console.WriteLine("✅ Basic file operations test passed");

                // Test 3: Directory operations
                if (!await TestDirectoryOperationsAsync(fileSystem))
                {
                    Console.WriteLine("❌ Directory operations test failed");
                    return false;
                }
                Console.WriteLine("✅ Directory operations test passed");

                // Test 4: Path resolution (., .., ~)
                if (!await TestPathResolutionAsync(fileSystem))
                {
                    Console.WriteLine("❌ Path resolution test failed");
                    return false;
                }
                Console.WriteLine("✅ Path resolution test passed");

                // Test 5: Symbolic links
                if (!await TestSymbolicLinksAsync(fileSystem))
                {
                    Console.WriteLine("❌ Symbolic links test failed");
                    return false;
                }
                Console.WriteLine("✅ Symbolic links test passed");

                // Test 6: Mount points
                if (!await TestMountPointsAsync(fileSystem))
                {
                    Console.WriteLine("❌ Mount points test failed");
                    return false;
                }
                Console.WriteLine("✅ Mount points test passed");

                // Test 7: Utility functions
                if (!await TestUtilityFunctionsAsync(fileSystem))
                {
                    Console.WriteLine("❌ Utility functions test failed");
                    return false;
                }
                Console.WriteLine("✅ Utility functions test passed");

                Console.WriteLine("🎉 All IO Module tests passed!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test suite failed with exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tests file system initialization and standard directory structure.
        /// </summary>
        private static async Task<bool> TestInitializationAsync(IVirtualFileSystem fileSystem)
        {
            await fileSystem.InitializeAsync();

            // Check standard directories exist
            string[] standardDirs = {
                "/bin", "/boot", "/dev", "/etc", "/home", "/lib", "/media",
                "/mnt", "/opt", "/proc", "/root", "/run", "/sbin", "/srv",
                "/sys", "/tmp", "/usr", "/usr/bin", "/usr/lib", "/usr/local",
                "/usr/sbin", "/var", "/var/log", "/var/tmp"
            };

            foreach (var dir in standardDirs)
            {
                if (!await fileSystem.ExistsAsync(dir))
                {
                    Console.WriteLine($"Standard directory missing: {dir}");
                    return false;
                }

                var node = await fileSystem.GetNodeAsync(dir);
                if (node == null || !node.IsDirectory)
                {
                    Console.WriteLine($"Path exists but is not a directory: {dir}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tests basic file operations (create, read, write, delete).
        /// </summary>
        private static async Task<bool> TestBasicFileOperationsAsync(IVirtualFileSystem fileSystem)
        {
            const string testFile = "/tmp/test_file.txt";
            const string testContent = "Hello, HackerOS Virtual File System!";

            // Create file
            if (!await fileSystem.CreateFileAsync(testFile, System.Text.Encoding.UTF8.GetBytes(testContent)))
            {
                Console.WriteLine("Failed to create test file");
                return false;
            }

            // Check file exists
            if (!await fileSystem.ExistsAsync(testFile))
            {
                Console.WriteLine("Test file does not exist after creation");
                return false;
            }

            // Read file content
            var content = await fileSystem.ReadFileAsync(testFile);
            if (content == null)
            {
                Console.WriteLine("Failed to read test file");
                return false;
            }

            var contentStr = System.Text.Encoding.UTF8.GetString(content);
            if (contentStr != testContent)
            {
                Console.WriteLine($"File content mismatch. Expected: '{testContent}', Got: '{contentStr}'");
                return false;
            }

            // Write new content
            const string newContent = "Updated content";
            if (!await fileSystem.WriteFileAsync(testFile, System.Text.Encoding.UTF8.GetBytes(newContent)))
            {
                Console.WriteLine("Failed to write to test file");
                return false;
            }

            // Verify updated content
            var updatedBytes = await fileSystem.ReadFileAsync(testFile);
            var updatedStr = System.Text.Encoding.UTF8.GetString(updatedBytes!);
            if (updatedStr != newContent)
            {
                Console.WriteLine($"Updated content mismatch. Expected: '{newContent}', Got: '{updatedStr}'");
                return false;
            }

            // Delete file
            if (!await fileSystem.DeleteAsync(testFile))
            {
                Console.WriteLine("Failed to delete test file");
                return false;
            }

            // Verify file is deleted
            if (await fileSystem.ExistsAsync(testFile))
            {
                Console.WriteLine("Test file still exists after deletion");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tests directory operations.
        /// </summary>
        private static async Task<bool> TestDirectoryOperationsAsync(IVirtualFileSystem fileSystem)
        {
            const string testDir = "/tmp/test_directory";
            const string nestedDir = "/tmp/test_directory/nested";
            const string testFile = "/tmp/test_directory/nested/file.txt";

            // Create directory structure
            if (!await fileSystem.CreateDirectoryAsync(nestedDir))
            {
                Console.WriteLine("Failed to create nested directory");
                return false;
            }

            // Verify directories exist
            if (!await fileSystem.ExistsAsync(testDir) || !await fileSystem.ExistsAsync(nestedDir))
            {
                Console.WriteLine("Test directories do not exist after creation");
                return false;
            }

            // Create a file in the nested directory
            if (!await fileSystem.CreateFileAsync(testFile, System.Text.Encoding.UTF8.GetBytes("test")))
            {
                Console.WriteLine("Failed to create file in nested directory");
                return false;
            }

            // List directory contents
            var contents = await fileSystem.ListDirectoryAsync(testDir);
            bool foundNested = false;
            foreach (var item in contents)
            {
                if (item.Name == "nested" && item.IsDirectory)
                {
                    foundNested = true;
                    break;
                }
            }

            if (!foundNested)
            {
                Console.WriteLine("Nested directory not found in listing");
                return false;
            }

            // Copy directory (should fail for non-empty directory without recursive)
            const string copyDir = "/tmp/test_directory_copy";
            if (!await fileSystem.CopyAsync(testDir, copyDir))
            {
                Console.WriteLine("Failed to copy directory");
                return false;
            }

            // Move directory
            const string moveDir = "/tmp/test_directory_moved";
            if (!await fileSystem.MoveAsync(copyDir, moveDir))
            {
                Console.WriteLine("Failed to move directory");
                return false;
            }

            // Cleanup - delete recursively
            if (!await fileSystem.DeleteAsync(testDir, true))
            {
                Console.WriteLine("Failed to delete test directory recursively");
                return false;
            }

            if (!await fileSystem.DeleteAsync(moveDir, true))
            {
                Console.WriteLine("Failed to delete moved directory recursively");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tests Linux-style path resolution (., .., ~).
        /// </summary>
        private static async Task<bool> TestPathResolutionAsync(IVirtualFileSystem fileSystem)
        {
            // Create test structure
            await fileSystem.CreateDirectoryAsync("/home/testuser");
            await fileSystem.CreateFileAsync("/home/testuser/file.txt", System.Text.Encoding.UTF8.GetBytes("test"));

            // Test current directory (.) - this would typically be tested with a working directory
            // For now, just verify relative path handling

            // Test parent directory (..)
            await fileSystem.CreateDirectoryAsync("/tmp/testdir/subdir");
            await fileSystem.CreateFileAsync("/tmp/testdir/subdir/../parentfile.txt", System.Text.Encoding.UTF8.GetBytes("parent"));

            if (!await fileSystem.ExistsAsync("/tmp/testdir/parentfile.txt"))
            {
                Console.WriteLine("Parent directory (..) resolution failed");
                return false;
            }

            // Test tilde (~) expansion - would need current user context
            // This is handled in the NormalizePath method

            // Cleanup
            await fileSystem.DeleteAsync("/home/testuser", true);
            await fileSystem.DeleteAsync("/tmp/testdir", true);

            return true;
        }

        /// <summary>
        /// Tests symbolic link functionality.
        /// </summary>
        private static async Task<bool> TestSymbolicLinksAsync(IVirtualFileSystem fileSystem)
        {
            const string targetFile = "/tmp/target_file.txt";
            const string linkFile = "/tmp/link_to_target";
            const string testContent = "symlink test content";

            // Create target file
            if (!await fileSystem.CreateFileAsync(targetFile, System.Text.Encoding.UTF8.GetBytes(testContent)))
            {
                Console.WriteLine("Failed to create target file for symlink test");
                return false;
            }

            // Create symbolic link
            if (!await fileSystem.CreateSymbolicLinkAsync(linkFile, targetFile))
            {
                Console.WriteLine("Failed to create symbolic link");
                return false;
            }

            // Read through symbolic link
            var content = await fileSystem.ReadFileAsync(linkFile);
            if (content == null)
            {
                Console.WriteLine("Failed to read through symbolic link");
                return false;
            }

            var contentStr = System.Text.Encoding.UTF8.GetString(content);
            if (contentStr != testContent)
            {
                Console.WriteLine($"Symlink content mismatch. Expected: '{testContent}', Got: '{contentStr}'");
                return false;
            }

            // Cleanup
            await fileSystem.DeleteAsync(linkFile);
            await fileSystem.DeleteAsync(targetFile);

            return true;
        }

        /// <summary>
        /// Tests mount point functionality.
        /// </summary>
        private static async Task<bool> TestMountPointsAsync(IVirtualFileSystem fileSystem)
        {
            if (fileSystem is not VirtualFileSystem vfs)
            {
                Console.WriteLine("FileSystem is not VirtualFileSystem, skipping mount tests");
                return true;
            }

            // Test mounting
            if (!await vfs.MountAsync("tmpfs", "/mnt/test", "tmpfs"))
            {
                Console.WriteLine("Failed to mount tmpfs");
                return false;
            }

            // Verify mount
            if (!vfs.MountManager.IsMounted("/mnt/test"))
            {
                Console.WriteLine("Mount point not registered");
                return false;
            }

            // Get mount info
            var mountInfo = vfs.GetMountInfo();
            if (!mountInfo.Contains("/mnt/test"))
            {
                Console.WriteLine("Mount not found in mount info");
                return false;
            }

            // Test unmounting
            if (!vfs.Unmount("/mnt/test"))
            {
                Console.WriteLine("Failed to unmount");
                return false;
            }

            // Verify unmount
            if (vfs.MountManager.IsMounted("/mnt/test"))
            {
                Console.WriteLine("Mount point still registered after unmount");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tests utility function compatibility.
        /// </summary>
        private static async Task<bool> TestUtilityFunctionsAsync(IVirtualFileSystem fileSystem)
        {
            const string testFile = "/tmp/utility_test.txt";
            const string testContent = "Utility function test";            // Test File utility functions
            await VFile.WriteAllTextAsync(testFile, testContent, fileSystem);

            if (!await VFile.ExistsAsync(testFile, fileSystem))
            {
                Console.WriteLine("File.Exists failed");
                return false;
            }

            var readContent = await VFile.ReadAllTextAsync(testFile, fileSystem);
            if (readContent != testContent)
            {
                Console.WriteLine($"File.ReadAllText failed. Expected: '{testContent}', Got: '{readContent}'");
                return false;
            }            // Test Directory utility functions
            const string testDir = "/tmp/utility_dir";
            await VDirectory.CreateDirectoryAsync(testDir, fileSystem);

            if (!await VDirectory.ExistsAsync(testDir, fileSystem))
            {
                Console.WriteLine("Directory.Exists failed");
                return false;
            }

            // Test Path utility functions
            var combined = VPath.Combine("/tmp", "test", "file.txt");
            var expected = "/tmp/test/file.txt";
            if (combined != expected)
            {
                Console.WriteLine($"Path.Combine failed. Expected: '{expected}', Got: '{combined}'");
                return false;
            }            var fileName = VPath.GetFileName("/tmp/test/file.txt");
            if (fileName != "file.txt")
            {
                Console.WriteLine($"Path.GetFileName failed. Expected: 'file.txt', Got: '{fileName}'");
                return false;
            }

            var extension = VPath.GetExtension("/tmp/test/file.txt");
            if (extension != ".txt")
            {
                Console.WriteLine($"Path.GetExtension failed. Expected: '.txt', Got: '{extension}'");
                return false;
            }

            // Cleanup
            await VFile.DeleteAsync(testFile, fileSystem);
            await VDirectory.DeleteAsync(testDir, fileSystem);

            return true;
        }
    }
}
