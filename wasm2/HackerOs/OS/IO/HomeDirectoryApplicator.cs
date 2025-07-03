using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Methods to create a home directory structure based on a template
    /// </summary>
    public static class HomeDirectoryApplicator
    {
        /// <summary>
        /// Applies a home directory template to a user's home directory
        /// </summary>
        /// <param name="fileSystem">The file system to use</param>
        /// <param name="templateManager">The template manager</param>
        /// <param name="user">The user</param>
        /// <param name="templateName">Optional specific template name to use</param>
        /// <returns>True if successful, false otherwise</returns>
        public static async Task<bool> ApplyHomeDirectoryTemplateAsync(
            IVirtualFileSystem fileSystem,
            HomeDirectoryTemplateManager templateManager,
            User user,
            string templateName = null)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            if (templateManager == null) throw new ArgumentNullException(nameof(templateManager));
            if (user == null) throw new ArgumentNullException(nameof(user));

            try
            {
                // Get the appropriate template
                HomeDirectoryTemplate template;
                if (string.IsNullOrEmpty(templateName))
                {
                    // Auto-select based on user type
                    template = templateManager.GetTemplateForUserType(user.IsAdmin, GetUserType(user));
                }
                else
                {
                    template = templateManager.GetTemplate(templateName);
                }

                // Create home directory
                string homePath = $"/home/{user.Username}";
                bool homeCreated = await fileSystem.CreateDirectoryAsync(homePath);
                
                if (!homeCreated && !await fileSystem.DirectoryExistsAsync(homePath))
                {
                    return false;
                }

                // Set home directory permissions (755 - rwxr-xr-x)
                await fileSystem.SetPermissionsAsync(homePath, 0755, user.UserId, user.PrimaryGroupId);

                // Create directories
                foreach (var (dirPath, permissions) in template.Directories)
                {
                    string fullPath = $"{homePath}/{dirPath}";
                    bool dirCreated = await fileSystem.CreateDirectoryAsync(fullPath);
                    
                    if (dirCreated || await fileSystem.DirectoryExistsAsync(fullPath))
                    {
                        // Set directory ownership and permissions
                        await fileSystem.SetPermissionsAsync(fullPath, permissions, user.UserId, user.PrimaryGroupId);
                    }
                }

                // Create config files
                foreach (var (fileName, fileInfo) in template.ConfigFiles)
                {
                    string fullPath = $"{homePath}/{fileInfo.Path}";
                    
                    // Generate content
                    string content = await templateManager.GenerateContentAsync(fileInfo.ContentGenerator, user, fileInfo.Path);
                    
                    // Create the file
                    bool fileCreated = await fileSystem.WriteFileAsync(fullPath, content);
                    
                    if (fileCreated)
                    {
                        // Set file ownership and permissions
                        await fileSystem.SetPermissionsAsync(fullPath, fileInfo.Permissions, user.UserId, user.PrimaryGroupId);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                Console.Error.WriteLine($"Error applying home directory template: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determines the user type based on group membership
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns>A user type string</returns>
        private static string GetUserType(User user)
        {
            if (user.IsAdmin)
            {
                return "admin";
            }
            
            // Check for developer group membership
            if (user.IsInGroup(Group.StandardGroups.DevelopersGroupId))
            {
                return "developer";
            }
            
            return "standard";
        }
    }
}
