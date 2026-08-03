using System.Collections.Immutable;
using HackerOs.Simulation.Abstractions.Network;

namespace HackerOs.Platform.Core.Network.Websites;

/// <summary>
/// Simulated search-engine website at hackersearch.net.
/// Mirrors the legacy browser.ts <c>getSearchEnginePage()</c> content
/// as structured typed sections instead of raw HTML strings.
/// </summary>
public sealed class HackerSearchController : SimulatedWebsiteControllerBase
{
    public override string PrimaryHostname => "hackersearch.net";
    public override string Theme          => "hacker";
    public override string SiteName       => "HackerSearch";

    public HackerSearchController()
    {
        Get("/", _ => SimulatedHttpResponse.Ok(BuildHomePage()));
        Get("/search", req =>
        {
            var query = req.Query.TryGetValue("q", out var q) ? q : "";
            return SimulatedHttpResponse.Ok(BuildSearchResultsPage(query));
        });
    }

    private static SimulatedPage BuildHomePage() => new(
        Title: "HackerSearch",
        PageTheme: "hacker",
        Sections:
        [
            new HeroSection("HackerSearch", "Search the dark corners of the web"),
            new FormSection(
                Title: "",
                Fields:
                [
                    new FormField("q", "", "search", "Search the web...", false)
                ],
                SubmitLabel: "Search",
                PostPath: "/search"),
            new NavigationSection(
            [
                new NavLink("HackMail",     "https://hackmail.com"),
                new NavLink("CryptoBank",   "https://cryptobank.com"),
                new NavLink("Hacker Forum", "https://hackerz.forum"),
            ]),
        ]);

    private static SimulatedPage BuildSearchResultsPage(string query) => new(
        Title: $"{query} - HackerSearch",
        PageTheme: "hacker",
        Sections:
        [
            new HeroSection("HackerSearch"),
            new FormSection(
                Title: "",
                Fields:
                [
                    new FormField("q", "", "search", query, false)
                ],
                SubmitLabel: "Search",
                PostPath: "/search"),
            new ListSection(
                Title: $"Search results for: {query}",
                Items:
                [
                    "hackersearch.net - No results found for this query.",
                    "Try a different search term or browse featured links.",
                ]),
        ]);
}
