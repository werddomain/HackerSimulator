using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem.AdminTools
{
    /// <summary>
    /// Administrative tools for managing group quotas
    /// </summary>
    public class QuotaAdminTool
    {
        private readonly GroupQuotaManager _quotaManager;
        private readonly VirtualFileSystem _fileSystem;
        
        /// <summary>
        /// Creates a new instance of the QuotaAdminTool
        /// </summary>
        /// <param name="quotaManager">The quota manager</param>
        /// <param name="fileSystem">The virtual file system</param>
        public QuotaAdminTool(GroupQuotaManager quotaManager, VirtualFileSystem fileSystem)
        {
            _quotaManager = quotaManager ?? throw new ArgumentNullException(nameof(quotaManager));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }
        
        /// <summary>
        /// Lists all group quotas
        /// </summary>
        /// <returns>Formatted string with quota information</returns>
        public string ListQuotas()
        {
            var quotas = _quotaManager.GetAllQuotas();
            
            if (quotas.Count == 0)
                return "No quotas configured.";
                
            var sb = new StringBuilder();
            sb.AppendLine("Group Quotas:");
            sb.AppendLine("=============");
            sb.AppendLine();
            sb.AppendLine("Group ID | Group Name        | Quota (MB) | Used (MB) | % Used");
            sb.AppendLine("---------|------------------|------------|-----------|-------");
            
            foreach (var quota in quotas)
            {
                string groupName = GroupManager.Instance.GetGroupNameById(quota.Key);
                long usedBytes = _quotaManager.GetCurrentUsage(quota.Key);
                double usedMB = Math.Round(usedBytes / 1024.0 / 1024.0, 2);
                double quotaMB = Math.Round(quota.Value.QuotaBytes / 1024.0 / 1024.0, 2);
                double percentUsed = quotaMB > 0 ? Math.Round((usedMB / quotaMB) * 100, 2) : 0;
                
                sb.AppendLine($"{quota.Key.ToString().PadRight(9)} | {groupName.PadRight(18)} | {quotaMB.ToString("F2").PadRight(10)} | {usedMB.ToString("F2").PadRight(9)} | {percentUsed}%");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Sets a quota for a group
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="quotaMB">The quota in megabytes</param>
        /// <returns>True if successful; otherwise, false</returns>
        public bool SetQuota(int groupId, double quotaMB)
        {
            try
            {
                // Convert MB to bytes
                long quotaBytes = (long)(quotaMB * 1024 * 1024);
                
                // Check if group exists
                string groupName = GroupManager.Instance.GetGroupNameById(groupId);
                if (string.IsNullOrEmpty(groupName))
                    return false;
                    
                // Set the quota
                var quota = new QuotaConfiguration
                {
                    QuotaBytes = quotaBytes,
                    Enabled = true
                };
                
                _quotaManager.SetQuota(groupId, quota);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Removes a quota for a group
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <returns>True if successful; otherwise, false</returns>
        public bool RemoveQuota(int groupId)
        {
            try
            {
                return _quotaManager.RemoveQuota(groupId);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Gets detailed quota information for a group
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <returns>Formatted string with quota information</returns>
        public string GetQuotaDetails(int groupId)
        {
            try
            {
                // Check if group exists
                string groupName = GroupManager.Instance.GetGroupNameById(groupId);
                if (string.IsNullOrEmpty(groupName))
                    return $"Error: Group with ID {groupId} does not exist.";
                    
                // Get the quota
                if (!_quotaManager.HasQuota(groupId))
                    return $"No quota configured for group {groupName} (ID: {groupId}).";
                    
                var quota = _quotaManager.GetQuota(groupId);
                long usedBytes = _quotaManager.GetCurrentUsage(groupId);
                
                var sb = new StringBuilder();
                sb.AppendLine($"Quota Details for Group: {groupName} (ID: {groupId})");
                sb.AppendLine("=====================================");
                sb.AppendLine();
                sb.AppendLine($"Quota Enabled: {quota.Enabled}");
                sb.AppendLine($"Quota Size: {Math.Round(quota.QuotaBytes / 1024.0 / 1024.0, 2)} MB ({quota.QuotaBytes} bytes)");
                sb.AppendLine($"Current Usage: {Math.Round(usedBytes / 1024.0 / 1024.0, 2)} MB ({usedBytes} bytes)");
                
                double percentUsed = quota.QuotaBytes > 0 
                    ? Math.Round((double)usedBytes / quota.QuotaBytes * 100, 2) 
                    : 0;
                    
                sb.AppendLine($"Percent Used: {percentUsed}%");
                sb.AppendLine();
                
                // Get usage statistics if available
                var stats = _quotaManager.GetUsageStatistics(groupId);
                if (stats != null)
                {
                    sb.AppendLine("Usage Statistics:");
                    sb.AppendLine("-----------------");
                    sb.AppendLine($"Peak Usage: {Math.Round(stats.PeakUsageBytes / 1024.0 / 1024.0, 2)} MB on {stats.PeakUsageTimestamp}");
                    sb.AppendLine($"Last Updated: {stats.LastUpdated}");
                    sb.AppendLine($"Number of Quota Exceeded Events: {stats.QuotaExceededCount}");
                    sb.AppendLine($"Last Quota Exceeded: {(stats.LastQuotaExceededTime.HasValue ? stats.LastQuotaExceededTime.Value.ToString() : "Never")}");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error retrieving quota details: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Gets a report of all quotas and their usage
        /// </summary>
        /// <returns>Formatted string with the report</returns>
        public string GetQuotaReport()
        {
            try
            {
                var quotas = _quotaManager.GetAllQuotas();
                
                if (quotas.Count == 0)
                    return "No quotas configured.";
                    
                var sb = new StringBuilder();
                sb.AppendLine("Group Quota Report");
                sb.AppendLine("=================");
                sb.AppendLine();
                
                // Overall statistics
                int totalGroups = quotas.Count;
                int groupsOverQuota = 0;
                int groupsNearQuota = 0; // Over 80%
                
                foreach (var quota in quotas)
                {
                    long usedBytes = _quotaManager.GetCurrentUsage(quota.Key);
                    double percentUsed = quota.Value.QuotaBytes > 0 
                        ? (double)usedBytes / quota.Value.QuotaBytes * 100 
                        : 0;
                        
                    if (percentUsed > 100)
                        groupsOverQuota++;
                    else if (percentUsed > 80)
                        groupsNearQuota++;
                }
                
                sb.AppendLine($"Total Groups with Quotas: {totalGroups}");
                sb.AppendLine($"Groups Over Quota: {groupsOverQuota}");
                sb.AppendLine($"Groups Near Quota (>80%): {groupsNearQuota}");
                sb.AppendLine();
                
                // List groups by usage percentage (highest first)
                var sortedQuotas = quotas
                    .Select(q => new
                    {
                        GroupId = q.Key,
                        GroupName = GroupManager.Instance.GetGroupNameById(q.Key),
                        Quota = q.Value,
                        UsedBytes = _quotaManager.GetCurrentUsage(q.Key),
                        PercentUsed = q.Value.QuotaBytes > 0 
                            ? (double)_quotaManager.GetCurrentUsage(q.Key) / q.Value.QuotaBytes * 100 
                            : 0
                    })
                    .OrderByDescending(q => q.PercentUsed)
                    .ToList();
                    
                sb.AppendLine("Groups by Usage (Highest First):");
                sb.AppendLine("-------------------------------");
                sb.AppendLine();
                sb.AppendLine("Group Name        | % Used | Status");
                sb.AppendLine("------------------|--------|-------");
                
                foreach (var item in sortedQuotas)
                {
                    string status = item.PercentUsed > 100 
                        ? "OVER QUOTA" 
                        : item.PercentUsed > 80 
                            ? "WARNING" 
                            : "OK";
                            
                    sb.AppendLine($"{item.GroupName.PadRight(18)} | {Math.Round(item.PercentUsed, 2).ToString().PadRight(6)}% | {status}");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generating quota report: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Exports all quotas to a JSON file
        /// </summary>
        /// <param name="path">The path to export to</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public async Task<bool> ExportQuotasAsync(string path, User user)
        {
            try
            {
                var quotas = _quotaManager.GetAllQuotas();
                
                // Build a serializable dictionary
                var exportData = new Dictionary<string, object>();
                
                foreach (var quota in quotas)
                {
                    string groupName = GroupManager.Instance.GetGroupNameById(quota.Key);
                    
                    exportData[groupName] = new
                    {
                        GroupId = quota.Key,
                        QuotaMB = Math.Round(quota.Value.QuotaBytes / 1024.0 / 1024.0, 2),
                        QuotaBytes = quota.Value.QuotaBytes,
                        Enabled = quota.Value.Enabled,
                        CurrentUsageBytes = _quotaManager.GetCurrentUsage(quota.Key),
                        CurrentUsageMB = Math.Round(_quotaManager.GetCurrentUsage(quota.Key) / 1024.0 / 1024.0, 2),
                        PercentUsed = quota.Value.QuotaBytes > 0 
                            ? Math.Round((double)_quotaManager.GetCurrentUsage(quota.Key) / quota.Value.QuotaBytes * 100, 2) 
                            : 0
                    };
                }
                
                // Serialize to JSON
                string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Write to file
                byte[] content = Encoding.UTF8.GetBytes(json);
                await _fileSystem.WriteFileSecurelyAsync(path, json, user);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Imports quotas from a JSON file
        /// </summary>
        /// <param name="path">The path to import from</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>Number of quotas imported</returns>
        public async Task<int> ImportQuotasAsync(string path, User user)
        {
            try
            {
                // Read the file
                var result = await _fileSystem.ReadFileSecurelyAsync(path, user);
                if (result == null || !result.Success || result.Content == null)
                    return 0;
                    
                string json = Encoding.UTF8.GetString(result.Content);
                
                // Parse JSON
                var importData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (importData == null)
                    return 0;
                    
                int imported = 0;
                
                // Process each group
                foreach (var item in importData)
                {
                    string groupName = item.Key;
                    int groupId = GroupManager.Instance.GetGroupIdByName(groupName);
                    
                    if (groupId < 0)
                        continue; // Group doesn't exist
                        
                    var element = item.Value;
                    
                    // Extract quota data
                    long quotaBytes = 0;
                    bool enabled = true;
                    
                    if (element.TryGetProperty("QuotaBytes", out var quotaBytesElement))
                    {
                        quotaBytes = quotaBytesElement.GetInt64();
                    }
                    else if (element.TryGetProperty("QuotaMB", out var quotaMBElement))
                    {
                        double quotaMB = quotaMBElement.GetDouble();
                        quotaBytes = (long)(quotaMB * 1024 * 1024);
                    }
                    
                    if (element.TryGetProperty("Enabled", out var enabledElement))
                    {
                        enabled = enabledElement.GetBoolean();
                    }
                    
                    // Set the quota
                    if (quotaBytes > 0)
                    {
                        var quota = new QuotaConfiguration
                        {
                            QuotaBytes = quotaBytes,
                            Enabled = enabled
                        };
                        
                        _quotaManager.SetQuota(groupId, quota);
                        imported++;
                    }
                }
                
                return imported;
            }
            catch
            {
                return 0;
            }
        }
    }
}
