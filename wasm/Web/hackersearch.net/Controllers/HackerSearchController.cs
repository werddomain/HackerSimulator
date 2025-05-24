using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.HackerSearch
{
    [Host("hackersearch.net")]
    public class HackerSearchController : BaseController
    {
        public HackerSearchController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "HackerSearch" };
            return View("HackerSearch/Index", model);
        }
    }
}
