import { BaseController, RoutesRegister, WebRequest, WebResponse, WebContentResponse, WebRedirectResponse, WebErrorResponse } from './web-server';

/**
 * Bank controller for a sample banking website
 */
export class BankController extends BaseController {
  public get Host(): string {
    return "mybank.net";
  }
  
  private users: Record<string, { password: string, balance: number }> = {
    'user': { password: 'SuperSecretPassword', balance: 5000 },
    'admin': { password: 'AdminPassword123', balance: 1000000 }
  };
  
  /**
   * Register routes for this controller
   */
  protected registerRoutes(routes: RoutesRegister): void {
    routes.Get("/", this.Index.bind(this));
    routes.Get("/about", this.About.bind(this));
    routes.Post("/login", this.Login.bind(this));
    routes.Get("/dashboard", this.Dashboard.bind(this));
    routes.Get("/logout", this.Logout.bind(this));
  }
  
  /**
   * Home page
   */
  private Index(request: WebRequest): WebResponse {
    // Check if user is logged in from cookie
    const username = request.cookies['user'];
    
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>MyBank - Secure Banking</title>
            <style>
              body {
                font-family: Arial, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f5f5f5;
              }
              header {
                background-color: #2c3e50;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 24px;
                font-weight: bold;
              }
              nav {
                background-color: #34495e;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
              }
              .container {
                max-width: 1200px;
                margin: 0 auto;
                padding: 20px;
              }
              .hero {
                background-color: white;
                padding: 40px;
                text-align: center;
                margin-bottom: 20px;
                border-radius: 5px;
                box-shadow: 0 2px 5px rgba(0,0,0,0.1);
              }
              .login-section {
                background-color: white;
                padding: 20px;
                border-radius: 5px;
                box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                width: 300px;
                margin: 0 auto;
              }
              .login-section h2 {
                text-align: center;
                color: #2c3e50;
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
                padding: 8px;
                border: 1px solid #ddd;
                border-radius: 3px;
              }
              .login-btn {
                width: 100%;
                padding: 10px;
                background-color: #2c3e50;
                color: white;
                border: none;
                border-radius: 3px;
                cursor: pointer;
              }
              footer {
                background-color: #2c3e50;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">MyBank</div>
            </header>
            <nav>
              <a href="https://mybank.net/">Home</a>
              <a href="https://mybank.net/about">About Us</a>
              ${username ? 
                `<a href="https://mybank.net/dashboard">Dashboard</a>
                 <a href="https://mybank.net/logout">Logout</a>` :
                ''}
            </nav>
            <div class="container">
              <div class="hero">
                <h1>Welcome to MyBank</h1>
                <p>Your trusted partner for secure online banking. Manage your finances with confidence.</p>
              </div>
              
              ${username ? 
                `<div style="text-align: center; padding: 20px;">
                  <h2>Welcome back, ${username}!</h2>
                  <p>You are already logged in.</p>
                  <a href="https://mybank.net/dashboard" style="display: inline-block; padding: 10px 20px; background-color: #2c3e50; color: white; text-decoration: none; border-radius: 3px; margin-top: 10px;">Go to Dashboard</a>
                </div>` :
                `<div class="login-section">
                  <h2>Online Banking Login</h2>
                  <form method="POST" action="https://mybank.net/login">
                    <div class="form-group">
                      <label for="user">Username:</label>
                      <input type="text" id="user" name="user" required>
                    </div>
                    <div class="form-group">
                      <label for="password">Password:</label>
                      <input type="password" id="password" name="password" required>
                    </div>
                    <button type="submit" class="login-btn">Login</button>
                  </form>
                </div>`
              }
            </div>
            <footer>
              <p>&copy; 2025 MyBank. All rights reserved.</p>
              <p>FDIC Insured</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * About page
   */
  private About(request: WebRequest): WebResponse {
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>About Us - MyBank</title>
            <style>
              body {
                font-family: Arial, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f5f5f5;
              }
              header {
                background-color: #2c3e50;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 24px;
                font-weight: bold;
              }
              nav {
                background-color: #34495e;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
              }
              .container {
                max-width: 800px;
                margin: 0 auto;
                padding: 20px;
                background-color: white;
                border-radius: 5px;
                box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                margin-top: 30px;
              }
              footer {
                background-color: #2c3e50;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">MyBank</div>
            </header>
            <nav>
              <a href="https://mybank.net/">Home</a>
              <a href="https://mybank.net/about">About Us</a>
            </nav>
            <div class="container">
              <h1>About MyBank</h1>
              <p>Founded in 2010, MyBank has been a trusted financial institution for over 15 years. We pride ourselves on offering secure, reliable, and innovative banking solutions for individuals and businesses.</p>
              
              <h2>Our Mission</h2>
              <p>To provide exceptional financial services that empower our customers to achieve their goals through personalized solutions, cutting-edge technology, and unwavering security.</p>
              
              <h2>Security Commitment</h2>
              <p>At MyBank, security is our top priority. We implement industry-leading encryption and security protocols to ensure that your financial information remains protected at all times.</p>
              
              <h2>Community Involvement</h2>
              <p>We believe in giving back to the communities we serve. Through our MyBank Foundation, we support local initiatives focused on education, economic development, and environmental sustainability.</p>
            </div>
            <footer>
              <p>&copy; 2025 MyBank. All rights reserved.</p>
              <p>FDIC Insured</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * Login handler
   */
  private Login(request: WebRequest): WebResponse {
    const username = request.body.form?.['user'];
    const password = request.body.form?.['password'];
    
    if (!username || !password) {
      return new WebErrorResponse({
        code: 400,
        reason: "Missing username or password"
      });
    }
    
    // Check credentials
    const user = this.users[username];
    if (user && user.password === password) {
      // Success - set cookie and redirect to dashboard
      return new WebRedirectResponse({
        url: "https://mybank.net/dashboard",
        // In a real response, we would set cookies here
      });
    } else {
      // Failed login
      return new WebErrorResponse({
        code: 403,
        reason: "Invalid username or password"
      });
    }
  }
  
  /**
   * Dashboard page (requires login)
   */
  private Dashboard(request: WebRequest): WebResponse {
    // Check if user is logged in
    const username = request.cookies['user'];
    if (!username || !this.users[username]) {
      return this.RedirectToAction("/");
    }
    
    const user = this.users[username];
    
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>Dashboard - MyBank</title>
            <style>
              body {
                font-family: Arial, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f5f5f5;
              }
              header {
                background-color: #2c3e50;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 24px;
                font-weight: bold;
              }
              nav {
                background-color: #34495e;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
              }
              .container {
                max-width: 1000px;
                margin: 30px auto;
                display: flex;
                gap: 20px;
              }
              .sidebar {
                width: 200px;
                background-color: white;
                border-radius: 5px;
                box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                padding: 20px;
              }
              .sidebar .menu-item {
                padding: 10px;
                border-bottom: 1px solid #eee;
                cursor: pointer;
              }
              .sidebar .menu-item:hover {
                background-color: #f5f5f5;
              }
              .content {
                flex: 1;
                background-color: white;
                border-radius: 5px;
                box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                padding: 20px;
              }
              .account-summary {
                background-color: #f9f9f9;
                border-radius: 5px;
                padding: 15px;
                margin-bottom: 20px;
              }
              .balance {
                font-size: 24px;
                font-weight: bold;
                color: #2c3e50;
              }
              .transactions {
                width: 100%;
                border-collapse: collapse;
              }
              .transactions th, .transactions td {
                padding: 10px;
                text-align: left;
                border-bottom: 1px solid #eee;
              }
              .transactions th {
                background-color: #f5f5f5;
              }
              footer {
                background-color: #2c3e50;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">MyBank</div>
            </header>
            <nav>
              <a href="https://mybank.net/">Home</a>
              <a href="https://mybank.net/dashboard">Dashboard</a>
              <a href="https://mybank.net/logout">Logout</a>
            </nav>
            <div class="container">
              <div class="sidebar">
                <div class="menu-item">Dashboard</div>
                <div class="menu-item">Accounts</div>
                <div class="menu-item">Transfers</div>
                <div class="menu-item">Payments</div>
                <div class="menu-item">Settings</div>
              </div>
              <div class="content">
                <h1>Welcome, ${username}</h1>
                <div class="account-summary">
                  <h2>Account Summary</h2>
                  <p>Checking Account: #12345678</p>
                  <p class="balance">Balance: $${user.balance.toLocaleString('en-US', { minimumFractionDigits: 2 })}</p>
                </div>
                
                <h2>Recent Transactions</h2>
                <table class="transactions">
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Description</th>
                      <th>Amount</th>
                      <th>Balance</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td>2025-04-15</td>
                      <td>Direct Deposit - Salary</td>
                      <td>+$2,500.00</td>
                      <td>$${user.balance.toLocaleString('en-US', { minimumFractionDigits: 2 })}</td>
                    </tr>
                    <tr>
                      <td>2025-04-14</td>
                      <td>Grocery Store</td>
                      <td>-$84.27</td>
                      <td>$${(user.balance - 2500 + 84.27).toLocaleString('en-US', { minimumFractionDigits: 2 })}</td>
                    </tr>
                    <tr>
                      <td>2025-04-12</td>
                      <td>Electric Bill Payment</td>
                      <td>-$124.50</td>
                      <td>$${(user.balance - 2500 + 84.27 + 124.50).toLocaleString('en-US', { minimumFractionDigits: 2 })}</td>
                    </tr>
                    <tr>
                      <td>2025-04-10</td>
                      <td>Online Shopping</td>
                      <td>-$59.99</td>
                      <td>$${(user.balance - 2500 + 84.27 + 124.50 + 59.99).toLocaleString('en-US', { minimumFractionDigits: 2 })}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
            <footer>
              <p>&copy; 2025 MyBank. All rights reserved.</p>
              <p>FDIC Insured</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * Logout handler
   */
  private Logout(request: WebRequest): WebResponse {
    // In a real response, we would clear cookies here
    return this.RedirectToAction("/");
  }
}
