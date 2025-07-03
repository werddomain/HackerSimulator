using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Manages disk quota tracking for user directories
    /// </summary>
    public class UserQuotaManager
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<UserQuotaManager> _logger;
        private readonly Dictionary<string, UserQuota> _quotas = new Dictionary<string, UserQuota>();
        private const string QuotaFilePath = "/etc/user.quota";
        
        /// <summary>
        /// Initializes a new instance of the UserQuotaManager
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="logger">The logger</param>
        public UserQuotaManager(IVirtualFileSystem fileSystem, ILogger<UserQuotaManager> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Initializes the quota manager by loading quota data
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing UserQuotaManager");
                await LoadQuotasAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UserQuotaManager");
            }
        }
        
        /// <summary>
        /// Sets a quota for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="softLimit">The soft limit in bytes</param>
        /// <param name="hardLimit">The hard limit in bytes</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SetQuotaAsync(string username, long softLimit, long hardLimit)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            if (softLimit < 0)
            {
                throw new ArgumentException("Soft limit cannot be negative", nameof(softLimit));
            }
            
            if (hardLimit < softLimit)
            {
                throw new ArgumentException("Hard limit cannot be less than soft limit", nameof(hardLimit));
            }
            
            try
            {
                _logger.LogInformation("Setting quota for user {Username}: soft={SoftLimit}, hard={HardLimit}", 
                    username, softLimit, hardLimit);
                
                // Create or update quota
                UserQuota quota = new UserQuota
                {
                    Username = username,
                    SoftLimit = softLimit,
                    HardLimit = hardLimit,
                    CurrentUsage = await CalculateUsageAsync(username)
                };
                
                _quotas[username] = quota;
                
                // Save to file
                await SaveQuotasAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting quota for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Gets the quota for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>The user quota, or null if not found</returns>
        public UserQuota GetQuota(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            return _quotas.TryGetValue(username, out var quota) ? quota : null;
        }
        
        /// <summary>
        /// Gets all user quotas
        /// </summary>
        /// <returns>The collection of user quotas</returns>
        public IEnumerable<UserQuota> GetAllQuotas()
        {
            return _quotas.Values;
        }
        
        /// <summary>
        /// Removes a quota for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>True if successful</returns>
        public async Task<bool> RemoveQuotaAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogInformation("Removing quota for user {Username}", username);
                
                if (_quotas.Remove(username))
                {
                    await SaveQuotasAsync();
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing quota for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Updates the usage statistics for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>True if successful</returns>
        public async Task<bool> UpdateUsageAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogDebug("Updating usage for user {Username}", username);
                
                if (_quotas.TryGetValue(username, out var quota))
                {
                    // Calculate current usage
                    long usage = await CalculateUsageAsync(username);
                    quota.CurrentUsage = usage;
                    
                    // Save to file
                    await SaveQuotasAsync();
                    
                    _logger.LogDebug("Updated usage for user {Username}: {Usage} bytes", username, usage);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating usage for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Updates usage statistics for all users
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> UpdateAllUsageAsync()
        {
            try
            {
                _logger.LogInformation("Updating usage for all users");
                
                foreach (var username in new List<string>(_quotas.Keys))
                {
                    await UpdateUsageAsync(username);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating usage for all users");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a user is over their quota
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="newBytes">Additional bytes to consider</param>
        /// <returns>QuotaStatus indicating the user's quota status</returns>
        public async Task<QuotaStatus> CheckQuotaAsync(string username, long newBytes = 0)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                // If no quota is set, allow the operation
                if (!_quotas.TryGetValue(username, out var quota))
                {
                    return QuotaStatus.BelowLimit;
                }
                
                // Update usage if needed
                if (quota.CurrentUsage < 0)
                {
                    await UpdateUsageAsync(username);
                }
                
                // Check against hard limit
                if (quota.CurrentUsage + newBytes > quota.HardLimit)
                {
                    _logger.LogWarning("User {Username} exceeded hard quota limit", username);
                    return QuotaStatus.AboveHardLimit;
                }
                
                // Check against soft limit
                if (quota.CurrentUsage + newBytes > quota.SoftLimit)
                {
                    _logger.LogInformation("User {Username} exceeded soft quota limit", username);
                    return QuotaStatus.AboveSoftLimit;
                }
                
                return QuotaStatus.BelowLimit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking quota for user {Username}", username);
                return QuotaStatus.Error;
            }
        }
        
        /// <summary>
        /// Gets a report of all user quotas
        /// </summary>
        /// <returns>A list of quota reports</returns>
        public async Task<List<UserQuotaReport>> GetQuotaReportAsync()
        {
            try
            {
                _logger.LogInformation("Generating quota report for all users");
                
                // Update all usage first
                await UpdateAllUsageAsync();
                
                var reports = new List<UserQuotaReport>();
                
                foreach (var quota in _quotas.Values)
                {
                    reports.Add(new UserQuotaReport
                    {
                        Username = quota.Username,
                        SoftLimit = quota.SoftLimit,
                        HardLimit = quota.HardLimit,
                        CurrentUsage = quota.CurrentUsage,
                        SoftLimitPercentage = quota.SoftLimit > 0 ? (double)quota.CurrentUsage / quota.SoftLimit * 100 : 0,
                        HardLimitPercentage = quota.HardLimit > 0 ? (double)quota.CurrentUsage / quota.HardLimit * 100 : 0,
                        Status = await CheckQuotaAsync(quota.Username)
                    });
                }
                
                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quota report");
                return new List<UserQuotaReport>();
            }
        }
        
        /// <summary>
        /// Calculates the current disk usage for a user's home directory
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>The usage in bytes</returns>
        private async Task<long> CalculateUsageAsync(string username)
        {
            string homePath = $"/home/{username}";
            
            try
            {
                // Check if home directory exists
                if (!await _fileSystem.DirectoryExistsAsync(homePath))
                {
                    return 0;
                }
                
                return await CalculateDirectorySizeAsync(homePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating usage for user {Username}", username);
                return -1;
            }
        }
        
        /// <summary>
        /// Recursively calculates the size of a directory and its contents
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>Size in bytes</returns>
        private async Task<long> CalculateDirectorySizeAsync(string path)
        {
            long totalSize = 0;
            
            // Get all files in the directory
            var files = await _fileSystem.GetFilesAsync(path);
            foreach (var file in files)
            {
                string filePath = $"{path}/{file}";
                var fileInfo = await _fileSystem.GetFileInfoAsync(filePath);
                totalSize += fileInfo.Size;
            }
            
            // Get all subdirectories
            var subdirectories = await _fileSystem.GetDirectoriesAsync(path);
            foreach (var subdir in subdirectories)
            {
                string subdirPath = $"{path}/{subdir}";
                totalSize += await CalculateDirectorySizeAsync(subdirPath);
            }
            
            return totalSize;
        }
        
        /// <summary>
        /// Loads quota data from the quota file
        /// </summary>
        private async Task LoadQuotasAsync()
        {
            try
            {
                _logger.LogDebug("Loading quotas from {QuotaFilePath}", QuotaFilePath);
                
                // Clear existing quotas
                _quotas.Clear();
                
                // Check if file exists
                if (!await _fileSystem.FileExistsAsync(QuotaFilePath))
                {
                    _logger.LogInformation("Quota file not found, creating empty one");
                    await SaveQuotasAsync();
                    return;
                }
                
                // Read file content
                string content = await _fileSystem.ReadFileAsync(QuotaFilePath);
                
                // Parse lines
                string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Skip comments
                    if (line.StartsWith("#"))
                    {
                        continue;
                    }
                    
                    // Parse quota entry: username:softLimit:hardLimit:currentUsage
                    string[] parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        string username = parts[0];
                        
                        if (long.TryParse(parts[1], out long softLimit) &&
                            long.TryParse(parts[2], out long hardLimit) &&
                            long.TryParse(parts[3], out long currentUsage))
                        {
                            _quotas[username] = new UserQuota
                            {
                                Username = username,
                                SoftLimit = softLimit,
                                HardLimit = hardLimit,
                                CurrentUsage = currentUsage
                            };
                        }
                    }
                }
                
                _logger.LogInformation("Loaded {Count} quota entries", _quotas.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading quotas");
            }
        }
        
        /// <summary>
        /// Saves quota data to the quota file
        /// </summary>
        private async Task SaveQuotasAsync()
        {
            try
            {
                _logger.LogDebug("Saving quotas to {QuotaFilePath}", QuotaFilePath);
                
                // Build file content
                string content = "# User quota file - username:softLimit:hardLimit:currentUsage\n";
                content += "# Generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n";
                
                foreach (var quota in _quotas.Values)
                {
                    content += $"{quota.Username}:{quota.SoftLimit}:{quota.HardLimit}:{quota.CurrentUsage}\n";
                }
                
                // Ensure /etc directory exists
                await _fileSystem.CreateDirectoryAsync("/etc");
                
                // Write to file
                await _fileSystem.WriteFileAsync(QuotaFilePath, content);
                
                _logger.LogInformation("Saved {Count} quota entries", _quotas.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving quotas");
            }
        }
    }
    
    /// <summary>
    /// Represents a user's disk quota
    /// </summary>
    public class UserQuota
    {
        /// <summary>
        /// The username
        /// </summary>
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// The soft limit in bytes (warning threshold)
        /// </summary>
        public long SoftLimit { get; set; }
        
        /// <summary>
        /// The hard limit in bytes (enforced limit)
        /// </summary>
        public long HardLimit { get; set; }
        
        /// <summary>
        /// The current usage in bytes
        /// </summary>
        public long CurrentUsage { get; set; }
    }
    
    /// <summary>
    /// Represents a user's quota status report
    /// </summary>
    public class UserQuotaReport
    {
        /// <summary>
        /// The username
        /// </summary>
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// The soft limit in bytes
        /// </summary>
        public long SoftLimit { get; set; }
        
        /// <summary>
        /// The hard limit in bytes
        /// </summary>
        public long HardLimit { get; set; }
        
        /// <summary>
        /// The current usage in bytes
        /// </summary>
        public long CurrentUsage { get; set; }
        
        /// <summary>
        /// The percentage of soft limit used
        /// </summary>
        public double SoftLimitPercentage { get; set; }
        
        /// <summary>
        /// The percentage of hard limit used
        /// </summary>
        public double HardLimitPercentage { get; set; }
        
        /// <summary>
        /// The current quota status
        /// </summary>
        public QuotaStatus Status { get; set; }
    }
    
    /// <summary>
    /// Represents the status of a user's quota
    /// </summary>
    public enum QuotaStatus
    {
        /// <summary>
        /// Below the soft limit
        /// </summary>
        BelowLimit,
        
        /// <summary>
        /// Above the soft limit but below the hard limit
        /// </summary>
        AboveSoftLimit,
        
        /// <summary>
        /// Above the hard limit
        /// </summary>
        AboveHardLimit,
        
        /// <summary>
        /// Error checking quota
        /// </summary>
        Error
    }
}
