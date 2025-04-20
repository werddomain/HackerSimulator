import { OS } from '../core/os';

/**
 * Interface for website registry entries
 */
export interface WebsiteEntry {
  domain: string;
  content: {
    [path: string]: string | (() => string);
  };
  dynamicHandler?: (path: string) => Promise<string>;
  controller?: any; // Controller instance for more complex websites
}

/**
 * Class responsible for managing default websites in the OS
 */
export class DefaultWebsites {
  private os: OS;

  constructor(os: OS) {
    this.os = os;
  }

  /**
   * Initialize default websites
   */
  public initDefaultWebsites(): void {
    // Define some default websites
    this.registerWebsite({
      domain: 'example.com',
      content: {
        '/': `
          <html>
            <head>
              <title>Example Website</title>
            </head>
            <body style="font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px;">
              <h1>Example Website</h1>
              <p>This is a simple example website available in the simulated internet.</p>
              <h2>Pages:</h2>
              <ul>
                <li><a href="https://example.com/about">About</a></li>
                <li><a href="https://example.com/contact">Contact</a></li>
              </ul>
            </body>
          </html>
        `,
        '/about': `
          <html>
            <head>
              <title>About - Example Website</title>
            </head>
            <body style="font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px;">
              <h1>About Example Website</h1>
              <p>This is the about page for the example website.</p>
              <p>This website exists to demonstrate the browser functionality in the HackerGame OS simulation.</p>
              <p><a href="https://example.com/">Back to Home</a></p>
            </body>
          </html>
        `,
        '/contact': `
          <html>
            <head>
              <title>Contact - Example Website</title>
            </head>
            <body style="font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px;">
              <h1>Contact Us</h1>
              <p>If this was a real website, you would find contact information here.</p>
              <form>
                <div style="margin-bottom: 15px;">
                  <label for="name" style="display: block; margin-bottom: 5px;">Name:</label>
                  <input type="text" id="name" style="width: 100%; padding: 8px; box-sizing: border-box;">
                </div>
                <div style="margin-bottom: 15px;">
                  <label for="email" style="display: block; margin-bottom: 5px;">Email:</label>
                  <input type="email" id="email" style="width: 100%; padding: 8px; box-sizing: border-box;">
                </div>
                <div style="margin-bottom: 15px;">
                  <label for="message" style="display: block; margin-bottom: 5px;">Message:</label>
                  <textarea id="message" rows="5" style="width: 100%; padding: 8px; box-sizing: border-box;"></textarea>
                </div>
                <button type="submit" style="background-color: #4CAF50; color: white; padding: 10px 15px; border: none; cursor: pointer;">Send Message</button>
              </form>
              <p style="margin-top: 20px;"><a href="https://example.com/">Back to Home</a></p>
            </body>
          </html>
        `
      }
    });
    
    // Tech company website
    this.registerWebsite({
      domain: 'techcorp.com',
      content: {
        '/': `
          <html>
            <head>
              <title>TechCorp - Innovative Solutions</title>
              <style>
                body {
                  font-family: 'Segoe UI', Arial, sans-serif;
                  margin: 0;
                  padding: 0;
                  color: #333;
                  line-height: 1.6;
                }
                header {
                  background-color: #0078d7;
                  color: white;
                  padding: 1rem;
                  text-align: center;
                }
                nav {
                  background-color: #f8f9fa;
                  padding: 1rem;
                  text-align: center;
                  border-bottom: 1px solid #ddd;
                }
                nav a {
                  margin: 0 15px;
                  text-decoration: none;
                  color: #0078d7;
                  font-weight: bold;
                }
                .container {
                  max-width: 1200px;
                  margin: 0 auto;
                  padding: 2rem;
                }
                .hero {
                  text-align: center;
                  margin-bottom: 2rem;
                }
                .cta-button {
                  display: inline-block;
                  background-color: #0078d7;
                  color: white;
                  padding: 10px 20px;
                  text-decoration: none;
                  border-radius: 5px;
                  font-weight: bold;
                  margin-top: 15px;
                }
                .features {
                  display: flex;
                  justify-content: space-between;
                  flex-wrap: wrap;
                }
                .feature {
                  flex: 1;
                  min-width: 300px;
                  margin: 1rem;
                  padding: 1.5rem;
                  background-color: #f8f9fa;
                  border-radius: 5px;
                  box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                }
                footer {
                  background-color: #333;
                  color: white;
                  text-align: center;
                  padding: 2rem;
                  margin-top: 2rem;
                }
              </style>
            </head>
            <body>
              <header>
                <h1>TechCorp</h1>
                <p>Innovative Solutions for Tomorrow's Challenges</p>
              </header>
              <nav>
                <a href="#">Home</a>
                <a href="https://techcorp.com/products">Products</a>
                <a href="https://techcorp.com/services">Services</a>
                <a href="https://techcorp.com/about">About</a>
                <a href="https://techcorp.com/careers">Careers</a>
                <a href="https://techcorp.com/contact">Contact</a>
              </nav>
              <div class="container">
                <section class="hero">
                  <h2>Transform Your Business with Cutting-Edge Technology</h2>
                  <p>TechCorp offers innovative solutions designed to elevate your business to new heights.</p>
                  <a href="#" class="cta-button">Learn More</a>
                </section>
                <section class="features">
                  <div class="feature">
                    <h3>Cloud Solutions</h3>
                    <p>Scalable cloud infrastructure designed for maximum efficiency and security.</p>
                  </div>
                  <div class="feature">
                    <h3>AI & Machine Learning</h3>
                    <p>Harness the power of artificial intelligence to gain actionable insights.</p>
                  </div>
                  <div class="feature">
                    <h3>Cybersecurity</h3>
                    <p>Protect your digital assets with our advanced security solutions.</p>
                  </div>
                </section>
              </div>
              <footer>
                <p>&copy; 2025 TechCorp. All rights reserved.</p>
                <p>Privacy Policy | Terms of Service</p>
              </footer>
            </body>
          </html>
        `,
        '/about': `
          <html>
            <head>
              <title>About - TechCorp</title>
              <style>
                body {
                  font-family: 'Segoe UI', Arial, sans-serif;
                  margin: 0;
                  padding: 0;
                  color: #333;
                  line-height: 1.6;
                }
                header {
                  background-color: #0078d7;
                  color: white;
                  padding: 1rem;
                  text-align: center;
                }
                nav {
                  background-color: #f8f9fa;
                  padding: 1rem;
                  text-align: center;
                  border-bottom: 1px solid #ddd;
                }
                nav a {
                  margin: 0 15px;
                  text-decoration: none;
                  color: #0078d7;
                  font-weight: bold;
                }
                .container {
                  max-width: 800px;
                  margin: 0 auto;
                  padding: 2rem;
                }
                .team-member {
                  display: flex;
                  margin-bottom: 2rem;
                  border-bottom: 1px solid #eee;
                  padding-bottom: 2rem;
                }
                .team-member-image {
                  width: 150px;
                  height: 150px;
                  background-color: #ddd;
                  border-radius: 50%;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  margin-right: 2rem;
                }
                .team-member-info {
                  flex: 1;
                }
                footer {
                  background-color: #333;
                  color: white;
                  text-align: center;
                  padding: 2rem;
                  margin-top: 2rem;
                }
              </style>
            </head>
            <body>
              <header>
                <h1>About TechCorp</h1>
                <p>Our Story, Mission, and Team</p>
              </header>
              <nav>
                <a href="https://techcorp.com/">Home</a>
                <a href="https://techcorp.com/products">Products</a>
                <a href="https://techcorp.com/services">Services</a>
                <a href="#">About</a>
                <a href="https://techcorp.com/careers">Careers</a>
                <a href="https://techcorp.com/contact">Contact</a>
              </nav>
              <div class="container">
                <section>
                  <h2>Our Story</h2>
                  <p>Founded in 2015, TechCorp began with a simple mission: to make cutting-edge technology accessible to businesses of all sizes. What started as a small startup with just 5 employees has grown into a global technology leader with offices in 12 countries.</p>
                  <p>Our journey has been marked by constant innovation and a commitment to excellence, driven by our passion for solving complex problems with elegant solutions.</p>
                </section>
                <section>
                  <h2>Our Leadership</h2>
                  <div class="team-member">
                    <div class="team-member-image">CEO</div>
                    <div class="team-member-info">
                      <h3>Jane Smith</h3>
                      <h4>Chief Executive Officer</h4>
                      <p>Jane founded TechCorp after 15 years in the technology sector. Her vision and leadership have been instrumental in shaping the company's success.</p>
                    </div>
                  </div>
                  <div class="team-member">
                    <div class="team-member-image">CTO</div>
                    <div class="team-member-info">
                      <h3>Michael Chen</h3>
                      <h4>Chief Technology Officer</h4>
                      <p>With a background in AI research, Michael leads our technology strategy and innovation initiatives.</p>
                    </div>
                  </div>
                  <div class="team-member">
                    <div class="team-member-image">COO</div>
                    <div class="team-member-info">
                      <h3>Alex Johnson</h3>
                      <h4>Chief Operations Officer</h4>
                      <p>Alex ensures that our operations run smoothly and efficiently, allowing us to deliver consistently excellent service to our clients.</p>
                    </div>
                  </div>
                </section>
              </div>
              <footer>
                <p>&copy; 2025 TechCorp. All rights reserved.</p>
                <p>Privacy Policy | Terms of Service</p>
              </footer>
            </body>
          </html>
        `
      }
    });
    
    // Target vulnerable website for hacking exercises
    this.registerWebsite({
      domain: 'targetbank.com',
      content: {
        '/': `
          <html>
            <head>
              <title>TargetBank - Secure Banking Solutions</title>
              <style>
                body {
                  font-family: Arial, sans-serif;
                  margin: 0;
                  padding: 0;
                  background-color: #f5f5f5;
                }
                header {
                  background-color: #003366;
                  color: white;
                  padding: 10px 0;
                  text-align: center;
                }
                .logo {
                  font-size: 24px;
                  font-weight: bold;
                }
                nav {
                  background-color: #004080;
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
                  color: #003366;
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
                  background-color: #003366;
                  color: white;
                  border: none;
                  border-radius: 3px;
                  cursor: pointer;
                }
                footer {
                  background-color: #003366;
                  color: white;
                  text-align: center;
                  padding: 20px 0;
                  margin-top: 40px;
                }
              </style>
            </head>
            <body>
              <header>
                <div class="logo">TargetBank</div>
              </header>
              <nav>
                <a href="#">Home</a>
                <a href="#">Personal Banking</a>
                <a href="#">Business Banking</a>
                <a href="#">Loans</a>
                <a href="#">About Us</a>
                <a href="#">Contact</a>
              </nav>
              <div class="container">
                <div class="hero">
                  <h1>Welcome to TargetBank</h1>
                  <p>Your trusted partner for all your banking needs. Secure, reliable, and always available.</p>
                </div>
                <div class="login-section">
                  <h2>Online Banking Login</h2>
                  <form method="GET" action="https://targetbank.com/login">
                    <div class="form-group">
                      <label for="username">Username:</label>
                      <input type="text" id="username" name="username" required>
                    </div>
                    <div class="form-group">
                      <label for="password">Password:</label>
                      <input type="password" id="password" name="password" required>
                    </div>
                    <button type="submit" class="login-btn">Login</button>
                  </form>
                </div>
              </div>
              <footer>
                <p>&copy; 2025 TargetBank. All rights reserved.</p>
                <p>FDIC Insured</p>
              </footer>
            </body>
          </html>
        `,
        '/login': () => {
          // Dynamically generated login response
          const params = new URLSearchParams(window.location.search);
          const username = params.get('username');
          const password = params.get('password');
          
          if (username && password) {
            // This is a deliberately vulnerable implementation for educational purposes
            return `
              <html>
                <head>
                  <title>Login Error - TargetBank</title>
                  <style>
                    body {
                      font-family: Arial, sans-serif;
                      margin: 0;
                      padding: 0;
                      background-color: #f5f5f5;
                    }
                    header {
                      background-color: #003366;
                      color: white;
                      padding: 10px 0;
                      text-align: center;
                    }
                    .logo {
                      font-size: 24px;
                      font-weight: bold;
                    }
                    .container {
                      max-width: 800px;
                      margin: 40px auto;
                      padding: 20px;
                      background-color: white;
                      border-radius: 5px;
                      box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                    }
                    .error {
                      color: #cc0000;
                      font-weight: bold;
                    }
                    .debug {
                      margin-top: 20px;
                      padding: 10px;
                      background-color: #f5f5f5;
                      border: 1px solid #ddd;
                      border-radius: 3px;
                      font-family: monospace;
                    }
                  </style>
                </head>
                <body>
                  <header>
                    <div class="logo">TargetBank</div>
                  </header>
                  <div class="container">
                    <h1>Login Error</h1>
                    <p class="error">Invalid username or password.</p>
                    <p>Please check your credentials and try again.</p>
                    <a href="https://targetbank.com/">Return to Home Page</a>
                    
                    <!-- SQL query information (deliberately exposed for educational/hacking purposes) -->
                    <div class="debug">
                      <p>Debug information (admin only):</p>
                      <code>
                        SQL Query: SELECT * FROM users WHERE username='${username}' AND password='${password}'
                      </code>
                    </div>
                  </div>
                </body>
              </html>
            `;
          } else {
            return `
              <html>
                <head>
                  <title>Login Error - TargetBank</title>
                  <style>
                    body {
                      font-family: Arial, sans-serif;
                      margin: 0;
                      padding: 0;
                      background-color: #f5f5f5;
                    }
                    header {
                      background-color: #003366;
                      color: white;
                      padding: 10px 0;
                      text-align: center;
                    }
                    .logo {
                      font-size: 24px;
                      font-weight: bold;
                    }
                    .container {
                      max-width: 800px;
                      margin: 40px auto;
                      padding: 20px;
                      background-color: white;
                      border-radius: 5px;
                      box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                    }
                    .error {
                      color: #cc0000;
                      font-weight: bold;
                    }
                  </style>
                </head>
                <body>
                  <header>
                    <div class="logo">TargetBank</div>
                  </header>
                  <div class="container">
                    <h1>Login Error</h1>
                    <p class="error">Missing username or password.</p>
                    <p>Please provide both username and password.</p>
                    <a href="https://targetbank.com/">Return to Home Page</a>
                  </div>
                </body>
              </html>
            `;
          }
        }
      }
    });
  }

  /**
   * Register a website in the OS
   * @param website Website entry to register
   */
  private registerWebsite(website: WebsiteEntry): void {
    this.os.registerWebsite(website);
  }
}
