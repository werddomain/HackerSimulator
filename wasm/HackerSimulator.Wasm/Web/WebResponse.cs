using System.Collections.Generic;

namespace HackerSimulator.Wasm.Web
{
    /// <summary>
    /// Basic web response used by the MVC emulation.
    /// </summary>
    public class WebResponse
    {
        public int StatusCode { get; set; } = 200;
        public Dictionary<string, string> Headers { get; set; } = new();
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Convenience response for returning an error.
    /// </summary>
    public class ErrorResponse : WebResponse
    {
        public ErrorResponse(int status, string message)
        {
            StatusCode = status;
            Content = message;
        }
    }

    /// <summary>
    /// Redirect response.
    /// </summary>
    public class RedirectResponse : WebResponse
    {
        public string RedirectUrl { get; }
        public RedirectResponse(string url, bool permanent = false)
        {
            RedirectUrl = url;
            StatusCode = permanent ? 301 : 302;
            Headers["Location"] = url;
        }
    }
}
