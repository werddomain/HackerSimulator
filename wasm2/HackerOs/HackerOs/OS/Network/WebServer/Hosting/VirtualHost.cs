using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;
using HackerOs.OS.Network.WebServer.Framework;

namespace HackerOs.OS.Network.WebServer.Hosting
{
    /// <summary>
    /// Represents a virtual host in the web server.
    /// </summary>
    public class VirtualHost : IVirtualHost
    {
        private readonly List<string> _hostAliases = new List<string>();
        private readonly List<Func<HttpContext, Func<Task>, Task>> _middlewares = new List<Func<HttpContext, Func<Task>, Task>>();
        private readonly ViewEngine _viewEngine = new ViewEngine();
        private readonly Router _router = new Router();

        /// <summary>
        /// Gets the hostname of this virtual host.
        /// </summary>
        public string Hostname { get; }

        /// <summary>
        /// Gets the list of hostnames this virtual host responds to.
        /// </summary>
        public IReadOnlyList<string> HostAliases => _hostAliases.AsReadOnly();

        /// <summary>
        /// Gets or sets the document root path for this host.
        /// </summary>
        public string DocumentRoot { get; set; }

        /// <summary>
        /// Gets a value indicating whether directory browsing is enabled.
        /// </summary>
        public bool DirectoryBrowsingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the default document names.
        /// </summary>
        public IList<string> DefaultDocuments { get; set; } = new List<string> { "index.cshtml", "default.cshtml", "index.html", "default.html", "index.htm" };

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualHost"/> class.
        /// </summary>
        /// <param name="hostname">The primary hostname for this virtual host.</param>
        public VirtualHost(string hostname)
        {
            Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            _hostAliases.Add(hostname);
        }

        /// <summary>
        /// Adds a hostname alias for this virtual host.
        /// </summary>
        /// <param name="hostnameAlias">The hostname alias to add.</param>
        public void AddHostAlias(string hostnameAlias)
        {
            if (!string.IsNullOrWhiteSpace(hostnameAlias) && !_hostAliases.Contains(hostnameAlias))
            {
                _hostAliases.Add(hostnameAlias);
            }
        }

        /// <summary>
        /// Adds a middleware to the processing pipeline for this host.
        /// </summary>
        /// <param name="middleware">The middleware function.</param>
        public void UseMiddleware(Func<HttpContext, Func<Task>, Task> middleware)
        {
            _middlewares.Add(middleware);
        }

        /// <summary>
        /// Registers a controller with this virtual host.
        /// </summary>
        /// <param name="controllerType">The controller type to register.</param>
        public void RegisterController(Type controllerType)
        {
            _router.RegisterController(controllerType);
        }        /// <summary>
        /// Registers a view with this virtual host.
        /// </summary>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="viewContent">The content of the view.</param>
        /// <param name="controllerName">Optional controller name for the view.</param>
        public void RegisterView(string viewName, string viewContent, string? controllerName = null)
        {
            _viewEngine.AddViewSource(viewName, viewContent, controllerName);
        }

        /// <summary>
        /// Sets the layout template for this virtual host.
        /// </summary>
        /// <param name="layoutContent">The layout template content.</param>
        public void SetLayout(string layoutContent)
        {
            _viewEngine.SetLayout(layoutContent);
        }

        /// <summary>
        /// Processes an HTTP request for this virtual host.
        /// </summary>
        /// <param name="context">The HTTP context for the request.</param>
        /// <returns>True if the request was handled, false otherwise.</returns>
        public async Task<bool> ProcessRequestAsync(HttpContext context)
        {
            // Add the view engine to the context
            context.Items["ViewEngine"] = _viewEngine;
            
            // Build the middleware pipeline
            var pipeline = BuildMiddlewarePipeline(0, context);
            
            // Execute the pipeline
            await pipeline();
            
            // Return true since we've processed the request
            return true;
        }

