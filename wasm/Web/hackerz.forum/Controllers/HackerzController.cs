using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.Hackerz
{
    [Host("hackerz.forum")]
    public class HackerzController : BaseController
    {
        public HackerzController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "Hackerz Forum" };
            return View("Hackerz/Index", model);
        }
    }
}
