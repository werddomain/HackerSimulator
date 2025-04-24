import { FileSystem } from './filesystem';
import { ProcessManager } from './process';
import { WindowManager } from './window';
import { SystemMonitor } from './monitor';
import { AppManager } from './app-manager';
import { CommandProcessor } from '../commands/command-processor';
import { CommandRegistry } from '../commands/command-registry';
import { WebClient } from '../websites/web-client';
import { NetworkInterface, DNSServer } from './network';
import { DefaultWebsites, WebsiteEntry } from '../websites/default-websites';
import { Desktop } from './desktop';
import { StartMenuController } from './start-menu';
import { UserSettings } from './UserSettings';
import { ComputerSettings } from './ComputerSettings';
import { createIcons, icons } from 'lucide';
import { ThemeSystem } from './theme-system';
import { InitComponents } from './components/init';
import { PlatformDetector, platformDetector } from './platform-detector';
import { WindowManagerFactory } from './window-manager-factory';
import { IWindowManager } from './window-manager-interface';
/**
 * Main OS class that manages the entire operating system simulation
 */
export class OS {
  private fileSystem: FileSystem;
  private processManager: ProcessManager;
  private windowManager: WindowManager;
  private windowManagerFactory: WindowManagerFactory = WindowManagerFactory.getInstance();
  private systemMonitor: SystemMonitor;
  private appManager: AppManager;
  private commandProcessor: CommandProcessor;
  private commandRegistry: CommandRegistry; private clockInterval: number | null = null;
  private websites: Map<string, WebsiteEntry> = new Map();
  private webClient: WebClient;
  private networkInterface: NetworkInterface;
  private dnsServer: DNSServer;
  private defaultWebsites: DefaultWebsites;
  private desktop: Desktop;
  private startMenuController: StartMenuController;
  private userSettings: UserSettings;
  private computerSettings: ComputerSettings;  private isReady: boolean = false;  private readyCallbacks: Array<() => void> = [];
  themeSystem: ThemeSystem;
  platformDetector: PlatformDetector = platformDetector; // Initialize with the singleton instance  constructor() {
    this.initIcons(); // Initialize icons using lucide
    this.fileSystem = new FileSystem(this);
    this.processManager = new ProcessManager();
    this.windowManager = this.windowManagerFactory.getDesktopWindowManager(); // Use factory to get desktop manager
    this.systemMonitor = new SystemMonitor(this.processManager);
    this.appManager = new AppManager(this);
    this.commandProcessor = new CommandProcessor(this);
    this.startMenuController = new StartMenuController(this);
    // Initialize command registry
    this.commandRegistry = CommandRegistry.getInstance(this);

    // Initialize network infrastructure
    this.dnsServer = new DNSServer();
    this.networkInterface = new NetworkInterface();

    // Initialize default websites using the dedicated class
    this.defaultWebsites = new DefaultWebsites(this);
    this.defaultWebsites.initDefaultWebsites();

    // Initialize settings utilities
    this.userSettings = new UserSettings(this.fileSystem);
    this.computerSettings = new ComputerSettings(this.fileSystem);

    this.webClient = new WebClient(this.websites);

    // Initialize desktop
    this.desktop = new Desktop(this);

