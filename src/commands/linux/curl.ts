import { CommandModule, CommandArgs,CommandContext, ExecuteMigrator } from '../command-processor';
import { OS } from '../../core/os';

/**
 * curl command - Transfer data from or to a server
 */
export class CurlCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  public execute(args: CommandArgs, context: CommandContext): Promise<number>{
    return ExecuteMigrator.execute(this, args, context);
    }
  public get name(): string {
    return 'curl';
  }
  
  public get description(): string {
    return 'Transfer data from or to a server';
  }
    public get usage(): string {
    return `Usage: curl [options] [URL...]

Options:
  -o, --output <file>      Write output to <file> instead of stdout
  -i, --include            Include protocol response headers in the output
  -s, --silent             Silent mode, don't output progress or error messages
  -X, --request <method>   Specify request method to use (GET, POST, PUT, etc.)
  -H, --header <header>    Pass custom header to server (can be used multiple times)
  -d, --data <data>        HTTP POST data (sets method to POST if not specified)
  --help                   Display this help message

Examples:
  curl https://example.com                     # Simple GET request
  curl -o output.html https://example.com      # Save response to file
  curl -i https://example.com                  # Include response headers
  curl -X POST -d "name=value" https://api.com # POST with data
  curl -H "User-Agent: MyApp" https://api.com  # Set custom header`;
  }
  
  public async exec(args: CommandArgs): Promise<string> {
    // Check if a URL is specified
    if (args.args.length === 0) {
      return 'curl: try \'curl --help\' for more information';
    }
    
    const url = args.args[0];
    
    // Parse options
    const outputFile = args.o || args.output; // -o, --output <file> Write to file instead of stdout
    const includeHeaders = args.i || args.include || false; // -i, --include Include protocol response headers in the output
    const silent = args.s || args.silent || false; // -s, --silent Silent mode
    let method = args.X || args.request || 'GET'; // -X, --request <method> Specify request method to use
    const headers: Record<string, string> = {}; // Store custom headers
    
    // Process custom headers from -H or --header options
    if (args.H || args.header) {
      const headerList = Array.isArray(args.H || args.header) 
        ? (args.H || args.header) 
        : [args.H || args.header];
      
      headerList.forEach((headerStr: string) => {
        const match = headerStr.match(/^(.*?):\s*(.*)$/);
        if (match) {
          const [, name, value] = match;
          headers[name] = value;
        }
      });
    }
    
    // Process data for POST requests
    const data = args.d || args.data;
    if (data && (method === 'GET' || method === 'HEAD')) {
      // If data is provided but method is GET or HEAD, switch to POST
      if (!args.X && !args.request) {
        method = 'POST';
      }
    }
    
    try {
      // Check if the URL can be resolved using our DNS server
      const urlObj = new URL(url);
      const hostname = urlObj.hostname;
      
      // Only attempt DNS resolution for non-IP hostnames
      if (!/^\d+\.\d+\.\d+\.\d+$/.test(hostname)) {
        const resolvedIP = this.os.getDNSServer().resolve(hostname);
        if (!resolvedIP) {
          return `curl: (6) Could not resolve host: ${hostname}`;
        }
      }
      
      // Make a request to the web server
      const response = await this.os.getWebServer().request({
        url,
        method,
        headers,
        body: data
      });
      
      // Format the output
      const output: string[] = [];
      
      // Add headers if requested
      if (includeHeaders) {
        output.push(`HTTP/1.1 ${response.statusCode} ${response.statusText}`);
        
        // Add response headers
        Object.entries(response.headers).forEach(([name, value]) => {
          output.push(`${name}: ${value}`);
        });
        
        output.push(''); // Empty line between headers and body
      }
      
      // Add response body
      output.push(response.body);
      
      // If output file is specified, write to a file
      if (outputFile) {
        try {
          await this.os.getFileSystem().writeFile(outputFile, output.join('\n'));
          return silent ? '' : `Total received: ${response.body.length} bytes`;
        } catch (error) {
          return `curl: (23) Failed writing body: ${error}`;
        }
      }
      
      // Return the formatted output to stdout
      return output.join('\n');
    } catch (error: any) {
      if (error.code === 'ENOTFOUND') {
        return `curl: (6) Could not resolve host: ${url}`;
      } else if (error.code === 'ECONNREFUSED') {
        return `curl: (7) Failed to connect to ${url}`;
      } else if (error.code === 'ETIMEDOUT') {
        return `curl: (28) Operation timed out`;
      } else {
        return `curl: (22) ${error.message || 'Unknown error'}`;
      }
    }
  }
}
