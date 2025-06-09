using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace HackerOs.OS.System.Net.Http
{
    /// <summary>
    /// HTTP client for making HTTP requests in HackerOS
    /// </summary>
    public class HttpClient : IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// Gets or sets the base address for HTTP requests
        /// </summary>
        public Uri? BaseAddress { get; set; }

        /// <summary>
        /// Gets or sets the timeout for HTTP requests
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets the default request headers
        /// </summary>
        public HttpRequestHeaders DefaultRequestHeaders { get; } = new HttpRequestHeaders();

        /// <summary>
        /// Sends an HTTP GET request
        /// </summary>
        public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            return await SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP POST request
        /// </summary>
        public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent? content, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = content
            };
            return await SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP PUT request
        /// </summary>
        public async Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent? content, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
            {
                Content = content
            };
            return await SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP DELETE request
        /// </summary>
        public async Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
            return await SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP request
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpClient));

            // Simulate HTTP request processing
            await Task.Delay(100, cancellationToken);
            
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("Mock response content")
            };
        }

        /// <summary>
        /// Releases all resources used by the HttpClient
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// HTTP request message
    /// </summary>
    public class HttpRequestMessage : IDisposable
    {
        public HttpMethod Method { get; set; }
        public Uri? RequestUri { get; set; }
        public HttpContent? Content { get; set; }
        public HttpRequestHeaders Headers { get; } = new HttpRequestHeaders();
        public Version Version { get; set; } = new Version(1, 1);

        public HttpRequestMessage() { }
        
        public HttpRequestMessage(HttpMethod method, string? requestUri)
        {
            Method = method;
            if (requestUri != null)
                RequestUri = new Uri(requestUri, UriKind.RelativeOrAbsolute);
        }

        public HttpRequestMessage(HttpMethod method, Uri? requestUri)
        {
            Method = method;
            RequestUri = requestUri;
        }

        public void Dispose()
        {
            Content?.Dispose();
        }
    }

    /// <summary>
    /// HTTP response message
    /// </summary>
    public class HttpResponseMessage : IDisposable
    {
        public HttpStatusCode StatusCode { get; set; }
        public string? ReasonPhrase { get; set; }
        public HttpContent? Content { get; set; }
        public HttpRequestMessage? RequestMessage { get; set; }
        public HttpResponseHeaders Headers { get; } = new HttpResponseHeaders();
        public Version Version { get; set; } = new Version(1, 1);
        public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;

        public HttpResponseMessage() { }
        
        public HttpResponseMessage(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpResponseMessage EnsureSuccessStatusCode()
        {
            if (!IsSuccessStatusCode)
                throw new HttpRequestException($"Response status code does not indicate success: {(int)StatusCode} ({StatusCode})");
            return this;
        }

        public void Dispose()
        {
            Content?.Dispose();
            RequestMessage?.Dispose();
        }
    }

    /// <summary>
    /// HTTP methods
    /// </summary>
    public class HttpMethod : IEquatable<HttpMethod>
    {
        public static readonly HttpMethod Get = new HttpMethod("GET");
        public static readonly HttpMethod Post = new HttpMethod("POST");
        public static readonly HttpMethod Put = new HttpMethod("PUT");
        public static readonly HttpMethod Delete = new HttpMethod("DELETE");
        public static readonly HttpMethod Head = new HttpMethod("HEAD");
        public static readonly HttpMethod Options = new HttpMethod("OPTIONS");
        public static readonly HttpMethod Trace = new HttpMethod("TRACE");
        public static readonly HttpMethod Patch = new HttpMethod("PATCH");

        public string Method { get; }

        public HttpMethod(string method)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));
        }

        public bool Equals(HttpMethod? other)
        {
            return other != null && string.Equals(Method, other.Method, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as HttpMethod);
        }

        public override int GetHashCode()
        {
            return Method?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return Method;
        }
    }

    /// <summary>
    /// HTTP status codes
    /// </summary>
    public enum HttpStatusCode
    {
        OK = 200,
        Created = 201,
        Accepted = 202,
        NoContent = 204,
        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        NotFound = 404,
        MethodNotAllowed = 405,
        InternalServerError = 500,
        NotImplemented = 501,
        BadGateway = 502,
        ServiceUnavailable = 503
    }

    /// <summary>
    /// Base class for HTTP content
    /// </summary>
    public abstract class HttpContent : IDisposable
    {
        public HttpContentHeaders Headers { get; } = new HttpContentHeaders();

        public abstract Task<string> ReadAsStringAsync();
        public abstract Task<byte[]> ReadAsByteArrayAsync();
        public abstract Task<System.IO.Stream> ReadAsStreamAsync();

        public virtual void Dispose() { }
    }

    /// <summary>
    /// String-based HTTP content
    /// </summary>
    public class StringContent : HttpContent
    {
        private readonly string _content;
        private readonly string _mediaType;

        public StringContent(string content) : this(content, null, "text/plain") { }
        
        public StringContent(string content, string? encoding, string mediaType)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _mediaType = mediaType ?? "text/plain";
        }

        public override Task<string> ReadAsStringAsync()
        {
            return Task.FromResult(_content);
        }

        public override Task<byte[]> ReadAsByteArrayAsync()
        {
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(_content));
        }        public override Task<System.IO.Stream> ReadAsStreamAsync()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(_content);
            return Task.FromResult<System.IO.Stream>(new System.IO.MemoryStream(bytes));
        }
    }

    /// <summary>
    /// HTTP request headers
    /// </summary>
    public class HttpRequestHeaders : Dictionary<string, IEnumerable<string>>
    {
    }

    /// <summary>
    /// HTTP response headers
    /// </summary>
    public class HttpResponseHeaders : Dictionary<string, IEnumerable<string>>
    {
    }

    /// <summary>
    /// HTTP content headers
    /// </summary>
    public class HttpContentHeaders : Dictionary<string, IEnumerable<string>>
    {
    }

    /// <summary>
    /// HTTP request exception
    /// </summary>
    public class HttpRequestException : Exception
    {
        public HttpRequestException() { }
        public HttpRequestException(string message) : base(message) { }
        public HttpRequestException(string message, Exception inner) : base(message, inner) { }
    }
}
