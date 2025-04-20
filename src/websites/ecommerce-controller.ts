import { BaseController, RoutesRegister, WebRequest, WebResponse, WebContentResponse, WebRedirectResponse, WebErrorResponse } from './web-server';

/**
 * E-commerce controller for simulating an online shopping experience
 */
export class EcommerceController extends BaseController {
  public get Host(): string {
    return "shopzone.net";
  }
  
  private products: Record<string, { name: string, price: number, description: string, category: string, imageUrl: string }> = {
    'prod001': { 
      name: 'SmartWatch X1', 
      price: 199.99, 
      description: 'Latest generation smartwatch with health monitoring features',
      category: 'electronics',
      imageUrl: 'watch.jpg'
    },
    'prod002': { 
      name: 'Wireless Earbuds Pro', 
      price: 129.99, 
      description: 'Premium wireless earbuds with noise cancellation',
      category: 'electronics',
      imageUrl: 'earbuds.jpg'
    },
    'prod003': { 
      name: 'Ergonomic Desk Chair', 
      price: 249.99, 
      description: 'Comfortable office chair with lumbar support',
      category: 'furniture',
      imageUrl: 'chair.jpg'
    },
    'prod004': { 
      name: 'Coffee Maker Deluxe', 
      price: 89.99, 
      description: 'Programmable coffee maker with thermal carafe',
      category: 'kitchen',
      imageUrl: 'coffee.jpg'
    },
    'prod005': { 
      name: 'Fitness Tracker Band', 
      price: 49.99, 
      description: 'Lightweight fitness tracker with heart rate monitor',
      category: 'electronics',
      imageUrl: 'tracker.jpg'
    }
  };
  
  private cart: Record<string, Record<string, number>> = {};
  
  /**
   * Register routes for this controller
   */
  protected registerRoutes(routes: RoutesRegister): void {
    routes.Get("/", this.HomePage.bind(this));
    routes.Get("/products", this.ProductsPage.bind(this));
    routes.Get("/product/:id", this.ProductDetailPage.bind(this));
    routes.Get("/cart", this.CartPage.bind(this));
    routes.Post("/add-to-cart", this.AddToCart.bind(this));
    routes.Post("/checkout", this.Checkout.bind(this));
  }
  
