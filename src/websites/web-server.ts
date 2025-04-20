/**
 * Web request interface to simulate HTTP requests
 */
export interface WebRequest {
  method: string;
  path: string;
  query: Record<string, string>;
  headers: Record<string, string>;
  body: {
    text?: string;
    json?: any;
    form?: Record<string, string>;
  };
  cookies: Record<string, string>;
}

/**
 * Web response interface to simulate HTTP responses
 */
export interface WebResponse {
  code: number;
  headers: Record<string, string>;
  content?: string;
  redirectUrl?: string;
}

/**
 * Web content response for HTML/Text content
 */
export class WebContentResponse implements WebResponse {
  public code: number;
  public headers: Record<string, string>;
  public content: string;
  
  constructor(options: { code: number, content: string, headers?: Record<string, string> }) {
    this.code = options.code;
    this.content = options.content;
    this.headers = options.headers || { 'Content-Type': 'text/html' };
  }
}

/**
 * Web JSON response for API data
 */
export class WebJsonResponse implements WebResponse {
  public code: number;
  public headers: Record<string, string>;
  public content: string;
  
  constructor(options: { code: number, data: any, headers?: Record<string, string> }) {
    this.code = options.code;
    this.content = JSON.stringify(options.data);
    this.headers = options.headers || { 'Content-Type': 'application/json' };
  }
}

/**
 * Web redirect response
 */
export class WebRedirectResponse implements WebResponse {
  public code: number;
  public headers: Record<string, string>;
  public redirectUrl: string;
  
  constructor(options: { url: string, permanent?: boolean }) {
    this.code = options.permanent ? 301 : 302;
    this.redirectUrl = options.url;
    this.headers = { 'Location': options.url };
  }
}

/**
 * Web error response
 */
export class WebErrorResponse implements WebResponse {
  public code: number;
  public headers: Record<string, string>;
  public content: string;
  
  constructor(options: { code: number, reason: string, headers?: Record<string, string> }) {
    this.code = options.code;
    this.headers = options.headers || { 'Content-Type': 'text/html' };
    
    // Generate simple error page
    this.content = `
      <html>
        <head>
          <title>Error ${options.code}</title>
          <style>
            body {
              font-family: Arial, sans-serif;
              max-width: 800px;
              margin: 0 auto;
              padding: 20px;
            }
            .error-code {
              color: #cc0000;
              font-size: 2em;
              margin-bottom: 10px;
            }
            .error-reason {
              font-weight: bold;
              margin-bottom: 20px;
            }
          </style>
        </head>
        <body>
          <h1>Error</h1>
          <div class="error-code">${options.code}</div>
          <div class="error-reason">${options.reason}</div>
        </body>
      </html>
    `;
  }
}

/**
 * Route handler function type
 */
export type RouteHandler = (request: WebRequest) => WebResponse | Promise<WebResponse>;

/**
 * Routes register for controller to register routes
 */
export class RoutesRegister {
  private routes: Map<string, Map<string, RouteHandler>> = new Map();
  
  constructor() {
    // Initialize with empty maps for common HTTP methods
    this.routes.set('GET', new Map());
    this.routes.set('POST', new Map());
    this.routes.set('PUT', new Map());
    this.routes.set('DELETE', new Map());
  }
  
  /**
   * Register a GET route
   */
  public Get(path: string, handler: RouteHandler): void {
    const getRoutes = this.routes.get('GET')!;
    getRoutes.set(path, handler);
  }
  
  /**
   * Register a POST route
   */
  public Post(path: string, handler: RouteHandler): void {
    const postRoutes = this.routes.get('POST')!;
    postRoutes.set(path, handler);
  }
  
  /**
   * Register a PUT route
   */
  public Put(path: string, handler: RouteHandler): void {
    const putRoutes = this.routes.get('PUT')!;
    putRoutes.set(path, handler);
  }
  
  /**
   * Register a DELETE route
   */
  public Delete(path: string, handler: RouteHandler): void {
    const deleteRoutes = this.routes.get('DELETE')!;
    deleteRoutes.set(path, handler);
  }
  
  /**
   * Find a handler for a given method and path
   */
  public findHandler(method: string, path: string): RouteHandler | null {
    const methodRoutes = this.routes.get(method.toUpperCase());
    if (!methodRoutes) return null;
    
    // Check for exact path match
    if (methodRoutes.has(path)) {
      return methodRoutes.get(path)!;
    }
    
    // Try to find a matching parameterized route (e.g., /users/:id)
    for (const [routePath, handler] of methodRoutes.entries()) {
      if (this.matchParameterizedRoute(routePath, path)) {
        return handler;
      }
    }
    
    return null;
  }
  
  /**
   * Match a parameterized route (e.g., /users/:id)
   */
  private matchParameterizedRoute(routePath: string, requestPath: string): boolean {
    if (!routePath.includes(':')) return false;
    
    const routeParts = routePath.split('/').filter(Boolean);
    const requestParts = requestPath.split('/').filter(Boolean);
    
    if (routeParts.length !== requestParts.length) return false;
    
    for (let i = 0; i < routeParts.length; i++) {
      const routePart = routeParts[i];
      
      // If this part is a parameter (starts with :), it matches anything
      if (routePart.startsWith(':')) {
        continue;
      }
      
      // Otherwise, it must match exactly
      if (routePart !== requestParts[i]) {
        return false;
      }
    }
    
    return true;
  }
  
  /**
   * Get all registered routes
   */
  public getAllRoutes(): Map<string, Map<string, RouteHandler>> {
    return this.routes;
  }
}

/**
 * Base controller class for web controllers
 */
export abstract class BaseController {
  public abstract get Host(): string;
  private routes: RoutesRegister;
  
  constructor() {
    this.routes = new RoutesRegister();
    this.registerRoutes(this.routes);
  }
  
  /**
   * Register routes for this controller
   */
  protected abstract registerRoutes(routes: RoutesRegister): void;
  
  /**
   * Helper to create a redirect to action
   */
  protected RedirectToAction(action: string, queryParams: Record<string, string> = {}): WebResponse {
    let url = action;
    
    // If action doesn't start with /, add it
    if (!url.startsWith('/')) {
      url = '/' + url;
    }
    
    // Add host prefix to the URL
    url = `https://${this.Host}${url}`;
    
    // Add query parameters if any
    if (Object.keys(queryParams).length > 0) {
      const searchParams = new URLSearchParams();
      for (const [key, value] of Object.entries(queryParams)) {
        searchParams.append(key, value);
      }
      url += '?' + searchParams.toString();
    }
    
    return new WebRedirectResponse({ url });
  }
  
  /**
   * Process a web request
   */
  public async processRequest(request: WebRequest): Promise<WebResponse> {
    const handler = this.routes.findHandler(request.method, request.path);
    
    if (!handler) {
      return new WebErrorResponse({
        code: 404,
        reason: `No route found for ${request.method} ${request.path}`
      });
    }
    
    try {
      return await handler(request);
    } catch (error) {
      console.error('Error processing request:', error);
      return new WebErrorResponse({
        code: 500,
        reason: 'Internal server error'
      });
    }
  }
  
  /**
   * Get all registered routes for this controller
   */
  public getRoutes(): Map<string, Map<string, RouteHandler>> {
    return this.routes.getAllRoutes();
  }
}
