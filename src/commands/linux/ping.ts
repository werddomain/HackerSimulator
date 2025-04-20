import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * ping command - Send ICMP ECHO_REQUEST to network hosts
 */
export class PingCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'ping';
  }
  
  public get description(): string {
    return 'Send ICMP ECHO_REQUEST to network hosts';
  }
  
  public get usage(): string {
    return 'ping [options] destination';
  }
  
  public async exec(args: CommandArgs): Promise<string> {
    // Check if a destination is specified
    if (args.args.length === 0) {
      return 'ping: usage error: Destination address required';
    }
    
    const destination = args.args[0];
    
    // Parse options
    const count = args.c || 4; // -c count, number of packets to send (default 4)
    const interval = args.i || 1; // -i interval in seconds (default 1)
    
    // Try to resolve the destination using DNS if it's not an IP address
    let targetIP = destination;
    let resolvedHostname = destination;
    
    if (!/^\d+\.\d+\.\d+\.\d+$/.test(destination)) {
      // It's a hostname, try to resolve it
      const resolvedIP = this.os.getDNSServer().resolve(destination);
      if (resolvedIP) {
        targetIP = resolvedIP;
      } else {
        // Cannot resolve hostname
        return `ping: cannot resolve ${destination}: Unknown host`;
      }
    } else {
      // It's an IP address, try reverse lookup
      const hostname = this.os.getDNSServer().reverseLookup(destination);
      if (hostname) {
        resolvedHostname = hostname;
      }
    }
    
    // Get host information from network interface
    const networkInterface = this.os.getNetworkInterface();
    const hostInfo = networkInterface.getHostByIp(targetIP);
    
    if (!hostInfo || !hostInfo.isUp) {
      // Host not found or not up
      return `PING ${destination} (${targetIP}): 56 data bytes
Request timeout for icmp_seq 0
Request timeout for icmp_seq 1
Request timeout for icmp_seq 2
Request timeout for icmp_seq 3

--- ${destination} ping statistics ---
4 packets transmitted, 0 packets received, 100.0% packet loss`;
    }
    
    // Host is available, simulate successful ping
    return this.simulateSuccessfulPing(destination, hostInfo.latency, count, targetIP);
  }
  
  /**
   * Simulate network delay based on destination
   */
  private simulateNetworkDelay(destination: string): number {
    if (destination.match(/^localhost$/i) || destination.match(/^127\.0\.0\.1$/)) {
      return 0.1; // Local is very fast
    }
    
    if (destination.match(/^192\.168\.\d+\.\d+$/)) {
      return 1 + Math.random() * 5; // 1-6ms for LAN
    }
    
    // Internet domains get higher latency
    return 20 + Math.random() * 80; // 20-100ms for internet
  }
  
  /**
   * Get a set of known hosts in the simulated environment
   */
  private getKnownHosts(): Set<string> {
    // This could be expanded to include all the websites in your simulated internet
    return new Set([
      'example.com', 
      'mybank.net', 
      'targetbank.com', 
      'techcorp.com',
      'google.com',
      'darkweb.onion'
    ]);
  }
  
  /**
   * Simulate a successful ping response
   */
  private simulateSuccessfulPing(destination: string, baseDelay: number, count: number, ipAddress: string): string {
    const results: string[] = [];
    let totalDelay = 0;
    let minDelay = Number.MAX_VALUE;
    let maxDelay = 0;
    
    results.push(`PING ${destination} (${ipAddress}): 56 data bytes`);
    
    for (let i = 0; i < count; i++) {
      // Add some random variation to the delay
      const jitter = Math.random() * baseDelay * 0.2;
      const delay = baseDelay + jitter;
      
      // Track min, max, and total for statistics
      minDelay = Math.min(minDelay, delay);
      maxDelay = Math.max(maxDelay, delay);
      totalDelay += delay;
      
      results.push(`64 bytes from ${ipAddress}: icmp_seq=${i} ttl=64 time=${delay.toFixed(1)} ms`);
    }
    
    // Calculate average
    const avgDelay = totalDelay / count;
    
    // Calculate standard deviation
    let sumSquaredDiff = 0;
    for (let i = 0; i < count; i++) {
      const jitter = Math.random() * baseDelay * 0.2;
      const delay = baseDelay + jitter;
      sumSquaredDiff += Math.pow(delay - avgDelay, 2);
    }
    const stdDev = Math.sqrt(sumSquaredDiff / count);
    
    // Add statistics
    results.push('');
    results.push(`--- ${destination} ping statistics ---`);
    results.push(`${count} packets transmitted, ${count} packets received, 0.0% packet loss`);
    results.push(`round-trip min/avg/max/stddev = ${minDelay.toFixed(1)}/${avgDelay.toFixed(1)}/${maxDelay.toFixed(1)}/${stdDev.toFixed(1)} ms`);
    
    return results.join('\n');
  }
  
  /**
   * Execute command with context for pipe support
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Use the existing exec method to maintain the same behavior
      const result = await this.exec(args);
      
      // Write the result to stdout for pipe support
      if (result) {
        context.stdout.writeLine(result);
      }
      
      // Return success exit code
      return 0;
    } catch (error) {
      // Write error to stderr and return error exit code
      context.stderr.writeLine(`ping: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
}
