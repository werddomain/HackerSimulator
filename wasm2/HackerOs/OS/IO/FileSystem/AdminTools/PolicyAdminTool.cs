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
    /// Administrative tool for managing group policies
    /// </summary>
    public class PolicyAdminTool
    {
        private readonly GroupPolicyManager _policyManager;
        private readonly VirtualFileSystem _fileSystem;
        
        /// <summary>
        /// Creates a new instance of the PolicyAdminTool
        /// </summary>
        /// <param name="policyManager">The policy manager</param>
        /// <param name="fileSystem">The virtual file system</param>
        public PolicyAdminTool(GroupPolicyManager policyManager, VirtualFileSystem fileSystem)
        {
            _policyManager = policyManager ?? throw new ArgumentNullException(nameof(policyManager));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }
        
        /// <summary>
        /// Lists all policies
        /// </summary>
        /// <returns>Formatted string with policy information</returns>
        public string ListPolicies()
        {
            var policies = _policyManager.GetAllPolicies();
            
            if (policies.Count == 0)
                return "No policies configured.";
                
            var sb = new StringBuilder();
            sb.AppendLine("Group Policies:");
            sb.AppendLine("==============");
            sb.AppendLine();
            sb.AppendLine("ID    | Type             | Group Name        | Name                    | Enabled");
            sb.AppendLine("------|------------------|------------------|-------------------------|--------");
            
            foreach (var policy in policies)
            {
                string groupName = GroupManager.Instance.GetGroupNameById(policy.GroupId);
                string policyType = policy.GetType().Name;
                
                sb.AppendLine($"{policy.Id.ToString().PadRight(5)} | {policyType.PadRight(18)} | {groupName.PadRight(18)} | {policy.Name.PadRight(25)} | {policy.Enabled}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Gets detailed information about a policy
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <returns>Formatted string with policy details</returns>
        public string GetPolicyDetails(string policyId)
        {
            try
            {
                var policy = _policyManager.GetPolicy(policyId);
                if (policy == null)
                    return $"Policy with ID {policyId} not found.";
                    
                string groupName = GroupManager.Instance.GetGroupNameById(policy.GroupId);
                
                var sb = new StringBuilder();
                sb.AppendLine($"Policy Details: {policy.Name} (ID: {policy.Id})");
                sb.AppendLine("==============================================");
                sb.AppendLine();
                sb.AppendLine($"Type: {policy.GetType().Name}");
                sb.AppendLine($"Group: {groupName} (ID: {policy.GroupId})");
                sb.AppendLine($"Enabled: {policy.Enabled}");
                sb.AppendLine($"Description: {policy.Description}");
                sb.AppendLine($"Last Updated: {policy.LastUpdated}");
                sb.AppendLine();
                
                // Add policy-specific details
                sb.AppendLine("Policy Properties:");
                sb.AppendLine("-----------------");
                
                // Use reflection to get all properties
                var properties = policy.GetType().GetProperties()
                    .Where(p => p.Name != "Id" && p.Name != "Name" && p.Name != "GroupId" && 
                                p.Name != "Enabled" && p.Name != "Description" && p.Name != "LastUpdated")
                    .ToList();
                                
                foreach (var property in properties)
                {
                    var value = property.GetValue(policy);
                    sb.AppendLine($"{property.Name}: {value}");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error retrieving policy details: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Enables a policy
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <returns>True if successful; otherwise, false</returns>
        public bool EnablePolicy(string policyId)
        {
            try
            {
                var policy = _policyManager.GetPolicy(policyId);
                if (policy == null)
                    return false;
                    
                policy.Enabled = true;
                _policyManager.UpdatePolicy(policy);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Disables a policy
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <returns>True if successful; otherwise, false</returns>
        public bool DisablePolicy(string policyId)
        {
            try
            {
                var policy = _policyManager.GetPolicy(policyId);
                if (policy == null)
                    return false;
                    
                policy.Enabled = false;
                _policyManager.UpdatePolicy(policy);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Removes a policy
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <returns>True if successful; otherwise, false</returns>
        public bool RemovePolicy(string policyId)
        {
            try
            {
                return _policyManager.RemovePolicy(policyId);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Creates a new security policy
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="securityLevel">The security level</param>
        /// <param name="restrictedPaths">The restricted paths</param>
        /// <returns>The policy ID if successful; otherwise, null</returns>
        public string CreateSecurityPolicy(
            int groupId,
            string name,
            string description,
            int securityLevel,
            List<string> restrictedPaths)
        {
            try
            {
                var policy = new SecurityPolicy
                {
                    GroupId = groupId,
                    Name = name,
                    Description = description,
                    SecurityLevel = securityLevel,
                    RestrictedPaths = restrictedPaths,
                    Enabled = true,
                    LastUpdated = DateTime.UtcNow
                };
                
                return _policyManager.AddPolicy(policy);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Creates a new resource policy
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="maxCpuPercentage">The maximum CPU percentage</param>
        /// <param name="maxMemoryMB">The maximum memory in MB</param>
        /// <param name="maxProcesses">The maximum processes</param>
        /// <returns>The policy ID if successful; otherwise, null</returns>
        public string CreateResourcePolicy(
            int groupId,
            string name,
            string description,
            int maxCpuPercentage,
            int maxMemoryMB,
            int maxProcesses)
        {
            try
            {
                var policy = new ResourcePolicy
                {
                    GroupId = groupId,
                    Name = name,
                    Description = description,
                    MaxCpuPercentage = maxCpuPercentage,
                    MaxMemoryMB = maxMemoryMB,
                    MaxProcesses = maxProcesses,
                    Enabled = true,
                    LastUpdated = DateTime.UtcNow
                };
                
                return _policyManager.AddPolicy(policy);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Creates a new access policy
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="allowedOperations">The allowed operations</param>
        /// <param name="deniedOperations">The denied operations</param>
        /// <param name="targetPaths">The target paths</param>
        /// <returns>The policy ID if successful; otherwise, null</returns>
        public string CreateAccessPolicy(
            int groupId,
            string name,
            string description,
            List<string> allowedOperations,
            List<string> deniedOperations,
            List<string> targetPaths)
        {
            try
            {
                var policy = new AccessPolicy
                {
                    GroupId = groupId,
                    Name = name,
                    Description = description,
                    AllowedOperations = allowedOperations,
                    DeniedOperations = deniedOperations,
                    TargetPaths = targetPaths,
                    Enabled = true,
                    LastUpdated = DateTime.UtcNow
                };
                
                return _policyManager.AddPolicy(policy);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Creates a new file system policy
        /// </summary>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="maxFileSize">The maximum file size in bytes</param>
        /// <param name="allowedExtensions">The allowed extensions</param>
        /// <param name="deniedExtensions">The denied extensions</param>
        /// <param name="targetDirectories">The target directories</param>
        /// <returns>The policy ID if successful; otherwise, null</returns>
        public string CreateFileSystemPolicy(
            int groupId,
            string name,
            string description,
            long maxFileSize,
            List<string> allowedExtensions,
            List<string> deniedExtensions,
            List<string> targetDirectories)
        {
            try
            {
                var policy = new FileSystemPolicy
                {
                    GroupId = groupId,
                    Name = name,
                    Description = description,
                    MaxFileSize = maxFileSize,
                    AllowedExtensions = allowedExtensions,
                    DeniedExtensions = deniedExtensions,
                    TargetDirectories = targetDirectories,
                    Enabled = true,
                    LastUpdated = DateTime.UtcNow
                };
                
                return _policyManager.AddPolicy(policy);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets a report of all policies
        /// </summary>
        /// <returns>Formatted string with the report</returns>
        public string GetPolicyReport()
        {
            try
            {
                var policies = _policyManager.GetAllPolicies();
                
                if (policies.Count == 0)
                    return "No policies configured.";
                    
                var sb = new StringBuilder();
                sb.AppendLine("Group Policy Report");
                sb.AppendLine("==================");
                sb.AppendLine();
                
                // Overall statistics
                int totalPolicies = policies.Count;
                int enabledPolicies = policies.Count(p => p.Enabled);
                int disabledPolicies = totalPolicies - enabledPolicies;
                
                sb.AppendLine($"Total Policies: {totalPolicies}");
                sb.AppendLine($"Enabled Policies: {enabledPolicies}");
                sb.AppendLine($"Disabled Policies: {disabledPolicies}");
                sb.AppendLine();
                
                // Group by policy type
                var policyTypes = policies
                    .GroupBy(p => p.GetType().Name)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToList();
                    
                sb.AppendLine("Policies by Type:");
                sb.AppendLine("-----------------");
                
                foreach (var type in policyTypes)
                {
                    sb.AppendLine($"{type.Type}: {type.Count}");
                }
                
                sb.AppendLine();
                
                // Group by group
                var policyGroups = policies
                    .GroupBy(p => p.GroupId)
                    .Select(g => new 
                    { 
                        GroupId = g.Key, 
                        GroupName = GroupManager.Instance.GetGroupNameById(g.Key),
                        Count = g.Count(),
                        Enabled = g.Count(p => p.Enabled)
                    })
                    .OrderByDescending(g => g.Count)
                    .ToList();
                    
                sb.AppendLine("Policies by Group:");
                sb.AppendLine("-----------------");
                sb.AppendLine();
                sb.AppendLine("Group Name        | Total | Enabled");
                sb.AppendLine("------------------|-------|--------");
                
                foreach (var group in policyGroups)
                {
                    sb.AppendLine($"{group.GroupName.PadRight(18)} | {group.Count.ToString().PadRight(5)} | {group.Enabled}");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generating policy report: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Exports all policies to a JSON file
        /// </summary>
        /// <param name="path">The path to export to</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public async Task<bool> ExportPoliciesAsync(string path, User user)
        {
            try
            {
                var policies = _policyManager.GetAllPolicies();
                
                // Build a serializable dictionary
                var exportData = new List<object>();
                
                foreach (var policy in policies)
                {
                    // Create a dynamic object with policy properties
                    var policyData = new Dictionary<string, object>
                    {
                        ["Id"] = policy.Id,
                        ["Type"] = policy.GetType().Name,
                        ["Name"] = policy.Name,
                        ["GroupId"] = policy.GroupId,
                        ["GroupName"] = GroupManager.Instance.GetGroupNameById(policy.GroupId),
                        ["Description"] = policy.Description,
                        ["Enabled"] = policy.Enabled,
                        ["LastUpdated"] = policy.LastUpdated
                    };
                    
                    // Add policy-specific properties
                    if (policy is SecurityPolicy securityPolicy)
                    {
                        policyData["SecurityLevel"] = securityPolicy.SecurityLevel;
                        policyData["RestrictedPaths"] = securityPolicy.RestrictedPaths;
                    }
                    else if (policy is ResourcePolicy resourcePolicy)
                    {
                        policyData["MaxCpuPercentage"] = resourcePolicy.MaxCpuPercentage;
                        policyData["MaxMemoryMB"] = resourcePolicy.MaxMemoryMB;
                        policyData["MaxProcesses"] = resourcePolicy.MaxProcesses;
                    }
                    else if (policy is AccessPolicy accessPolicy)
                    {
                        policyData["AllowedOperations"] = accessPolicy.AllowedOperations;
                        policyData["DeniedOperations"] = accessPolicy.DeniedOperations;
                        policyData["TargetPaths"] = accessPolicy.TargetPaths;
                    }
                    else if (policy is FileSystemPolicy fileSystemPolicy)
                    {
                        policyData["MaxFileSize"] = fileSystemPolicy.MaxFileSize;
                        policyData["AllowedExtensions"] = fileSystemPolicy.AllowedExtensions;
                        policyData["DeniedExtensions"] = fileSystemPolicy.DeniedExtensions;
                        policyData["TargetDirectories"] = fileSystemPolicy.TargetDirectories;
                    }
                    
                    exportData.Add(policyData);
                }
                
                // Serialize to JSON
                string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Write to file
                await _fileSystem.WriteFileSecurelyAsync(path, json, user);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Imports policies from a JSON file
        /// </summary>
        /// <param name="path">The path to import from</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>Number of policies imported</returns>
        public async Task<int> ImportPoliciesAsync(string path, User user)
        {
            try
            {
                // Read the file
                var result = await _fileSystem.ReadFileSecurelyAsync(path, user);
                if (result == null || !result.Success || result.Content == null)
                    return 0;
                    
                string json = Encoding.UTF8.GetString(result.Content);
                
                // Parse JSON
                var importData = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (importData == null)
                    return 0;
                    
                int imported = 0;
                
                // Process each policy
                foreach (var element in importData)
                {
                    try
                    {
                        // Extract common properties
                        string policyType = element.GetProperty("Type").GetString();
                        string name = element.GetProperty("Name").GetString();
                        string description = element.GetProperty("Description").GetString();
                        
                        // Try to get group ID first, then fall back to group name if needed
                        int groupId;
                        if (element.TryGetProperty("GroupId", out var groupIdElement))
                        {
                            groupId = groupIdElement.GetInt32();
                        }
                        else if (element.TryGetProperty("GroupName", out var groupNameElement))
                        {
                            string groupName = groupNameElement.GetString();
                            groupId = GroupManager.Instance.GetGroupIdByName(groupName);
                            
                            if (groupId < 0)
                                continue; // Group doesn't exist
                        }
                        else
                        {
                            continue; // Missing group information
                        }
                        
                        // Create policy based on type
                        string policyId = null;
                        
                        switch (policyType)
                        {
                            case "SecurityPolicy":
                                int securityLevel = element.GetProperty("SecurityLevel").GetInt32();
                                var restrictedPaths = JsonSerializer.Deserialize<List<string>>(
                                    element.GetProperty("RestrictedPaths").GetRawText());
                                    
                                policyId = CreateSecurityPolicy(
                                    groupId, name, description, securityLevel, restrictedPaths);
                                break;
                                
                            case "ResourcePolicy":
                                int maxCpu = element.GetProperty("MaxCpuPercentage").GetInt32();
                                int maxMemory = element.GetProperty("MaxMemoryMB").GetInt32();
                                int maxProcesses = element.GetProperty("MaxProcesses").GetInt32();
                                
                                policyId = CreateResourcePolicy(
                                    groupId, name, description, maxCpu, maxMemory, maxProcesses);
                                break;
                                
                            case "AccessPolicy":
                                var allowedOps = JsonSerializer.Deserialize<List<string>>(
                                    element.GetProperty("AllowedOperations").GetRawText());
                                var deniedOps = JsonSerializer.Deserialize<List<string>>(
                                    element.GetProperty("DeniedOperations").GetRawText());
                                var targetPaths = JsonSerializer.Deserialize<List<string>>(
                                    element.GetProperty("TargetPaths").GetRawText());
                                    
                                policyId = CreateAccessPolicy(
                                    groupId, name, description, allowedOps, deniedOps, targetPaths);
                                break;
                                
                            case "FileSystemPolicy":
                                long maxFileSize = element.GetProperty("MaxFileSize").GetInt64();
                                var allowedExts = JsonSerializer.Deserialize<List<string>>(
                                    element.GetProperty("AllowedExtensions").GetRawText());
                                var deniedExts = JsonSerializer.Deserialize<List<string>>(
                                    element.GetProperty("DeniedExtensions").GetRawText());
                                var targetDirs = JsonSerializer.Deserialize<List<string>>(
                                    element.GetProperty("TargetDirectories").GetRawText());
                                    
                                policyId = CreateFileSystemPolicy(
                                    groupId, name, description, maxFileSize, allowedExts, deniedExts, targetDirs);
                                break;
                        }
                        
                        if (policyId != null)
                        {
                            // Set enabled status if present
                            if (element.TryGetProperty("Enabled", out var enabledElement))
                            {
                                bool enabled = enabledElement.GetBoolean();
                                if (!enabled)
                                {
                                    DisablePolicy(policyId);
                                }
                            }
                            
                            imported++;
                        }
                    }
                    catch
                    {
                        // Skip this policy and continue with the next one
                        continue;
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
