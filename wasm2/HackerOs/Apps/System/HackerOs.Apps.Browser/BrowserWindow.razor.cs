using System.Collections.Immutable;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Core.Network;
using HackerOs.Simulation.Abstractions.Network;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.Apps.Browser;

/// <summary>
/// Code-behind for the Browser window application.
/// Manages URL navigation history, session cookie jar, and dispatches
/// simulated HTTP requests through <see cref="ISimulatedNetworkService"/>.
/// All browsing state is window-scoped and discarded on close.
/// </summary>
public sealed partial class BrowserWindow : WindowAppBase, IDisposable
{
    private const string HomeUrl = "https://hackersearch.net";

    /// <summary>Default bookmarks shown on the new-tab page.</summary>
    internal static readonly IReadOnlyList<(string Label, string Url)> DefaultBookmarks =
    [
        ("HackerSearch",   "https://hackersearch.net"),
        ("HackMail",       "https://hackmail.com"),
        ("CryptoBank",     "https://cryptobank.com"),
        ("DarkNet Market", "https://darknet.market"),
        ("Hacker Forum",   "https://hackerz.forum"),
    ];

    // ── Injected services ─────────────────────────────────────────────────
    [Inject] private ISimulatedNetworkService NetworkService { get; set; } = default!;

    // ── Navigation state ─────────────────────────────────────────────────
    private readonly List<string> _history = [];
    private int _historyPos = -1;

    private string _currentUrl      = string.Empty;
    private string _finalUrl        = string.Empty;
    private string _urlInputValue   = string.Empty;
    private bool   _isLoading;
    private string? _networkError;
    private SimulatedPage? _currentPage;

    // ── Per-window session cookie jar (host → name → value) ───────────────
    private readonly Dictionary<string, Dictionary<string, string>> _cookieJar = [];

    // ── Computed helpers ──────────────────────────────────────────────────
    private bool CanGoBack    => _historyPos > 0;
    private bool CanGoForward => _historyPos < _history.Count - 1;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnAppInitialized()
    {
        // Navigate home asynchronously after first render
    }

    /// <inheritdoc/>
    protected override async Task OnAppAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await NavigateToAsync(HomeUrl);
    }

    // ── Navigation commands ───────────────────────────────────────────────

    /// <summary>Navigates to the given URL string.</summary>
    internal async Task NavigateToAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        _currentUrl    = NormalizeUrl(url);
        _urlInputValue = _currentUrl;
        _isLoading     = true;
        _networkError  = null;
        _currentPage   = null;
        StateHasChanged();

        // Simulate network delay on a background thread
        await Task.Run(() =>
        {
            var result = NetworkService.Navigate(_currentUrl, _cookieJar);
            InvokeAsync(() =>
            {
                _isLoading = false;
                if (result.IsSuccess)
                {
                    _finalUrl    = result.FinalUrl;
                    _currentPage = result.Page;
                    _urlInputValue = _finalUrl;
                    PushHistory(_finalUrl);
                }
                else
                {
                    _networkError = result.NetworkError;
                    _finalUrl     = _currentUrl;
                }
                StateHasChanged();
            });
        });
    }

    private async Task NavigateToCurrentInputAsync() =>
        await NavigateToAsync(_urlInputValue);

    private async Task NavigateHomeAsync() =>
        await NavigateToAsync(HomeUrl);

    private async Task RefreshAsync() =>
        await NavigateToAsync(_currentUrl);

    private async Task GoBack()
    {
        if (!CanGoBack) return;
        _historyPos--;
        await NavigateToAsync(_history[_historyPos]);
    }

    private async Task GoForward()
    {
        if (!CanGoForward) return;
        _historyPos++;
        await NavigateToAsync(_history[_historyPos]);
    }

    private async Task HandleInPageNavigationAsync(string href)
    {
        if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#')) return;

        // Resolve relative hrefs against current host
        if (href.StartsWith('/') && !string.IsNullOrEmpty(_finalUrl))
        {
            try
            {
                var uri = new Uri(_finalUrl);
                href = $"{uri.Scheme}://{uri.Host}{href}";
            }
            catch (UriFormatException) { }
        }

        await NavigateToAsync(href);
    }

    // ── Form submission (login/generic forms rendered by SimulatedPageRenderer) ──

    private async Task HandleFormSubmitAsync((string PostPath, ImmutableDictionary<string, string> FormBody) submission)
    {
        string url = ResolveSubmitUrl(submission.PostPath);
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        _isLoading = true;
        _networkError = null;
        StateHasChanged();

        await Task.Run(() =>
        {
            SimulatedHttpResponse response = NetworkService.Post(url, submission.FormBody, _cookieJar);
            InvokeAsync(async () =>
            {
                _isLoading = false;

                if (response.IsRedirect && response.RedirectUrl is not null)
                {
                    await NavigateToAsync(response.RedirectUrl);
                    return;
                }

                if (response.Page is not null)
                {
                    _finalUrl = url;
                    _currentPage = response.Page;
                    _urlInputValue = _finalUrl;
                    PushHistory(_finalUrl);
                }
                else
                {
                    _networkError = $"Request failed with status {response.StatusCode}.";
                }
                StateHasChanged();
            });
        });
    }

    private string ResolveSubmitUrl(string postPath) => ResolveSubmitUrl(_finalUrl, postPath);

    /// <summary>
    /// Resolves a form's relative or absolute post path against the current page's URL. Pure
    /// (no instance state) so it can be unit-tested without rendering the component.
    /// </summary>
    public static string ResolveSubmitUrl(string finalUrl, string postPath)
    {
        if (string.IsNullOrEmpty(finalUrl))
        {
            return string.Empty;
        }

        if (!postPath.StartsWith('/'))
        {
            return postPath;
        }

        try
        {
            Uri baseUri = new(finalUrl);
            return $"{baseUri.Scheme}://{baseUri.Host}{postPath}";
        }
        catch (UriFormatException)
        {
            return string.Empty;
        }
    }

    // ── Address bar input ────────────────────────────────────────────────

    private void OnUrlInputChanged(ChangeEventArgs e) =>
        _urlInputValue = e.Value?.ToString() ?? string.Empty;

    private async Task HandleAddressKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await NavigateToCurrentInputAsync();
    }

    // ── Bookmarks (session-only, first slice) ────────────────────────────

    private readonly List<(string Label, string Url)> _sessionBookmarks = [..DefaultBookmarks];

    private bool _bookmarksOpen;
    private Task ToggleBookmarksAsync()
    {
        _bookmarksOpen = !_bookmarksOpen;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── History helpers ──────────────────────────────────────────────────

    private void PushHistory(string url)
    {
        // Truncate forward history on new navigation
        if (_historyPos < _history.Count - 1)
            _history.RemoveRange(_historyPos + 1, _history.Count - _historyPos - 1);

        // Avoid duplicate consecutive entries
        if (_history.Count == 0 || _history[^1] != url)
        {
            _history.Add(url);
            _historyPos = _history.Count - 1;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        // Session cookies are cleared with the window; nothing to explicitly
        // dispose since all state is window-scoped in-memory dictionaries.
        _cookieJar.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string NormalizeUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? url
            : "https://" + url;
}
