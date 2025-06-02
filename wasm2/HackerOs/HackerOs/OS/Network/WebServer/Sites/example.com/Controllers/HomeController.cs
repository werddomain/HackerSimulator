using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;
using HackerOs.OS.Network.WebServer.Framework;
using HackerOs.OS.Network.WebServer.Sites.ExampleDotCom.Models;

namespace HackerOs.OS.Network.WebServer.Sites.ExampleDotCom.Controllers
{
    /// <summary>
    /// Controller for home page and general site navigation.
    /// </summary>
    public class HomeController : ControllerBase
    {
        /// <summary>
        /// Displays the home page.
        /// </summary>
        [Route("")]
        [Route("home")]
        [Route("home/index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Home - Example Store";
            ViewData["FeaturedProducts"] = ProductRepository.GetAll().FindAll(p => p.InStock);
            return View();
        }

        /// <summary>
        /// Displays the about page.
        /// </summary>
        [Route("about")]
        public IActionResult About()
        {
            ViewData["Title"] = "About Us - Example Store";
            return View();
        }

        /// <summary>
        /// Displays the contact page (GET).
        /// </summary>
        [Route("contact", "GET")]
        public IActionResult Contact()
        {
            ViewData["Title"] = "Contact Us - Example Store";
            return View();
        }

        /// <summary>
        /// Processes the contact form submission (POST).
        /// </summary>
        [Route("contact", "POST")]
        public IActionResult SubmitContact()
        {
            // Bind the form data to a ContactForm model
            var contactForm = BindModel<ContactForm>();

            // Validate the model
            if (!TryValidateModel(contactForm))
            {
                // If validation fails, return to the form with error messages
                ViewData["Title"] = "Contact Us - Example Store";
                ViewData["FormData"] = contactForm;
                return View("Contact");
            }

            // In a real application, you would save the form data or send an email
            // For this example, we'll just redirect to a thank you page
            ViewData["SubmittedForm"] = contactForm;
            ViewData["Title"] = "Thank You - Example Store";
            return View("ThankYou");
        }
    }

    /// <summary>
    /// Controller for product-related pages.
    /// </summary>
    public class ProductController : ControllerBase
    {
        /// <summary>
        /// Displays a list of all products.
        /// </summary>
        [Route("products")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Products - Example Store";
            ViewData["Products"] = ProductRepository.GetAll();
            return View();
        }

        /// <summary>
        /// Displays details for a specific product.
        /// </summary>
        [Route("products/{id}")]
        public IActionResult Details(int id)
        {
            var product = ProductRepository.GetById(id);
            
            if (product == null)
            {
                return View("NotFound");
            }

            ViewData["Title"] = $"{product.Name} - Example Store";
            ViewData["Product"] = product;
            return View();
        }

        /// <summary>
        /// Displays products in a specific category.
        /// </summary>
        [Route("category/{category}")]
        public IActionResult Category(string category)
        {
            var products = ProductRepository.GetByCategory(category);
            
            ViewData["Title"] = $"{category} - Example Store";
            ViewData["Category"] = category;
            ViewData["Products"] = products;
            return View();
        }

        /// <summary>
        /// Displays the form to add a new product (GET).
        /// </summary>
        [Route("products/add", "GET")]
        public IActionResult Add()
        {
            ViewData["Title"] = "Add Product - Example Store";
            return View();
        }

        /// <summary>
        /// Processes the form to add a new product (POST).
        /// </summary>
        [Route("products/add", "POST")]
        public IActionResult AddPost()
        {
            // Bind the form data to a Product model
            var product = BindModel<Product>();

            // Validate the model
            if (!TryValidateModel(product))
            {
                // If validation fails, return to the form with error messages
                ViewData["Title"] = "Add Product - Example Store";
                ViewData["Product"] = product;
                return View("Add");
            }

            // Add the product to the repository
            ProductRepository.Add(product);

            // Redirect to the product list
            return Redirect("/products");
        }

        /// <summary>
        /// Displays details for a specific product using CSHTML view.
        /// </summary>
        [Route("products/{id}/cshtml")]
        public IActionResult DetailsCshtml(int id)
        {
            var product = ProductRepository.GetById(id);
            
            if (product == null)
            {
                return View("NotFound");
            }
            
            // Create a ViewModel for the CSHTML view
            var viewModel = new HackerOs.OS.Network.WebServer.Sites.example.com.Models.ProductViewModel
            {
                Name = product.Name,
                Price = product.Price,
                Description = product.Description,
                IsInStock = product.InStock,
                Categories = new List<HackerOs.OS.Network.WebServer.Sites.example.com.Models.CategoryModel>()
            };
            
            // Add some sample categories
            viewModel.Categories.Add(new HackerOs.OS.Network.WebServer.Sites.example.com.Models.CategoryModel { Name = "Electronics" });
            viewModel.Categories.Add(new HackerOs.OS.Network.WebServer.Sites.example.com.Models.CategoryModel { Name = "Gadgets" });
            
            return new ViewResult
            {
                ViewName = "Product/Details", // Will prioritize .cshtml over .html
                Model = viewModel,
                ContentType = "text/html"
            };
        }
    }

    /// <summary>
    /// Controller for API endpoints.
    /// </summary>
    public class ApiController : ControllerBase
    {
        /// <summary>
        /// Returns all products as JSON.
        /// </summary>
        [Route("api/products")]
        public IActionResult GetProducts()
        {
            return Json(ProductRepository.GetAll());
        }

        /// <summary>
        /// Returns a specific product as JSON.
        /// </summary>
        [Route("api/products/{id}")]
        public IActionResult GetProduct(int id)
        {
            var product = ProductRepository.GetById(id);
            
            if (product == null)
            {
                HttpContext.Response.StatusCode = HttpStatusCode.NotFound;
                return Json(new { error = "Product not found" });
            }

            return Json(product);
        }
    }
}
