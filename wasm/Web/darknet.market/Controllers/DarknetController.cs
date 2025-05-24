using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.Darknet
{
    [Host("darknet.market")]
    public class DarknetController : BaseController
    {
        public DarknetController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "DarkNet Market" };
            return View("Darknet/Index", model);
        }
    }
}
