using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Web.Controllers
{
    [Host("localhost")]
    public class HomeController : BaseController
    {
        public HomeController(NetworkService network, DnsService dns) : base(network, dns) { }

        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "Home", Message = "Welcome to Hacker Simulator" };
            return View("Home/Index", model);
        }
    }
}
