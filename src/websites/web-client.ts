/**
 * Web client interface for making HTTP requests
 */
export interface WebClientRequest {
  url: string;
  method: string;
  headers?: Record<string, string>;
  body?: any;
}

export interface WebClientResponse {
  body: string;
  statusCode: number;
  statusText: string;
  headers: Record<string, string>;
}

/**
 * Web client for making HTTP requests to simulated websites
 */
export class WebClient {
  private websites: Map<string, any>;
  private dnsServer: any; // Reference to DNS server

  constructor(websites: Map<string, any>, dnsServer?: any) {
    this.websites = websites;
    this.dnsServer = dnsServer;
  }

  /**
   * Make a web request to a simulated website
   */
  public async request(requestOptions: WebClientRequest): Promise<WebClientResponse> {
    try {
      const url = new URL(requestOptions.url);
      const domain = url.hostname;
      const path = url.pathname || '/';
      const method = requestOptions.method.toUpperCase();
      
      // Check DNS resolution if DNS server is available
      if (this.dnsServer && !/^\d+\.\d+\.\d+\.\d+$/.test(domain)) {
        const resolvedIP = this.dnsServer.resolve(domain);
        if (!resolvedIP) {
          throw { code: 'ENOTFOUND', message: `Could not resolve host: ${domain}` };
        }
      }
      
      // Check if the domain exists
      const website = this.websites.get(domain);
      if (!website) {
        throw { code: 'ENOTFOUND', message: `Could not resolve host: ${domain}` };
      }

      // If this is a controller-based website
      if (website.controller) {
        // Create a web request object for the controller
        const webRequest = {
          method,
          path,
          query: Object.fromEntries(url.searchParams.entries()),
          headers: requestOptions.headers || {},
          body: {
            text: typeof requestOptions.body === 'string' ? requestOptions.body : undefined,
            json: typeof requestOptions.body === 'object' ? requestOptions.body : undefined
          },
          cookies: {}
        };

        // Process the request through the controller
        const response = await website.controller.processRequest(webRequest);
        
        return {
          body: response.content || '',
          statusCode: response.code,
          statusText: this.getStatusText(response.code),
          headers: response.headers || {}
        };
      }
      
      // For simple content websites
      let content: string | (() => string) | undefined = website.content[path];
      let contentStr = "";
      // If content is a function, execute it to get the dynamic content
      if (typeof content === 'function') {
        contentStr = content();
      } 
      // If no content exists but there's a dynamic handler, use it
      else if (!content && website.dynamicHandler) {
        contentStr = await website.dynamicHandler(path);
      }
      
      if (!content) {
        return {
          body: `<html><body><h1>404 Not Found</h1><p>The requested URL ${path} was not found on this server.</p></body></html>`,
          statusCode: 404,
          statusText: 'Not Found',
          headers: { 'Content-Type': 'text/html' }
        };
      }
      
      return {
        body: contentStr,
        statusCode: 200,
        statusText: 'OK',
        headers: { 'Content-Type': 'text/html' }
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get HTTP status text from status code
   */
  private getStatusText(code: number): string {
    const statusTexts: Record<number, string> = {
      200: 'OK',
      201: 'Created',
      301: 'Moved Permanently',
      302: 'Found',
      400: 'Bad Request',
      401: 'Unauthorized',
      403: 'Forbidden',
      404: 'Not Found',
      500: 'Internal Server Error'
    };
    
    return statusTexts[code] || 'Unknown';
  }
}
