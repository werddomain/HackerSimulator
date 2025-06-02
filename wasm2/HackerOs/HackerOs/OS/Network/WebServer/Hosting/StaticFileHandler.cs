using System;
using System.IO;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Hosting
{
    /// <summary>
    /// Middleware for serving static files.
    /// </summary>
    public class StaticFileHandler
    {
        private readonly string _rootPath;
        private readonly string _urlPrefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticFileHandler"/> class.
        /// </summary>
        /// <param name="rootPath">The root directory path for static files.</param>
        /// <param name="urlPrefix">The URL prefix for static files.</param>
        public StaticFileHandler(string rootPath, string urlPrefix = "/static")
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _urlPrefix = urlPrefix?.TrimEnd('/') ?? "";
        }

        /// <summary>
        /// Gets a middleware function for handling static files.
        /// </summary>
        /// <returns>A middleware function.</returns>
        public Func<HttpContext, Func<Task>, Task> GetMiddleware()
        {
            return async (context, next) =>
            {
                var path = context.Request.Path;
                
                // Check if this request is for a static file
                if (path.StartsWith(_urlPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Remove the prefix from the path
                    var relativePath = path.Substring(_urlPrefix.Length).TrimStart('/');
                    var filePath = Path.Combine(_rootPath, relativePath);
                    
                    // Prevent directory traversal attacks
                    var normalizedFilePath = Path.GetFullPath(filePath);
                    var normalizedRootPath = Path.GetFullPath(_rootPath);
                    
                    if (normalizedFilePath.StartsWith(normalizedRootPath) && File.Exists(normalizedFilePath))
                    {
                        await ServeFileAsync(context, normalizedFilePath);
                        return;
                    }
                }
                
                // Not a static file request or file not found, continue to next middleware
                await next();
            };
        }

        /// <summary>
        /// Serves a file to the response.
        /// </summary>
        private async Task ServeFileAsync(HttpContext context, string filePath)
        {
            try
            {
                // Determine content type based on file extension
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var contentType = GetMimeTypeForExtension(extension);
                
                // Set content type
                context.Response.ContentType = contentType;
                
                // Add caching headers
                context.Response.Headers["Cache-Control"] = "public, max-age=86400";
                
                // Read and send the file
                var fileContent = await File.ReadAllBytesAsync(filePath);
                await context.Response.WriteBytesAsync(fileContent);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = HttpStatusCode.InternalServerError;
                await context.Response.WriteAsync($"Error serving file: {ex.Message}");
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
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".eot" => "application/vnd.ms-fontobject",
                ".otf" => "font/otf",
                _ => "application/octet-stream"
            };
        }
    }
}
