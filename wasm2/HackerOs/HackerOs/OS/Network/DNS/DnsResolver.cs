using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HackerOs.OS.IO.FileSystem;
using System.Text.RegularExpressions;
using HackerOs.OS.User;

namespace HackerOs.OS.Network.DNS
{
    /// <summary>
    /// Implementation of DNS resolver for HackerOS
    /// Handles hostname resolution and DNS record management
    /// </summary>
    public class DnsResolver : IDnsResolver
    {
        private readonly ILogger<DnsResolver> _logger;
        private readonly IVirtualFileSystem _fileSystem;
        private readonly Dictionary<string, List<DnsRecord>> _records;
        private readonly Dictionary<string, Dictionary<DnsRecordType, DnsRecord>> _cache;
        private readonly List<DnsZone> _zones;
        private readonly object _lockObject = new object();
        private readonly string _hostsFilePath = "/etc/hosts";
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromHours(1);

        /// <summary>
        /// Event fired when the DNS resolver configuration changes
        /// </summary>
        public event EventHandler<DnsConfigChangedEventArgs>? ConfigChanged;

        /// <summary>
        /// Event fired when a DNS query is processed
        /// </summary>
        public event EventHandler<DnsQueryEventArgs>? QueryProcessed;

        /// <summary>
        /// Creates a new instance of the DNS resolver
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="fileSystem">The virtual file system</param>
        public DnsResolver(ILogger<DnsResolver> logger, IVirtualFileSystem fileSystem)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            _records = new Dictionary<string, List<DnsRecord>>(StringComparer.OrdinalIgnoreCase);
            _cache = new Dictionary<string, Dictionary<DnsRecordType, DnsRecord>>(StringComparer.OrdinalIgnoreCase);
            _zones = new List<DnsZone>();

            // Initialize with default records
            InitializeDefaultRecordsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Initializes the resolver with default DNS records and loads the hosts file
        /// </summary>
        private async Task InitializeDefaultRecordsAsync()
        {
            // Add localhost record
            await AddRecordAsync(new DnsRecord("localhost", DnsRecordType.A, "127.0.0.1", 0));
            
            // Add default zones
            var localZone = new DnsZone
            {
                ZoneName = "local",
                PrimaryNameServer = "ns.local",
                AdminEmail = "admin@local"
            };
            _zones.Add(localZone);

            var exampleZone = new DnsZone
            {
                ZoneName = "example.com",
                PrimaryNameServer = "ns.example.com",
                AdminEmail = "admin@example.com"
            };
            _zones.Add(exampleZone);

            // Add example.com records
            await AddRecordAsync(new DnsRecord("example.com", DnsRecordType.A, "192.168.1.10"));
            await AddRecordAsync(new DnsRecord("www.example.com", DnsRecordType.A, "192.168.1.10"));
            await AddRecordAsync(new DnsRecord("mail.example.com", DnsRecordType.A, "192.168.1.11"));
            await AddRecordAsync(new DnsRecord("ns.example.com", DnsRecordType.A, "192.168.1.12"));
            
            // Add MX record for example.com
            await AddRecordAsync(new DnsRecord("example.com", DnsRecordType.MX, "mail.example.com"));
            
            // Add test.local records
            await AddRecordAsync(new DnsRecord("test.local", DnsRecordType.A, "192.168.1.20"));
            await AddRecordAsync(new DnsRecord("dev.test.local", DnsRecordType.A, "192.168.1.21"));
            
            // Add hackeros.net records
            await AddRecordAsync(new DnsRecord("hackeros.net", DnsRecordType.A, "192.168.1.30"));
            await AddRecordAsync(new DnsRecord("www.hackeros.net", DnsRecordType.A, "192.168.1.30"));
            await AddRecordAsync(new DnsRecord("docs.hackeros.net", DnsRecordType.A, "192.168.1.31"));

            // Try to load hosts file if it exists
            await LoadHostsFileAsync();

            _logger.LogInformation("DNS resolver initialized with default records");
        }

