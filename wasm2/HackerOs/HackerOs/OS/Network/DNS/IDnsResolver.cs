using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.DNS
{
    /// <summary>
    /// Interface for DNS resolution services
    /// </summary>
    public interface IDnsResolver
    {
        /// <summary>
        /// Resolves a hostname to an IP address
        /// </summary>
        /// <param name="hostname">The hostname to resolve</param>
        /// <returns>The IP address as a string, or null if resolution fails</returns>
        Task<string?> ResolveHostnameAsync(string hostname);
        
        /// <summary>
        /// Performs a reverse DNS lookup to get a hostname from an IP
        /// </summary>
        /// <param name="ipAddress">The IP address to look up</param>
        /// <returns>The hostname, or null if lookup fails</returns>
        Task<string?> ReverseLookupAsync(string ipAddress);
        
        /// <summary>
        /// Adds a DNS record to the local resolver
        /// </summary>
        /// <param name="record">The DNS record to add</param>
        Task<bool> AddRecordAsync(DnsRecord record);
        
        /// <summary>
        /// Removes a DNS record from the local resolver
        /// </summary>
        /// <param name="hostname">The hostname for the record to remove</param>
        /// <param name="recordType">The type of record to remove</param>
        Task<bool> RemoveRecordAsync(string hostname, DnsRecordType recordType);
        
        /// <summary>
        /// Gets all DNS records for a given hostname
        /// </summary>
        /// <param name="hostname">The hostname to query</param>
        /// <returns>List of DNS records for the hostname</returns>
        Task<IEnumerable<DnsRecord>> GetRecordsAsync(string hostname);
        
        /// <summary>
        /// Clears the DNS cache
        /// </summary>
        Task ClearCacheAsync();
        
        /// <summary>
        /// Event fired when the DNS resolver configuration changes
        /// </summary>
        event EventHandler<DnsConfigChangedEventArgs> ConfigChanged;
        
        /// <summary>
        /// Event fired when a DNS query is processed
        /// </summary>
        event EventHandler<DnsQueryEventArgs> QueryProcessed;
    }
    
    /// <summary>
    /// Event arguments for DNS configuration changes
    /// </summary>
    public class DnsConfigChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the type of configuration change
        /// </summary>
        public DnsConfigChangeType ChangeType { get; set; }
        
        /// <summary>
        /// Gets or sets the affected hostname (if applicable)
        /// </summary>
        public string? Hostname { get; set; }
    }
    
    /// <summary>
    /// Event arguments for DNS query events
    /// </summary>
    public class DnsQueryEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the hostname that was queried
        /// </summary>
        public string Hostname { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the query type
        /// </summary>
        public DnsRecordType QueryType { get; set; }
        
        /// <summary>
        /// Gets or sets whether the query was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the result (IP address or hostname)
        /// </summary>
        public string? Result { get; set; }
        
        /// <summary>
        /// Gets or sets whether the result was served from cache
        /// </summary>
        public bool FromCache { get; set; }
    }
    
    /// <summary>
    /// Types of DNS configuration changes
    /// </summary>
    public enum DnsConfigChangeType
    {
        /// <summary>
        /// A record was added
        /// </summary>
        RecordAdded,
        
        /// <summary>
        /// A record was removed
        /// </summary>
        RecordRemoved,
        
        /// <summary>
        /// The DNS servers were changed
        /// </summary>
        ServersChanged,
        
        /// <summary>
        /// The cache was cleared
        /// </summary>
        CacheCleared
    }
}
