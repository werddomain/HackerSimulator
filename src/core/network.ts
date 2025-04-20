/**
 * Network infrastructure classes for the HackerGame
 * Provides DNS resolution, IP assignment, and network scanning
 */

/**
 * Interface for port information
 */
export interface PortInfo {
  port: number;
  state: 'open' | 'closed' | 'filtered';
  service?: {
    name: string;
    version?: string;
    info?: string;
  };
}

/**
 * Interface for OS information
 */
export interface OsInfo {
  name: string;
  version: string;
  accuracy: number; // Accuracy percentage of OS detection
}

/**
 * Interface for host information
 */
export interface HostInfo {
  ip: string;
  hostname: string; // Can be null for IP-only hosts
  osInfo?: OsInfo;
  ports: PortInfo[];
  isUp: boolean;
  latency: number; // in ms
  mac?: string; // MAC address for LAN hosts
}

/**
 * Network interface for managing network resources
 */
export class NetworkInterface {
  private hosts: Map<string, HostInfo> = new Map();
  
  constructor() {
    this.initializeDefaultHosts();
  }
  
  /**
   * Initialize default hosts in the network
   */
  private initializeDefaultHosts(): void {
    // Localhost
    this.registerHost({
      ip: '127.0.0.1',
      hostname: 'localhost',
      osInfo: { name: 'Linux', version: '5.4.0', accuracy: 99 },
      ports: [
        { port: 22, state: 'open', service: { name: 'ssh', version: 'OpenSSH 8.2p1', info: 'SSH protocol 2.0' } },
        { port: 80, state: 'open', service: { name: 'http', version: 'Apache httpd 2.4.41', info: 'HTTP server' } },
        { port: 3306, state: 'open', service: { name: 'mysql', version: 'MySQL 5.7.30', info: 'MySQL database server' } },
        { port: 8080, state: 'open', service: { name: 'http-proxy', version: 'nginx 1.18.0', info: 'Proxy server' } },
        { port: 25, state: 'filtered', service: { name: 'smtp' } },
        { port: 443, state: 'filtered', service: { name: 'https' } }
      ],
      isUp: true,
      latency: 0.1
    });
    
    // Example.com
    this.registerHost({
      ip: '192.168.1.10',
      hostname: 'example.com',
      osInfo: { name: 'Linux', version: 'Ubuntu', accuracy: 85 },
      ports: [
        { port: 80, state: 'open', service: { name: 'http', version: 'Apache httpd 2.4.41', info: 'HTTP server' } },
        { port: 443, state: 'open', service: { name: 'https', version: 'Apache httpd 2.4.41', info: 'HTTP server' } },
        { port: 22, state: 'filtered', service: { name: 'ssh' } },
        { port: 25, state: 'filtered', service: { name: 'smtp' } },
        { port: 110, state: 'filtered', service: { name: 'pop3' } }
      ],
      isUp: true,
      latency: 15
    });
    
    // MyBank.net
    this.registerHost({
      ip: '192.168.1.20',
      hostname: 'mybank.net',
      osInfo: { name: 'Unix', version: 'CentOS', accuracy: 75 },
      ports: [
        { port: 80, state: 'open', service: { name: 'http', version: 'Apache httpd 2.4.41', info: 'HTTP server' } },
        { port: 443, state: 'open', service: { name: 'https', version: 'Apache httpd 2.4.41', info: 'HTTP server' } },
        { port: 8443, state: 'open', service: { name: 'https-alt', version: 'nginx 1.18.0', info: 'Proxy server' } },
        { port: 21, state: 'filtered', service: { name: 'ftp' } },
        { port: 22, state: 'filtered', service: { name: 'ssh' } },
        { port: 23, state: 'filtered', service: { name: 'telnet' } },
        { port: 3306, state: 'filtered', service: { name: 'mysql' } }
      ],
      isUp: true,
      latency: 25
    });
    
    // TargetBank.com
    this.registerHost({
      ip: '192.168.1.30',
      hostname: 'targetbank.com',
      osInfo: { name: 'Linux', version: 'Debian', accuracy: 90 },
      ports: [
        { port: 80, state: 'open', service: { name: 'http', version: 'Apache httpd 2.4.41', info: 'HTTP server' } },
        { port: 443, state: 'open', service: { name: 'https', version: 'Apache httpd 2.4.41', info: 'HTTP server' } },
        { port: 8080, state: 'open', service: { name: 'http-proxy', version: 'nginx 1.18.0', info: 'Proxy server' } },
        { port: 21, state: 'open', service: { name: 'ftp', version: 'vsftpd 3.0.3', info: 'FTP server' } },
        { port: 22, state: 'open', service: { name: 'ssh', version: 'OpenSSH 8.2p1', info: 'SSH protocol 2.0' } },
        { port: 3306, state: 'open', service: { name: 'mysql', version: 'MySQL 5.7.30', info: 'MySQL database server' } },
        { port: 23, state: 'filtered', service: { name: 'telnet' } },
        { port: 25, state: 'filtered', service: { name: 'smtp' } },
        { port: 110, state: 'filtered', service: { name: 'pop3' } },
        { port: 8443, state: 'filtered', service: { name: 'https-alt' } }
      ],
      isUp: true,
      latency: 30
    });
    
    // Router
    this.registerHost({
      ip: '192.168.1.1',
      hostname: 'router.local',
      osInfo: { name: 'Router', version: 'DD-WRT', accuracy: 95 },
      ports: [
        { port: 53, state: 'open', service: { name: 'domain', version: 'BIND 9.16.1', info: 'DNS server' } },
        { port: 80, state: 'open', service: { name: 'http', version: 'lighttpd 1.4.55', info: 'HTTP server' } },
        { port: 443, state: 'open', service: { name: 'https', version: 'lighttpd 1.4.55', info: 'HTTP server' } },
        { port: 22, state: 'filtered', service: { name: 'ssh' } },
        { port: 23, state: 'filtered', service: { name: 'telnet' } },
        { port: 25, state: 'filtered', service: { name: 'smtp' } }
      ],
      isUp: true,
      latency: 2
    });
    
    // Add more hosts as needed
  }
  