        /// <summary>
        /// Loads the hosts file from the file system
        /// </summary>
        private async Task LoadHostsFileAsync()
        {
            try
            {
                // Use system-level permissions when interacting with the hosts file
                if (await _fileSystem.FileExistsAsync(_hostsFilePath, UserManager.SystemUser))
                {
                    var hostsContent = await _fileSystem.ReadAllTextAsync(_hostsFilePath, UserManager.SystemUser);
                    await ParseHostsFileAsync(hostsContent);
                    _logger.LogInformation("Loaded hosts file from {Path}", _hostsFilePath);
                }
                else
                {
                    _logger.LogInformation("Hosts file not found at {Path}", _hostsFilePath);
                    await CreateDefaultHostsFileAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading hosts file from {Path}", _hostsFilePath);
            }
        }

        /// <summary>
        /// Creates a default hosts file
        /// </summary>
        private async Task CreateDefaultHostsFileAsync()
        {
            var defaultContent = 
@"# Default hosts file for HackerOS
# Format: IP_ADDRESS HOSTNAME [HOSTNAME2 HOSTNAME3 ...]

127.0.0.1 localhost
::1       localhost ip6-localhost

# Virtual Hosts
192.168.1.10 example.com www.example.com
192.168.1.11 mail.example.com
192.168.1.20 test.local dev.test.local
192.168.1.30 hackeros.net www.hackeros.net docs.hackeros.net

# Add custom host entries below this line
";
            try
            {
                // Ensure the directory exists
                var directory = HSystem.IO.HPath.GetDirectoryName(_hostsFilePath);
                if (!string.IsNullOrEmpty(directory) && !await _fileSystem.DirectoryExistsAsync(directory, UserManager.SystemUser))
                {
                    await _fileSystem.CreateDirectoryAsync(directory, UserManager.SystemUser);
                }

                await _fileSystem.WriteAllTextAsync(_hostsFilePath, defaultContent, UserManager.SystemUser);
                _logger.LogInformation("Created default hosts file at {Path}", _hostsFilePath);
                
                // Parse the default hosts file
                await ParseHostsFileAsync(defaultContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating default hosts file at {Path}", _hostsFilePath);
            }
        }

        /// <summary>
        /// Parses hosts file content and adds records to the resolver
        /// </summary>
        private async Task ParseHostsFileAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                // Parse the line: IP HOSTNAME [HOSTNAME2 ...]
                var parts = Regex.Split(trimmedLine, @"\s+").Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                if (parts.Length < 2)
                    continue;

                var ip = parts[0];
                // Validate IP format
                if (!IsValidIpAddress(ip))
                {
                    _logger.LogWarning("Invalid IP address in hosts file: {IP}", ip);
                    continue;
                }

                // Add each hostname
                for (int i = 1; i < parts.Length; i++)
                {
                    var hostname = parts[i].ToLowerInvariant();
                    await AddRecordAsync(new DnsRecord(hostname, DnsRecordType.A, ip));
                    _logger.LogDebug("Added hosts file entry: {IP} -> {Hostname}", ip, hostname);
                }
            }
        }

        /// <summary>
        /// Validates if a string is a valid IP address
        /// </summary>
        private bool IsValidIpAddress(string ip)
        {
            // Basic IPv4 validation
            if (Regex.IsMatch(ip, @"^(\d{1,3}\.){3}\d{1,3}$"))
            {
                var parts = ip.Split('.');
                return parts.All(p => byte.TryParse(p, out _));
            }
            
            // Basic IPv6 validation (simplified)
            return Regex.IsMatch(ip, @"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$") || ip == "::1";
        }

        /// <summary>
        /// Resolves a hostname to an IP address
        /// </summary>
        public async Task<string?> ResolveHostnameAsync(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                _logger.LogWarning("Empty hostname provided to DNS resolver");
                return null;
            }

            hostname = hostname.ToLowerInvariant().Trim();
            
            // If it's already an IP address, return it
            if (IsValidIpAddress(hostname))
            {
                _logger.LogDebug("Hostname is already an IP address: {Hostname}", hostname);
                return hostname;
            }

            // Check cache first
            var cacheResult = CheckCache(hostname, DnsRecordType.A);
            if (cacheResult != null)
            {
                FireQueryProcessedEvent(hostname, DnsRecordType.A, true, cacheResult, true);
                return cacheResult;
            }

            // Look up in records
            string? result = null;
            lock (_lockObject)
            {
                if (_records.TryGetValue(hostname, out var recordList))
                {
                    var aRecord = recordList.FirstOrDefault(r => r.RecordType == DnsRecordType.A && !r.IsExpired);
                    if (aRecord != null)
                    {
                        result = aRecord.Value;
                        
                        // Add to cache
                        AddToCache(hostname, aRecord);
                    }
                }
            }

            if (result != null)
            {
                _logger.LogDebug("Resolved hostname {Hostname} to {IP}", hostname, result);
                FireQueryProcessedEvent(hostname, DnsRecordType.A, true, result, false);
                return result;
            }

