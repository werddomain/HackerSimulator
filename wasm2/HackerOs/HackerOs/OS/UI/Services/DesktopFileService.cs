using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.UI.Models;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HackerOs.OS.UI.Services
{
    /// <summary>
    /// Service for managing desktop files and icons
    /// </summary>
    public class DesktopFileService
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly IUserManager _userManager;
        private readonly DesktopSettingsService _desktopSettings;
        private readonly ILogger<DesktopFileService> _logger;

        /// <summary>
        /// The path to the desktop directory
        /// </summary>
        private const string DefaultDesktopPath = "/home/{0}/Desktop";

        /// <summary>
        /// Initializes a new instance of the <see cref="DesktopFileService"/> class
        /// </summary>
        public DesktopFileService(
            IVirtualFileSystem fileSystem,
            IUserManager userManager,
            DesktopSettingsService desktopSettings,
            ILogger<DesktopFileService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _desktopSettings = desktopSettings ?? throw new ArgumentNullException(nameof(desktopSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the desktop directory path for a user
        /// </summary>
        public string GetDesktopPath(UserSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return string.Format(DefaultDesktopPath, session.User.Username);
        }

        /// <summary>
        /// Gets the desktop files for a user
        /// </summary>
        public async Task<List<DesktopIconModel>> GetDesktopIconsAsync(UserSession session)
        {
            try
            {
                var desktopPath = GetDesktopPath(session);
                
                // Ensure desktop directory exists
                if (!await _fileSystem.DirectoryExistsAsync(desktopPath))
                {
                    _logger.LogInformation("Creating desktop directory: {Path}", desktopPath);
                    await _fileSystem.CreateDirectoryAsync(desktopPath);
                }

                // Get desktop files and directories
                var entries = await _fileSystem.GetDirectoryEntriesAsync(desktopPath);
                var icons = new List<DesktopIconModel>();
                
                int gridX = 0;
                int gridY = 0;
                const int maxIconsPerColumn = 10; // Maximum icons per column before wrapping
                
                foreach (var entry in entries)
                {
                    // Skip hidden files
                    if (Path.GetFileName(entry.Path).StartsWith("."))
                    {
                        continue;
                    }

                    // Create desktop icon
                    var icon = new DesktopIconModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        DisplayName = Path.GetFileName(entry.Path),
                        FilePath = entry.Path,
                        IsDirectory = entry.IsDirectory,
                        IconPath = GetIconPathForEntry(entry),
                        GridX = gridX,
                        GridY = gridY,
                        IsSelected = false,
                        Tooltip = entry.Path
                    };
                    
                    icons.Add(icon);
                    
                    // Update grid position for next icon
                    gridY++;
                    if (gridY >= maxIconsPerColumn)
                    {
                        gridY = 0;
                        gridX++;
                    }
                }
                
                return icons;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting desktop icons");
                return new List<DesktopIconModel>();
            }
        }

        /// <summary>
        /// Gets the icon path for a file system entry
        /// </summary>
        private string GetIconPathForEntry(HackerOs.OS.IO.FileSystemEntry entry)
        {
            if (entry.IsDirectory)
            {
                return "/images/icons/folder.png";
            }
            
            // Get file extension
            var extension = Path.GetExtension(entry.Path).ToLowerInvariant();
            
            // Return icon based on file extension
            return extension switch
            {
                ".txt" => "/images/icons/text-file.png",
                ".md" => "/images/icons/markdown-file.png",
                ".pdf" => "/images/icons/pdf-file.png",
                ".doc" or ".docx" => "/images/icons/word-file.png",
                ".xls" or ".xlsx" => "/images/icons/excel-file.png",
                ".ppt" or ".pptx" => "/images/icons/powerpoint-file.png",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "/images/icons/image-file.png",
                ".mp3" or ".wav" or ".ogg" or ".flac" => "/images/icons/audio-file.png",
                ".mp4" or ".avi" or ".mkv" or ".mov" => "/images/icons/video-file.png",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "/images/icons/archive-file.png",
                ".exe" or ".dll" => "/images/icons/executable-file.png",
                ".sh" or ".bash" => "/images/icons/script-file.png",
                ".py" => "/images/icons/python-file.png",
                ".js" => "/images/icons/javascript-file.png",
                ".html" or ".htm" => "/images/icons/html-file.png",
                ".css" => "/images/icons/css-file.png",
                ".json" => "/images/icons/json-file.png",
                ".xml" => "/images/icons/xml-file.png",
                ".cs" => "/images/icons/csharp-file.png",
                ".fs" => "/images/icons/fsharp-file.png",
                ".java" => "/images/icons/java-file.png",
                ".go" => "/images/icons/go-file.png",
                ".rs" => "/images/icons/rust-file.png",
                _ => "/images/icons/unknown-file.png"
            };
        }

        /// <summary>
        /// Creates a desktop shortcut to a file or directory
        /// </summary>
        public async Task<bool> CreateShortcutAsync(UserSession session, string targetPath, string shortcutName)
        {
            try
            {
                var desktopPath = GetDesktopPath(session);
                var shortcutPath = Path.Combine(desktopPath, shortcutName + ".lnk");
                
                // Create shortcut file
                var shortcutContent = $"[Shortcut]\nTarget={targetPath}\nName={shortcutName}\n";
                await _fileSystem.WriteAllTextAsync(shortcutPath, shortcutContent);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating desktop shortcut to {Path}", targetPath);
                return false;
            }
        }

        /// <summary>
        /// Refreshes the desktop icons
        /// </summary>
        public async Task<List<DesktopIconModel>> RefreshDesktopIconsAsync(UserSession session)
        {
            return await GetDesktopIconsAsync(session);
        }
    }
}
