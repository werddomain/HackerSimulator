using System;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Interface for web controllers in the HackerOS MVC framework
    /// </summary>
    public interface IController
    {
        /// <summary>
        /// Gets or sets the HTTP context for the current request
        /// </summary>
        HttpContext HttpContext { get; set; }
        
        /// <summary>
        /// Initializes the controller with the HTTP context
        /// </summary>
        void Initialize(HttpContext context);
    }
}
