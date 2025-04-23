import { CommandModule, CommandArgs, CommandContext, ExecuteMigrator } from '../command-processor';
import { OS } from '../../core/os';

/**
 * nmap command - Network exploration and security scanning
 */
export class NmapCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'nmap';
  }
  
  public get description(): string {
    return 'Network exploration tool and security / port scanner';
  }
    public get usage(): string {
    return `Usage: nmap [options] target

Network exploration tool and security / port scanner.

Options:
  -p, --ports <range>   Specify port range to scan (default: 1-1000)
                        Examples: -p 22; -p 1-65535; -p 80,443,8080
  -s<C|T|U>             Scan type:
                         -sS: TCP SYN scan (default, stealthy)
                         -sT: TCP connect scan (more detectable)
                         -sU: UDP scan
  -T<0-5>               Set timing template (higher is faster)
                         -T0: Paranoid; -T5: Insane
  -O                    Enable OS detection
  -sV                   Probe open ports to determine service/version info
  -sC                   Perform script scanning (default scripts)
  -v                    Increase verbosity level

Examples:
  nmap localhost               # Scan localhost with default options
  nmap -p 80,443 example.com   # Scan specific ports of a domain
  nmap -sS -T4 192.168.1.1     # Fast SYN scan of an IP address
  nmap -sV -O 10.0.0.1         # Detect OS and service versions

Target can be hostname, IP address, network range, or CIDR notation.`;
  }
  public execute(args: CommandArgs, context: CommandContext): Promise<number>{
    return ExecuteMigrator.execute(this, args, context);
    }
  public async exec(args: CommandArgs): Promise<string> {
    // Check if a target is specified
    if (args.args.length === 0) {
      return 'nmap: No target specified';
    }
    
    const target = args.args[0];
    
    // Parse options
    const portScan = args.p || args.ports || '1-1000'; // -p <port ranges> - Scan specified ports
    const scanType = args.s || 'S'; // -s<type> - Scan type (S: SYN, T: TCP connect, U: UDP)
    const timing = args.T || 3; // -T<0-5> - Timing template (higher is faster)
    const osDetection = args.O || false; // -O - Enable OS detection
    const serviceInfo = args.sV || false; // -sV - Probe open ports to determine service/version info
    const scriptScan = args.sC || false; // -sC - Equivalent to --script=default
    const verbose = args.v || false; // -v - Increase verbosity level
    
    try {
      // Simulate network scanning delay based on target and options
      const delay = this.calculateScanDelay(target, timing, portScan);
      
      // Format port range for display
      const portRangeDisplay = this.formatPortRange(portScan);
      
      // Simulate scan results
      const scanResults = this.simulateScanResults(target, portScan, scanType, osDetection, serviceInfo, scriptScan);
      
      // Format output
      return this.formatScanOutput(target, scanResults, portRangeDisplay, scanType, osDetection, serviceInfo, scriptScan, verbose);
    } catch (error) {
      return `nmap: error: ${error}`;
    }
  }
  
  /**
   * Calculate simulated scan delay based on scan parameters
   */
  private calculateScanDelay(target: string, timing: number, portRange: string): number {
    // Base delay in milliseconds
    let delay = 1000;
    
    // Adjust for timing (T0-T5)
    delay = delay * (6 - timing) / 3;
    
    // Adjust for port range
    const ports = this.countPortsInRange(portRange);
    delay = delay * (1 + ports / 1000);
    
    // Adjust for target (localhost is faster)
    if (target === 'localhost' || target === '127.0.0.1') {
      delay = delay / 5;
    }
    
    return delay;
  }
  
  /**
   * Count how many ports are in the specified range
   */
  private countPortsInRange(portRange: string): number {
    let count = 0;
    
    // Handle comma-separated ranges
    const ranges = portRange.split(',');
    
    for (const range of ranges) {
      if (range.includes('-')) {
        // Handle range like "1-1000"
        const [start, end] = range.split('-').map(Number);
        if (!isNaN(start) && !isNaN(end)) {
          count += end - start + 1;
        }
      } else {
        // Single port
        if (!isNaN(Number(range))) {
          count += 1;
        }
      }
    }
    
    return count;
  }
  
  /**
   * Format port range for display
   */
  private formatPortRange(portRange: string): string {
    return portRange;
  }
    /**
   * Simulate scan results based on target and options
   */
  private simulateScanResults(
    target: string, 
    portRange: string, 
    scanType: string,
    osDetection: boolean,
    serviceInfo: boolean,
    scriptScan: boolean
  ): any {
    // Create simulated results based on target
    const results: any = {
      openPorts: [],
      filteredPorts: [],
      closedPorts: [],
      osInfo: null,
      serviceVersions: {}
    };
    
    // Resolve target to IP if it's a hostname
    let targetIP = target;
    
    // Use the DNS server to resolve hostname to IP
    if (!/^\d+\.\d+\.\d+\.\d+$/.test(target)) {
      const resolvedIP = this.os.getDNSServer().resolve(target);
      if (resolvedIP) {
        targetIP = resolvedIP;
      } else {
        // For unknown hosts, generate a random IP
        targetIP = `192.168.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}`;
      }
    }
    
    // Use NetworkInterface to get host information
    const networkInterface = this.os.getNetworkInterface();
    const hostInfo = networkInterface.scanHost(targetIP, portRange);
    
    if (hostInfo) {
      // Extract open and filtered ports
      results.openPorts = hostInfo.ports
        .filter(port => port.state === 'open')
        .map(port => port.port);
      
      results.filteredPorts = hostInfo.ports
        .filter(port => port.state === 'filtered')
        .map(port => port.port);
      
      // Extract OS info if available
      if (osDetection && hostInfo.osInfo) {
        results.osInfo = {
          name: hostInfo.osInfo.name,
          version: hostInfo.osInfo.version,
          accuracy: hostInfo.osInfo.accuracy
        };
      }
      
      // Add service information if requested
      if (serviceInfo) {
        for (const portInfo of hostInfo.ports) {
          if (portInfo.state === 'open' && portInfo.service) {
            results.serviceVersions[portInfo.port] = {
              name: portInfo.service.name,
              version: portInfo.service.version || '',
              info: portInfo.service.info || ''
            };
          }
        }
      }
    } else {
      // If host not found in our network interface, generate random results
      const numOpenPorts = Math.floor(Math.random() * 5) + 1;
      const commonPorts = [21, 22, 23, 25, 53, 80, 110, 443, 3306, 8080, 8443];
      
      // Randomly select from common ports for both open and filtered
      const selectedPorts = new Set<number>();
      while (selectedPorts.size < numOpenPorts) {
        const randomPort = commonPorts[Math.floor(Math.random() * commonPorts.length)];
        selectedPorts.add(randomPort);
      }
      
      // Some filtered ports
      const numFilteredPorts = Math.floor(Math.random() * 3) + 1;
      const filteredPorts = new Set<number>();
      while (filteredPorts.size < numFilteredPorts) {
        const randomPort = commonPorts[Math.floor(Math.random() * commonPorts.length)];
        if (!selectedPorts.has(randomPort)) {
          filteredPorts.add(randomPort);
        }
      }
      
      // Apply to results
      results.openPorts = Array.from(selectedPorts);
      results.filteredPorts = Array.from(filteredPorts);
      results.osInfo = osDetection && Math.random() > 0.3 ? 
        { name: 'Linux', accuracy: Math.floor(Math.random() * 30) + 60, version: 'Unknown' } : 
        null;
      
      // Add service information if requested
      if (serviceInfo) {
        // Define common services for ports
        const services: Record<number, any> = {
          21: { name: 'ftp', version: 'vsftpd 3.0.3', info: 'FTP server' },
          22: { name: 'ssh', version: 'OpenSSH 8.2p1', info: 'SSH protocol 2.0' },
          23: { name: 'telnet', version: '', info: 'Telnet server' },
          25: { name: 'smtp', version: 'Postfix', info: 'SMTP server' },
          53: { name: 'domain', version: 'BIND 9.16.1', info: 'DNS server' },
          80: { name: 'http', version: 'Apache httpd 2.4.41', info: 'HTTP server' },
          110: { name: 'pop3', version: 'Dovecot', info: 'POP3 server' },
          443: { name: 'https', version: 'Apache httpd 2.4.41', info: 'HTTP server' },
          3306: { name: 'mysql', version: 'MySQL 5.7.30', info: 'MySQL database server' },
          8080: { name: 'http-proxy', version: 'nginx 1.18.0', info: 'Proxy server' },
          8443: { name: 'https-alt', version: 'nginx 1.18.0', info: 'Proxy server' }
        };
        
        // Add services for open ports
        for (const port of results.openPorts) {
          if (services[port]) {
            results.serviceVersions[port] = services[port];
          } else {
            results.serviceVersions[port] = { name: 'unknown', version: '', info: '' };
          }
        }
      }
    }
    
    return results;
  }
  
  /**
   * Format scan output for display
   */
  private formatScanOutput(
    target: string,
    results: any,
    portRange: string,
    scanType: string,
    osDetection: boolean,
    serviceInfo: boolean,
    scriptScan: boolean,
    verbose: boolean
  ): string {
    const output: string[] = [];
    
    // Header
    output.push(`Starting Nmap ( https://nmap.org )`, '');
    if (verbose) {
      output.push(`Initiating SYN Stealth Scan at ${new Date().toLocaleTimeString()}`);
      output.push(`Scanning ${target} [${portRange} ports]`);
      output.push('Discovered open port 80/tcp on ' + target);
      output.push('Discovered open port 443/tcp on ' + target);
      output.push('Completed SYN Stealth Scan at ' + new Date().toLocaleTimeString());
      output.push('');
    }
    
    // Basic scan info
    const scanTypeDesc = scanType === 'S' ? 'SYN Stealth Scan' : 
                         scanType === 'T' ? 'TCP Connect Scan' : 
                         scanType === 'U' ? 'UDP Scan' : 'Custom Scan';
    
    output.push(`Nmap scan report for ${target}`);
      // Attempt to resolve hostname if IP address
    if (/^\d+\.\d+\.\d+\.\d+$/.test(target)) {
      // For IP addresses, try reverse DNS lookup
      const hostname = this.os.getDNSServer().reverseLookup(target);
      if (hostname) {
        output.push(`Host is up (0.00042s latency).`);
        output.push(`rDNS record for ${target}: ${hostname}`);
      } else {
        output.push(`Host is up (0.015s latency).`);
      }
    } else {
      // For hostnames, show resolved IP
      const resolvedIP = this.os.getDNSServer().resolve(target) || 
        `192.168.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}`;
      output.push(`Host is up (0.015s latency).`);
      output.push(`rDNS record for ${resolvedIP}: ${target}`);
    }
    
    // Port scan results
    if (results.openPorts.length > 0 || results.filteredPorts.length > 0) {
      output.push('');
      output.push('PORT     STATE    SERVICE' + (serviceInfo ? '         VERSION' : ''));
      
      // Sort ports
      const allPorts = [...results.openPorts, ...results.filteredPorts].sort((a, b) => a - b);
      
      for (const port of allPorts) {
        let line = `${port.toString().padEnd(8)} `;
        
        if (results.openPorts.includes(port)) {
          line += 'open     ';
        } else {
          line += 'filtered ';
        }
        
        // Add service info if available
        if (serviceInfo && results.serviceVersions[port]) {
          const service = results.serviceVersions[port];
          line += service.name.padEnd(15) + service.version;
        } else {
          const commonServices: Record<number, string> = {
            21: 'ftp', 22: 'ssh', 23: 'telnet', 25: 'smtp', 53: 'domain',
            80: 'http', 110: 'pop3', 443: 'https', 3306: 'mysql', 
            8080: 'http-proxy', 8443: 'https-alt'
          };
          
          line += commonServices[port] || 'unknown';
        }
        
        output.push(line);
      }
    } else {
      output.push('All 1000 scanned ports on ' + target + ' are closed');
    }
    
    // OS detection results
    if (osDetection && results.osInfo) {
      output.push('');
      output.push('OS detection performed. Please report any incorrect results at https://nmap.org/submit/ .');
      output.push(`OS details: ${results.osInfo.name} ${results.osInfo.version} (${results.osInfo.accuracy}% accuracy)`);
    }
    
    // Script scan results
    if (scriptScan && results.openPorts.length > 0) {
      output.push('');
      output.push('SCRIPT SCAN RESULTS:');
      
      // Simulate script scan results for specific ports
      if (results.openPorts.includes(80) || results.openPorts.includes(443)) {
        output.push('| http-server-header: Apache/2.4.41 (Ubuntu)');
        output.push('| http-title: Site title: ' + (target === 'targetbank.com' ? 'Target Bank - Secure Online Banking' : 'Welcome'));
      }
      
      if (results.openPorts.includes(22)) {
        output.push('| ssh-hostkey: SSH host key fingerprint detected');
        output.push('|   2048 8e:5d:3b:1c:7d:12:f3:24:56:eb:d8:41:c1:9e:5c:c6 (RSA)');
      }
      
      if (results.openPorts.includes(3306)) {
        output.push('| mysql-info: Protocol: 10');
        output.push('|   Version: 5.7.30');
        output.push('|   Thread ID: 10');
        output.push('|   Status: Autocommit');
      }
    }
    
    // Summary
    output.push('');
    output.push(`Nmap done: 1 IP address (1 host up) scanned in 2.45 seconds`);
    
    return output.join('\n');
  }
}
