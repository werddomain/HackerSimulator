using System;
using System.Collections.Generic;
using System.Linq;
using HackerOs.OS.Network.WebServer.Framework;

namespace HackerOs.OS.Network.WebServer.Sites.BlogLocal.Models
{
    /// <summary>
    /// Represents a blog post.
    /// </summary>
    public class BlogPost
    {
        /// <summary>
        /// Gets or sets the post ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the post title.
        /// </summary>
        [Required]
        [StringLength(200, MinimumLength = 3)]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the post content.
        /// </summary>
        [Required]
        [StringLength(10000, MinimumLength = 10)]
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the post author.
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Author { get; set; }

        /// <summary>
        /// Gets or sets the post publication date.
        /// </summary>
        public DateTime PublishedDate { get; set; }

        /// <summary>
        /// Gets or sets the post tags.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the post featured image URL.
        /// </summary>
        public string FeaturedImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the number of views for this post.
        /// </summary>
        public int ViewCount { get; set; }
    }

    /// <summary>
    /// Represents a comment on a blog post.
    /// </summary>
    public class Comment
    {
        /// <summary>
        /// Gets or sets the comment ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the post this comment belongs to.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the name of the commenter.
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the email of the commenter.
        /// </summary>
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the comment content.
        /// </summary>
        [Required]
        [StringLength(1000, MinimumLength = 2)]
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the comment date.
        /// </summary>
        public DateTime CommentDate { get; set; }
    }

    /// <summary>
    /// Represents a subscription form.
    /// </summary>
    public class SubscriptionForm
    {
        /// <summary>
        /// Gets or sets the name of the subscriber.
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the email of the subscriber.
        /// </summary>
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets whether the subscriber wants to receive weekly newsletters.
        /// </summary>
        public bool ReceiveNewsletter { get; set; } = true;
    }

    /// <summary>
    /// Helper class for storing blog posts and comments.
    /// </summary>
    public static class BlogRepository
    {
        private static readonly List<BlogPost> _posts = new List<BlogPost>
        {
            new BlogPost
            {
                Id = 1,
                Title = "Getting Started with HackerOS",
                Content = "HackerOS is a powerful operating system designed for developers and security enthusiasts. In this post, we'll explore the basic features and how to get started with your first project.\n\nFirst, let's talk about the command line interface. HackerOS comes with a powerful terminal that supports all standard Linux commands plus some custom utilities designed specifically for penetration testing and development tasks.\n\nThe file system is organized in a familiar way, with /home, /usr, and other standard directories. However, HackerOS adds some special directories like /tools and /exploits that contain pre-installed utilities.\n\nTo get started, simply open the terminal and type 'tutorial' to launch the interactive guide.",
                Author = "System Admin",
                PublishedDate = new DateTime(2025, 5, 15),
                Tags = new List<string> { "tutorial", "basics", "getting-started" },
                FeaturedImageUrl = "/img/hacker-os-desktop.jpg",
                ViewCount = 1542
            },
            new BlogPost
            {
                Id = 2,
                Title = "Customizing Your Development Environment",
                Content = "One of the greatest strengths of HackerOS is its customizability. In this post, we'll look at how to personalize your environment for maximum productivity.\n\nThe theme system in HackerOS allows for complete visual customization. You can adjust colors, fonts, transparency, and even create your own themes using the built-in theme editor.\n\nFor terminal users, the .bashrc and .profile files can be modified to set up aliases, environment variables, and custom functions. HackerOS also supports zsh and fish shells out of the box.\n\nThe code editor can be extended with plugins, and custom keybindings can be configured to match your preferences from other editors like VSCode or Vim.",
                Author = "ThemeExpert",
                PublishedDate = new DateTime(2025, 5, 22),
                Tags = new List<string> { "customization", "themes", "productivity" },
                FeaturedImageUrl = "/img/custom-theme.jpg",
                ViewCount = 879
            },
            new BlogPost
            {
                Id = 3,
                Title = "Web Development with HackerOS",
                Content = "HackerOS comes with everything you need for modern web development. This post explores the built-in tools and frameworks that make web development a breeze.\n\nThe system comes pre-installed with Node.js, npm, and popular frameworks like React, Angular, and Vue. The built-in code editor has syntax highlighting and IntelliSense for JavaScript, TypeScript, HTML, and CSS.\n\nFor backend development, HackerOS includes Python, Ruby, PHP, and .NET Core. The built-in web server can be configured to run your applications locally for testing.\n\nDocker integration makes it easy to containerize your applications and run them in isolated environments. The network monitor allows you to inspect HTTP traffic and debug API calls.",
                Author = "WebDevPro",
                PublishedDate = new DateTime(2025, 5, 30),
                Tags = new List<string> { "web-development", "javascript", "frameworks" },
                FeaturedImageUrl = "/img/web-development.jpg",
                ViewCount = 1205
            }
        };

