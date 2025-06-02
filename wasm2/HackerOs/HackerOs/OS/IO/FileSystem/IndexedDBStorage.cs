using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Provides IndexedDB persistence layer for the virtual file system.
    /// Handles serialization, storage, and retrieval of file system data in the browser.
    /// </summary>
    public class IndexedDBStorage
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly string _databaseName;
        private readonly int _version;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the IndexedDBStorage class.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for browser interop</param>
        /// <param name="databaseName">The name of the IndexedDB database</param>
        /// <param name="version">The version of the database schema</param>
        public IndexedDBStorage(IJSRuntime jsRuntime, string databaseName = "HackerOSFileSystem", int version = 1)
        {
            _jsRuntime = jsRuntime;
            _databaseName = databaseName;
            _version = version;
        }

        /// <summary>
        /// Initializes the IndexedDB database with the required object stores.
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
                return true;

            try
            {
                var result = await _jsRuntime.InvokeAsync<bool>("hackerOSIndexedDB.initializeDatabase", 
                    _databaseName, _version);
                
                _isInitialized = result;
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize IndexedDB: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves the entire file system tree to IndexedDB.
        /// </summary>
        /// <param name="rootDirectory">The root directory of the file system</param>
        /// <returns>True if save was successful, false otherwise</returns>
        public async Task<bool> SaveFileSystemAsync(VirtualDirectory rootDirectory)
        {
            if (!_isInitialized && !await InitializeAsync())
                return false;

            try
            {
                var serializedData = SerializeFileSystem(rootDirectory);
                var metadata = new FileSystemMetadata
                {
                    Version = _version,
                    SavedAt = DateTime.UtcNow,
                    Checksum = ComputeChecksum(serializedData)
                };

                // Save both data and metadata
                var dataResult = await _jsRuntime.InvokeAsync<bool>("hackerOSIndexedDB.saveData", 
                    "filesystem", "root", serializedData);
                
                var metadataResult = await _jsRuntime.InvokeAsync<bool>("hackerOSIndexedDB.saveData", 
                    "metadata", "filesystem", JsonSerializer.Serialize(metadata));

                return dataResult && metadataResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save file system: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads the file system tree from IndexedDB.
        /// </summary>
        /// <returns>The restored root directory, or null if load failed</returns>
        public async Task<VirtualDirectory?> LoadFileSystemAsync()
        {
            if (!_isInitialized && !await InitializeAsync())
                return null;

            try
            {
                // Load metadata first to verify integrity
                var metadataJson = await _jsRuntime.InvokeAsync<string>("hackerOSIndexedDB.loadData", 
                    "metadata", "filesystem");
                
                if (string.IsNullOrEmpty(metadataJson))
                    return null;

                var metadata = JsonSerializer.Deserialize<FileSystemMetadata>(metadataJson);
                if (metadata == null)
                    return null;

                // Load the actual file system data
                var serializedData = await _jsRuntime.InvokeAsync<string>("hackerOSIndexedDB.loadData", 
                    "filesystem", "root");
                
                if (string.IsNullOrEmpty(serializedData))
                    return null;

                // Verify data integrity
                if (!VerifyChecksum(serializedData, metadata.Checksum))
                {
                    Console.WriteLine("File system data integrity check failed");
                    return null;
                }

                return DeserializeFileSystem(serializedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load file system: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves individual file data to IndexedDB.
        /// </summary>
        /// <param name="filePath">The path of the file to save</param>
        /// <param name="content">The file content</param>
        /// <returns>True if save was successful, false otherwise</returns>
        public async Task<bool> SaveFileAsync(string filePath, byte[] content)
        {
            if (!_isInitialized && !await InitializeAsync())
                return false;

            try
            {
                var base64Content = Convert.ToBase64String(content);
                return await _jsRuntime.InvokeAsync<bool>("hackerOSIndexedDB.saveData", 
                    "files", filePath, base64Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save file {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads individual file data from IndexedDB.
        /// </summary>
        /// <param name="filePath">The path of the file to load</param>
        /// <returns>The file content, or null if load failed</returns>
        public async Task<byte[]?> LoadFileAsync(string filePath)
        {
            if (!_isInitialized && !await InitializeAsync())
                return null;

            try
            {
                var base64Content = await _jsRuntime.InvokeAsync<string>("hackerOSIndexedDB.loadData", 
                    "files", filePath);
                
                if (string.IsNullOrEmpty(base64Content))
                    return null;

                return Convert.FromBase64String(base64Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load file {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes file data from IndexedDB.
        /// </summary>
        /// <param name="filePath">The path of the file to delete</param>
        /// <returns>True if deletion was successful, false otherwise</returns>
        public async Task<bool> DeleteFileAsync(string filePath)
        {
            if (!_isInitialized)
                return false;

            try
            {
                return await _jsRuntime.InvokeAsync<bool>("hackerOSIndexedDB.deleteData", 
                    "files", filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete file {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clears all file system data from IndexedDB.
        /// </summary>
        /// <returns>True if clear was successful, false otherwise</returns>
        public async Task<bool> ClearAllDataAsync()
        {
            if (!_isInitialized)
                return false;

            try
            {
                return await _jsRuntime.InvokeAsync<bool>("hackerOSIndexedDB.clearAllData");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear all data: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Serializes the file system tree to JSON.
        /// </summary>
        private string SerializeFileSystem(VirtualDirectory rootDirectory)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(rootDirectory, options);
        }

        /// <summary>
        /// Deserializes the file system tree from JSON.
        /// </summary>
        private VirtualDirectory? DeserializeFileSystem(string serializedData)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Deserialize<VirtualDirectory>(serializedData, options);
        }

        /// <summary>
        /// Computes a simple checksum for data integrity verification.
        /// </summary>
        private string ComputeChecksum(string data)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Verifies data integrity using checksum.
        /// </summary>
        private bool VerifyChecksum(string data, string expectedChecksum)
        {
            var actualChecksum = ComputeChecksum(data);
            return actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Metadata for file system storage.
    /// </summary>
    public class FileSystemMetadata
    {
        public int Version { get; set; }
        public DateTime SavedAt { get; set; }
        public string Checksum { get; set; } = string.Empty;
    }
}
