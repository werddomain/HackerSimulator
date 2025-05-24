using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Web
{
    /// <summary>
    /// Simple HttpClient emulation for consuming controller based websites.
    /// </summary>
    public class HackerHttpClient
    {
        private readonly NetworkService _network;
        private readonly DnsService _dns;

        public HackerHttpClient(NetworkService network, DnsService dns)
        {
            _network = network;
            _dns = dns;
        }

        public async Task<WebResponse> SendAsync(string url, string method = "GET", Dictionary<string, string>? headers = null, string? body = null)
        {
            var uri = new Uri(url);
            var host = uri.Host;
            if (!_dns.Resolve(host)?.Equals(host) == true)
            {
                var ip = _dns.Resolve(host);
                if (ip == null)
                    throw new Exception($"Unable to resolve host {host}");
            }

            var controller = _network.GetController(host);
            if (controller == null)
                return new ErrorResponse(404, $"Host {host} not found");

            var request = new WebRequest
            {
                Method = method.ToUpperInvariant(),
                Path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath,
                Headers = headers ?? new Dictionary<string, string>(),
                Body = body
            };

            if (!string.IsNullOrEmpty(uri.Query))
            {
                foreach (var kv in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = kv.Split('=', 2);
                    request.Query[parts[0]] = parts.Length > 1 ? parts[1] : string.Empty;
                }
            }

            return await controller.HandleAsync(request);
        }
    }
}
