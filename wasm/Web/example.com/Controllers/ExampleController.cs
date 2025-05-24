using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.Example
{
    [Host("example.com")]
    public class ExampleController : BaseController
    {
        public ExampleController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "Example Website" };
            return View("Example/Index", model);
        }
    }
}
