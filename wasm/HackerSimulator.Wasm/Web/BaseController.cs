using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Web
{
    /// <summary>
    /// Base controller emulating a minimal ASP.NET MVC style API.
    /// Routes are discovered via attributes when the controller is created.
    /// </summary>
    public abstract class BaseController
    {
        private readonly Dictionary<string, Dictionary<string, MethodInfo>> _routes = new(StringComparer.OrdinalIgnoreCase);

        protected NetworkService Network { get; }
        protected DnsService Dns { get; }

        public string Host { get; }

        protected BaseController(NetworkService network, DnsService dns)
        {
            Network = network;
            Dns = dns;

            var hostAttr = GetType().GetCustomAttribute<HostAttribute>();
            Host = !string.IsNullOrWhiteSpace(hostAttr?.Path)
                ? hostAttr!.Path
                : GetType().Name.Replace("Controller", string.Empty);

            Network.RegisterController(Host, this, Dns);
            DiscoverRoutes();
        }

        private void DiscoverRoutes()
        {
            var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                if (method.IsSpecialName) continue;
                var httpAttr = method.GetCustomAttribute<HttpMethodAttribute>();
                var httpMethod = httpAttr?.Method ?? "GET";
                var path = httpAttr?.Path;
                if (string.IsNullOrEmpty(path))
                    path = method.Name;
                if (!path.StartsWith("/"))
                    path = "/" + path;

                if (!_routes.TryGetValue(httpMethod, out var map))
                {
                    map = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
                    _routes[httpMethod] = map;
                }
                map[path] = method;
            }
        }

        public async Task<WebResponse> HandleAsync(WebRequest request)
        {
            if (_routes.TryGetValue(request.Method.ToUpperInvariant(), out var map) &&
                map.TryGetValue(request.Path, out var method))
            {
                var result = method.Invoke(this, new object[] { request });
                if (result is Task<WebResponse> task)
                    return await task;
                if (result is WebResponse resp)
                    return resp;
                return new WebResponse { Content = result?.ToString() ?? string.Empty };
            }

            return new ErrorResponse(404, $"No route for {request.Method} {request.Path}");
        }

        /// <summary>
        /// Renders a view by name with the provided model using the simple view renderer.
        /// </summary>
        protected WebResponse View(string viewName, object model)
        {
            var content = SimpleViewRenderer.Render(viewName, model);
            return new WebResponse { StatusCode = 200, Content = content, Headers = new Dictionary<string, string> { ["Content-Type"] = "text/html" } };
        }
    }
}
