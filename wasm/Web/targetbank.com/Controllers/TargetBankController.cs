using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.TargetBank
{
    [Host("targetbank.com")]
    public class TargetBankController : BaseController
    {
        public TargetBankController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "TargetBank" };
            return View("TargetBank/Index", model);
        }
    }
}