  /**
   * Home page
   */
  private HomePage(request: WebRequest): WebResponse {
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>ShopZone - Online Shopping</title>
            <style>
              body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f8f9fa;
                color: #333;
              }
              header {
                background-color: #4a154b;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 28px;
                font-weight: bold;
              }
              nav {
                background-color: #611f69;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
                font-weight: 500;
              }
              .container {
                max-width: 1200px;
                margin: 0 auto;
                padding: 20px;
              }
              .hero {
                background-image: linear-gradient(rgba(0,0,0,0.5), rgba(0,0,0,0.5)), url('hero-bg.jpg');
                background-size: cover;
                background-position: center;
                height: 400px;
                display: flex;
                flex-direction: column;
                justify-content: center;
                align-items: center;
                text-align: center;
                color: white;
                border-radius: 5px;
                margin-bottom: 30px;
              }
              .hero h1 {
                font-size: 48px;
                margin-bottom: 20px;
              }
              .hero p {
                font-size: 20px;
                margin-bottom: 30px;
              }
              .btn {
                display: inline-block;
                background-color: #4a154b;
                color: white;
                padding: 12px 25px;
                text-decoration: none;
                border-radius: 4px;
                font-weight: bold;
                transition: background-color 0.3s;
              }
              .btn:hover {
                background-color: #611f69;
              }
              .featured {
                display: grid;
                grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
                gap: 20px;
                margin-top: 30px;
              }
              .product-card {
                background-color: white;
                border-radius: 5px;
                overflow: hidden;
                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                transition: transform 0.3s;
              }
              .product-card:hover {
                transform: translateY(-5px);
              }
              .product-img {
                height: 200px;
                background-color: #f0f0f0;
                display: flex;
                align-items: center;
                justify-content: center;
              }
              .product-info {
                padding: 15px;
              }
              .product-info h3 {
                margin-top: 0;
              }
              .product-info .price {
                font-weight: bold;
                color: #4a154b;
                font-size: 18px;
              }
              footer {
                background-color: #4a154b;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">ShopZone</div>
            </header>
            <nav>
              <a href="https://shopzone.net/">Home</a>
              <a href="https://shopzone.net/products">Products</a>
              <a href="https://shopzone.net/cart">Cart</a>
              <a href="#">Contact</a>
            </nav>
            <div class="container">
              <div class="hero">
                <h1>Welcome to ShopZone</h1>
                <p>Discover amazing products at unbeatable prices</p>
                <a href="https://shopzone.net/products" class="btn">Shop Now</a>
              </div>
              
              <h2>Featured Products</h2>
              <div class="featured">
                ${this.renderFeaturedProducts()}
              </div>
            </div>
            <footer>
              <p>&copy; 2025 ShopZone. All rights reserved.</p>
              <p>Privacy Policy | Terms of Service</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * Products listing page
   */
  private ProductsPage(request: WebRequest): WebResponse {
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>All Products - ShopZone</title>
            <style>
              body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f8f9fa;
                color: #333;
              }
              header {
                background-color: #4a154b;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 28px;
                font-weight: bold;
              }
              nav {
                background-color: #611f69;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
                font-weight: 500;
              }
              .container {
                max-width: 1200px;
                margin: 0 auto;
                padding: 20px;
              }
              .product-grid {
                display: grid;
                grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
                gap: 20px;
              }
              .product-card {
                background-color: white;
                border-radius: 5px;
                overflow: hidden;
                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                transition: transform 0.3s;
              }
              .product-card:hover {
                transform: translateY(-5px);
              }
              .product-img {
                height: 200px;
                background-color: #f0f0f0;
                display: flex;
                align-items: center;
                justify-content: center;
              }
              .product-info {
                padding: 15px;
              }
              .product-info h3 {
                margin-top: 0;
              }
              .product-info .price {
                font-weight: bold;
                color: #4a154b;
                font-size: 18px;
              }
              .filters {
                background-color: white;
                padding: 15px;
                border-radius: 5px;
                margin-bottom: 20px;
                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
              }
              footer {
                background-color: #4a154b;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">ShopZone</div>
            </header>
            <nav>
              <a href="https://shopzone.net/">Home</a>
              <a href="https://shopzone.net/products">Products</a>
              <a href="https://shopzone.net/cart">Cart</a>
              <a href="#">Contact</a>
            </nav>
            <div class="container">
              <h1>All Products</h1>
              <div class="filters">
                <h3>Filter by:</h3>
                <div>
                  <label><input type="checkbox"> Electronics</label>
                  <label><input type="checkbox"> Furniture</label>
                  <label><input type="checkbox"> Kitchen</label>
                </div>
              </div>
              <div class="product-grid">
                ${this.renderAllProducts()}
              </div>
            </div>
            <footer>
              <p>&copy; 2025 ShopZone. All rights reserved.</p>
              <p>Privacy Policy | Terms of Service</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * Product detail page
   */
  private ProductDetailPage(request: WebRequest): WebResponse {
    const productId = request.path.split('/').pop();
    
    if (!productId || !this.products[productId]) {
      return new WebErrorResponse({
        code: 404,
        reason: "Product not found"
      });
    }
    
    const product = this.products[productId];
    
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>${product.name} - ShopZone</title>
            <style>
              body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f8f9fa;
                color: #333;
              }
              header {
                background-color: #4a154b;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 28px;
                font-weight: bold;
              }
              nav {
                background-color: #611f69;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
                font-weight: 500;
              }
              .container {
                max-width: 1200px;
                margin: 0 auto;
                padding: 20px;
              }
              .product-detail {
                display: flex;
                background-color: white;
                border-radius: 5px;
                overflow: hidden;
                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
              }
              .product-image {
                flex: 1;
                background-color: #f0f0f0;
                padding: 30px;
                display: flex;
                align-items: center;
                justify-content: center;
              }
              .product-content {
                flex: 1;
                padding: 30px;
              }
              .product-content h1 {
                margin-top: 0;
                color: #4a154b;
              }
              .price {
                font-size: 24px;
                font-weight: bold;
                color: #4a154b;
                margin: 15px 0;
              }
              .btn {
                display: inline-block;
                background-color: #4a154b;
                color: white;
                padding: 12px 25px;
                text-decoration: none;
                border-radius: 4px;
                font-weight: bold;
                transition: background-color 0.3s;
                border: none;
                cursor: pointer;
              }
              .btn:hover {
                background-color: #611f69;
              }
              footer {
                background-color: #4a154b;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">ShopZone</div>
            </header>
            <nav>
              <a href="https://shopzone.net/">Home</a>
              <a href="https://shopzone.net/products">Products</a>
              <a href="https://shopzone.net/cart">Cart</a>
              <a href="#">Contact</a>
            </nav>
            <div class="container">
              <div class="product-detail">
                <div class="product-image">
                  <div style="width: 350px; height: 350px; background-color: #ddd; display: flex; align-items: center; justify-content: center;">
                    Product Image: ${product.name}
                  </div>
                </div>
                <div class="product-content">
                  <h1>${product.name}</h1>
                  <div class="price">$${product.price.toFixed(2)}</div>
                  <p>${product.description}</p>
                  <p>Category: ${product.category}</p>
                  <form method="POST" action="https://shopzone.net/add-to-cart">
                    <input type="hidden" name="productId" value="${productId}">
                    <div style="margin-bottom: 15px;">
                      <label for="quantity">Quantity:</label>
                      <select id="quantity" name="quantity">
                        <option value="1">1</option>
                        <option value="2">2</option>
                        <option value="3">3</option>
                        <option value="4">4</option>
                        <option value="5">5</option>
                      </select>
                    </div>
                    <button type="submit" class="btn">Add to Cart</button>
                  </form>
                </div>
              </div>
            </div>
            <footer>
              <p>&copy; 2025 ShopZone. All rights reserved.</p>
              <p>Privacy Policy | Terms of Service</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * Shopping cart page
   */
  private CartPage(request: WebRequest): WebResponse {
    const userId = request.cookies['user_id'] || 'anonymous';
    const userCart = this.cart[userId] || {};
    
    const cartItems = Object.entries(userCart).map(([productId, quantity]) => {
      const product = this.products[productId];
      return {
        id: productId,
        name: product.name,
        price: product.price,
        quantity: quantity,
        total: product.price * quantity
      };
    });
    
    const cartTotal = cartItems.reduce((sum, item) => sum + item.total, 0);
    
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>Shopping Cart - ShopZone</title>
            <style>
              body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f8f9fa;
                color: #333;
              }
              header {
                background-color: #4a154b;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 28px;
                font-weight: bold;
              }
              nav {
                background-color: #611f69;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
                font-weight: 500;
              }
              .container {
                max-width: 1200px;
                margin: 0 auto;
                padding: 20px;
              }
              .cart-container {
                background-color: white;
                border-radius: 5px;
                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                padding: 20px;
              }
              table {
                width: 100%;
                border-collapse: collapse;
              }
              th, td {
                padding: 12px 15px;
                text-align: left;
                border-bottom: 1px solid #eee;
              }
              th {
                background-color: #f8f9fa;
                font-weight: 600;
              }
              .btn {
                display: inline-block;
                background-color: #4a154b;
                color: white;
                padding: 12px 25px;
                text-decoration: none;
                border-radius: 4px;
                font-weight: bold;
                transition: background-color 0.3s;
                border: none;
                cursor: pointer;
              }
              .btn:hover {
                background-color: #611f69;
              }
              .cart-total {
                margin-top: 20px;
                text-align: right;
              }
              .cart-total .total-price {
                font-size: 24px;
                font-weight: bold;
                color: #4a154b;
                margin: 10px 0;
              }
              footer {
                background-color: #4a154b;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">ShopZone</div>
            </header>
            <nav>
              <a href="https://shopzone.net/">Home</a>
              <a href="https://shopzone.net/products">Products</a>
              <a href="https://shopzone.net/cart">Cart</a>
              <a href="#">Contact</a>
            </nav>
            <div class="container">
              <h1>Your Shopping Cart</h1>
              <div class="cart-container">
                ${cartItems.length === 0 ? 
                  `<p>Your cart is empty. <a href="https://shopzone.net/products">Continue shopping</a></p>` : 
                  `<table>
                    <thead>
                      <tr>
                        <th>Product</th>
                        <th>Price</th>
                        <th>Quantity</th>
                        <th>Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      ${cartItems.map(item => `
                        <tr>
                          <td>${item.name}</td>
                          <td>$${item.price.toFixed(2)}</td>
                          <td>${item.quantity}</td>
                          <td>$${item.total.toFixed(2)}</td>
                        </tr>
                      `).join('')}
                    </tbody>
                  </table>
                  <div class="cart-total">
                    <div class="total-price">Total: $${cartTotal.toFixed(2)}</div>
                    <form method="POST" action="https://shopzone.net/checkout">
                      <button type="submit" class="btn">Proceed to Checkout</button>
                    </form>
                  </div>`
                }
              </div>
            </div>
            <footer>
              <p>&copy; 2025 ShopZone. All rights reserved.</p>
              <p>Privacy Policy | Terms of Service</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * Add to cart handler
   */
  private AddToCart(request: WebRequest): WebResponse {
    const productId = request.body.form?.['productId'];
    const quantityStr = request.body.form?.['quantity'] || '1';
    const quantity = parseInt(quantityStr, 10);
    
    if (!productId || !this.products[productId]) {
      return new WebErrorResponse({
        code: 400,
        reason: "Invalid product"
      });
    }
    
    // Get user ID from cookie (or anonymous)
    const userId = request.cookies['user_id'] || 'anonymous';
    
    // Initialize cart for user if needed
    if (!this.cart[userId]) {
      this.cart[userId] = {};
    }
    
    // Add or update product in cart
    if (this.cart[userId][productId]) {
      this.cart[userId][productId] += quantity;
    } else {
      this.cart[userId][productId] = quantity;
    }
    
    // Redirect to cart page
    return new WebRedirectResponse({
      url: "https://shopzone.net/cart"
    });
  }
  
  /**
   * Checkout handler
   */
  private Checkout(request: WebRequest): WebResponse {
    const userId = request.cookies['user_id'] || 'anonymous';
    
    // In a real site, we would process payment here
    
    // Clear the cart
    this.cart[userId] = {};
    
    return new WebContentResponse({
      code: 200,
      content: `
        <html>
          <head>
            <title>Order Confirmation - ShopZone</title>
            <style>
              body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                margin: 0;
                padding: 0;
                background-color: #f8f9fa;
                color: #333;
              }
              header {
                background-color: #4a154b;
                color: white;
                padding: 15px 0;
                text-align: center;
              }
              .logo {
                font-size: 28px;
                font-weight: bold;
              }
              nav {
                background-color: #611f69;
                padding: 10px 0;
                text-align: center;
              }
              nav a {
                color: white;
                text-decoration: none;
                margin: 0 15px;
                font-weight: 500;
              }
              .container {
                max-width: 800px;
                margin: 0 auto;
                padding: 20px;
              }
              .confirmation {
                background-color: white;
                border-radius: 5px;
                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                padding: 30px;
                text-align: center;
              }
              .confirmation h1 {
                color: #4a154b;
              }
              .btn {
                display: inline-block;
                background-color: #4a154b;
                color: white;
                padding: 12px 25px;
                text-decoration: none;
                border-radius: 4px;
                font-weight: bold;
                transition: background-color 0.3s;
              }
              .btn:hover {
                background-color: #611f69;
              }
              footer {
                background-color: #4a154b;
                color: white;
                text-align: center;
                padding: 20px 0;
                margin-top: 40px;
              }
            </style>
          </head>
          <body>
            <header>
              <div class="logo">ShopZone</div>
            </header>
            <nav>
              <a href="https://shopzone.net/">Home</a>
              <a href="https://shopzone.net/products">Products</a>
              <a href="https://shopzone.net/cart">Cart</a>
              <a href="#">Contact</a>
            </nav>
            <div class="container">
              <div class="confirmation">
                <h1>Order Confirmed!</h1>
                <p>Thank you for your purchase. Your order has been processed successfully.</p>
                <p>Order number: ORD-${Math.floor(100000 + Math.random() * 900000)}</p>
                <p>A confirmation email has been sent to your email address.</p>
                <a href="https://shopzone.net/" class="btn">Continue Shopping</a>
              </div>
            </div>
            <footer>
              <p>&copy; 2025 ShopZone. All rights reserved.</p>
              <p>Privacy Policy | Terms of Service</p>
            </footer>
          </body>
        </html>
      `
    });
  }
  
  /**
   * Helper method to render featured products
   */
  private renderFeaturedProducts(): string {
    const featuredProductIds = ['prod001', 'prod002', 'prod003'];
    
    return featuredProductIds.map(id => {
      const product = this.products[id];
      return `
        <div class="product-card">
          <div class="product-img">Product Image: ${product.name}</div>
          <div class="product-info">
            <h3>${product.name}</h3>
            <p>${product.description.substring(0, 60)}${product.description.length > 60 ? '...' : ''}</p>
            <div class="price">$${product.price.toFixed(2)}</div>
            <a href="https://shopzone.net/product/${id}" style="color: #4a154b; text-decoration: none; font-weight: bold;">View Details</a>
          </div>
        </div>
      `;
    }).join('');
  }
  
  /**
   * Helper method to render all products
   */
  private renderAllProducts(): string {
    return Object.entries(this.products).map(([id, product]) => {
      return `
        <div class="product-card">
          <div class="product-img">Product Image: ${product.name}</div>
          <div class="product-info">
            <h3>${product.name}</h3>
            <p>${product.description.substring(0, 60)}${product.description.length > 60 ? '...' : ''}</p>
            <div class="price">$${product.price.toFixed(2)}</div>
            <a href="https://shopzone.net/product/${id}" style="color: #4a154b; text-decoration: none; font-weight: bold;">View Details</a>
          </div>
        </div>
      `;
    }).join('');
  }
}
