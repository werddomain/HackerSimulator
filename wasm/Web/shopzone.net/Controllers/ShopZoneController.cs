using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.ShopZone
{
    [Host("shopzone.net")]
    public class ShopZoneController : BaseController
    {
        public ShopZoneController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "ShopZone" };
            return View("ShopZone/Index", model);
        }
    }
}
