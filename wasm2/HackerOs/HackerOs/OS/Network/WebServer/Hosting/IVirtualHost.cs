using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Hosting
{
    /// <summary>
    /// Interface for a virtual host in the web server.
    /// </summary>
    public interface IVirtualHost
    {
        /// <summary>
        /// Gets the hostname of this virtual host.
        /// </summary>
        string Hostname { get; }
        
        /// <summary>
        /// Gets the list of hostnames this virtual host responds to.
        /// </summary>
        IReadOnlyList<string> HostAliases { get; }
        
        /// <summary>
        /// Gets or sets the document root path for this host.
        /// </summary>
        string DocumentRoot { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether directory browsing is enabled.
        /// </summary>
        bool DirectoryBrowsingEnabled { get; set; }
        
        /// <summary>
        /// Gets or sets the default document names.
        /// </summary>
        IList<string> DefaultDocuments { get; set; }

        /// <summary>
        /// Processes an HTTP request for this virtual host.
        /// </summary>
        /// <param name="context">The HTTP context for the request.</param>
        /// <returns>True if the request was handled, false otherwise.</returns>
        Task<bool> ProcessRequestAsync(HttpContext context);
        
        /// <summary>
        /// Adds a middleware to the processing pipeline for this host.
        /// </summary>
        /// <param name="middleware">The middleware function.</param>
        void UseMiddleware(Func<HttpContext, Func<Task>, Task> middleware);
    }
}
