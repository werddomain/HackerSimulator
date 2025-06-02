using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.HTTP
{
    /// <summary>
    /// Represents an HTTP request in the HackerOS web server
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// Gets or sets the HTTP method for this request
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.GET;
        
        /// <summary>
        /// Gets or sets the URL for this request
        /// </summary>
        public Uri Url { get; set; }
        
        /// <summary>
        /// Gets or sets the HTTP protocol version
        /// </summary>
        public string HttpVersion { get; set; } = "HTTP/1.1";
        
        /// <summary>
        /// Gets the request headers collection
        /// </summary>
        public HttpHeaderCollection Headers { get; } = new HttpHeaderCollection();
        
        /// <summary>
        /// Gets or sets the request body stream
        /// </summary>
        public Stream Body { get; set; } = new MemoryStream();
        
        /// <summary>
        /// Gets or sets the request body content as string
        /// </summary>
        public string Content
        {
            get
            {
                if (Body == null || !Body.CanRead || Body.Length == 0)
                {
                    return string.Empty;
                }
                
                // Save the original position
                var originalPosition = Body.Position;
                
                try
                {
                    // Rewind to beginning
                    Body.Position = 0;
                    
                    // Read the entire body
                    using var reader = new StreamReader(Body, Encoding.UTF8, true, 1024, true);
                    return reader.ReadToEnd();
                }
                finally
                {
                    // Restore the original position
                    Body.Position = originalPosition;
                }
            }
            set
            {
                if (Body != null && Body != Stream.Null)
                {
                    // Dispose existing stream if it's not the null stream
                    if (Body is MemoryStream)
                    {
                        Body.Dispose();
                    }
                }
                
                // Create new memory stream with the content
                var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                Body = new MemoryStream(bytes);
                
                // Update Content-Length header
                Headers[HttpHeaderCollection.CommonHeaders.ContentLength] = bytes.Length.ToString();
            }
        }
        
        /// <summary>
        /// Gets the query parameters from the URL
        /// </summary>
        public IDictionary<string, string> QueryParameters { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Gets the form data parameters (for POST requests)
        /// </summary>
        public IDictionary<string, string> FormData { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Gets the route data parameters (populated by routing middleware)
        /// </summary>
        public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Gets or sets a collection of cookies sent with the request
        /// </summary>
        public IDictionary<string, string> Cookies { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Gets or sets the client's IP address
        /// </summary>
        public string RemoteIpAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the client's port
        /// </summary>
        public int RemotePort { get; set; }
        
        /// <summary>
        /// Gets or sets the local IP address to which the request was sent
        /// </summary>
        public string LocalIpAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the local port to which the request was sent
        /// </summary>
        public int LocalPort { get; set; }
        
        /// <summary>
        /// Creates a new instance of the HttpRequest class
        /// </summary>
        public HttpRequest()
        {
            Url = new Uri("http://localhost/");
        }
        
        /// <summary>
        /// Creates a new instance of the HttpRequest class with the specified URL
        /// </summary>
        public HttpRequest(Uri url)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            ParseQueryParameters();
        }
        
        /// <summary>
        /// Creates a new instance of the HttpRequest class with the specified URL and method
        /// </summary>
        public HttpRequest(Uri url, HttpMethod method)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Method = method;
            ParseQueryParameters();
        }
        
        /// <summary>
        /// Creates a new instance of the HttpRequest class with the specified URL string
        /// </summary>
        public HttpRequest(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }
            
            // Add http:// prefix if not present
            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }
            
            Url = new Uri(url);
            ParseQueryParameters();
        }
        
        /// <summary>
        /// Parses query parameters from the URL
        /// </summary>
        private void ParseQueryParameters()
        {
            if (Url == null || string.IsNullOrEmpty(Url.Query))
            {
                return;
            }
            
            // Skip the ? character at the beginning
            var query = Url.Query.StartsWith("?") ? Url.Query.Substring(1) : Url.Query;
            
            // Split by & to get individual key-value pairs
            var pairs = query.Split('&');
            
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                
                if (keyValue.Length >= 1)
                {
                    var key = Uri.UnescapeDataString(keyValue[0]);
                    var value = keyValue.Length >= 2 ? Uri.UnescapeDataString(keyValue[1]) : string.Empty;
                    
                    QueryParameters[key] = value;
                }
            }
        }
        
        /// <summary>
        /// Parses form data from the request body (for POST requests)
        /// </summary>
        public async Task ParseFormDataAsync()
        {
            if (Method != HttpMethod.POST || Body == null || !Body.CanRead || Body.Length == 0)
            {
                return;
            }
            
            var contentType = Headers.GetHeaderValue(HttpHeaderCollection.CommonHeaders.ContentType);
            
            // Only parse if it's a form submission
            if (!string.Equals(contentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            
            // Save the original position
            var originalPosition = Body.Position;
            
            try
            {
                // Rewind to beginning
                Body.Position = 0;
                
                // Read the entire body
                using var reader = new StreamReader(Body, Encoding.UTF8, true, 1024, true);
                var formContent = await reader.ReadToEndAsync();
                
                // Parse in the same way as query parameters
                var pairs = formContent.Split('&');
                
                foreach (var pair in pairs)
                {
                    var keyValue = pair.Split('=');
                    
                    if (keyValue.Length >= 1)
                    {
                        var key = Uri.UnescapeDataString(keyValue[0]);
                        var value = keyValue.Length >= 2 ? Uri.UnescapeDataString(keyValue[1]) : string.Empty;
                        
                        FormData[key] = value;
                    }
                }
            }
            finally
            {
                // Restore the original position
                Body.Position = originalPosition;
            }
        }
        
        /// <summary>
        /// Parses cookies from the Cookie header
        /// </summary>
        public void ParseCookies()
        {
            var cookieHeader = Headers.GetHeaderValue(HttpHeaderCollection.CommonHeaders.Cookie);
            
            if (string.IsNullOrEmpty(cookieHeader))
            {
                return;
            }
            
            // Split by ; to get individual cookies
            var cookiePairs = cookieHeader.Split(';');
            
            foreach (var pair in cookiePairs)
            {
                var keyValue = pair.Split('=');
                
                if (keyValue.Length >= 2)
                {
                    var key = keyValue[0].Trim();
                    var value = keyValue[1].Trim();
                    
                    Cookies[key] = value;
                }
            }
        }
    }
}
