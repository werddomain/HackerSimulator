using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Interface for action results returned by controller actions
    /// </summary>
    public interface IActionResult
    {
        /// <summary>
        /// Executes the result operation by writing to the response
        /// </summary>
        Task ExecuteResultAsync(HttpContext context);
    }
}
