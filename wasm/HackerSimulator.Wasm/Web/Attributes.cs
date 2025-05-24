using System;

namespace HackerSimulator.Wasm.Web
{
    /// <summary>
    /// Specifies the host name for a controller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HostAttribute : Attribute
    {
        public string Path { get; }
        public HostAttribute(string path = "") => Path = path;
    }

    /// <summary>
    /// Base attribute for HTTP method routing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class HttpMethodAttribute : Attribute
    {
        public string Method { get; }
        public string Path { get; }
        protected HttpMethodAttribute(string method, string path = "")
        {
            Method = method.ToUpperInvariant();
            Path = path;
        }
    }

    public class GetAttribute : HttpMethodAttribute
    {
        public GetAttribute(string path = "") : base("GET", path) { }
    }

    public class PostAttribute : HttpMethodAttribute
    {
        public PostAttribute(string path = "") : base("POST", path) { }
    }

    public class DeleteAttribute : HttpMethodAttribute
    {
        public DeleteAttribute(string path = "") : base("DELETE", path) { }
    }

    // Additional methods could be added here (Put, Patch, etc.)
}
