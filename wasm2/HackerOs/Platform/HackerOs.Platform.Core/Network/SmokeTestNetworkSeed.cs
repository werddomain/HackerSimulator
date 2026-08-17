using System.Collections.Immutable;
using HackerOs.Simulation.Abstractions.Network;

namespace HackerOs.Platform.Core.Network;

/// <summary>
/// Minimal, explicitly-labeled seed data for the simulated network (ADR 0034) — just enough for
/// <c>curl</c>/<c>ping</c>/<c>nmap</c> to be smoke-tested end-to-end now that they're wired into the
/// app catalog. This is deliberately not an attempt at the "Game domain" content pack ADR 0023
/// scoped separately (a full simulated internet is a game-design effort, not plumbing) — every
/// hostname here uses the <c>.hackeros</c> suffix the server-side proxy already treats as a
/// simulated-only domain (<c>ProxyService</c>'s blocked-suffix list), so it's unambiguous that
/// these hosts are smoke-test fixtures, never real network destinations.
/// </summary>
public static class SmokeTestNetworkSeed
{
    /// <summary>Gets the seeded hosts.</summary>
    public static IReadOnlyList<SimulatedHost> Hosts { get; } =
    [
        new SimulatedHost(
            Ip: "10.0.0.10",
            Hostname: "example.hackeros",
            IsUp: true,
            LatencyMs: 12,
            Ports:
            [
                new SimulatedPort(22, SimulatedPortState.Filtered, new SimulatedPortService("ssh")),
                new SimulatedPort(80, SimulatedPortState.Open, new SimulatedPortService("http", "1.1")),
                new SimulatedPort(443, SimulatedPortState.Open, new SimulatedPortService("https", "1.1"))
            ]),
        new SimulatedHost(
            Ip: "10.0.0.20",
            Hostname: "empty.hackeros",
            IsUp: true,
            LatencyMs: 30,
            Ports: [new SimulatedPort(443, SimulatedPortState.Open, new SimulatedPortService("https"))])
    ];

    /// <summary>Gets the seeded website controllers — only <c>example.hackeros</c> serves content.</summary>
    public static IReadOnlyList<ISimulatedWebsiteController> Websites { get; } =
    [
        new ExampleWebsiteController()
    ];

    private sealed class ExampleWebsiteController : ISimulatedWebsiteController
    {
        public string PrimaryHostname => "example.hackeros";
        public IReadOnlyCollection<string> AliasHostnames => [];
        public string Theme => "hacker";
        public string SiteName => "Example (smoke test)";

        public SimulatedHttpResponse ProcessRequest(SimulatedHttpRequest request) =>
            SimulatedHttpResponse.Ok(new SimulatedPage(
                Title: "Example",
                PageTheme: Theme,
                Sections: ImmutableArray.Create<SimulatedPageSection>(
                    new HeroSection("Welcome to example.hackeros", "A minimal smoke-test host."),
                    new ParagraphSection(
                        "This page exists only to prove curl/ping/nmap work end-to-end (ADR 0034). " +
                        "It is not real game content."))));
    }
}
