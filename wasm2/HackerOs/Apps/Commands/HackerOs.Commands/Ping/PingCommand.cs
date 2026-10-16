using System.Globalization;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Net;

namespace HackerOs.Commands.Ping;

/// <summary>
/// Simulated <c>ping</c> terminal command (P4-W4-006): four echo requests through
/// <see cref="HackPing"/>, then the usual statistics block. Resolution and the real-network
/// fallback (ADR 0028) belong to the OS (ADR 0056, section 6): a host outside the simulated
/// network is reached through the optional server only when the OS can, otherwise the name simply
/// does not resolve.
/// </summary>
public sealed class PingCommand : TerminalAppBase
{
    private const int EchoCount = 4;

    /// <summary>Initializes the command with its manifest.</summary>
    public PingCommand(AppManifest manifest) : base(manifest)
    {
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine("ping: usage: ping <hostname|ip>");
            return 1;
        }

        string target = context.Arguments[0];
        List<HackPingReply> replies = [];
        for (int sequence = 1; sequence <= EchoCount; sequence++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            HackPingReply reply;
            try
            {
                reply = await HackPing.SendAsync(target, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (HackPingException)
            {
                context.StandardOutput.WriteLine($"ping: cannot resolve '{target}': Name or service not known");
                return 2;
            }

            if (sequence == 1)
            {
                context.StandardOutput.WriteLine(reply.Address is null
                    ? $"PING {target} 56(84) bytes of data."
                    : $"PING {target} ({reply.Address}) 56(84) bytes of data.");
            }

            replies.Add(reply);
            string from = reply.Address?.ToString() ?? target;
            switch (reply.Status)
            {
                case HackIPStatus.Success:
                    context.StandardOutput.WriteLine(
                        $"64 bytes from {from}: icmp_seq={sequence} ttl={reply.Ttl} time={FormatMilliseconds(reply.RoundtripTime)} ms");
                    break;
                case HackIPStatus.DestinationHostUnreachable:
                    context.StandardOutput.WriteLine($"From {from} icmp_seq={sequence} Destination Host Unreachable");
                    break;
                default:
                    context.StandardOutput.WriteLine($"Request timeout for icmp_seq {sequence}");
                    break;
            }
        }

        return WriteStatistics(context, target, replies);
    }

    /// <summary>Writes the summary <c>ping</c> prints on exit and returns its status: 0 if any reply came back.</summary>
    private static int WriteStatistics(TerminalExecutionContext context, string target, IReadOnlyList<HackPingReply> replies)
    {
        double[] times = [.. replies.Where(r => r.Status == HackIPStatus.Success).Select(r => r.RoundtripTime.TotalMilliseconds)];
        int lossPercent = replies.Count == 0 ? 100 : (int)Math.Round(100.0 * (replies.Count - times.Length) / replies.Count);

        context.StandardOutput.WriteLine();
        context.StandardOutput.WriteLine($"--- {target} ping statistics ---");
        context.StandardOutput.WriteLine(
            $"{replies.Count} packets transmitted, {times.Length} received, {lossPercent}% packet loss, time {Math.Max(0, replies.Count - 1) * 1001}ms");
        if (times.Length == 0)
        {
            return 1;
        }

        double average = times.Average();
        double deviation = Math.Sqrt(times.Average(t => (t - average) * (t - average)));
        context.StandardOutput.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"rtt min/avg/max/mdev = {times.Min():0.000}/{average:0.000}/{times.Max():0.000}/{deviation:0.000} ms"));
        return 0;
    }

    private static string FormatMilliseconds(TimeSpan time) =>
        Math.Round(time.TotalMilliseconds, 3).ToString(CultureInfo.InvariantCulture);
}
