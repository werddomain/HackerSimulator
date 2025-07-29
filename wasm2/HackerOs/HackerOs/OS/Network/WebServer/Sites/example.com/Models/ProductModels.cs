using System;
using System.Collections.Generic;
using HackerOs.OS.Network.WebServer.Framework;

namespace HackerOs.OS.Network.WebServer.Sites.example.com.Models
{
    /// <summary>
    /// Represents a product in the store.
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Gets or sets the product ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the product description.
        /// </summary>
        [StringLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the product price.
        /// </summary>
        [Range(0.01, 10000.00)]
        public decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the product category.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the product image URL.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets whether the product is in stock.
        /// </summary>
        public bool InStock { get; set; }
    }

    /// <summary>
    /// Represents a contact form submission.
    /// </summary>
    public class ContactForm
    {
        /// <summary>
        /// Gets or sets the name of the person submitting the form.
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the subject of the message.
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        [Required]
        [StringLength(2000, MinimumLength = 10)]
        public string Message { get; set; }
    }

    /// <summary>
    /// Helper class for storing product data.
    /// </summary>
    public static class ProductRepository
    {
        private static readonly List<Product> _products = new List<Product>
        {
            new Product
            {
                Id = 1,
                Name = "HackerOS Desktop Computer",
                Description = "High-performance computer optimized for hacking and coding tasks.",
                Price = 1299.99m,
                Category = "Computers",
                ImageUrl = "/img/computer.png",
                InStock = true
            },
            new Product
            {
                Id = 2,
                Name = "Mechanical Keyboard",
                Description = "Tactile mechanical keyboard with programmable RGB lighting.",
                Price = 149.99m,
                Category = "Peripherals",
                ImageUrl = "/img/keyboard.png",
                InStock = true
            },
            new Product
            {
                Id = 3,
                Name = "32-inch Monitor",
                Description = "Ultra-wide curved monitor with 4K resolution.",
                Price = 499.99m,
                Category = "Displays",
                ImageUrl = "/img/monitor.png",
                InStock = false
            },
            new Product
            {
                Id = 4,
                Name = "Wireless Mouse",
                Description = "Ergonomic wireless mouse with long battery life.",
                Price = 59.99m,
                Category = "Peripherals",
                ImageUrl = "/img/mouse.png",
                InStock = true
            },
            new Product
            {
                Id = 5,
                Name = "External SSD",
                Description = "1TB external SSD with USB-C connection.",
                Price = 159.99m,
                Category = "Storage",
                ImageUrl = "/img/ssd.png",
                InStock = true
            }
        };

        /// <summary>
        /// Gets all products.
        /// </summary>
        public static List<Product> GetAll()
        {
            return _products;
        }

        /// <summary>
        /// Gets a product by ID.
        /// </summary>
        public static Product GetById(int id)
        {
            return _products.Find(p => p.Id == id);
        }

        /// <summary>
        /// Gets products by category.
        /// </summary>
        public static List<Product> GetByCategory(string category)
        {
            return _products.FindAll(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds a new product.
        /// </summary>
        public static void Add(Product product)
        {
            product.Id = _products.Count + 1;
            _products.Add(product);
        }
    }
}
