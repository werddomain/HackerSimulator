using System;
using System.Collections.Generic;
using System.Linq;

namespace HackerOs.OS.Network.DNS
{
    /// <summary>
    /// Represents a DNS zone in the simulated DNS system
    /// </summary>
    public class DnsZone
    {
        /// <summary>
        /// Gets or sets the zone name (domain)
        /// </summary>
        public string ZoneName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the primary name server for this zone
        /// </summary>
        public string PrimaryNameServer { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the zone administrator email
        /// </summary>
        public string AdminEmail { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the serial number for this zone
        /// </summary>
        public long SerialNumber { get; set; } = DateTimeToSerial(DateTime.Now);
        
        /// <summary>
        /// Gets or sets the refresh interval in seconds
        /// </summary>
        public int RefreshInterval { get; set; } = 3600; // 1 hour
        
        /// <summary>
        /// Gets or sets the retry interval in seconds
        /// </summary>
        public int RetryInterval { get; set; } = 600; // 10 minutes
        
        /// <summary>
        /// Gets or sets the expiry interval in seconds
        /// </summary>
        public int ExpiryInterval { get; set; } = 86400; // 1 day
        
        /// <summary>
        /// Gets or sets the minimum TTL in seconds
        /// </summary>
        public int MinimumTtl { get; set; } = 3600; // 1 hour
        
        /// <summary>
        /// Gets or sets the records in this zone
        /// </summary>
        public List<DnsRecord> Records { get; set; } = new List<DnsRecord>();
        
        /// <summary>
        /// Converts a DateTime to a DNS serial number (YYYYMMDDNN format)
        /// </summary>
        public static long DateTimeToSerial(DateTime dateTime)
        {
            return dateTime.Year * 1000000L + 
                   dateTime.Month * 10000L + 
                   dateTime.Day * 100L + 
                   1; // Start with 01 as the revision number
        }
        
        /// <summary>
        /// Increments the zone serial number
        /// </summary>
        public void IncrementSerialNumber()
        {
            var currentDate = DateTime.Now;
            var currentSerial = DateTimeToSerial(currentDate);
            
            // If the date part is the same, increment the revision number
            if (currentSerial / 100 == SerialNumber / 100)
            {
                SerialNumber++;
            }
            else
            {
                // Otherwise, use the new date with revision 01
                SerialNumber = currentSerial;
            }
        }
        
        /// <summary>
        /// Adds a record to this zone
        /// </summary>
        public void AddRecord(DnsRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
                
            // Remove any existing record of the same type and hostname
            Records.RemoveAll(r => 
                r.Hostname == record.Hostname && 
                r.RecordType == record.RecordType);
                
            // Add the new record
            Records.Add(record);
            
            // Update the serial number
            IncrementSerialNumber();
        }
        
        /// <summary>
        /// Removes a record from this zone
        /// </summary>
        public bool RemoveRecord(string hostname, DnsRecordType recordType)
        {
            int count = Records.RemoveAll(r => 
                r.Hostname == hostname && 
                r.RecordType == recordType);
                
            if (count > 0)
            {
                // Update the serial number
                IncrementSerialNumber();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets records of a specific type for a hostname
        /// </summary>
        public IEnumerable<DnsRecord> GetRecords(string hostname, DnsRecordType? recordType = null)
        {
            IEnumerable<DnsRecord> query = Records.Where(r => r.Hostname == hostname);
            
            if (recordType.HasValue)
            {
                query = query.Where(r => r.RecordType == recordType.Value);
            }
            
            return query.ToList();
        }
        
        /// <summary>
        /// Gets all records in this zone
        /// </summary>
        public IEnumerable<DnsRecord> GetAllRecords()
        {
            return Records.ToList();
        }
        
        /// <summary>
        /// Gets the SOA (Start of Authority) record for this zone
        /// </summary>
        public string GetSoaRecord()
        {
            return $"{ZoneName}. IN SOA {PrimaryNameServer}. {AdminEmail}. (" +
                   $"{SerialNumber} {RefreshInterval} {RetryInterval} {ExpiryInterval} {MinimumTtl})";
        }
        
        /// <summary>
        /// Checks if a hostname is in this zone
        /// </summary>
        public bool ContainsHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return false;
                
            // Check if the hostname ends with the zone name
            return hostname.EndsWith($".{ZoneName}", StringComparison.OrdinalIgnoreCase) ||
                   hostname.Equals(ZoneName, StringComparison.OrdinalIgnoreCase);
        }
    }
}