        /// <summary>
        /// Builds the middleware pipeline.
        /// </summary>
        private Func<Task> BuildMiddlewarePipeline(int index, HttpContext context)
        {
            if (index >= _middlewares.Count)
            {
                // End of middleware chain, handle with router
                return () => HandleWithRouter(context);
            }

            var current = _middlewares[index];
            var next = BuildMiddlewarePipeline(index + 1, context);
            
            return () => current(context, next);
        }

        /// <summary>
        /// Handles a request using the router.
        /// </summary>
        private async Task HandleWithRouter(HttpContext context)
        {
            // Match the route
            var route = _router.MatchRoute(
                context.Request.Url.AbsolutePath,
                context.Request.Method.ToString(),
                out var routeParameters);
            
            if (route != null)
            {
                try
                {
                    // Add route parameters to the request
                    foreach (var param in routeParameters)
                    {
                        context.Request.RouteData[param.Key] = param.Value;
                    }
                    
                    // Add controller name to context
                    context.Items["ControllerName"] = route.ControllerType.Name.Replace("Controller", "");
                    
                    // Create controller instance
                    var controller = Activator.CreateInstance(route.ControllerType);
                    
                    // Initialize controller if it implements IController
                    if (controller is IController controllerInstance)
                    {
                        controllerInstance.Initialize(context);
                    }
                    
                    // Convert route parameters to method parameters
                    var methodParams = route.ActionMethod.GetParameters();
                    var paramValues = new object[methodParams.Length];
                    
                    for (int i = 0; i < methodParams.Length; i++)
                    {
                        var param = methodParams[i];
                        
                        // Get parameter from route values
                        if (routeParameters.TryGetValue(param.Name, out var value))
                        {
                            // Convert the string value to the parameter type
                            paramValues[i] = ConvertValue(value, param.ParameterType);
                        }
                        else
                        {
                            // Use default value
                            paramValues[i] = param.DefaultValue;
                        }
                    }
                    
                    // Invoke the action method
                    var result = route.ActionMethod.Invoke(controller, paramValues);
                    
                    // Handle async results
                    if (result is Task<IActionResult> taskResult)
                    {
                        result = await taskResult;
                    }
                    
                    // Execute the action result
                    if (result is IActionResult actionResult)
                    {
                        await actionResult.ExecuteResultAsync(context);
                    }
                }
                catch (Exception ex)
                {
                    // Handle controller errors
                    context.Response.StatusCode = HttpStatusCode.InternalServerError;
                    await context.Response.WriteAsync($"Error processing request: {ex.Message}");
                }
            }
            else
            {
                // Try to serve a static file
                if (!await TryServeStaticFileAsync(context))
                {
                    // Return 404 if no route matches and no static file exists
                    context.Response.StatusCode = HttpStatusCode.NotFound;
                    await context.Response.WriteAsync("Resource not found");
                }
            }
        }

        /// <summary>
        /// Tries to serve a static file for the request.
        /// </summary>
        private async Task<bool> TryServeStaticFileAsync(HttpContext context)
        {
            if (string.IsNullOrEmpty(DocumentRoot))
                return false;
            
            var path = context.Request.Url.AbsolutePath.TrimStart('/');
            var filePath = Path.Combine(DocumentRoot, path);
            
            // Check if it's a directory
            if (Directory.Exists(filePath))
            {
                // Try to serve a default document
                foreach (var defaultDoc in DefaultDocuments)
                {
                    var defaultPath = Path.Combine(filePath, defaultDoc);
                    if (File.Exists(defaultPath))
                    {
                        return await ServeFileAsync(context, defaultPath);
                    }
                }
                
                // Serve directory listing if enabled
                if (DirectoryBrowsingEnabled)
                {
                    return await ServeDirectoryListingAsync(context, filePath, path);
                }
                
                return false;
            }
            
            // Check for CSHTML version first (prioritize over HTML)
            var filePathWithoutExt = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
            var csHtmlPath = filePathWithoutExt + ".cshtml";
            
            if (File.Exists(csHtmlPath))
            {
                return await ServeFileAsync(context, csHtmlPath);
            }
            
            // Check if the original file exists
            if (File.Exists(filePath))
            {
                return await ServeFileAsync(context, filePath);
            }
            
            return false;
        }

