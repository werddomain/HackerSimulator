using System;
using System.Net;
using System.Net.Security;
using System.Threading.Tasks;
using HackerOs.OS.HSystem.Text;
using HackerOs.OS.HSystem.Text.Json;

namespace HackerOs.OS.HSystem.Net.Http.Json
{
    /// <summary>
    /// Extension methods for HttpClient to support JSON operations
    /// </summary>
    public static class HttpClientJsonExtensions
    {
        /// <summary>
        /// Sends a GET request to the specified Uri and returns the value as a JSON string
        /// </summary>
        public static async Task<T?> GetFromJsonAsync<T>(this HttpClient client, string requestUri, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            // Call the client's SendAsync method without passing the cancellation token
            // This avoids the type conversion issue
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            string content = "";
            if (response.Content != null)
            {
                content = await response.Content.ReadAsStringAsync();
            }
            
            if (string.IsNullOrEmpty(content))
                return default;
                
            return JsonSerializer.Deserialize<T>(content, options ?? new JsonSerializerOptions());
        }

        /// <summary>
        /// Sends a GET request to the specified Uri and returns the value as a JSON string
        /// </summary>
        public static async Task<T?> GetFromJsonAsync<T>(this HttpClient client, Uri requestUri, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await client.GetFromJsonAsync<T>(requestUri.ToString(), cancellationToken, options);
        }

        /// <summary>
        /// Sends a POST request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpClient client, string requestUri, T value, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            var json = JsonSerializer.Serialize(value!, options ?? new JsonSerializerOptions());
            var content = new StringContent(json, "utf-8", "application/json");
            
            // Create a request message and send it without the cancellation token
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };
            return await client.SendAsync(request);
        }

        /// <summary>
        /// Sends a POST request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpClient client, Uri requestUri, T value, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await client.PostAsJsonAsync(requestUri.ToString(), value, cancellationToken, options);
        }

        /// <summary>
        /// Sends a PUT request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpClient client, string requestUri, T value, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            var json = JsonSerializer.Serialize(value!, options ?? new JsonSerializerOptions());
            var content = new StringContent(json, "utf-8", "application/json");
            
            // Create a request message and send it without the cancellation token
            var request = new HttpRequestMessage(HttpMethod.Put, requestUri) { Content = content };
            return await client.SendAsync(request);
        }

        /// <summary>
        /// Sends a PUT request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpClient client, Uri requestUri, T value, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await client.PutAsJsonAsync(requestUri.ToString(), value, cancellationToken, options);
        }

        /// <summary>
        /// Sends a PATCH request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string requestUri, T value, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            var json = JsonSerializer.Serialize(value!, options ?? new JsonSerializerOptions());
            var content = new StringContent(json, "utf-8", "application/json");
            
            // Create a request message with PATCH method and send it without the cancellation token
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri) { Content = content };
            return await client.SendAsync(request);
        }

        /// <summary>
        /// Sends a PATCH request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, Uri requestUri, T value, Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await client.PatchAsJsonAsync(requestUri.ToString(), value, cancellationToken, options);
        }
    }

    /// <summary>
    /// JSON content for HTTP requests
    /// </summary>
    public class JsonContent : HttpContent
    {
        private readonly string _jsonContent;

        /// <summary>
        /// Creates new JSON content from an object
        /// </summary>
        public JsonContent(object value, JsonSerializerOptions? options = null)
        {
            _jsonContent = JsonSerializer.Serialize(value, options ?? new JsonSerializerOptions());
            Headers.ContentType = "application/json";
        }

        /// <summary>
        /// Creates new JSON content from a JSON string
        /// </summary>
        public JsonContent(string json)
        {
            _jsonContent = json;
            Headers.ContentType = "application/json";
        }
        
        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation
        /// </summary>
        public async Task SerializeToStreamAsync(Stream stream, TransportContext? context = null)
        {
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(_jsonContent);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }
        
        /// <summary>
        /// Determines whether the HTTP content has a valid length in bytes
        /// </summary>
        public bool TryComputeLength(out long length)
        {
            length = global::System.Text.Encoding.UTF8.GetByteCount(_jsonContent);
            return true;
        }

        /// <summary>
        /// Serialize the HTTP content to a byte array as an asynchronous operation
        /// </summary>
        public override Task<byte[]> ReadAsByteArrayAsync()
        {
            return Task.FromResult(global::System.Text.Encoding.UTF8.GetBytes(_jsonContent));
        }

        /// <summary>
        /// Serialize the HTTP content to a string as an asynchronous operation
        /// </summary>
        public override Task<string> ReadAsStringAsync()
        {
            return Task.FromResult(_jsonContent);
        }

        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation
        /// </summary>
        public override Task<Stream> ReadAsStreamAsync()
        {
            var bytes = Encoding.UTF8.GetBytes(_jsonContent);
            Stream stream = new MemoryStream(bytes);
            return Task.FromResult(stream);
        }
    }
}
