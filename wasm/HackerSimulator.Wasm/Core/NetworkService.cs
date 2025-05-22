using System;
using System.Collections.Generic;
using System.Linq;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Information about an open or closed port on a host.
    /// </summary>
    public class PortInfo
    {
        public int Port { get; set; }
        public string State { get; set; } = "closed"; // open, closed, filtered
        public ServiceInfo? Service { get; set; }
    }

    /// <summary>
    /// Optional details about the service running on a port.
    /// </summary>
    public class ServiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? Info { get; set; }
    }

    /// <summary>
    /// OS information detected for a host.
    /// </summary>
    public class OsInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int Accuracy { get; set; }
    }

    /// <summary>
    /// Describes a host on the simulated network.
    /// </summary>
    public class HostInfo
    {
        public string Ip { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public OsInfo? OsInfo { get; set; }
        public List<PortInfo> Ports { get; set; } = new();
        public bool IsUp { get; set; }
        public double Latency { get; set; }
        public string? Mac { get; set; }
    }

    /// <summary>
    /// Provides network functions similar to the JavaScript implementation.
    /// </summary>
    public class NetworkService
    {
        private readonly Dictionary<string, HostInfo> _hosts = new();

        public NetworkService()
        {
            InitializeDefaultHosts();
        }

        private void InitializeDefaultHosts()
        {
            // Localhost
            RegisterHost(new HostInfo
            {
                Ip = "127.0.0.1",
                Hostname = "localhost",
                OsInfo = new OsInfo { Name = "Linux", Version = "5.4.0", Accuracy = 99 },
                Ports = new List<PortInfo>
                {
                    new PortInfo { Port = 22, State = "open", Service = new ServiceInfo { Name = "ssh", Version = "OpenSSH 8.2p1", Info = "SSH protocol 2.0" } },
                    new PortInfo { Port = 80, State = "open", Service = new ServiceInfo { Name = "http", Version = "Apache httpd 2.4.41", Info = "HTTP server" } },
                    new PortInfo { Port = 3306, State = "open", Service = new ServiceInfo { Name = "mysql", Version = "MySQL 5.7.30", Info = "MySQL database server" } },
                    new PortInfo { Port = 8080, State = "open", Service = new ServiceInfo { Name = "http-proxy", Version = "nginx 1.18.0", Info = "Proxy server" } },
                    new PortInfo { Port = 25, State = "filtered", Service = new ServiceInfo { Name = "smtp" } },
                    new PortInfo { Port = 443, State = "filtered", Service = new ServiceInfo { Name = "https" } }
                },
                IsUp = true,
                Latency = 0.1
            });

            // Example.com
            RegisterHost(new HostInfo
            {
                Ip = "192.168.1.10",
                Hostname = "example.com",
                OsInfo = new OsInfo { Name = "Linux", Version = "Ubuntu", Accuracy = 85 },
                Ports = new List<PortInfo>
                {
                    new PortInfo { Port = 80, State = "open", Service = new ServiceInfo { Name = "http", Version = "Apache httpd 2.4.41", Info = "HTTP server" } },
                    new PortInfo { Port = 443, State = "open", Service = new ServiceInfo { Name = "https", Version = "Apache httpd 2.4.41", Info = "HTTP server" } },
                    new PortInfo { Port = 22, State = "filtered", Service = new ServiceInfo { Name = "ssh" } },
                    new PortInfo { Port = 25, State = "filtered", Service = new ServiceInfo { Name = "smtp" } },
                    new PortInfo { Port = 110, State = "filtered", Service = new ServiceInfo { Name = "pop3" } }
                },
                IsUp = true,
                Latency = 15
            });

            // MyBank.net
            RegisterHost(new HostInfo
            {
                Ip = "192.168.1.20",
                Hostname = "mybank.net",
                OsInfo = new OsInfo { Name = "Unix", Version = "CentOS", Accuracy = 75 },
                Ports = new List<PortInfo>
                {
                    new PortInfo { Port = 80, State = "open", Service = new ServiceInfo { Name = "http", Version = "Apache httpd 2.4.41", Info = "HTTP server" } },
                    new PortInfo { Port = 443, State = "open", Service = new ServiceInfo { Name = "https", Version = "Apache httpd 2.4.41", Info = "HTTP server" } },
                    new PortInfo { Port = 8443, State = "open", Service = new ServiceInfo { Name = "https-alt", Version = "nginx 1.18.0", Info = "Proxy server" } },
                    new PortInfo { Port = 21, State = "filtered", Service = new ServiceInfo { Name = "ftp" } },
                    new PortInfo { Port = 22, State = "filtered", Service = new ServiceInfo { Name = "ssh" } },
                    new PortInfo { Port = 23, State = "filtered", Service = new ServiceInfo { Name = "telnet" } },
                    new PortInfo { Port = 3306, State = "filtered", Service = new ServiceInfo { Name = "mysql" } }
                },
                IsUp = true,
                Latency = 25
            });

            // TargetBank.com
            RegisterHost(new HostInfo
            {
                Ip = "192.168.1.30",
                Hostname = "targetbank.com",
                OsInfo = new OsInfo { Name = "Linux", Version = "Debian", Accuracy = 90 },
                Ports = new List<PortInfo>
                {
                    new PortInfo { Port = 80, State = "open", Service = new ServiceInfo { Name = "http", Version = "Apache httpd 2.4.41", Info = "HTTP server" } },
                    new PortInfo { Port = 443, State = "open", Service = new ServiceInfo { Name = "https", Version = "Apache httpd 2.4.41", Info = "HTTP server" } },
                    new PortInfo { Port = 8080, State = "open", Service = new ServiceInfo { Name = "http-proxy", Version = "nginx 1.18.0", Info = "Proxy server" } },
                    new PortInfo { Port = 21, State = "open", Service = new ServiceInfo { Name = "ftp", Version = "vsftpd 3.0.3", Info = "FTP server" } },
                    new PortInfo { Port = 22, State = "open", Service = new ServiceInfo { Name = "ssh", Version = "OpenSSH 8.2p1", Info = "SSH protocol 2.0" } },
                    new PortInfo { Port = 3306, State = "open", Service = new ServiceInfo { Name = "mysql", Version = "MySQL 5.7.30", Info = "MySQL database server" } },
                    new PortInfo { Port = 23, State = "filtered", Service = new ServiceInfo { Name = "telnet" } },
                    new PortInfo { Port = 25, State = "filtered", Service = new ServiceInfo { Name = "smtp" } },
                    new PortInfo { Port = 110, State = "filtered", Service = new ServiceInfo { Name = "pop3" } },
                    new PortInfo { Port = 8443, State = "filtered", Service = new ServiceInfo { Name = "https-alt" } }
                },
                IsUp = true,
                Latency = 30
            });

            // Router
            RegisterHost(new HostInfo
            {
                Ip = "192.168.1.1",
                Hostname = "router.local",
                OsInfo = new OsInfo { Name = "Router", Version = "DD-WRT", Accuracy = 95 },
                Ports = new List<PortInfo>
                {
                    new PortInfo { Port = 53, State = "open", Service = new ServiceInfo { Name = "domain", Version = "BIND 9.16.1", Info = "DNS server" } },
                    new PortInfo { Port = 80, State = "open", Service = new ServiceInfo { Name = "http", Version = "lighttpd 1.4.55", Info = "HTTP server" } },
                    new PortInfo { Port = 443, State = "open", Service = new ServiceInfo { Name = "https", Version = "lighttpd 1.4.55", Info = "HTTP server" } },
                    new PortInfo { Port = 22, State = "filtered", Service = new ServiceInfo { Name = "ssh" } },
                    new PortInfo { Port = 23, State = "filtered", Service = new ServiceInfo { Name = "telnet" } },
                    new PortInfo { Port = 25, State = "filtered", Service = new ServiceInfo { Name = "smtp" } }
                },
                IsUp = true,
                Latency = 2
            });
        }

        public void RegisterHost(HostInfo host) => _hosts[host.Ip] = host;

        public HostInfo? GetHostByIp(string ip) => _hosts.TryGetValue(ip, out var host) ? host : null;

        public bool UpdateHost(string ip, Action<HostInfo> update)
        {
            if (!_hosts.TryGetValue(ip, out var host))
                return false;
            update(host);
            return true;
        }

        public IEnumerable<HostInfo> GetAllHosts() => _hosts.Values;

        public HostInfo? ScanHost(string ip, string portRange = "1-1000")
        {
            if (!_hosts.TryGetValue(ip, out var host) || !host.IsUp)
                return null;

            var portsToCheck = ParsePortRange(portRange);
            var filteredPorts = host.Ports.Where(p => portsToCheck.Contains(p.Port)).ToList();

            return new HostInfo
            {
                Ip = host.Ip,
                Hostname = host.Hostname,
                OsInfo = host.OsInfo,
                Ports = filteredPorts,
                IsUp = host.IsUp,
                Latency = host.Latency,
                Mac = host.Mac
            };
        }

        private static List<int> ParsePortRange(string range)
        {
            var result = new List<int>();
            var parts = range.Split(',');
            foreach (var part in parts)
            {
                if (part.Contains('-'))
                {
                    var ends = part.Split('-');
                    if (int.TryParse(ends[0], out var start) && int.TryParse(ends[1], out var end))
                    {
                        for (int i = start; i <= end; i++)
                            result.Add(i);
                    }
                }
                else if (int.TryParse(part, out var single))
                {
                    result.Add(single);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Simple DNS server for mapping host names to IP addresses.
    /// </summary>
    public class DnsService
    {
        private readonly Dictionary<string, string> _records = new();
        private readonly Dictionary<string, string> _reverse = new();

        public DnsService()
        {
            InitializeDefaultRecords();
        }

        private void InitializeDefaultRecords()
        {
            AddRecord("localhost", "127.0.0.1");
            AddRecord("example.com", "192.168.1.10");
            AddRecord("mybank.net", "192.168.1.20");
            AddRecord("targetbank.com", "192.168.1.30");
            AddRecord("router.local", "192.168.1.1");
            AddRecord("techcorp.com", "192.168.1.40");
            AddRecord("hackmail.com", "192.168.1.50");
            AddRecord("cryptobank.com", "192.168.1.60");
            AddRecord("darknet.market", "192.168.1.70");
            AddRecord("hackerz.forum", "192.168.1.80");
            AddRecord("hackersearch.net", "192.168.1.90");
        }

        public void AddRecord(string hostname, string ip)
        {
            _records[hostname] = ip;
            _reverse[ip] = hostname;
        }

        public string? Resolve(string hostname) => _records.TryGetValue(hostname, out var ip) ? ip : null;

        public string? ReverseLookup(string ip) => _reverse.TryGetValue(ip, out var host) ? host : null;

        public IEnumerable<(string Hostname, string Ip)> GetAllRecords() => _records.Select(kvp => (kvp.Key, kvp.Value));
    }
}