        /// <summary>
        /// Serves a file to the response.
        /// </summary>
        private async Task<bool> ServeFileAsync(HttpContext context, string filePath)
        {
            try
            {
                // Determine content type based on file extension
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var contentType = GetMimeTypeForExtension(extension);
                
                // Set content type
                context.Response.ContentType = contentType;
                
                // Process CSHTML files through the view engine
                if (extension == ".cshtml")
                {
                    var fileContent = await File.ReadAllTextAsync(filePath);
                    
                    // Default to an empty model if none provided
                    var model = new { };
                    
                    // Process the CSHTML file using the view engine
                    var processedContent = await _viewEngine.RenderTemplateAsync(fileContent, model);
                    await context.Response.WriteAsync(processedContent);
                    
                    return true;
                }
                
                // For other file types, serve directly
                var bytes = await File.ReadAllBytesAsync(filePath);
                await context.Response.WriteAsync(bytes, 0, bytes.Length);
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Serves a directory listing to the response.
        /// </summary>
        private async Task<bool> ServeDirectoryListingAsync(HttpContext context, string dirPath, string relativePath)
        {
            try
            {
                var sb = new global::System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html>");
                sb.AppendLine("<head>");
                sb.AppendLine($"<title>Directory Listing: /{relativePath}</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
                sb.AppendLine("h1 { color: #333; }");
                sb.AppendLine("ul { list-style-type: none; padding: 0; }");
                sb.AppendLine("li { margin: 5px 0; }");
                sb.AppendLine("a { text-decoration: none; color: #0366d6; }");
                sb.AppendLine("a:hover { text-decoration: underline; }");
                sb.AppendLine(".folder { font-weight: bold; }");
                sb.AppendLine("</style>");
                sb.AppendLine("</head>");
                sb.AppendLine("<body>");
                sb.AppendLine($"<h1>Directory Listing: /{relativePath}</h1>");
                sb.AppendLine("<ul>");

                // Add parent directory link if not in root
                if (!string.IsNullOrEmpty(relativePath))
                {
                    var parentPath = Path.GetDirectoryName(relativePath.TrimEnd('/'))?.Replace('\\', '/') ?? "";
                    sb.AppendLine($"<li><a href=\"/{parentPath}\">..</a></li>");
                }

                // Add directories
                foreach (var dir in Directory.GetDirectories(dirPath))
                {
                    var dirName = Path.GetFileName(dir);
                    var dirUrl = Path.Combine(relativePath, dirName).Replace('\\', '/');
                    sb.AppendLine($"<li><a class=\"folder\" href=\"/{dirUrl}/\">{dirName}/</a></li>");
                }

                // Add files
                foreach (var file in Directory.GetFiles(dirPath))
                {
                    var fileName = Path.GetFileName(file);
                    var fileUrl = Path.Combine(relativePath, fileName).Replace('\\', '/');
                    sb.AppendLine($"<li><a href=\"/{fileUrl}\">{fileName}</a></li>");
                }

                sb.AppendLine("</ul>");
                sb.AppendLine("</body>");
                sb.AppendLine("</html>");

                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(sb.ToString());
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the MIME type for a file extension.
        /// </summary>
        private string GetMimeTypeForExtension(string extension)
        {
            return extension switch
            {
                ".html" => "text/html",
                ".htm" => "text/html",
                ".cshtml" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Converts a string value to the specified type.
        /// </summary>
        private object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value;
                
            if (targetType == typeof(int) || targetType == typeof(int?))
                return int.TryParse(value, out var intValue) ? intValue : default(int?);
                
            if (targetType == typeof(long) || targetType == typeof(long?))
                return long.TryParse(value, out var longValue) ? longValue : default(long?);
                
            if (targetType == typeof(bool) || targetType == typeof(bool?))
                return bool.TryParse(value, out var boolValue) ? boolValue : default(bool?);
                
            if (targetType == typeof(Guid) || targetType == typeof(Guid?))
                return Guid.TryParse(value, out var guidValue) ? guidValue : default(Guid?);
                
            // Handle more types as needed
                
            return null;
        }
    }
}
