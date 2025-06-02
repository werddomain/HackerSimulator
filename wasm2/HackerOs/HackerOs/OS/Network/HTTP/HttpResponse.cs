using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.HTTP
{
    /// <summary>
    /// Represents an HTTP response in the HackerOS web server
    /// </summary>
    public class HttpResponse
    {
        private readonly MemoryStream _bodyStream;
        private readonly JsonSerializerOptions _jsonOptions;
        
        /// <summary>
        /// Gets or sets the HTTP status code for this response
        /// </summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        
        /// <summary>
        /// Gets or sets the HTTP protocol version
        /// </summary>
        public string HttpVersion { get; set; } = "HTTP/1.1";
        
        /// <summary>
        /// Gets the response headers collection
        /// </summary>
        public HttpHeaderCollection Headers { get; } = new HttpHeaderCollection();
        
        /// <summary>
        /// Gets the response body stream
        /// </summary>
        public Stream Body => _bodyStream;
        
        /// <summary>
        /// Gets or sets the content type header
        /// </summary>
        public string ContentType
        {
            get => Headers.GetHeaderValue(HttpHeaderCollection.CommonHeaders.ContentType);
            set => Headers[HttpHeaderCollection.CommonHeaders.ContentType] = value;
        }
        
        /// <summary>
        /// Gets or sets the response cookies
        /// </summary>
        public Dictionary<string, string> Cookies { get; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Creates a new instance of the HttpResponse class
        /// </summary>
        public HttpResponse()
        {
            _bodyStream = new MemoryStream();
            
            // Set default headers
            Headers[HttpHeaderCollection.CommonHeaders.Server] = "HackerOS/1.0";
            Headers[HttpHeaderCollection.CommonHeaders.Date] = DateTime.UtcNow.ToString("R");
            
            // Set up JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
        
        /// <summary>
        /// Writes a string to the response body
        /// </summary>
        public async Task WriteAsync(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }
            
            var bytes = Encoding.UTF8.GetBytes(content);
            await _bodyStream.WriteAsync(bytes, 0, bytes.Length);
            
            // Update Content-Length header
            Headers[HttpHeaderCollection.CommonHeaders.ContentLength] = _bodyStream.Length.ToString();
        }
        
        /// <summary>
        /// Writes bytes to the response body
        /// </summary>
        public async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            if (buffer == null || count == 0)
            {
                return;
            }
            
            await _bodyStream.WriteAsync(buffer, offset, count);
            
            // Update Content-Length header
            Headers[HttpHeaderCollection.CommonHeaders.ContentLength] = _bodyStream.Length.ToString();
        }
        
        /// <summary>
        /// Writes an object as JSON to the response body
        /// </summary>
        public async Task WriteJsonAsync(object value)
        {
            if (value == null)
            {
                await WriteAsync("null");
                return;
            }
            
            // Set content type to JSON
            ContentType = "application/json; charset=utf-8";
            
            // Serialize object to JSON
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await WriteAsync(json);
        }
        
        /// <summary>
        /// Adds a cookie to the response
        /// </summary>
        public void SetCookie(string name, string value, CookieOptions options = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Cookie name cannot be null or empty", nameof(name));
            }
            
            var cookieValue = $"{name}={value}";
            
            if (options != null)
            {
                if (options.Expires.HasValue)
                {
                    cookieValue += $"; Expires={options.Expires.Value.ToUniversalTime():R}";
                }
                
                if (options.MaxAge.HasValue)
                {
                    cookieValue += $"; Max-Age={options.MaxAge.Value.TotalSeconds}";
                }
                
                if (!string.IsNullOrEmpty(options.Domain))
                {
                    cookieValue += $"; Domain={options.Domain}";
                }
                
                if (!string.IsNullOrEmpty(options.Path))
                {
                    cookieValue += $"; Path={options.Path}";
                }
                
                if (options.Secure)
                {
                    cookieValue += "; Secure";
                }
                
                if (options.HttpOnly)
                {
                    cookieValue += "; HttpOnly";
                }
            }
            
            Headers.AddHeader(HttpHeaderCollection.CommonHeaders.SetCookie, cookieValue);
            Cookies[name] = value;
        }
        
        /// <summary>
        /// Redirects to a URL by setting the Location header and status code
        /// </summary>
        public void Redirect(string url, bool permanent = false)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }
            
            Headers[HttpHeaderCollection.CommonHeaders.Location] = url;
            StatusCode = permanent ? HttpStatusCode.MovedPermanently : HttpStatusCode.Found;
        }
        
        /// <summary>
        /// Gets the response as a string, including headers and body
        /// </summary>
        public async Task<string> GetFullResponseAsync()
        {
            var builder = new StringBuilder();
            
            // Status line
            builder.Append(HttpVersion);
            builder.Append(' ');
            builder.Append((int)StatusCode);
            builder.Append(' ');
            builder.Append(StatusCode.ToString());
            builder.Append("\r\n");
            
            // Update Content-Length header if not set
            if (!Headers.ContainsHeader(HttpHeaderCollection.CommonHeaders.ContentLength))
            {
                Headers[HttpHeaderCollection.CommonHeaders.ContentLength] = _bodyStream.Length.ToString();
            }
            
            // Headers
            builder.Append(Headers.ToString());
            
            // Empty line after headers
            builder.Append("\r\n");
            
            // Body
            if (_bodyStream.Length > 0)
            {
                _bodyStream.Position = 0;
                using var reader = new StreamReader(_bodyStream, Encoding.UTF8, true, 1024, true);
                builder.Append(await reader.ReadToEndAsync());
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Resets the response to its initial state
        /// </summary>
        public void Reset()
        {
            StatusCode = HttpStatusCode.OK;
            Headers.Clear();
            Cookies.Clear();
            
            // Reset body stream
            _bodyStream.SetLength(0);
            
            // Reset default headers
            Headers[HttpHeaderCollection.CommonHeaders.Server] = "HackerOS/1.0";
            Headers[HttpHeaderCollection.CommonHeaders.Date] = DateTime.UtcNow.ToString("R");
        }
    }
    
    /// <summary>
    /// Options for cookies set in HTTP responses
    /// </summary>
    public class CookieOptions
    {
        /// <summary>
        /// Gets or sets the expiration date for the cookie
        /// </summary>
        public DateTime? Expires { get; set; }
        
        /// <summary>
        /// Gets or sets the max age for the cookie in seconds
        /// </summary>
        public TimeSpan? MaxAge { get; set; }
        
        /// <summary>
        /// Gets or sets the domain for the cookie
        /// </summary>
        public string Domain { get; set; }
        
        /// <summary>
        /// Gets or sets the path for the cookie
        /// </summary>
        public string Path { get; set; } = "/";
        
        /// <summary>
        /// Gets or sets whether the cookie should only be transmitted over secure connections
        /// </summary>
        public bool Secure { get; set; }
        
        /// <summary>
        /// Gets or sets whether the cookie should only be accessible via HTTP(S) requests
        /// </summary>
        public bool HttpOnly { get; set; } = true;
    }
}
