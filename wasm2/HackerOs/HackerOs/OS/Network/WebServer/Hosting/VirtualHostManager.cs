using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Hosting
{
    /// <summary>
    /// Manages virtual hosts for the web server.
    /// </summary>
    public class VirtualHostManager
    {
        private readonly Dictionary<string, IVirtualHost> _hosts = new Dictionary<string, IVirtualHost>(StringComparer.OrdinalIgnoreCase);
        private IVirtualHost _defaultHost;

        /// <summary>
        /// Adds a virtual host to the manager.
        /// </summary>
        /// <param name="host">The virtual host to add.</param>
        public void AddHost(IVirtualHost host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            
            // Add the host for each of its hostnames
            foreach (var hostname in host.HostAliases)
            {
                _hosts[hostname] = host;
            }
            
            // If this is the first host, make it the default
            if (_defaultHost == null)
            {
                _defaultHost = host;
            }
        }

        /// <summary>
        /// Sets the default virtual host.
        /// </summary>
        /// <param name="host">The virtual host to set as default.</param>
        public void SetDefaultHost(IVirtualHost host)
        {
            _defaultHost = host ?? throw new ArgumentNullException(nameof(host));
            
            // Make sure the host is registered
            if (host.HostAliases.All(h => !_hosts.ContainsKey(h)))
            {
                AddHost(host);
            }
        }

        /// <summary>
        /// Gets a virtual host by hostname.
        /// </summary>
        /// <param name="hostname">The hostname to look up.</param>
        /// <returns>The virtual host, or null if not found.</returns>
        public IVirtualHost GetHost(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return _defaultHost;
            
            return _hosts.TryGetValue(hostname, out var host) ? host : _defaultHost;
        }

        /// <summary>
        /// Gets all registered virtual hosts.
        /// </summary>
        /// <returns>A collection of all virtual hosts.</returns>
        public IEnumerable<IVirtualHost> GetAllHosts()
        {
            return _hosts.Values.Distinct();
        }

        /// <summary>
        /// Processes an HTTP request by routing it to the appropriate virtual host.
        /// </summary>
        /// <param name="context">The HTTP context for the request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ProcessRequestAsync(HttpContext context)
        {
            // Extract the host from the request
            var hostname = context.Request.Headers.TryGetValue("Host", out var host)
                ? host.Split(':')[0]  // Remove port if present
                : null;
            
            // Get the appropriate virtual host
            var virtualHost = GetHost(hostname);
            
            if (virtualHost != null)
            {
                // Process the request with the virtual host
                await virtualHost.ProcessRequestAsync(context);
            }
            else
            {
                // No host found, return 404
                context.Response.StatusCode = HttpStatusCode.NotFound;
                await context.Response.WriteAsync("No virtual host configured for this hostname");
            }
        }
    }
}
