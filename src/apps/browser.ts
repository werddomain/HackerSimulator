import { OS } from '../core/os';
import { AppEventHandler, GuiApplication } from '../core/gui-application';

/**
 * Browser App for the Hacker Game
 * Provides a simulated web browsing experience
 */
export class BrowserApp extends GuiApplication {
  private currentUrl: string = 'https://hackersearch.net';
  private history: string[] = [];
  private historyPosition: number = -1;
  private favoriteUrls: Map<string, string> = new Map();
  private contentElement: HTMLElement | null = null;
  private urlInput: HTMLInputElement | null = null;
  
  constructor(os: OS) {
    super(os);
    
    // Initialize default favorites
    this.favoriteUrls.set('HackerSearch', 'https://hackersearch.net');
    this.favoriteUrls.set('HackMail', 'https://hackmail.com');
    this.favoriteUrls.set('CryptoBank', 'https://cryptobank.com');
    this.favoriteUrls.set('DarkNet Market', 'https://darknet.market');
    this.favoriteUrls.set('Hacker Forum', 'https://hackerz.forum');
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'browser';
  }
  
  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    this.render();
    
    // Add initial URL to history
    this.addToHistory(this.currentUrl);
    
    // Navigate to the initial URL
    this.navigate(this.currentUrl);
  }

  /**
   * Navigate to a URL
   */
  public navigate(url: string): void {
    // Add protocol if not present
    if (!url.startsWith('http://') && !url.startsWith('https://')) {
      url = 'https://' + url;
    }
    
    // Update current URL
    this.currentUrl = url;
    
    // Update URL input
    if (this.urlInput) {
      this.urlInput.value = url;
    }
    
    // Add to history
    this.addToHistory(url);
    
    // Show loading indicator
    if (this.contentElement) {
      this.contentElement.innerHTML = '<div class="loading">Loading...</div>';
    }
    
    // Simulate network delay
    setTimeout(() => {
      this.loadContent(url);
    }, 300);
  }
  /**
   * Load content for a URL
   */
  private loadContent(url: string): void {
    if (!this.contentElement) return;
    
    // Show loading indicator
    this.contentElement.innerHTML = '<div class="loading">Loading...</div>';
    
    // Request content from the "server"
    this.requestWebContent(url)
      .then(content => {
        // Create iframe if it doesn't exist yet
        let iframe = this.contentElement!.querySelector<HTMLIFrameElement>('.browser-iframe');
        if (!iframe) {
          iframe = document.createElement('iframe');
          iframe.className = 'browser-iframe';
          iframe.style.width = '100%';
          iframe.style.height = '100%';
          iframe.style.border = 'none';
          this.contentElement!.innerHTML = '';
          this.contentElement!.appendChild(iframe);
        }
        
        // Get the iframe document
        const iframeDoc = iframe.contentDocument || iframe.contentWindow?.document;
        if (!iframeDoc) return;
        
        // Write content to the iframe
        iframeDoc.open();
        iframeDoc.write(content);
        iframeDoc.close();
        
        // Set up event listeners for links
        this.setupLinkHandlers(iframe);
        
        // Update page title and favicon (if present)
        const title = iframeDoc.querySelector('title');
        if (title) {
          // Update window title if possible
          const windowElement = this.container?.closest('.window');
          const titleElement = windowElement?.querySelector('.window-title');
          if (titleElement) {
            titleElement.textContent = `${title.textContent} - Browser`;
          }
        }
      })
      .catch(error => {
        // Display error page
        this.contentElement!.innerHTML = `
          <div class="error-page">
            <h1>Cannot Access This Page</h1>
            <p>The site at <strong>${url}</strong> might be temporarily down or it may have moved permanently to a new web address.</p>
            <p>Error: ${error.message}</p>
          </div>
        `;
      });
  }

  /**
   * Request web content from the "server"
   */
  private requestWebContent(url: string): Promise<string> {
    return new Promise((resolve, reject) => {
      // Parse URL components
      const urlObj = new URL(url);
      const domain = urlObj.hostname;
      const path = urlObj.pathname || '/';
      
      // Different handling based on domain
      switch (domain) {
        case 'hackersearch.net':
          resolve(this.getSearchEnginePage());
          break;
        
        case 'hackmail.com':
          resolve(this.getEmailPage(path));
          break;
        
        case 'cryptobank.com':
          resolve(this.getBankPage(path));
          break;
        
        case 'darknet.market':
          resolve(this.getDarknetMarketPage(path));
          break;
        
        case 'hackerz.forum':
          resolve(this.getHackerForumPage(path));
          break;
        
        default:
          // Attempt to find a website handler
          this.os.getWebsite(domain, path)
            .then(content => {
              resolve(content);
            })
            .catch(error => {
              // Generate a generic "Site not found" page
              resolve(`
                <html>
                  <head>
                    <title>Site Not Found</title>
                  </head>
                  <body>
                    <div style="text-align: center; padding: 50px;">
                      <h1>Site Not Found</h1>
                      <p>The site ${domain} couldn't be found.</p>
                      <p>Try:</p>
                      <ul style="display: inline-block; text-align: left;">
                        <li>Checking the address for typing errors</li>
                        <li>Making sure you have a network connection</li>
                        <li>Checking your firewall settings</li>
                      </ul>
                    </div>
                  </body>
                </html>
              `);
            });
      }
    });
  }

  /**
   * Get search engine page content
   */
  private getSearchEnginePage(): string {
    return `
      <html>
        <head>
          <title>HackerSearch</title>
          <style>
            body {
              font-family: Arial, sans-serif;
              display: flex;
              flex-direction: column;
              align-items: center;
              justify-content: center;
              height: 80vh;
              margin: 0;
              background-color: #1a1a1a;
              color: #33ff33;
            }
            .search-container {
              text-align: center;
              width: 100%;
              max-width: 600px;
            }
            .logo {
              font-size: 48px;
              margin-bottom: 30px;
              font-weight: bold;
            }
            .search-box {
              width: 100%;
              padding: 12px 20px;
              margin: 8px 0;
              box-sizing: border-box;
              border: 2px solid #33ff33;
              background-color: #1a1a1a;
              color: #33ff33;
              font-size: 16px;
            }
            .search-btn {
              background-color: #33ff33;
              color: #1a1a1a;
              padding: 12px 20px;
              border: none;
              cursor: pointer;
              font-size: 16px;
              margin-top: 10px;
            }
            .links {
              margin-top: 30px;
              display: flex;
              gap: 20px;
            }
            .links a {
              color: #33ff33;
              text-decoration: none;
            }
            .links a:hover {
              text-decoration: underline;
            }
          </style>
        </head>
        <body>
          <div class="search-container">
            <div class="logo">HackerSearch</div>
            <form>
              <input type="text" class="search-box" placeholder="Search the web...">
              <button type="submit" class="search-btn">Search</button>
            </form>
            <div class="links">
              <a href="https://hackmail.com">HackMail</a>
              <a href="https://cryptobank.com">CryptoBank</a>
              <a href="https://hackerz.forum">Hacker Forum</a>
            </div>
          </div>
        </body>
      </html>
    `;
  }

  /**
   * Get email page content
   */
  private getEmailPage(path: string): string {
    // Basic email interface
    return `
      <html>
        <head>
          <title>HackMail</title>
          <style>
            body {
              font-family: Arial, sans-serif;
              margin: 0;
              padding: 0;
              background-color: #f5f5f5;
              color: #333;
              height: 100vh;
              display: flex;
              flex-direction: column;
            }
            header {
              background-color: #4285f4;
              color: white;
              padding: 10px 20px;
              display: flex;
              align-items: center;
            }
            .logo {
              font-size: 24px;
              font-weight: bold;
              margin-right: 20px;
            }
            .main-content {
              display: flex;
              flex: 1;
            }
            .sidebar {
              width: 200px;
              background-color: #fff;
              padding: 10px;
              border-right: 1px solid #ddd;
            }
            .folder {
              padding: 8px 10px;
              cursor: pointer;
              border-radius: 4px;
            }
            .folder:hover {
              background-color: #f1f1f1;
            }
            .folder.active {
              background-color: #e8f0fe;
              color: #1a73e8;
            }
            .email-list {
              flex: 1;
              background-color: #fff;
              border-right: 1px solid #ddd;
            }
            .email-item {
              padding: 10px 15px;
              border-bottom: 1px solid #f1f1f1;
              cursor: pointer;
            }
            .email-item:hover {
              background-color: #f9f9f9;
            }
            .email-item .subject {
              font-weight: bold;
            }
            .email-item .snippet {
              color: #666;
              font-size: 0.9em;
            }
            .compose-btn {
              background-color: #c2e7ff;
              color: #001d35;
              border: none;
              padding: 10px 15px;
              margin: 10px;
              border-radius: 4px;
              cursor: pointer;
              font-weight: bold;
              text-align: center;
            }
            .compose-btn:hover {
              background-color: #a5d4f7;
            }
          </style>
        </head>
        <body>
          <header>
            <div class="logo">HackMail</div>
          </header>
          <div class="main-content">
            <div class="sidebar">
              <div class="compose-btn">Compose</div>
              <div class="folder active">Inbox (3)</div>
              <div class="folder">Sent</div>
              <div class="folder">Drafts</div>
              <div class="folder">Spam</div>
              <div class="folder">Trash</div>
            </div>
            <div class="email-list">
              <div class="email-item">
                <div class="subject">Welcome to HackMail</div>
                <div class="sender">HackMail Team</div>
                <div class="snippet">Welcome to your new secure email account. We're excited to have you join our...</div>
              </div>
              <div class="email-item">
                <div class="subject">Your CryptoBank Statement</div>
                <div class="sender">noreply@cryptobank.com</div>
                <div class="snippet">Your monthly statement is now available. Log in to view the details...</div>
              </div>
              <div class="email-item">
                <div class="subject">First Contract: Security Audit</div>
                <div class="sender">anonymous@secure.net</div>
                <div class="snippet">I need someone with your skills for a security audit. The target is a small...</div>
              </div>
            </div>
          </div>
        </body>
      </html>
    `;
  }

  /**
   * Get bank page content
   */
  private getBankPage(path: string): string {
    // Banking website
    return `
      <html>
        <head>
          <title>CryptoBank</title>
          <style>
            body {
              font-family: Arial, sans-serif;
              margin: 0;
              padding: 0;
              background-color: #f7f9fc;
              color: #333;
              height: 100vh;
              display: flex;
              flex-direction: column;
            }
            header {
              background-color: #1e4620;
              color: white;
              padding: 15px 20px;
              display: flex;
              align-items: center;
              justify-content: space-between;
            }
            .logo {
              font-size: 24px;
              font-weight: bold;
            }
            .main-content {
              max-width: 1200px;
              margin: 0 auto;
              padding: 20px;
              width: 100%;
              flex: 1;
            }
            .login-form {
              background-color: white;
              padding: 30px;
              border-radius: 5px;
              box-shadow: 0 2px 10px rgba(0,0,0,0.1);
              max-width: 400px;
              margin: 50px auto;
            }
            .login-form h2 {
              margin-top: 0;
              color: #1e4620;
            }
            .form-group {
              margin-bottom: 15px;
            }
            .form-group label {
              display: block;
              margin-bottom: 5px;
              font-weight: bold;
            }
            .form-group input {
              width: 100%;
              padding: 10px;
              border: 1px solid #ddd;
              border-radius: 3px;
            }
            .login-btn {
              background-color: #1e4620;
              color: white;
              border: none;
              padding: 12px 20px;
              border-radius: 3px;
              cursor: pointer;
              width: 100%;
              font-weight: bold;
            }
            .login-btn:hover {
              background-color: #2a5a2a;
            }
            .nav-links {
              display: flex;
            }
            .nav-links a {
              color: white;
              text-decoration: none;
              margin-left: 20px;
            }
          </style>
        </head>
        <body>
          <header>
            <div class="logo">CryptoBank</div>
            <div class="nav-links">
              <a href="#">Personal</a>
              <a href="#">Business</a>
              <a href="#">About</a>
              <a href="#">Contact</a>
            </div>
          </header>
          <div class="main-content">
            <div class="login-form">
              <h2>Secure Login</h2>
              <form>
                <div class="form-group">
                  <label for="username">Username</label>
                  <input type="text" id="username" placeholder="Enter your username">
                </div>
                <div class="form-group">
                  <label for="password">Password</label>
                  <input type="password" id="password" placeholder="Enter your password">
                </div>
                <button type="submit" class="login-btn">Login</button>
              </form>
            </div>
          </div>
        </body>
      </html>
    `;
  }

  /**
   * Get darknet market page content
   */
  private getDarknetMarketPage(path: string): string {
    // Dark web marketplace
    return `
      <html>
        <head>
          <title>DarkNet Market</title>
          <style>
            body {
              font-family: 'Courier New', monospace;
              margin: 0;
              padding: 0;
              background-color: #121212;
              color: #33ff33;
              height: 100vh;
              display: flex;
              flex-direction: column;
            }
            header {
              background-color: #1a1a1a;
              color: #33ff33;
              padding: 15px 20px;
              display: flex;
              align-items: center;
              justify-content: space-between;
              border-bottom: 1px solid #33ff33;
            }
            .logo {
              font-size: 24px;
              font-weight: bold;
            }
            .main-content {
              max-width: 1200px;
              margin: 0 auto;
              padding: 20px;
              flex: 1;
              width: 100%;
            }
            .warning {
              background-color: #331111;
              border: 1px solid #ff3333;
              color: #ff3333;
              padding: 10px;
              margin-bottom: 20px;
              text-align: center;
            }
            .categories {
              display: flex;
              flex-wrap: wrap;
              gap: 10px;
              margin-bottom: 20px;
            }
            .category {
              background-color: #1a1a1a;
              padding: 8px 15px;
              border: 1px solid #33ff33;
              cursor: pointer;
            }
            .products {
              display: grid;
              grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
              gap: 20px;
            }
            .product {
              background-color: #1a1a1a;
              border: 1px solid #444;
              padding: 15px;
              display: flex;
              flex-direction: column;
              height: 100%;
            }
            .product-title {
              font-weight: bold;
              margin-bottom: 10px;
            }
            .product-price {
              margin-top: auto;
              color: #ff9900;
              font-weight: bold;
            }
            .nav-links {
              display: flex;
            }
            .nav-links a {
              color: #33ff33;
              text-decoration: none;
              margin-left: 20px;
            }
            .disclaimer {
              font-size: 0.8em;
              text-align: center;
              margin-top: 20px;
              color: #777;
            }
          </style>
        </head>
        <body>
          <header>
            <div class="logo">DarkNet Market</div>
            <div class="nav-links">
              <a href="#">Home</a>
              <a href="#">Marketplace</a>
              <a href="#">Account</a>
              <a href="#">Messages</a>
              <a href="#">Cart</a>
            </div>
          </header>
          <div class="main-content">
            <div class="warning">
              Warning: This marketplace is monitored. Use of this service implies acceptance of all risks.
            </div>
            <div class="categories">
              <div class="category">Digital Goods</div>
              <div class="category">Services</div>
              <div class="category">Hardware</div>
              <div class="category">Software</div>
              <div class="category">Zero-day Exploits</div>
            </div>
            <div class="products">
              <div class="product">
                <div class="product-title">Premium VPN Service - 1 Year</div>
                <div class="product-description">Untraceable connection, no logs policy, 50+ servers globally.</div>
                <div class="product-price">0.012 BTC</div>
              </div>
              <div class="product">
                <div class="product-title">USB Password Cracker</div>
                <div class="product-description">Hardware device to extract stored passwords from any system.</div>
                <div class="product-price">0.25 BTC</div>
              </div>
              <div class="product">
                <div class="product-title">Custom Malware Development</div>
                <div class="product-description">Bespoke malware created for your specific needs. Undetectable by most AV.</div>
                <div class="product-price">Starting at 0.5 BTC</div>
              </div>
              <div class="product">
                <div class="product-title">WiFi Pineapple Mark VII</div>
                <div class="product-description">The ultimate rogue access point for MITM attacks.</div>
                <div class="product-price">0.15 BTC</div>
              </div>
            </div>
            <div class="disclaimer">
              This is a simulated illegal marketplace for educational purposes only. All products are fictional.
            </div>
          </div>
        </body>
      </html>
    `;
  }

  /**
   * Get hacker forum page content
   */
  private getHackerForumPage(path: string): string {
    // Hacker forum
    return `
      <html>
        <head>
          <title>Hackerz Forum</title>
          <style>
            body {
              font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
              margin: 0;
              padding: 0;
              background-color: #0f0f0f;
              color: #ddd;
              height: 100vh;
              display: flex;
              flex-direction: column;
            }
            header {
              background-color: #1a1a1a;
              color: #00aaff;
              padding: 15px 20px;
              display: flex;
              align-items: center;
              justify-content: space-between;
              border-bottom: 1px solid #333;
            }
            .logo {
              font-size: 24px;
              font-weight: bold;
              color: #00aaff;
            }
            .main-content {
              max-width: 1200px;
              margin: 0 auto;
              padding: 20px;
              flex: 1;
              width: 100%;
            }
            .forum-section {
              background-color: #1a1a1a;
              border: 1px solid #333;
              margin-bottom: 20px;
            }
            .section-header {
              background-color: #252525;
              padding: 10px 15px;
              font-weight: bold;
              border-bottom: 1px solid #333;
              color: #00aaff;
            }
            .thread {
              padding: 10px 15px;
              border-bottom: 1px solid #222;
              display: flex;
              align-items: center;
            }
            .thread:last-child {
              border-bottom: none;
            }
            .thread-title {
              flex: 1;
              font-weight: bold;
              color: #ddd;
              text-decoration: none;
              display: block;
            }
            .thread-title:hover {
              color: #00aaff;
            }
            .thread-meta {
              color: #777;
              font-size: 0.9em;
              margin-left: 20px;
              white-space: nowrap;
            }
            .nav-links {
              display: flex;
            }
            .nav-links a {
              color: #ddd;
              text-decoration: none;
              margin-left: 20px;
            }
            .nav-links a:hover {
              color: #00aaff;
            }
            .hot-thread {
              color: #ff5555 !important;
            }
            .new-thread-btn {
              background-color: #00aaff;
              color: #0f0f0f;
              padding: 8px 15px;
              border: none;
              cursor: pointer;
              font-weight: bold;
              margin-bottom: 20px;
            }
          </style>
        </head>
        <body>
          <header>
            <div class="logo">Hackerz Forum</div>
            <div class="nav-links">
              <a href="#">Home</a>
              <a href="#">Forums</a>
              <a href="#">Members</a>
              <a href="#">Profile</a>
              <a href="#">Messages (3)</a>
            </div>
          </header>
          <div class="main-content">
            <button class="new-thread-btn">+ New Thread</button>
            
            <div class="forum-section">
              <div class="section-header">Announcements</div>
              <div class="thread">
                <a href="#" class="thread-title">Forum Rules - READ BEFORE POSTING</a>
                <div class="thread-meta">Moderator • 2 days ago • 1.2k views</div>
              </div>
              <div class="thread">
                <a href="#" class="thread-title">Monthly Security Digest - June 2025</a>
                <div class="thread-meta">Admin • 3 days ago • 876 views</div>
              </div>
            </div>
            
            <div class="forum-section">
              <div class="section-header">Exploits & Vulnerabilities</div>
              <div class="thread">
                <a href="#" class="thread-title hot-thread">URGENT: Zero-day in Windows Kernel (CVE-2025-1337)</a>
                <div class="thread-meta">darkbyte • 6 hours ago • 3.4k views</div>
              </div>
              <div class="thread">
                <a href="#" class="thread-title">SQL Injection technique that bypasses WAFs</a>
                <div class="thread-meta">hackmaster42 • 1 day ago • 1.5k views</div>
              </div>
              <div class="thread">
                <a href="#" class="thread-title">SSH weak key vulnerability in IoT devices</a>
                <div class="thread-meta">iotslayer • 3 days ago • 628 views</div>
              </div>
            </div>
            
            <div class="forum-section">
              <div class="section-header">Tools & Resources</div>
              <div class="thread">
                <a href="#" class="thread-title">Custom script for automated network scanning</a>
                <div class="thread-meta">netscanner • 12 hours ago • 412 views</div>
              </div>
              <div class="thread">
                <a href="#" class="thread-title">Updated password list for 2025</a>
                <div class="thread-meta">crackmaster • 2 days ago • 1.7k views</div>
              </div>
              <div class="thread">
                <a href="#" class="thread-title">My custom web scraping tool (source included)</a>
                <div class="thread-meta">webspider • 5 days ago • 893 views</div>
              </div>
            </div>
          </div>
        </body>
      </html>
    `;
  }

  /**
   * Add URL to history
   */
  private addToHistory(url: string): void {
    // If we're not at the end of the history, truncate the future history
    if (this.historyPosition < this.history.length - 1) {
      this.history = this.history.slice(0, this.historyPosition + 1);
    }
    
    // Add URL to history
    this.history.push(url);
    this.historyPosition = this.history.length - 1;
    
    // Update navigation buttons
    this.updateNavButtonsState();
  }

  /**
   * Navigate back in history
   */
  private navigateBack(): void {
    if (this.historyPosition <= 0) return;
    
    this.historyPosition--;
    const url = this.history[this.historyPosition];
    
    // Update current URL without adding to history
    this.currentUrl = url;
    if (this.urlInput) {
      this.urlInput.value = url;
    }
    
    // Load content
    this.loadContent(url);
    
    // Update navigation buttons
    this.updateNavButtonsState();
  }

  /**
   * Navigate forward in history
   */
  private navigateForward(): void {
    if (this.historyPosition >= this.history.length - 1) return;
    
    this.historyPosition++;
    const url = this.history[this.historyPosition];
    
    // Update current URL without adding to history
    this.currentUrl = url;
    if (this.urlInput) {
      this.urlInput.value = url;
    }
    
    // Load content
    this.loadContent(url);
    
    // Update navigation buttons
    this.updateNavButtonsState();
  }

  /**
   * Refresh current page
   */
  private refresh(): void {
    this.loadContent(this.currentUrl);
  }

  /**
   * Update navigation buttons state
   */
  private updateNavButtonsState(): void {
    const backBtn = this.container?.querySelector<HTMLButtonElement>('.browser-back');
    const forwardBtn = this.container?.querySelector<HTMLButtonElement>('.browser-forward');
    
    if (backBtn) {
      backBtn.disabled = this.historyPosition <= 0;
    }
    
    if (forwardBtn) {
      forwardBtn.disabled = this.historyPosition >= this.history.length - 1;
    }
  }
  /**
   * Set up link handlers
   */
  private setupLinkHandlers(iframe?: HTMLIFrameElement): void {
    if (!iframe) return;
    
    const iframeDoc = iframe.contentDocument || iframe.contentWindow?.document;
    if (!iframeDoc) return;
    
    // Add click event to all links in the iframe
    const links = iframeDoc.querySelectorAll('a');
    
    links.forEach(link => {
      const href = link.getAttribute('href');
      if (!href || href === '#') return;
      
      // Replace default behavior with our navigate function
      link.addEventListener('click', (e) => {
        e.preventDefault();
        this.navigate(href);
      });
    });
    
    // Handle form submissions in the iframe
    const forms = iframeDoc.querySelectorAll('form');
    
    forms.forEach(form => {
      form.addEventListener('submit', (e) => {
        e.preventDefault();
        // For now, just show an alert
        alert('Form submission is not implemented yet.');
      });
    });
  }

  /**
   * Render the browser UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="browser-app">
        <div class="browser-toolbar">
          <div class="browser-nav-buttons">
            <button class="browser-btn browser-back" title="Back">◀</button>
            <button class="browser-btn browser-forward" title="Forward">▶</button>
            <button class="browser-btn browser-refresh" title="Refresh">↻</button>
          </div>
          <div class="browser-address-bar">
            <div class="browser-url-prefix">🔒</div>
            <input type="text" class="browser-url-input" value="${this.currentUrl}">
            <button class="browser-btn browser-go" title="Go">→</button>
          </div>
          <div class="browser-actions">
            <button class="browser-btn browser-bookmark" title="Bookmark">★</button>
            <button class="browser-btn browser-menu" title="Menu">⋮</button>
          </div>
        </div>
        <div class="browser-bookmarks-bar">
          ${Array.from(this.favoriteUrls.entries()).map(([name, url]) => 
            `<div class="bookmark-item" data-url="${url}">${name}</div>`
          ).join('')}
        </div>
        <div class="browser-content"></div>
        <div class="bookmarks-menu" style="display: none;">
          <div class="bookmarks-header">
            <div class="bookmarks-title">Bookmarks</div>
            <button class="bookmarks-close">×</button>
          </div>
          <div class="bookmarks-list">
            ${Array.from(this.favoriteUrls.entries()).map(([name, url]) => 
              `<div class="bookmark-entry">
                <div class="bookmark-name">${name}</div>
                <div class="bookmark-url">${url}</div>
                <button class="bookmark-delete" data-url="${url}">×</button>
              </div>`
            ).join('')}
          </div>
          <div class="bookmarks-add">
            <input type="text" class="bookmark-name-input" placeholder="Name">
            <input type="text" class="bookmark-url-input" placeholder="URL">
            <button class="bookmark-add-btn">Add</button>
          </div>
        </div>
         <!-- NOTE: DO NOT ADD STYLES HERE! 
     All styles for the file explorer should be added to browser.less instead.
     This ensures proper scoping and prevents conflicts with other components. -->
      <style>
 <!-- NOTE: DO NOT ADD STYLES HERE! -->
      </style>
      </div>
    `;
    
    // Store references to important elements
    this.contentElement = this.container.querySelector('.browser-content');
    this.urlInput = this.container.querySelector('.browser-url-input');
    
    // Set up event listeners
    this.setupEventListeners();
  }

  /**
   * Set up event listeners
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // URL input
    const urlInput = this.container.querySelector<HTMLInputElement>('.browser-url-input');
    const goBtn = this.container.querySelector('.browser-go');
    
    urlInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        this.navigate(urlInput.value);
      }
    });
    
    goBtn?.addEventListener('click', () => {
      if (urlInput) {
        this.navigate(urlInput.value);
      }
    });
    
    // Navigation buttons
    const backBtn = this.container.querySelector('.browser-back');
    const forwardBtn = this.container.querySelector('.browser-forward');
    const refreshBtn = this.container.querySelector('.browser-refresh');
    
    backBtn?.addEventListener('click', () => this.navigateBack());
    forwardBtn?.addEventListener('click', () => this.navigateForward());
    refreshBtn?.addEventListener('click', () => this.refresh());
    
    // Bookmark button
    const bookmarkBtn = this.container.querySelector('.browser-bookmark');
    bookmarkBtn?.addEventListener('click', () => {
      const name = prompt('Enter a name for this bookmark:', new URL(this.currentUrl).hostname);
      if (name) {
        this.favoriteUrls.set(name, this.currentUrl);
        this.renderBookmarksBar();
      }
    });
    
    // Bookmark items
    const bookmarkItems = this.container.querySelectorAll('.bookmark-item');
    bookmarkItems.forEach(item => {
      item.addEventListener('click', () => {
        const url = item.getAttribute('data-url');
        if (url) {
          this.navigate(url);
        }
      });
    });
      // Menu button
    const menuBtn = this.container.querySelector('.browser-menu');
    const bookmarksMenu = this.container.querySelector<HTMLElement>('.bookmarks-menu');
    const bookmarksClose = this.container.querySelector('.bookmarks-close');
    
    menuBtn?.addEventListener('click', () => {
      if (bookmarksMenu) {
        bookmarksMenu.style.display = bookmarksMenu.style.display === 'none' ? 'block' : 'none';
      }
    });
    
    bookmarksClose?.addEventListener('click', () => {
      if (bookmarksMenu) {
        bookmarksMenu.style.display = 'none';
      }
    });
    
    // Bookmark management
    const bookmarkDeleteBtns = this.container.querySelectorAll('.bookmark-delete');
    bookmarkDeleteBtns.forEach(btn => {
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const url = btn.getAttribute('data-url');
        if (url) {
          // Find and remove the bookmark with this URL
          for (const [name, bookmarkUrl] of this.favoriteUrls.entries()) {
            if (bookmarkUrl === url) {
              this.favoriteUrls.delete(name);
              break;
            }
          }
          this.renderBookmarksBar();
          this.renderBookmarksMenu();
        }
      });
    });
    
    // Add bookmark button
    const bookmarkAddBtn = this.container.querySelector('.bookmark-add-btn');
    const bookmarkNameInput = this.container.querySelector<HTMLInputElement>('.bookmark-name-input');
    const bookmarkUrlInput = this.container.querySelector<HTMLInputElement>('.bookmark-url-input');
    
    bookmarkAddBtn?.addEventListener('click', () => {
      if (bookmarkNameInput && bookmarkUrlInput) {
        const name = bookmarkNameInput.value.trim();
        let url = bookmarkUrlInput.value.trim();
        
        if (name && url) {
          // Add protocol if missing
          if (!url.startsWith('http://') && !url.startsWith('https://')) {
            url = 'https://' + url;
          }
          
          this.favoriteUrls.set(name, url);
          this.renderBookmarksBar();
          this.renderBookmarksMenu();
          
          // Clear inputs
          bookmarkNameInput.value = '';
          bookmarkUrlInput.value = '';
        }
      }
    });
  }

  /**
   * Render bookmarks bar
   */
  private renderBookmarksBar(): void {
    const bookmarksBar = this.container?.querySelector('.browser-bookmarks-bar');
    if (!bookmarksBar) return;
    
    bookmarksBar.innerHTML = Array.from(this.favoriteUrls.entries())
      .map(([name, url]) => `<div class="bookmark-item" data-url="${url}">${name}</div>`)
      .join('');
    
    // Re-attach event listeners
    const bookmarkItems = bookmarksBar.querySelectorAll('.bookmark-item');
    bookmarkItems.forEach(item => {
      item.addEventListener('click', () => {
        const url = item.getAttribute('data-url');
        if (url) {
          this.navigate(url);
        }
      });
    });
  }

  /**
   * Render bookmarks menu
   */
  private renderBookmarksMenu(): void {
    const bookmarksList = this.container?.querySelector('.bookmarks-list');
    if (!bookmarksList) return;
    
    bookmarksList.innerHTML = Array.from(this.favoriteUrls.entries())
      .map(([name, url]) => `
        <div class="bookmark-entry">
          <div class="bookmark-name">${name}</div>
          <div class="bookmark-url">${url}</div>
          <button class="bookmark-delete" data-url="${url}">×</button>
        </div>
      `)
      .join('');
    
    // Re-attach delete event listeners
    const bookmarkDeleteBtns = bookmarksList.querySelectorAll('.bookmark-delete');
    bookmarkDeleteBtns.forEach(btn => {
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const url = btn.getAttribute('data-url');
        if (url) {
          // Find and remove the bookmark with this URL
          for (const [name, bookmarkUrl] of this.favoriteUrls.entries()) {
            if (bookmarkUrl === url) {
              this.favoriteUrls.delete(name);
              break;
            }
          }
          this.renderBookmarksBar();
          this.renderBookmarksMenu();
        }
      });
    });
  }
}
