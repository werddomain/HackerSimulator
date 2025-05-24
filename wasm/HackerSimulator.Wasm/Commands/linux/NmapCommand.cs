using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class NmapCommand : CommandBase
    {
        private readonly NetworkService _network;
        private readonly DnsService _dns;
        public NmapCommand(ShellService shell, KernelService kernel, NetworkService network, DnsService dns) : base("nmap", shell, kernel)
        {
            _network = network;
            _dns = dns;
        }

        public override string Description => "Network scanner";
        public override string Usage => "nmap <host>";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (args.Length == 0)
            {
                context.Stderr.WriteLine($"Usage: {Usage}");
                return Task.CompletedTask;
            }
            var host = args[0];
            var ip = _dns.Resolve(host) ?? host;
            var info = _network.ScanHost(ip);
            if (info == null)
            {
                context.Stderr.WriteLine("Host not found");
                return Task.CompletedTask;
            }
            context.Stdout.WriteLine($"Nmap scan report for {host} ({ip})");
            foreach (var p in info.Ports)
                context.Stdout.WriteLine($"{p.Port}/tcp\t{p.State}\t{p.Service?.Name}");
            return Task.CompletedTask;
        }
    }
}
