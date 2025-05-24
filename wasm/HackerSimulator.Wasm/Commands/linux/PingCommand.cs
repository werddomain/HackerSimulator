using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class PingCommand : CommandBase
    {
        private readonly NetworkService _network;
        private readonly DnsService _dns;
        public PingCommand(ShellService shell, KernelService kernel, NetworkService network, DnsService dns) : base("ping", shell, kernel)
        {
            _network = network;
            _dns = dns;
        }

        public override string Description => "Send ICMP ECHO_REQUEST";
        public override string Usage => "ping <host>";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return Task.CompletedTask;
            }
            var host = args[0];
            var ip = _dns.Resolve(host) ?? host;
            var info = _network.GetHostByIp(ip);
            if (info == null || !info.IsUp)
            {
                context.Stderr.WriteLine("Host unreachable");
                return Task.CompletedTask;
            }
            context.Stdout.WriteLine($"Reply from {ip}: time={info.Latency}ms");
            return Task.CompletedTask;
        }
    }
}
