using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Manages disk quotas for user groups in the HackerOS system.
    /// Handles quota enforcement, tracking, and reporting.
    /// </summary>
    public class GroupQuotaManager
    {
        private const string QUOTA_FILE_PATH = "/etc/group.quota";
        private const string QUOTA_BACKUP_PATH = "/etc/group.quota.bak";
        private readonly Dictionary<int, QuotaConfiguration> _quotaConfigurations;
        private readonly VirtualFileSystem _fileSystem;
        private readonly GroupManager _groupManager;
        private bool _initialized;
        private bool _enforcementEnabled;

        /// <summary>
        /// Event fired when a quota configuration is created or updated
        /// </summary>
        public event EventHandler<QuotaEventArgs> QuotaUpdated;

        /// <summary>
        /// Event fired when a quota is exceeded
        /// </summary>
        public event EventHandler<QuotaExceededEventArgs> QuotaExceeded;

        /// <summary>
        /// Event fired when usage statistics are updated
        /// </summary>
        public event EventHandler<QuotaUsageEventArgs> UsageUpdated;

        /// <summary>
        /// Constructs a new GroupQuotaManager instance
        /// </summary>
        /// <param name="fileSystem">The virtual file system to use</param>
        /// <param name="groupManager">The group manager to use</param>
        public GroupQuotaManager(VirtualFileSystem fileSystem, GroupManager groupManager)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _groupManager = groupManager ?? throw new ArgumentNullException(nameof(groupManager));
            _quotaConfigurations = new Dictionary<int, QuotaConfiguration>();
            _initialized = false;
            _enforcementEnabled = true;
        }

        /// <summary>
        /// Initializes the quota manager, loading quota configurations from the file system
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            // Ensure the /etc directory exists
            if (!await _fileSystem.DirectoryExistsAsync("/etc"))
            {
                await _fileSystem.CreateDirectoryAsync("/etc", new FilePermissions(0755));
            }

            // Load quotas from /etc/group.quota if it exists
            if (await _fileSystem.FileExistsAsync(QUOTA_FILE_PATH))
            {
                await LoadQuotaConfigurationsAsync();
            }
            else
            {
                // Create default quota file
                await SaveQuotaConfigurationsAsync();
            }

            _initialized = true;
        }

        /// <summary>
        /// Enables or disables quota enforcement
        /// </summary>
        /// <param name="enabled">Whether quota enforcement should be enabled</param>
        public void SetEnforcementEnabled(bool enabled)
        {
            _enforcementEnabled = enabled;
        }

        /// <summary>
        /// Checks if a file operation would exceed the group's quota
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="sizeInBytes">The additional size in bytes</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public async Task<bool> CheckQuotaAsync(int groupId, long sizeInBytes)
        {
            if (!_initialized)
                await InitializeAsync();

            // If enforcement is disabled, always allow
            if (!_enforcementEnabled)
                return true;

            // If no quota is set for this group, allow
            if (!_quotaConfigurations.TryGetValue(groupId, out var quota) || !quota.IsEnabled)
                return true;

            // Check if operation would exceed hard limit
            if (quota.CurrentUsage + sizeInBytes > quota.HardLimit)
            {
                // Raise quota exceeded event
                QuotaExceeded?.Invoke(this, new QuotaExceededEventArgs(quota, sizeInBytes, QuotaExceededType.HardLimit));
                return false;
            }

            // Check if operation would exceed soft limit
            if (quota.CurrentUsage + sizeInBytes > quota.SoftLimit)
            {
                // If soft limit is already exceeded, check grace period
                if (quota.CurrentUsage > quota.SoftLimit)
                {
                    // If grace period is set and has expired, deny
                    if (quota.GracePeriodStart.HasValue)
                    {
                        DateTime expirationTime = quota.GracePeriodStart.Value.Add(quota.GracePeriodDuration);
                        if (DateTime.UtcNow > expirationTime)
                        {
                            QuotaExceeded?.Invoke(this, new QuotaExceededEventArgs(quota, sizeInBytes, QuotaExceededType.GracePeriodExpired));
                            return false;
                        }
                    }
                }
                else
                {
                    // First time exceeding soft limit, set grace period start
                    quota.GracePeriodStart = DateTime.UtcNow;
                    await SaveQuotaConfigurationsAsync();
                    
                    // Raise quota exceeded event
                    QuotaExceeded?.Invoke(this, new QuotaExceededEventArgs(quota, sizeInBytes, QuotaExceededType.SoftLimit));
                }
            }

            return true;
        }

        /// <summary>
        /// Updates the usage statistics for a group after a file operation
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="sizeInBytes">The size change in bytes (positive for increase, negative for decrease)</param>
        /// <returns>Task representing the async operation</returns>
        public async Task UpdateUsageAsync(int groupId, long sizeInBytes)
        {
            if (!_initialized)
                await InitializeAsync();

            // If no quota is set for this group, create one with default values
            if (!_quotaConfigurations.TryGetValue(groupId, out var quota))
            {
                quota = new QuotaConfiguration
                {
                    GroupId = groupId,
                    SoftLimit = long.MaxValue,
                    HardLimit = long.MaxValue,
                    GracePeriodDuration = TimeSpan.FromDays(7),
                    IsEnabled = false,
                    CurrentUsage = 0
                };
                _quotaConfigurations[groupId] = quota;
            }

            // Update usage
            long oldUsage = quota.CurrentUsage;
            quota.CurrentUsage = Math.Max(0, quota.CurrentUsage + sizeInBytes);

            // If usage dropped below soft limit, clear grace period
            if (oldUsage > quota.SoftLimit && quota.CurrentUsage <= quota.SoftLimit)
            {
                quota.GracePeriodStart = null;
            }

            // Save changes
            await SaveQuotaConfigurationsAsync();

            // Raise usage updated event
            UsageUpdated?.Invoke(this, new QuotaUsageEventArgs(quota, oldUsage, quota.CurrentUsage));
        }

        /// <summary>
        /// Sets the quota configuration for a group
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="softLimit">The soft limit in bytes</param>
        /// <param name="hardLimit">The hard limit in bytes</param>
        /// <param name="gracePeriod">The grace period duration</param>
        /// <param name="isEnabled">Whether the quota is enabled</param>
        /// <returns>Task representing the async operation</returns>
        public async Task SetQuotaAsync(int groupId, long softLimit, long hardLimit, TimeSpan gracePeriod, bool isEnabled)
        {
            if (!_initialized)
                await InitializeAsync();

            // Validate input
            if (softLimit < 0)
                throw new ArgumentException("Soft limit cannot be negative", nameof(softLimit));
            
            if (hardLimit < 0)
                throw new ArgumentException("Hard limit cannot be negative", nameof(hardLimit));
            
            if (hardLimit < softLimit)
                throw new ArgumentException("Hard limit cannot be less than soft limit", nameof(hardLimit));
            
            if (gracePeriod.TotalSeconds < 0)
                throw new ArgumentException("Grace period cannot be negative", nameof(gracePeriod));

            // Check if group exists
            var group = await _groupManager.GetGroupByIdAsync(groupId);
            if (group == null)
                throw new KeyNotFoundException($"Group with ID {groupId} not found");

            // Create or update quota configuration
            if (_quotaConfigurations.TryGetValue(groupId, out var quota))
            {
                // Update existing configuration
                quota.SoftLimit = softLimit;
                quota.HardLimit = hardLimit;
                quota.GracePeriodDuration = gracePeriod;
                quota.IsEnabled = isEnabled;
                
                // If soft limit is no longer exceeded, clear grace period
                if (quota.CurrentUsage <= quota.SoftLimit)
                {
                    quota.GracePeriodStart = null;
                }
            }
            else
            {
                // Create new configuration
                quota = new QuotaConfiguration
                {
                    GroupId = groupId,
                    SoftLimit = softLimit,
                    HardLimit = hardLimit,
                    GracePeriodDuration = gracePeriod,
                    IsEnabled = isEnabled,
                    CurrentUsage = 0
                };
                _quotaConfigurations[groupId] = quota;
            }

            // Save changes
            await SaveQuotaConfigurationsAsync();

            // Raise quota updated event
            QuotaUpdated?.Invoke(this, new QuotaEventArgs(quota));
        }

        /// <summary>
        /// Gets the quota configuration for a group
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <returns>The quota configuration, or null if not found</returns>
        public async Task<QuotaConfiguration> GetQuotaAsync(int groupId)
        {
            if (!_initialized)
                await InitializeAsync();

            return _quotaConfigurations.TryGetValue(groupId, out var quota) ? quota : null;
        }

        /// <summary>
        /// Gets all quota configurations
        /// </summary>
        /// <returns>Collection of all quota configurations</returns>
        public async Task<IEnumerable<QuotaConfiguration>> GetAllQuotasAsync()
        {
            if (!_initialized)
                await InitializeAsync();

            return _quotaConfigurations.Values;
        }

        /// <summary>
        /// Rebuilds quota usage statistics by scanning the file system
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public async Task RebuildQuotaStatisticsAsync()
        {
            if (!_initialized)
                await InitializeAsync();

            // Reset all usage statistics
            foreach (var quota in _quotaConfigurations.Values)
            {
                quota.CurrentUsage = 0;
                quota.GracePeriodStart = null;
            }

            // Scan the file system to calculate group usage
            await ScanDirectoryForUsageAsync("/");

            // Save updated statistics
            await SaveQuotaConfigurationsAsync();
        }

        /// <summary>
        /// Recursively scans a directory to calculate group usage
        /// </summary>
        /// <param name="directoryPath">The directory path to scan</param>
        /// <returns>Task representing the async operation</returns>
        private async Task ScanDirectoryForUsageAsync(string directoryPath)
        {
            try
            {
                // Get directory entries
                var entries = await _fileSystem.ListDirectoryAsync(directoryPath);

                foreach (var entry in entries)
                {
                    string fullPath = directoryPath == "/" 
                        ? $"/{entry.Name}" 
                        : $"{directoryPath}/{entry.Name}";

                    if (entry.IsDirectory)
                    {
                        // Recursively scan subdirectory
                        await ScanDirectoryForUsageAsync(fullPath);
                    }
                    else
                    {
                        // Get file info
                        var node = await _fileSystem.GetNodeAsync(fullPath);
                        if (node != null && !node.IsDirectory)
                        {
                            // Get the group ID
                            if (int.TryParse(node.Group, out int groupId))
                            {
                                // Update group usage
                                if (_quotaConfigurations.TryGetValue(groupId, out var quota))
                                {
                                    quota.CurrentUsage += node.Size;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue scanning
                Console.WriteLine($"Error scanning directory {directoryPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads quota configurations from the file system
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        private async Task LoadQuotaConfigurationsAsync()
        {
            try
            {
                // Clear existing configurations
                _quotaConfigurations.Clear();

                // Read the quota file
                string content = await _fileSystem.ReadFileAsync(QUOTA_FILE_PATH);

                // Deserialize the JSON content
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var quotas = JsonSerializer.Deserialize<List<QuotaConfiguration>>(content, options);
                
                // Add to dictionary
                if (quotas != null)
                {
                    foreach (var quota in quotas)
                    {
                        _quotaConfigurations[quota.GroupId] = quota;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading quota configurations: {ex.Message}");
                // Create default empty dictionary
                _quotaConfigurations.Clear();
            }
        }

        /// <summary>
        /// Saves quota configurations to the file system
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        private async Task SaveQuotaConfigurationsAsync()
        {
            try
            {
                // Create a backup if the file exists
                if (await _fileSystem.FileExistsAsync(QUOTA_FILE_PATH))
                {
                    string content = await _fileSystem.ReadFileAsync(QUOTA_FILE_PATH);
                    await _fileSystem.WriteFileAsync(QUOTA_BACKUP_PATH, content);
                }

                // Serialize the configurations
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(_quotaConfigurations.Values.ToList(), options);

                // Write the file
                await _fileSystem.WriteFileAsync(QUOTA_FILE_PATH, json);

                // Set proper permissions (644, root:root)
                var permissions = new FilePermissions(0644);
                await _fileSystem.SetFilePermissionsAsync(QUOTA_FILE_PATH, permissions);
                await _fileSystem.SetFileOwnerAsync(QUOTA_FILE_PATH, "0", "0");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving quota configurations: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Represents a quota configuration for a group
    /// </summary>
    public class QuotaConfiguration
    {
        /// <summary>
        /// The group ID
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// The soft limit in bytes (warning threshold)
        /// </summary>
        public long SoftLimit { get; set; }

        /// <summary>
        /// The hard limit in bytes (strict maximum)
        /// </summary>
        public long HardLimit { get; set; }

        /// <summary>
        /// The time when the soft limit was first exceeded
        /// </summary>
        public DateTime? GracePeriodStart { get; set; }

        /// <summary>
        /// The grace period duration
        /// </summary>
        public TimeSpan GracePeriodDuration { get; set; }

        /// <summary>
        /// Whether the quota is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The current usage in bytes
        /// </summary>
        public long CurrentUsage { get; set; }

        /// <summary>
        /// Gets the percentage of soft limit used
        /// </summary>
        public double SoftLimitPercentage => SoftLimit > 0 ? (double)CurrentUsage / SoftLimit * 100 : 0;

        /// <summary>
        /// Gets the percentage of hard limit used
        /// </summary>
        public double HardLimitPercentage => HardLimit > 0 ? (double)CurrentUsage / HardLimit * 100 : 0;

        /// <summary>
        /// Gets whether the soft limit is exceeded
        /// </summary>
        public bool IsSoftLimitExceeded => CurrentUsage > SoftLimit;

        /// <summary>
        /// Gets whether the hard limit is exceeded
        /// </summary>
        public bool IsHardLimitExceeded => CurrentUsage > HardLimit;

        /// <summary>
        /// Gets whether the grace period has expired
        /// </summary>
        public bool IsGracePeriodExpired
        {
            get
            {
                if (!GracePeriodStart.HasValue || !IsSoftLimitExceeded)
                    return false;

                return DateTime.UtcNow > GracePeriodStart.Value.Add(GracePeriodDuration);
            }
        }

        /// <summary>
        /// Gets the remaining grace period time
        /// </summary>
        public TimeSpan? RemainingGracePeriod
        {
            get
            {
                if (!GracePeriodStart.HasValue || !IsSoftLimitExceeded)
                    return null;

                DateTime expirationTime = GracePeriodStart.Value.Add(GracePeriodDuration);
                TimeSpan remaining = expirationTime - DateTime.UtcNow;
                
                return remaining.TotalSeconds > 0 ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Gets a formatted string of the quota usage
        /// </summary>
        public string GetUsageSummary()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine($"Group ID: {GroupId}");
            sb.AppendLine($"Status: {(IsEnabled ? "Enabled" : "Disabled")}");
            sb.AppendLine($"Current Usage: {FormatSize(CurrentUsage)}");
            sb.AppendLine($"Soft Limit: {FormatSize(SoftLimit)} ({SoftLimitPercentage:F1}%)");
            sb.AppendLine($"Hard Limit: {FormatSize(HardLimit)} ({HardLimitPercentage:F1}%)");
            
            if (IsSoftLimitExceeded)
            {
                if (IsGracePeriodExpired)
                {
                    sb.AppendLine("Grace Period: Expired");
                }
                else if (RemainingGracePeriod.HasValue)
                {
                    var remaining = RemainingGracePeriod.Value;
                    sb.AppendLine($"Grace Period: {remaining.Days}d {remaining.Hours}h {remaining.Minutes}m remaining");
                }
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Formats a size in bytes to a human-readable string
        /// </summary>
        private string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            double size = bytes;
            
            while (size > 1024 && counter < suffixes.Length - 1)
            {
                size /= 1024;
                counter++;
            }
            
            return $"{size:F2} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// Type of quota exceeded event
    /// </summary>
    public enum QuotaExceededType
    {
        /// <summary>
        /// Soft limit exceeded
        /// </summary>
        SoftLimit,
        
        /// <summary>
        /// Hard limit exceeded
        /// </summary>
        HardLimit,
        
        /// <summary>
        /// Grace period expired
        /// </summary>
        GracePeriodExpired
    }

    /// <summary>
    /// Event arguments for quota events
    /// </summary>
    public class QuotaEventArgs : EventArgs
    {
        /// <summary>
        /// The quota configuration
        /// </summary>
        public QuotaConfiguration Quota { get; }

        public QuotaEventArgs(QuotaConfiguration quota)
        {
            Quota = quota;
        }
    }

    /// <summary>
    /// Event arguments for quota exceeded events
    /// </summary>
    public class QuotaExceededEventArgs : QuotaEventArgs
    {
        /// <summary>
        /// The attempted size change in bytes
        /// </summary>
        public long AttemptedSize { get; }
        
        /// <summary>
        /// The type of quota exceeded
        /// </summary>
        public QuotaExceededType ExceededType { get; }

        public QuotaExceededEventArgs(QuotaConfiguration quota, long attemptedSize, QuotaExceededType exceededType)
            : base(quota)
        {
            AttemptedSize = attemptedSize;
            ExceededType = exceededType;
        }
    }

    /// <summary>
    /// Event arguments for quota usage updated events
    /// </summary>
    public class QuotaUsageEventArgs : QuotaEventArgs
    {
        /// <summary>
        /// The previous usage in bytes
        /// </summary>
        public long PreviousUsage { get; }
        
        /// <summary>
        /// The new usage in bytes
        /// </summary>
        public long NewUsage { get; }

        public QuotaUsageEventArgs(QuotaConfiguration quota, long previousUsage, long newUsage)
            : base(quota)
        {
            PreviousUsage = previousUsage;
            NewUsage = newUsage;
        }
    }
}
