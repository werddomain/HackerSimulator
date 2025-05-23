using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.HackMail
{
    [Host("hackmail.com")]
    public class HackMailController : BaseController
    {
        public HackMailController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "HackMail" };
            return View("HackMail/Index", model);
        }
    }
}
