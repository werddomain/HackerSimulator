using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class CurlCommand : CommandBase
    {
        private readonly HackerHttpClient _http;
        public CurlCommand(ShellService shell, KernelService kernel, HackerHttpClient http) : base("curl", shell, kernel)
        {
            _http = http;
        }

        public override string Description => "Transfer a URL";
        public override string Usage => "curl <url>";

        protected override async Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return;
            }

            var url = args[0];
            try
            {
                var resp = await _http.SendAsync(url);
                context.Stdout.WriteLine(resp.Content);
            }
            catch (System.Exception ex)
            {
                context.Stderr.WriteLine($"curl: {ex.Message}");
            }
        }
    }
}