            // Try to handle subdomains
            var domainParts = hostname.Split('.');
            if (domainParts.Length > 2)
            {
                // Try wildcard record
                var wildcardHost = $"*.{string.Join(".", domainParts.Skip(1))}";
                var wildcardResult = await ResolveHostnameAsync(wildcardHost);
                if (wildcardResult != null)
                {
                    _logger.LogDebug("Resolved hostname {Hostname} using wildcard {Wildcard} to {IP}", 
                        hostname, wildcardHost, wildcardResult);
                    FireQueryProcessedEvent(hostname, DnsRecordType.A, true, wildcardResult, false);
                    return wildcardResult;
                }
            }

            _logger.LogWarning("Failed to resolve hostname: {Hostname}", hostname);
            FireQueryProcessedEvent(hostname, DnsRecordType.A, false, null, false);
            return null;
        }

        /// <summary>
        /// Performs a reverse DNS lookup to get a hostname from an IP
        /// </summary>
        public async Task<string?> ReverseLookupAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || !IsValidIpAddress(ipAddress))
            {
                _logger.LogWarning("Invalid IP address for reverse lookup: {IP}", ipAddress);
                return null;
            }

            // Check cache first
            var cacheKey = $"rev:{ipAddress}";
            var cacheResult = CheckCache(cacheKey, DnsRecordType.PTR);
            if (cacheResult != null)
            {
                FireQueryProcessedEvent(ipAddress, DnsRecordType.PTR, true, cacheResult, true);
                return cacheResult;
            }

            // Look for matching records
            string? result = null;
            lock (_lockObject)
            {
                foreach (var kvp in _records)
                {
                    var matchingRecord = kvp.Value.FirstOrDefault(r => 
                        r.RecordType == DnsRecordType.A && 
                        r.Value.Equals(ipAddress, StringComparison.OrdinalIgnoreCase) &&
                        !r.IsExpired);
                    
                    if (matchingRecord != null)
                    {
                        result = matchingRecord.Hostname;
                        
                        // Add to cache
                        var ptrRecord = new DnsRecord(cacheKey, DnsRecordType.PTR, result, matchingRecord.TimeToLive);
                        AddToCache(cacheKey, ptrRecord);
                        break;
                    }
                }
            }

            if (result != null)
            {
                _logger.LogDebug("Reverse lookup for IP {IP} returned {Hostname}", ipAddress, result);
                FireQueryProcessedEvent(ipAddress, DnsRecordType.PTR, true, result, false);
                return result;
            }

            _logger.LogWarning("Failed to perform reverse lookup for IP: {IP}", ipAddress);
            FireQueryProcessedEvent(ipAddress, DnsRecordType.PTR, false, null, false);
            return null;
        }

        /// <summary>
        /// Adds a DNS record to the local resolver
        /// </summary>
        public Task<bool> AddRecordAsync(DnsRecord record)
        {
            if (record == null)
            {
                _logger.LogWarning("Attempted to add null DNS record");
                return Task.FromResult(false);
            }

            if (string.IsNullOrWhiteSpace(record.Hostname))
            {
                _logger.LogWarning("Attempted to add DNS record with empty hostname");
                return Task.FromResult(false);
            }

            var hostname = record.Hostname.ToLowerInvariant();
            
            lock (_lockObject)
            {
                if (!_records.TryGetValue(hostname, out var recordList))
                {
                    recordList = new List<DnsRecord>();
                    _records[hostname] = recordList;
                }
                
                // Remove any existing records of the same type
                recordList.RemoveAll(r => r.RecordType == record.RecordType);
                
                // Add the new record
                recordList.Add(record);
                
                _logger.LogDebug("Added DNS record: {Type} {Hostname} -> {Value}", 
                    record.RecordType, hostname, record.Value);
            }

            // Fire event
            FireConfigChangedEvent(DnsConfigChangeType.RecordAdded, hostname);
            
            return Task.FromResult(true);
        }

        /// <summary>
        /// Removes a DNS record from the local resolver
        /// </summary>
        public Task<bool> RemoveRecordAsync(string hostname, DnsRecordType recordType)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                _logger.LogWarning("Attempted to remove DNS record with empty hostname");
                return Task.FromResult(false);
            }

            hostname = hostname.ToLowerInvariant();
            bool removed = false;
            
            lock (_lockObject)
            {
                if (_records.TryGetValue(hostname, out var recordList))
                {
                    int count = recordList.RemoveAll(r => r.RecordType == recordType);
                    removed = count > 0;
                    
                    // If no records left, remove the hostname entry
                    if (recordList.Count == 0)
                    {
                        _records.Remove(hostname);
                    }
                    
                    if (removed)
                    {
                        _logger.LogDebug("Removed DNS record: {Type} {Hostname}", recordType, hostname);
                    }
                }
            }

            // Remove from cache as well
            RemoveFromCache(hostname, recordType);
            
            // Fire event if something was removed
            if (removed)
            {
                FireConfigChangedEvent(DnsConfigChangeType.RecordRemoved, hostname);
            }
            
            return Task.FromResult(removed);
        }

        /// <summary>
        /// Gets all DNS records for a given hostname
        /// </summary>
        public Task<IEnumerable<DnsRecord>> GetRecordsAsync(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                _logger.LogWarning("Attempted to get DNS records with empty hostname");
                return Task.FromResult(Enumerable.Empty<DnsRecord>());
            }

            hostname = hostname.ToLowerInvariant();
            IEnumerable<DnsRecord> result;
            
            lock (_lockObject)
            {
                if (_records.TryGetValue(hostname, out var recordList))
                {
                    // Return a copy of non-expired records
                    result = recordList.Where(r => !r.IsExpired).ToList();
                }
                else
                {
                    result = Enumerable.Empty<DnsRecord>();
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Clears the DNS cache
        /// </summary>
        public Task ClearCacheAsync()
        {
            lock (_lockObject)
            {
                _cache.Clear();
                _logger.LogInformation("DNS cache cleared");
            }
            
            // Fire event
            FireConfigChangedEvent(DnsConfigChangeType.CacheCleared, null);
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks the cache for a DNS record
        /// </summary>
        private string? CheckCache(string hostname, DnsRecordType recordType)
        {
            lock (_lockObject)
            {
                if (_cache.TryGetValue(hostname, out var typeDict) && 
                    typeDict.TryGetValue(recordType, out var record) && 
                    !record.IsExpired)
                {
                    _logger.LogDebug("DNS cache hit: {Type} {Hostname} -> {Value}", 
                        recordType, hostname, record.Value);
                    return record.Value;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Adds a record to the cache
        /// </summary>
        private void AddToCache(string hostname, DnsRecord record)
        {
            lock (_lockObject)
            {
                if (!_cache.TryGetValue(hostname, out var typeDict))
                {
                    typeDict = new Dictionary<DnsRecordType, DnsRecord>();
                    _cache[hostname] = typeDict;
                }
                
                typeDict[record.RecordType] = record;
            }
        }

        /// <summary>
        /// Removes a record from the cache
        /// </summary>
        private void RemoveFromCache(string hostname, DnsRecordType recordType)
        {
            lock (_lockObject)
            {
                if (_cache.TryGetValue(hostname, out var typeDict))
                {
                    typeDict.Remove(recordType);
                    
                    // If no records left for this hostname, remove the hostname entry
                    if (typeDict.Count == 0)
                    {
                        _cache.Remove(hostname);
                    }
                }
            }
        }

        /// <summary>
        /// Fires the ConfigChanged event
        /// </summary>
        private void FireConfigChangedEvent(DnsConfigChangeType changeType, string? hostname)
        {
            ConfigChanged?.Invoke(this, new DnsConfigChangedEventArgs
            {
                ChangeType = changeType,
                Hostname = hostname
            });
        }

        /// <summary>
        /// Fires the QueryProcessed event
        /// </summary>
        private void FireQueryProcessedEvent(string hostname, DnsRecordType queryType, bool success, string? result, bool fromCache)
        {
            QueryProcessed?.Invoke(this, new DnsQueryEventArgs
            {
                Hostname = hostname,
                QueryType = queryType,
                Success = success,
                Result = result,
                FromCache = fromCache
            });
        }
    }

    /// <summary>
    /// DNS record types
    /// </summary>
    public enum DnsRecordType
    {
        /// <summary>
        /// Address record (IPv4)
        /// </summary>
        A,
        
        /// <summary>
        /// Address record (IPv6)
        /// </summary>
        AAAA,
        
        /// <summary>
        /// Canonical name record (alias)
        /// </summary>
        CNAME,
        
        /// <summary>
        /// Mail exchange record
        /// </summary>
        MX,
        
        /// <summary>
        /// Name server record
        /// </summary>
        NS,
        
        /// <summary>
        /// Pointer record (reverse DNS)
        /// </summary>
        PTR,
        
        /// <summary>
        /// Service locator record
        /// </summary>
        SRV,
        
        /// <summary>
        /// Text record
        /// </summary>
        TXT
    }
}
