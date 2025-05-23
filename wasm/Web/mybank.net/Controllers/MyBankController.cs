using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.MyBank
{
    [Host("mybank.net")]
    public class MyBankController : BaseController
    {
        public MyBankController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "MyBank" };
            return View("MyBank/Index", model);
        }
    }
}
