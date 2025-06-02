using System;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Specifies the route template, HTTP method, and name for an action method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RouteAttribute : Attribute
    {
        /// <summary>
        /// Gets the route template.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// Gets the HTTP method this route responds to.
        /// </summary>
        public string HttpMethod { get; }

        /// <summary>
        /// Gets the name of the route.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteAttribute"/> class with the specified template.
        /// </summary>
        /// <param name="template">The route template.</param>
        public RouteAttribute(string template)
            : this(template, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteAttribute"/> class with the specified template and HTTP method.
        /// </summary>
        /// <param name="template">The route template.</param>
        /// <param name="httpMethod">The HTTP method this route responds to (GET, POST, etc.).</param>
        public RouteAttribute(string template, string httpMethod)
            : this(template, httpMethod, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteAttribute"/> class with the specified template, HTTP method, and name.
        /// </summary>
        /// <param name="template">The route template.</param>
        /// <param name="httpMethod">The HTTP method this route responds to (GET, POST, etc.).</param>
        /// <param name="name">The name of the route.</param>
        public RouteAttribute(string template, string httpMethod, string name)
        {
            Template = template ?? string.Empty;
            HttpMethod = httpMethod;
            Name = name;
        }
    }
}
