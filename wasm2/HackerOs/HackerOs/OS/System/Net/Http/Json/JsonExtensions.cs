using System;
using System.Net;
using System.Net.Security;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.System.Net.Http;

namespace HackerOs.OS.System.Net.Http.Json
{
    /// <summary>
    /// Extension methods for HttpClient to support JSON operations
    /// </summary>
    public static class HttpClientJsonExtensions
    {
        /// <summary>
        /// Sends a GET request to the specified Uri and returns the value as a JSON string
        /// </summary>
        public static async Task<T?> GetFromJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, string requestUri, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            global::System.Threading.CancellationToken systemToken = cancellationToken.GetSystemToken();
            var response = await client.GetAsync(requestUri, systemToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
                return default(T);
                
            return JsonSerializer.Deserialize<T>(content, options);
        }

        /// <summary>
        /// Sends a GET request to the specified Uri and returns the value as a JSON string
        /// </summary>
        public static async Task<T?> GetFromJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, Uri requestUri, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await GetFromJsonAsync<T>(client, requestUri.ToString(), cancellationToken, options);
        }

        /// <summary>
        /// Sends a POST request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, string requestUri, T value, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            var json = JsonSerializer.Serialize(value, options);
            var content = new StringContent(json, "utf-8", "application/json");
            global::System.Threading.CancellationToken systemToken = cancellationToken.GetSystemToken();
            return await client.PostAsync(requestUri, content, systemToken);
        }

        /// <summary>
        /// Sends a POST request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, Uri requestUri, T value, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await PostAsJsonAsync(client, requestUri.ToString(), value, cancellationToken, options);
        }

        /// <summary>
        /// Sends a PUT request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PutAsJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, string requestUri, T value, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            var json = JsonSerializer.Serialize(value, options);
            var content = new StringContent(json, "utf-8", "application/json");
            global::System.Threading.CancellationToken systemToken = cancellationToken.GetSystemToken();
            return await client.PutAsync(requestUri, content, systemToken);
        }

        /// <summary>
        /// Sends a PUT request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PutAsJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, Uri requestUri, T value, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await PutAsJsonAsync(client, requestUri.ToString(), value, cancellationToken, options);
        }

        /// <summary>
        /// Sends a PATCH request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, string requestUri, T value, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            var json = JsonSerializer.Serialize(value, options);
            var content = new StringContent(json, "utf-8", "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri)
            {
                Content = content
            };
            global::System.Threading.CancellationToken systemToken = cancellationToken.GetSystemToken();
            return await client.SendAsync(request, systemToken);
        }

        /// <summary>
        /// Sends a PATCH request to the specified Uri containing the value serialized as JSON
        /// </summary>
        public static async Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HackerOs.OS.System.Net.Http.HttpClient client, Uri requestUri, T value, HackerOs.OS.System.Threading.CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
        {
            return await PatchAsJsonAsync(client, requestUri.ToString(), value, cancellationToken, options);
        }
    }

    /// <summary>
    /// JSON content for HTTP requests
    /// </summary>
    public class JsonContent : HackerOs.OS.System.Net.Http.HttpContent
    {
        private readonly string _jsonContent;

        /// <summary>
        /// Creates new JSON content from an object
        /// </summary>
        public JsonContent(object value, JsonSerializerOptions? options = null)
        {
            _jsonContent = JsonSerializer.Serialize(value, options);
            Headers.ContentType = "application/json";
        }

        /// <summary>
        /// Creates new JSON content from a JSON string
        /// </summary>
        public JsonContent(string json)
        {
            _jsonContent = json;
            Headers.ContentType = "application/json";
        }        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation
        /// </summary>
        public async Task SerializeToStreamAsync(global::System.IO.Stream stream, TransportContext? context = null)
        {
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(_jsonContent);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }/// <summary>
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
        public override Task<global::System.IO.Stream> ReadAsStreamAsync()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(_jsonContent);
            global::System.IO.Stream stream = new global::System.IO.MemoryStream(bytes);
            return Task.FromResult(stream);
        }
    }
}
