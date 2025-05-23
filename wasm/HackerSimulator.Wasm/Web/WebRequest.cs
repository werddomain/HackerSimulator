using System.Collections.Generic;

namespace HackerSimulator.Wasm.Web
{
    /// <summary>
    /// Simplified web request representation used by the MVC emulation.
    /// </summary>
    public class WebRequest
    {
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = "/";
        public Dictionary<string, string> Query { get; set; } = new();
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? Body { get; set; }
    }
}
