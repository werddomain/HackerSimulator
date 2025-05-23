using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.TechCorp
{
    [Host("techcorp.com")]
    public class TechCorpController : BaseController
    {
        public TechCorpController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "TechCorp" };
            return View("TechCorp/Index", model);
        }
    }
}
