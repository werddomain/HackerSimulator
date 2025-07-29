using System;

namespace HackerOs.OS.Network.DNS
{
    /// <summary>
    /// Represents a DNS record in the simulated DNS system
    /// </summary>
    public class DnsRecord
    {
        /// <summary>
        /// Gets or sets the hostname for this record
        /// </summary>
        public string Hostname { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the record type
        /// </summary>
        public DnsRecordType RecordType { get; set; }
        
        /// <summary>
        /// Gets or sets the record value (e.g., IP address for A records)
        /// </summary>
        public string Value { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the time-to-live (TTL) in seconds
        /// </summary>
        public int TimeToLive { get; set; } = 3600; // Default 1 hour
        
        /// <summary>
        /// Gets or sets when this record was created or last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets whether this record is expired based on TTL
        /// </summary>
        public bool IsExpired => DateTime.Now > LastUpdated.AddSeconds(TimeToLive);
        
        /// <summary>
        /// Creates a new DNS record
        /// </summary>
        public DnsRecord() { }
        
        /// <summary>
        /// Creates a new DNS record with the specified parameters
        /// </summary>
        /// <param name="hostname">The hostname for this record</param>
        /// <param name="recordType">The record type</param>
        /// <param name="value">The record value</param>
        /// <param name="ttl">The time-to-live in seconds</param>
        public DnsRecord(string hostname, DnsRecordType recordType, string value, int ttl = 3600)
        {
            Hostname = hostname;
            RecordType = recordType;
            Value = value;
            TimeToLive = ttl;
        }
    }
}
