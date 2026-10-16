using System.Collections.Immutable;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Net;
using HackerOs.Simulation.Abstractions.Network;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.Apps.Browser;

/// <summary>
/// Code-behind for the Browser window application.
/// Manages URL navigation history and the window's cookie container, and sends every request
/// through the SDK's <see cref="HackHttpClient"/> (ADR 0056, section 6): the OS, not the browser,
/// decides which network serves a URL. All browsing state is window-scoped and discarded on close.
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

    // ── Navigation state ─────────────────────────────────────────────────
    private readonly List<string> _history = [];
    private int _historyPos = -1;

    private string _currentUrl      = string.Empty;
    private string _finalUrl        = string.Empty;
    private string _urlInputValue   = string.Empty;
    private bool   _isLoading;
    private string? _networkError;
    private SimulatedPage? _currentPage;
    private ResponseBodyView? _currentBody;
    private int _currentStatus;

    // ── Per-window session cookies, sent and filled by the OS on each request ──
    private readonly HackCookieContainer _cookies = new();

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
        _currentBody   = null;
        StateHasChanged();

        try
        {
            using HackHttpClient client = new(_cookies);
            HackHttpResponse response = await client.GetAsync(_currentUrl);
            ShowResponse(response);
        }
        catch (HackHttpRequestException exception)
        {
            _networkError = exception.ErrorCode;
            _finalUrl     = _currentUrl;
        }
        catch (ArgumentException)
        {
            _networkError = "ERR_INVALID_URL";
            _finalUrl     = _currentUrl;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Shows a response: its structured page when a historical site sent one, otherwise its body by
    /// <c>Content-Type</c> (ADR 0074, section 1). A response with neither and an error status shows
    /// the browser's own error page, as Chromium's "HTTP ERROR 500".
    /// </summary>
    private void ShowResponse(HackHttpResponse response)
    {
        _finalUrl      = response.RequestUri.AbsoluteUri;
        _currentStatus = response.StatusCode;
        _currentPage   = null;
        _currentBody   = null;

        if (response.Page is not null)
        {
            _currentPage = response.Page;
        }
        else if (response.ContentBytes.Length > 0 || response.IsSuccessStatusCode)
        {
            _currentBody = ResponseBodyPresenter.Present(response.ContentType, response.ContentBytes);
        }
        else
        {
            _networkError = $"HTTP ERROR {response.StatusCode}";
            return;
        }

        _urlInputValue = _finalUrl;
        PushHistory(_finalUrl);
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

        try
        {
            // Redirects are followed by the OS: a login answered by 302 lands on the target page.
            using HackHttpClient client = new(_cookies);
            ShowResponse(await client.PostAsync(url, submission.FormBody));
        }
        catch (HackHttpRequestException exception)
        {
            _networkError = exception.ErrorCode;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
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
        // Session cookies are cleared with the window; nothing else holds them.
        _cookies.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string NormalizeUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? url
            : "https://" + url;
}
