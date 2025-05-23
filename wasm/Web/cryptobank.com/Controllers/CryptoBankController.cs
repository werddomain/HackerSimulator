using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Web.Sites.CryptoBank
{
    [Host("cryptobank.com")]
    public class CryptoBankController : BaseController
    {
        public CryptoBankController(NetworkService network, DnsService dns) : base(network, dns) { }

        [Get("/")]
        public WebResponse Index(WebRequest request)
        {
            var model = new { Title = "CryptoBank" };
            return View("CryptoBank/Index", model);
        }
    }
}
