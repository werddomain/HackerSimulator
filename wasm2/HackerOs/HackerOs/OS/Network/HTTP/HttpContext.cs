using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.Network.HTTP
{
    /// <summary>
    /// Represents the context of an HTTP request and response
    /// Provides access to request, response, and other contextual information
    /// </summary>
    public class HttpContext
    {
        /// <summary>
        /// Gets the HTTP request object
        /// </summary>
        public HttpRequest Request { get; }
        
        /// <summary>
        /// Gets the HTTP response object
        /// </summary>
        public HttpResponse Response { get; }
        
        /// <summary>
        /// Gets or sets the connection ID for this context
        /// </summary>
        public string ConnectionId { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when this request was received
        /// </summary>
        public DateTime RequestTimestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the user associated with this request (if authenticated)
        /// </summary>
        public User.User User { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the request has been authenticated
        /// </summary>
        public bool IsAuthenticated => User != null;
        
        /// <summary>
        /// Gets a dictionary of items that can be used to share data within the scope of this request
        /// </summary>
        public IDictionary<object, object> Items { get; } = new Dictionary<object, object>();
        
        /// <summary>
        /// Gets or sets the virtual host that received this request
        /// </summary>
        public string Host { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets a value indicating whether the connection uses HTTPS
        /// </summary>
        public bool IsHttps { get; set; }
        
        /// <summary>
        /// Creates a new instance of the HttpContext class
        /// </summary>
        public HttpContext()
        {
            Request = new HttpRequest();
            Response = new HttpResponse();
            RequestTimestamp = DateTime.UtcNow;
            ConnectionId = Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// Creates a new instance of the HttpContext class with the specified request and response
        /// </summary>
        public HttpContext(HttpRequest request, HttpResponse response)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Response = response ?? throw new ArgumentNullException(nameof(response));
            RequestTimestamp = DateTime.UtcNow;
            ConnectionId = Guid.NewGuid().ToString();
        }
          /// <summary>
        /// Authenticates the request using the provided user session
        /// </summary>
        public bool Authenticate(UserSession? session)
        {
            if (session == null || !session.IsAuthenticated())
            {
                User = null!;
                return false;
            }
            
            User = session.User;
            return true;
        }
        
        /// <summary>
        /// Gets a value from the route data, query string, or form data
        /// </summary>
        public string GetValue(string key)
        {
            // Check route data first
            if (Request.RouteData.TryGetValue(key, out var routeValue))
            {
                return routeValue?.ToString();
            }
            
            // Then query string
            if (Request.QueryParameters.TryGetValue(key, out var queryValue))
            {
                return queryValue;
            }
            
            // Then form data
            if (Request.FormData.TryGetValue(key, out var formValue))
            {
                return formValue;
            }
            
            return null;
        }
          /// <summary>
        /// Creates a response with the specified status code and content
        /// </summary>
        public async Task SetStatusCodeResultAsync(HttpStatusCode statusCode, string? content = null)
        {
            Response.StatusCode = statusCode;
            
            if (!string.IsNullOrEmpty(content))
            {
                Response.ContentType = "text/plain; charset=utf-8";
                await Response.WriteAsync(content);
            }
        }
        
        /// <summary>
        /// Redirects to the specified URL
        /// </summary>
        public void Redirect(string url, bool permanent = false)
        {
            Response.Redirect(url, permanent);
        }
        
        /// <summary>
        /// Sets an error response with the specified status code and message
        /// </summary>
        public async Task SetErrorAsync(HttpStatusCode statusCode, string errorMessage)
        {
            Response.StatusCode = statusCode;
            
            var errorHtml = $@"<!DOCTYPE html>
<html>
<head>
    <title>{(int)statusCode} {statusCode}</title>
    <style>
        body {{ font-family: 'Consolas', monospace; background-color: #0a0a0a; color: #0f0; padding: 20px; }}
        h1 {{ color: #f00; }}
        hr {{ border-color: #0f0; }}
    </style>
</head>
<body>
    <h1>{(int)statusCode} {statusCode}</h1>
    <hr/>
    <p>{errorMessage}</p>
    <hr/>
    <p>HackerOS Server</p>
</body>
</html>";
            
            Response.ContentType = "text/html; charset=utf-8";
            await Response.WriteAsync(errorHtml);
        }
    }
}
