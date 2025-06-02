using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HackerOs.OS.Network.HTTP
{
    /// <summary>
    /// Represents a collection of HTTP headers with case-insensitive keys
    /// </summary>
    public class HttpHeaderCollection
    {
        private readonly Dictionary<string, List<string>> _headers;

        /// <summary>
        /// Initializes a new instance of the HttpHeaderCollection class
        /// </summary>
        public HttpHeaderCollection()
        {
            _headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the value of a header by name
        /// Setting a header replaces any existing values
        /// </summary>
        public string this[string name]
        {
            get => GetHeaderValue(name);
            set => SetHeader(name, value);
        }

        /// <summary>
        /// Gets all header names in the collection
        /// </summary>
        public IEnumerable<string> Names => _headers.Keys;

        /// <summary>
        /// Gets the number of headers in the collection
        /// </summary>
        public int Count => _headers.Count;

        /// <summary>
        /// Determines whether the collection contains a specific header
        /// </summary>
        public bool ContainsHeader(string name)
        {
            return _headers.ContainsKey(name);
        }

        /// <summary>
        /// Gets all values for a specific header
        /// </summary>
        public IEnumerable<string> GetHeaderValues(string name)
        {
            if (_headers.TryGetValue(name, out var values))
            {
                return values.ToList(); // Return a copy to prevent modification
            }
            
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the first value for a specific header
        /// </summary>
        public string GetHeaderValue(string name)
        {
            if (_headers.TryGetValue(name, out var values) && values.Count > 0)
            {
                return values[0];
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Sets a header value, replacing any existing values
        /// </summary>
        public void SetHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Header name cannot be null or empty", nameof(name));
            }

            var values = new List<string>();
            
            if (!string.IsNullOrEmpty(value))
            {
                values.Add(value);
            }
            
            _headers[name] = values;
        }

        /// <summary>
        /// Adds a value to a header, preserving any existing values
        /// </summary>
        public void AddHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Header name cannot be null or empty", nameof(name));
            }

            if (!_headers.TryGetValue(name, out var values))
            {
                values = new List<string>();
                _headers[name] = values;
            }
            
            if (!string.IsNullOrEmpty(value))
            {
                values.Add(value);
            }
        }

        /// <summary>
        /// Removes a header from the collection
        /// </summary>
        public bool RemoveHeader(string name)
        {
            return _headers.Remove(name);
        }

        /// <summary>
        /// Clears all headers from the collection
        /// </summary>
        public void Clear()
        {
            _headers.Clear();
        }

        /// <summary>
        /// Returns the collection as a string in HTTP header format
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            
            foreach (var header in _headers)
            {
                foreach (var value in header.Value)
                {
                    builder.Append(header.Key);
                    builder.Append(": ");
                    builder.Append(value);
                    builder.Append("\r\n");
                }
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Common HTTP header names as constants
        /// </summary>
        public static class CommonHeaders
        {
            public const string Accept = "Accept";
            public const string AcceptCharset = "Accept-Charset";
            public const string AcceptEncoding = "Accept-Encoding";
            public const string AcceptLanguage = "Accept-Language";
            public const string Authorization = "Authorization";
            public const string CacheControl = "Cache-Control";
            public const string Connection = "Connection";
            public const string ContentEncoding = "Content-Encoding";
            public const string ContentLanguage = "Content-Language";
            public const string ContentLength = "Content-Length";
            public const string ContentLocation = "Content-Location";
            public const string ContentType = "Content-Type";
            public const string Cookie = "Cookie";
            public const string Date = "Date";
            public const string Expect = "Expect";
            public const string Expires = "Expires";
            public const string From = "From";
            public const string Host = "Host";
            public const string IfMatch = "If-Match";
            public const string IfModifiedSince = "If-Modified-Since";
            public const string IfNoneMatch = "If-None-Match";
            public const string IfRange = "If-Range";
            public const string IfUnmodifiedSince = "If-Unmodified-Since";
            public const string LastModified = "Last-Modified";
            public const string Location = "Location";
            public const string MaxForwards = "Max-Forwards";
            public const string Pragma = "Pragma";
            public const string ProxyAuthenticate = "Proxy-Authenticate";
            public const string ProxyAuthorization = "Proxy-Authorization";
            public const string Range = "Range";
            public const string Referer = "Referer";
            public const string RetryAfter = "Retry-After";
            public const string Server = "Server";
            public const string SetCookie = "Set-Cookie";
            public const string Te = "TE";
            public const string Trailer = "Trailer";
            public const string TransferEncoding = "Transfer-Encoding";
            public const string Upgrade = "Upgrade";
            public const string UserAgent = "User-Agent";
            public const string Vary = "Vary";
            public const string Via = "Via";
            public const string Warning = "Warning";
            public const string WwwAuthenticate = "WWW-Authenticate";
        }
    }
}