    this.themeSystem = new ThemeSystem(this);
  }  /**
   * Initialize the OS
   */

  private initIcons(): void {
    createIcons({
      icons,
      nameAttr: 'data-lucide'
    });
  }

  public async init(): Promise<void> {
    console.log('Initializing HackerOS...');

    // Initialize filesystem
    await this.fileSystem.init();

    // Initialize process manager
    this.processManager.init();

    // Initialize window manager
    this.windowManager.init();

    // Initialize system monitor
    this.systemMonitor.init();

    // Initialize app manager and register default apps
    this.appManager.init();

    // Register all built-in commands
    this.commandRegistry.registerBuiltInCommands();



    // Initialize clock
    this.initClock();
    // Initialize desktop
    this.desktop.init();

    await this.themeSystem.initialize();
    await InitComponents(this);

    console.log('HackerOS initialized successfully');

    // Mark the OS as ready and execute any ready callbacks
    this.isReady = true;
    this.readyCallbacks.forEach(callback => callback());
    this.readyCallbacks = []; // Clear the callbacks after executing them
  }

  /**
   * Register a callback to be executed when the OS is fully initialized
   * If the OS is already initialized, the callback will be executed immediately
   * @param callback The function to call when the OS is ready
   */
  public Ready(callback: () => void): void {
    if (this.isReady) {
      // If OS is already ready, execute callback immediately
      callback();
    } else {
      // Otherwise, store the callback to be executed when the OS is ready
      this.readyCallbacks.push(callback);
    }
  }

  public get currentUserName(): string {
    return "user"; // Placeholder for the current user name
  }
  /**
   * Get file system instance
   */
  public getFileSystem(): FileSystem {
    return this.fileSystem;
  }

  /**
   * Get process manager instance
   */
  public getProcessManager(): ProcessManager {
    return this.processManager;
  }

  /**
   * Get window manager instance
   */
  public getWindowManager(): WindowManager {
    return this.windowManager;
  }

  /**
   * Get system monitor instance
   */
  public getSystemMonitor(): SystemMonitor {
    return this.systemMonitor;
  }

  /**
   * Get app manager instance
   */
  public getAppManager(): AppManager {
    return this.appManager;
  }
  /**
   * Get command processor instance
   */
  public getCommandProcessor(): CommandProcessor {
    return this.commandProcessor;
  }

  /**
   * Get the DNS server instance
   */
  public getDNSServer(): DNSServer {
    return this.dnsServer;
  }

  /**
   * Get the network interface instance
   */
  public getNetworkInterface(): NetworkInterface {
    return this.networkInterface;
  }

  /**
   * Get the desktop instance
   */
  public getDesktop(): Desktop {
    return this.desktop;
  } public getWebClient(): WebClient {
    return this.webClient;
  }

  /**
   * Get the user settings manager instance
   * Used for managing user-specific settings stored in the filesystem
   */
  public getUserSettings(): UserSettings {
    if (!this.userSettings) {
      this.userSettings = new UserSettings(this.fileSystem);
    }
    return this.userSettings;
  }

  /**
   * Get the computer settings manager instance
   * Used for managing system-wide settings stored in the filesystem
   */
  public getComputerSettings(): ComputerSettings {
    return this.computerSettings;
  }

  /**
   * Initialize the clock in the taskbar
   */
  private initClock(): void {
    const clockElement = document.getElementById('clock');
    if (!clockElement) return;

    const updateClock = () => {
      const now = new Date();
      const hours = now.getHours().toString().padStart(2, '0');
      const minutes = now.getMinutes().toString().padStart(2, '0');
      clockElement.textContent = `${hours}:${minutes}`;
    };

    // Update immediately and then every minute
    updateClock();
    this.clockInterval = window.setInterval(updateClock, 60000);
  }

  /**
   * Register a website in the OS
   * @param websiteOrController Website entry or controller to register
   */
  public registerWebsite(websiteOrController: WebsiteEntry | any): void {
    // Check if this is a controller instance
    if (websiteOrController.Host && typeof websiteOrController.processRequest === 'function') {
      // This is a controller
      const domain = websiteOrController.Host;
      this.websites.set(domain, {
        domain,
        content: {},
        controller: websiteOrController
      });
    } else {
      // This is a traditional website entry
      this.websites.set(websiteOrController.domain, websiteOrController);
    }
  }
  /**
   * Get website content for a given domain and path
   * @param domain Domain name (e.g., example.com)
   * @param path URL path (e.g., /about)
   * @param method HTTP method (default: GET)
   * @param requestData Additional request data (body, headers, etc.)
   * @returns Promise resolving to HTML content
   */
  public getWebsite(
    domain: string,
    path: string,
    method: string = 'GET',
    requestData: {
      query?: Record<string, string>,
      headers?: Record<string, string>,
      body?: {
        text?: string,
        json?: any,
        form?: Record<string, string>
      },
      cookies?: Record<string, string>
    } = {}
  ): Promise<string> {
    return new Promise((resolve, reject) => {
      // Look up the website in our registry
      const website = this.websites.get(domain);

      if (!website) {
        reject(new Error(`Website not found: ${domain}`));
        return;
      }

      // Check if this website has a controller
      if (website.controller) {
        // Prepare the request object for the controller
        const request = {
          method: method.toUpperCase(),
          path: path,
          query: requestData.query || {},
          headers: requestData.headers || {},
          body: requestData.body || {},
          cookies: requestData.cookies || {}
        };

        // Process the request through the controller
        website.controller.processRequest(request)
          .then((response: any) => {
            if (response.redirectUrl) {
              // Handle redirects
              const redirectUrl = new URL(response.redirectUrl);
              if (redirectUrl.hostname === domain) {
                // Internal redirect, update path and reprocess
                return this.getWebsite(
                  domain,
                  redirectUrl.pathname,
                  'GET',
                  {
                    query: Object.fromEntries(redirectUrl.searchParams.entries()),
                    headers: request.headers,
                    cookies: request.cookies
                  }
                );
              } else {
                // External redirect, create a redirect page
                resolve(`
                  <html>
                    <head>
                      <title>Redirecting...</title>
                      <meta http-equiv="refresh" content="0;url=${response.redirectUrl}">
                    </head>
                    <body>
                      <p>Redirecting to <a href="${response.redirectUrl}">${response.redirectUrl}</a>...</p>
                    </body>
                  </html>
                `);
              }
            } else if (response.content) {
              // Return the content directly
              resolve(response.content);
            } else {
              // No content or redirect, generic error
              reject(new Error(`Invalid response from controller for ${domain}${path}`));
            }
          })
          .catch((error: Error) => reject(error));

        return;
      }

      // Check if there's a dynamic handler for this website
      if (website.dynamicHandler) {
        website.dynamicHandler(path)
          .then(content => resolve(content))
          .catch(error => reject(error));
        return;
      }

      // Check if there's a static content for this path
      if (website.content[path]) {
        const content = website.content[path];

        if (typeof content === 'function') {
          // Execute function to get dynamic content
          try {
            resolve(content());
          } catch (error) {
            reject(error);
          }
        } else {
          // Return static content
          resolve(content);
        }
      } else {
        // Check if we have content for the root
        if (website.content['/']) {
          // Try to find most relevant content by checking for path prefixes
          const paths = Object.keys(website.content)
            .filter(p => p !== '/' && path.startsWith(p))
            .sort((a, b) => b.length - a.length); // Sort by length descending

          if (paths.length > 0) {
            const bestMatch = paths[0];
            const content = website.content[bestMatch];

            if (typeof content === 'function') {
              try {
                resolve(content());
              } catch (error) {
                reject(error);
              }
            } else {
              resolve(content);
            }
            return;
          }

          // If no matching paths, return a 404 page
          resolve(`
            <html>
              <head>
                <title>404 Not Found</title>
              </head>
              <body style="font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px;">
                <h1>404 Not Found</h1>
                <p>The page you requested (${path}) could not be found on this server.</p>
                <p><a href="https://${domain}/">Return to Homepage</a></p>
              </body>
            </html>
          `);
        } else {
          reject(new Error(`Content not found for path: ${path}`));
        }
      }
    });
  }

  /**
   * Shutdown the OS
   */
  public shutdown(): void {
    console.log('Shutting down HackerOS...');

    // Clear the clock interval
    if (this.clockInterval !== null) {
      clearInterval(this.clockInterval);
      this.clockInterval = null;
    }

    // Perform any necessary cleanup
    this.processManager.killAllProcesses();
    this.windowManager.closeAllWindows();

    console.log('HackerOS shutdown complete');
  }
  /**
   * Get a web client for making HTTP requests to simulated websites
   */
  public getWebServer(): any {
    return {
      request: async (options: { url: string, method: string, headers?: Record<string, string>, body?: any }) => {
        try {
          const url = new URL(options.url);
          const domain = url.hostname;
          const path = url.pathname || '/';
          const method = options.method.toUpperCase();

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
              headers: options.headers || {},
              body: {
                text: typeof options.body === 'string' ? options.body : undefined,
                json: typeof options.body === 'object' ? options.body : undefined
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

          // If content is a function, execute it to get the dynamic content
          if (typeof content === 'function') {
            content = content();
          }
          // If no content exists but there's a dynamic handler, use it
          else if (!content && website.dynamicHandler) {
            content = await website.dynamicHandler(path);
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
            body: content,
            statusCode: 200,
            statusText: 'OK',
            headers: { 'Content-Type': 'text/html' }
          };
        } catch (error) {
          throw error;
        }
      }
    };
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
