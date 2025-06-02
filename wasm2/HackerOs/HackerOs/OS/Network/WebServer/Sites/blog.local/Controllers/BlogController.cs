using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;
using HackerOs.OS.Network.WebServer.Framework;
using HackerOs.OS.Network.WebServer.Sites.BlogLocal.Models;

namespace HackerOs.OS.Network.WebServer.Sites.BlogLocal.Controllers
{
    /// <summary>
    /// Controller for blog home page and general site navigation.
    /// </summary>
    public class HomeController : ControllerBase
    {
        /// <summary>
        /// Displays the blog home page.
        /// </summary>
        [Route("")]
        [Route("home")]
        [Route("home/index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "HackerOS Blog - Latest Posts";
            ViewData["Posts"] = BlogRepository.GetAllPosts();
            return View();
        }

        /// <summary>
        /// Displays the about page.
        /// </summary>
        [Route("about")]
        public IActionResult About()
        {
            ViewData["Title"] = "About HackerOS Blog";
            return View();
        }

        /// <summary>
        /// Displays the subscription page (GET).
        /// </summary>
        [Route("subscribe", "GET")]
        public IActionResult Subscribe()
        {
            ViewData["Title"] = "Subscribe to HackerOS Blog";
            return View();
        }

        /// <summary>
        /// Processes the subscription form submission (POST).
        /// </summary>
        [Route("subscribe", "POST")]
        public IActionResult SubmitSubscription()
        {
            // Bind the form data to a SubscriptionForm model
            var subscriptionForm = BindModel<SubscriptionForm>();

            // Validate the model
            if (!TryValidateModel(subscriptionForm))
            {
                // If validation fails, return to the form with error messages
                ViewData["Title"] = "Subscribe to HackerOS Blog";
                ViewData["FormData"] = subscriptionForm;
                return View("Subscribe");
            }

            // In a real application, you would save the subscriber to a database
            // For this example, we'll just redirect to a thank you page
            ViewData["Subscriber"] = subscriptionForm;
            ViewData["Title"] = "Thank You for Subscribing - HackerOS Blog";
            return View("ThankYou");
        }
    }

    /// <summary>
    /// Controller for blog post-related pages.
    /// </summary>
    public class PostController : ControllerBase
    {
        /// <summary>
        /// Displays a specific blog post.
        /// </summary>
        [Route("post/{id}")]
        public IActionResult Post(int id)
        {
            var post = BlogRepository.GetPostById(id);
            
            if (post == null)
            {
                ViewData["Title"] = "Post Not Found - HackerOS Blog";
                return View("NotFound");
            }

            // Increment the view count
            BlogRepository.IncrementViewCount(id);

            ViewData["Title"] = $"{post.Title} - HackerOS Blog";
            ViewData["Post"] = post;
            ViewData["Comments"] = BlogRepository.GetCommentsForPost(id);
            return View();
        }

        /// <summary>
        /// Displays posts with a specific tag.
        /// </summary>
        [Route("tag/{tag}")]
        public IActionResult Tag(string tag)
        {
            var posts = BlogRepository.GetPostsByTag(tag);
            
            ViewData["Title"] = $"Posts tagged with '{tag}' - HackerOS Blog";
            ViewData["Tag"] = tag;
            ViewData["Posts"] = posts;
            return View();
        }

        /// <summary>
        /// Displays the form to create a new blog post (GET).
        /// </summary>
        [Route("post/create", "GET")]
        public IActionResult Create()
        {
            ViewData["Title"] = "Create New Post - HackerOS Blog";
            return View();
        }

        /// <summary>
        /// Processes the form to create a new blog post (POST).
        /// </summary>
        [Route("post/create", "POST")]
        public IActionResult CreatePost()
        {
            // Bind the form data to a BlogPost model
            var post = BindModel<BlogPost>();

            // Validate the model
            if (!TryValidateModel(post))
            {
                // If validation fails, return to the form with error messages
                ViewData["Title"] = "Create New Post - HackerOS Blog";
                ViewData["Post"] = post;
                return View("Create");
            }

            // Parse tags from comma-separated string
            string tagsString = HttpContext.Request.Form["Tags"];
            if (!string.IsNullOrEmpty(tagsString))
            {
                post.Tags = new List<string>();
                foreach (var tag in tagsString.Split(','))
                {
                    post.Tags.Add(tag.Trim());
                }
            }

            // Add the post to the repository
            BlogRepository.AddPost(post);

            // Redirect to the new post
            return Redirect($"/post/{post.Id}");
        }

        /// <summary>
        /// Adds a comment to a blog post (POST).
        /// </summary>
        [Route("post/{id}/comment", "POST")]
        public IActionResult AddComment(int id)
        {
            var post = BlogRepository.GetPostById(id);
            
            if (post == null)
            {
                ViewData["Title"] = "Post Not Found - HackerOS Blog";
                return View("NotFound");
            }

            // Bind the form data to a Comment model
            var comment = BindModel<Comment>();
            comment.PostId = id;

            // Validate the model
            if (!TryValidateModel(comment))
            {
                // If validation fails, return to the post with error messages
                ViewData["Title"] = $"{post.Title} - HackerOS Blog";
                ViewData["Post"] = post;
                ViewData["Comments"] = BlogRepository.GetCommentsForPost(id);
                ViewData["CommentFormData"] = comment;
                return View("Post");
            }

            // Add the comment to the repository
            BlogRepository.AddComment(comment);

            // Redirect back to the post
            return Redirect($"/post/{id}");
        }
    }

    /// <summary>
    /// Controller for API endpoints.
    /// </summary>
    public class ApiController : ControllerBase
    {
        /// <summary>
        /// Returns all blog posts as JSON.
        /// </summary>
        [Route("api/posts")]
        public IActionResult GetPosts()
        {
            return Json(BlogRepository.GetAllPosts());
        }

        /// <summary>
        /// Returns a specific blog post as JSON.
        /// </summary>
        [Route("api/posts/{id}")]
        public IActionResult GetPost(int id)
        {
            var post = BlogRepository.GetPostById(id);
            
            if (post == null)
            {
                HttpContext.Response.StatusCode = HttpStatusCode.NotFound;
                return Json(new { error = "Post not found" });
            }

            return Json(post);
        }

        /// <summary>
        /// Returns comments for a specific post as JSON.
        /// </summary>
        [Route("api/posts/{id}/comments")]
        public IActionResult GetComments(int id)
        {
            var post = BlogRepository.GetPostById(id);
            
            if (post == null)
            {
                HttpContext.Response.StatusCode = HttpStatusCode.NotFound;
                return Json(new { error = "Post not found" });
            }

            return Json(BlogRepository.GetCommentsForPost(id));
        }

        /// <summary>
        /// Returns posts with a specific tag as JSON.
        /// </summary>
        [Route("api/tags/{tag}")]
        public IActionResult GetPostsByTag(string tag)
        {
            return Json(BlogRepository.GetPostsByTag(tag));
        }
    }
}