  /**
   * Register a host in the network
   */
  public registerHost(host: HostInfo): void {
    this.hosts.set(host.ip, host);
  }
  
  /**
   * Get a host by IP address
   */
  public getHostByIp(ip: string): HostInfo | undefined {
    return this.hosts.get(ip);
  }
  
  /**
   * Update host information
   */
  public updateHost(ip: string, updates: Partial<HostInfo>): boolean {
    const host = this.hosts.get(ip);
    if (!host) return false;
    
    Object.assign(host, updates);
    return true;
  }
  
  /**
   * Get all hosts
   */
  public getAllHosts(): HostInfo[] {
    return Array.from(this.hosts.values());
  }
  
  /**
   * Simulate a port scan on a host
   */
  public scanHost(ip: string, portRange: string = '1-1000'): HostInfo | null {
    const host = this.hosts.get(ip);
    if (!host || !host.isUp) return null;
    
    // Parse port range
    const parsedPorts = this.parsePortRange(portRange);
    
    // Filter host ports to only include those in the specified range
    const filteredPorts = host.ports.filter(port => parsedPorts.includes(port.port));
    
    // Create a copy of the host with only the requested ports
    const scanResult: HostInfo = {
      ...host,
      ports: filteredPorts
    };
    
    return scanResult;
  }
  
  /**
   * Parse a port range string into an array of port numbers
   */
  private parsePortRange(portRange: string): number[] {
    const ports: number[] = [];
    
    // Handle comma-separated ranges
    const ranges = portRange.split(',');
    
    for (const range of ranges) {
      if (range.includes('-')) {
        // Handle range like "1-1000"
        const [start, end] = range.split('-').map(Number);
        if (!isNaN(start) && !isNaN(end)) {
          for (let i = start; i <= end; i++) {
            ports.push(i);
          }
        }
      } else {
        // Single port
        const port = Number(range);
        if (!isNaN(port)) {
          ports.push(port);
        }
      }
    }
    
    return ports;
  }
}

/**
 * DNS server for resolving hostnames to IP addresses
 */
export class DNSServer {
  private records: Map<string, string> = new Map(); // hostname -> IP
  private reverseRecords: Map<string, string> = new Map(); // IP -> hostname
  
  constructor() {
    this.initializeDefaultRecords();
  }
  
  /**
   * Initialize default DNS records
   */
  private initializeDefaultRecords(): void {
    this.addRecord('localhost', '127.0.0.1');
    this.addRecord('example.com', '192.168.1.10');
    this.addRecord('mybank.net', '192.168.1.20');
    this.addRecord('targetbank.com', '192.168.1.30');
    this.addRecord('router.local', '192.168.1.1');
    this.addRecord('techcorp.com', '192.168.1.40');
    this.addRecord('hackmail.com', '192.168.1.50');
    this.addRecord('cryptobank.com', '192.168.1.60');
    this.addRecord('darknet.market', '192.168.1.70');
    this.addRecord('hackerz.forum', '192.168.1.80');
    this.addRecord('hackersearch.net', '192.168.1.90');
  }
  
  /**
   * Add a DNS record
   */
  public addRecord(hostname: string, ip: string): void {
    this.records.set(hostname, ip);
    this.reverseRecords.set(ip, hostname);
  }
  
  /**
   * Resolve a hostname to an IP address
   */
  public resolve(hostname: string): string | null {
    return this.records.get(hostname) || null;
  }
  
  /**
   * Reverse lookup - get hostname from IP
   */
  public reverseLookup(ip: string): string | null {
    return this.reverseRecords.get(ip) || null;
  }
  
  /**
   * Get all DNS records
   */
  public getAllRecords(): { hostname: string, ip: string }[] {
    return Array.from(this.records.entries()).map(([hostname, ip]) => ({ hostname, ip }));
  }
}