        private static readonly List<Comment> _comments = new List<Comment>
        {
            new Comment
            {
                Id = 1,
                PostId = 1,
                Name = "First Time User",
                Email = "user@example.com",
                Content = "This tutorial was really helpful! I'm excited to explore more of HackerOS.",
                CommentDate = new DateTime(2025, 5, 16)
            },
            new Comment
            {
                Id = 2,
                PostId = 1,
                Name = "Security Researcher",
                Email = "researcher@security.org",
                Content = "I've been using HackerOS for my security work. The pre-installed tools are a real time-saver.",
                CommentDate = new DateTime(2025, 5, 17)
            },
            new Comment
            {
                Id = 3,
                PostId = 2,
                Name = "UI Designer",
                Email = "designer@creative.net",
                Content = "The theme system is impressive. I was able to create a custom theme that matches my company's branding.",
                CommentDate = new DateTime(2025, 5, 23)
            },
            new Comment
            {
                Id = 4,
                PostId = 3,
                Name = "Full Stack Developer",
                Email = "dev@webagency.com",
                Content = "The Docker integration is seamless. I was able to set up my entire development environment in minutes.",
                CommentDate = new DateTime(2025, 5, 31)
            }
        };

        /// <summary>
        /// Gets all blog posts.
        /// </summary>
        public static List<BlogPost> GetAllPosts()
        {
            return _posts.OrderByDescending(p => p.PublishedDate).ToList();
        }

        /// <summary>
        /// Gets a blog post by ID.
        /// </summary>
        public static BlogPost GetPostById(int id)
        {
            return _posts.Find(p => p.Id == id);
        }

        /// <summary>
        /// Gets blog posts by tag.
        /// </summary>
        public static List<BlogPost> GetPostsByTag(string tag)
        {
            return _posts.FindAll(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                         .OrderByDescending(p => p.PublishedDate)
                         .ToList();
        }

        /// <summary>
        /// Adds a new blog post.
        /// </summary>
        public static void AddPost(BlogPost post)
        {
            post.Id = _posts.Count > 0 ? _posts.Max(p => p.Id) + 1 : 1;
            post.PublishedDate = DateTime.Now;
            post.ViewCount = 0;
            _posts.Add(post);
        }

        /// <summary>
        /// Increments the view count for a post.
        /// </summary>
        public static void IncrementViewCount(int postId)
        {
            var post = GetPostById(postId);
            if (post != null)
            {
                post.ViewCount++;
            }
        }

        /// <summary>
        /// Gets all comments for a post.
        /// </summary>
        public static List<Comment> GetCommentsForPost(int postId)
        {
            return _comments.FindAll(c => c.PostId == postId)
                            .OrderByDescending(c => c.CommentDate)
                            .ToList();
        }

        /// <summary>
        /// Adds a new comment to a post.
        /// </summary>
        public static void AddComment(Comment comment)
        {
            comment.Id = _comments.Count > 0 ? _comments.Max(c => c.Id) + 1 : 1;
            comment.CommentDate = DateTime.Now;
            _comments.Add(comment);
        }
    }
}
