using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Apps.Browser.Bridge;
using HackerOs.Apps.Browser.DevTools;
using HackerOs.Apps.Browser.Net;
using HackerOs.Apps.Browser.Rendering;
using HackerOs.Apps.Browser.Storage;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Net;
using HackerOs.Simulation.Abstractions.Network;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.Apps.Browser;

/// <summary>
/// Code-behind for the Browser window application.
/// </summary>
/// <remarks>
/// <para>
/// Every request goes through the SDK (ADR 0056 §6): <see cref="HackHttpClient"/> for
/// <c>http(s)</c>, <c>HackFile</c> for <c>file://</c> (ADR 0075 §4). The response is shown by type
/// (ADR 0075 §5): a historical site's structured page with <see cref="SimulatedPageRenderer"/>,
/// HTML in the isolated frame of <see cref="BrowserPageView"/> after <see cref="HtmlPageComposer"/>
/// inlined its sub-resources, other bodies with <see cref="ResponseBodyViewer"/>, and anything the
/// browser cannot show is saved to <c>~/Downloads</c>.
/// </para>
/// <para>
/// All browsing state (history, cookies, <c>sessionStorage</c>, developer tools) belongs to the
/// window and is discarded on close; <c>localStorage</c> is kept in the user's files
/// (<see cref="PageStorage"/>).
/// </para>
/// </remarks>
public sealed partial class BrowserWindow : WindowAppBase, IAsyncDisposable
{
    private const string HomeUrl = "https://hackersearch.net";
    private const string SearchUrl = "https://hackersearch.net/search?q=";
    private const string DocumentAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";

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
    private readonly List<Uri> _history = [];
    private int _historyPos = -1;

    private string _currentUrl      = string.Empty;
    private string _finalUrl        = string.Empty;
    private string _urlInputValue   = string.Empty;
    private bool   _isLoading;
    private BrowserErrorView? _error;
    private SimulatedPage? _currentPage;
    private ResponseBodyView? _currentBody;
    private int _currentStatus;
    private string? _downloadedTo;
    private Uri? _documentUrl;
    private bool _viewSource;
    private string _pageTitle = string.Empty;
    private string? _alertMessage;
    private string _alertOrigin = string.Empty;
    private bool _devToolsOpen;
    private CancellationTokenSource? _navigation;

    // ── HTML page (ADR 0075) ─────────────────────────────────────────────
    private PageFrameDocument? _frameDocument;
    private PageSession? _session;
    private BrowserPageView? _pageView;

    // ── Per-window session state, sent and filled by the OS on each request ──
    private readonly HackCookieContainer _cookies = new();
    private readonly DevToolsLog _devTools = new();
    private BrowserResourceLoader _loader = default!;
    private HtmlPageComposer _composer = default!;
    private PageBridgeHandler _bridge = default!;
    private PageStorage? _storage;
    private string _homeDirectory = "/";

    // ── Computed helpers ──────────────────────────────────────────────────
    private bool CanGoBack    => _historyPos > 0;
    private bool CanGoForward => _historyPos < _history.Count - 1;
    private bool CanViewSource => _currentBody is { Kind: ResponseBodyKind.Html };
    private bool ShowsFrame => !_isLoading && _error is null && _currentPage is null && _frameDocument is not null && !_viewSource;

    private string SchemeIcon => _documentUrl?.Scheme switch
    {
        "https" => "\U0001F512",
        "file" => "\U0001F4C4",
        _ => "ⓘ",
    };

    private string SchemeClass => _documentUrl?.Scheme ?? "none";

    private string SchemeTitle => _documentUrl?.Scheme switch
    {
        "https" => "Secure connection (simulated)",
        "file" => "Local file",
        "http" => "Not secure",
        _ => string.Empty,
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnAppInitialized()
    {
        _loader = new BrowserResourceLoader(new SdkBrowserTransport(_cookies), _devTools);
        _composer = new HtmlPageComposer(_loader);
        _bridge = new PageBridgeHandler(_loader, _cookies);
        _devTools.Changed += OnDevToolsChanged;

        try
        {
            _homeDirectory = HackUser.Current.HomeDirectory;
            _storage = new PageStorage(new HackBrowserStorageFiles(), PageStorage.GetRoot(_homeDirectory),
                message => _devTools.Add(new ConsoleLogEntry(ConsoleLevel.Warning, message, "browser")));
        }
        catch (Exception exception) when (exception is InvalidOperationException or HackExecutionContextMissingException)
        {
            // No user (a test host): pages get an in-memory storage only.
            _storage = null;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAppAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // A file passed at launch (an .html opened from the file manager) wins over the home page.
            string start = LaunchFile is { } file ? BrowserUrl.FromLocalPath(file.Value).AbsoluteUri
                : LaunchArguments.FirstOrDefault(argument => !argument.StartsWith('-')) ?? HomeUrl;
            await NavigateToAsync(start);
        }
    }

    // ── Navigation commands ───────────────────────────────────────────────

    /// <summary>Navigates to what the user typed (or a bookmark): a URL, a path or a search.</summary>
    internal async Task NavigateToAsync(string input)
    {
        AddressTarget? target = BrowserUrl.FromAddressBar(input, _homeDirectory, SearchUrl);
        if (target is null)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                ShowNetworkError(BrowserErrorCodes.InvalidUrl, null, input);
            }

            return;
        }

        _viewSource = target.ViewSource;
        await NavigateAsync(target.Url, HistoryMode.Push);
    }

    private enum HistoryMode
    {
        Push,
        Replace,
        Traverse,
    }

    /// <summary>
    /// Loads <paramref name="url"/> in the tab, as a browser's navigation does: one document request
    /// (logged in the Network panel), then a view chosen by the response's type.
    /// </summary>
    private async Task NavigateAsync(
        Uri url,
        HistoryMode mode,
        string method = "GET",
        byte[]? body = null,
        string? contentType = null,
        bool download = false)
    {
        _navigation?.Cancel();
        _navigation?.Dispose();
        _navigation = new CancellationTokenSource();
        CancellationToken cancellationToken = _navigation.Token;
        Uri? referrer = _documentUrl;

        _devTools.Clear();
        _currentUrl = BrowserUrl.ToDisplayString(url);
        _urlInputValue = BrowserUrl.ToDisplayString(url, _viewSource);
        _isLoading = true;
        _error = null;
        _alertMessage = null;
        StateHasChanged();

        try
        {
            if (url == BrowserUrl.AboutBlank)
            {
                ClearView();
                _documentUrl = url;
                _finalUrl = url.AbsoluteUri;
                await ShowHtmlAsync(url, string.Empty, referrer, cancellationToken);
                UpdateHistory(url, mode);
                return;
            }

            List<KeyValuePair<string, string>> headers = [new("Accept", DocumentAccept)];
            if (referrer is { Scheme: "http" or "https" } && url.Scheme is "http" or "https")
            {
                headers.Add(new("Referer", referrer.GetLeftPart(UriPartial.Query)));
            }

            BrowserFetchResult result = await _loader.SendAsync(
                new BrowserRequest(method, url, headers, body, contentType), NetworkRequestKind.Document, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.Response is not { } response)
            {
                ShowNetworkError(result.ErrorCode ?? BrowserErrorCodes.Failed, url, BrowserUrl.ToDisplayString(url));
                UpdateHistory(url, mode);
                return;
            }

            await ShowResponseAsync(response, referrer, download, cancellationToken);
            UpdateHistory(response.Url, mode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer navigation took over.
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _isLoading = false;
                StateHasChanged();
            }
        }
    }

    /// <summary>
    /// Shows a response (ADR 0075 §5): a historical site's structured page, HTML in the isolated
    /// frame, a body the viewer can show, a download, or the browser's error page for an error
    /// status with nothing to show.
    /// </summary>
    private async Task ShowResponseAsync(BrowserResponse response, Uri? referrer, bool download, CancellationToken cancellationToken)
    {
        ClearView();
        _documentUrl = response.Url;
        _finalUrl = BrowserUrl.ToDisplayString(response.Url);
        _currentStatus = response.StatusCode;
        _urlInputValue = BrowserUrl.ToDisplayString(response.Url, _viewSource);

        if (response.Page is not null && !download)
        {
            _currentPage = response.Page;
            _pageTitle = response.Page.Title;
            return;
        }

        if (response.Body.Length == 0 && !response.IsSuccessStatusCode)
        {
            _error = BrowserErrorPages.ForHttpStatus(response.StatusCode, response.Url);
            return;
        }

        _currentBody = ResponseBodyPresenter.Present(response.ContentType, response.Body);
        bool attachment = response.GetHeader("Content-Disposition")?.TrimStart().StartsWith("attachment", StringComparison.OrdinalIgnoreCase) == true;
        if (download || attachment || _currentBody.Kind == ResponseBodyKind.Binary)
        {
            _currentBody = _currentBody with { Kind = ResponseBodyKind.Binary };
            await DownloadAsync(response);
            return;
        }

        if (_currentBody.Kind == ResponseBodyKind.Html)
        {
            await ShowHtmlAsync(response.Url, _currentBody.Text, referrer, cancellationToken);
        }
    }

    /// <summary>Composes an HTML document and writes it in the isolated frame (ADR 0075 §1–§3).</summary>
    private async Task ShowHtmlAsync(Uri documentUrl, string html, Uri? referrer, CancellationToken cancellationToken)
    {
        string token = Guid.NewGuid().ToString("N");
        PageSession session = new(documentUrl, token, _loader, cancellationToken);
        string storageKey = session.StorageKey;
        IReadOnlyDictionary<string, string> local = _storage is null
            ? new Dictionary<string, string>()
            : await _storage.GetLocalAsync(storageKey, cancellationToken);
        IReadOnlyDictionary<string, string> sessionStorage = _storage?.GetSession(storageKey) ?? new Dictionary<string, string>();

        PageBootstrap bootstrap = new(
            token,
            documentUrl.AbsoluteUri,
            referrer is { Scheme: "http" or "https" } ? referrer.AbsoluteUri : string.Empty,
            _bridge.GetDocumentCookies(documentUrl),
            local,
            sessionStorage,
            PageStorage.Quota,
            documentUrl.Scheme == "file");

        ComposedPage page = await _composer.ComposeAsync(html, session, bootstrap);
        cancellationToken.ThrowIfCancellationRequested();
        _session = session;
        _pageTitle = page.Title;
        _frameDocument = new PageFrameDocument(page.Html, token);
    }

    private async Task DownloadAsync(BrowserResponse response)
    {
        string fileName = BrowserDownloads.SuggestFileName(response.Url, response.GetHeader("Content-Disposition"));
        try
        {
            _downloadedTo = await BrowserDownloads.SaveAsync(_homeDirectory, fileName, response.Body);
            _devTools.Add(new ConsoleLogEntry(ConsoleLevel.Log, $"Downloaded {fileName} to {_downloadedTo}", "browser"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _devTools.Add(new ConsoleLogEntry(ConsoleLevel.Error, $"Download of {fileName} failed: {exception.Message}", "browser"));
        }
    }

    private void ClearView()
    {
        _currentPage = null;
        _currentBody = null;
        _frameDocument = null;
        _session = null;
        _downloadedTo = null;
        _error = null;
        _pageTitle = string.Empty;
    }

    private void ShowNetworkError(string code, Uri? url, string display)
    {
        ClearView();
        _documentUrl = url;
        _finalUrl = display;
        _currentUrl = display;
        _error = BrowserErrorPages.ForNetworkError(code, url);
    }

    private async Task NavigateToCurrentInputAsync() =>
        await NavigateToAsync(_urlInputValue);

    private async Task NavigateHomeAsync() =>
        await NavigateToAsync(HomeUrl);

    private async Task RefreshAsync()
    {
        if (_historyPos >= 0)
        {
            await NavigateAsync(_history[_historyPos], HistoryMode.Traverse);
        }
        else if (_currentUrl.Length > 0)
        {
            await NavigateToAsync(_currentUrl);
        }
    }

    private async Task GoBack()
    {
        if (!CanGoBack) return;
        _historyPos--;
        await NavigateAsync(_history[_historyPos], HistoryMode.Traverse);
    }

    private async Task GoForward()
    {
        if (!CanGoForward) return;
        _historyPos++;
        await NavigateAsync(_history[_historyPos], HistoryMode.Traverse);
    }

    private async Task ToggleViewSourceAsync()
    {
        _viewSource = !_viewSource;
        if (_documentUrl is not null)
        {
            _urlInputValue = BrowserUrl.ToDisplayString(_documentUrl, _viewSource);
        }

        await Task.CompletedTask;
    }

    private void ToggleDevTools() => _devToolsOpen = !_devToolsOpen;

    private void DismissAlert() => _alertMessage = null;

    private void OnDevToolsChanged() => _ = InvokeAsync(StateHasChanged);

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
        string url = ResolveSubmitUrl(_finalUrl, submission.PostPath);
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? target))
        {
            return;
        }

        // Redirects are followed by the OS: a login answered by 302 lands on the target page.
        SimulatedHttpBody form = SimulatedHttpBody.FormUrlEncoded(submission.FormBody);
        await NavigateAsync(target, HistoryMode.Push, "POST", form.ToArray(), form.ContentType);
    }

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

    // ── Page bridge (ADR 0075 §3) ────────────────────────────────────────

    private async Task<string> HandleBridgeRequestAsync(string operation, string payload)
    {
        PageSession? session = _session;
        if (session is null)
        {
            return "{}";
        }

        switch (operation)
        {
            case "fetch":
                BridgeFetchRequest? fetch = JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeFetchRequest);
                BridgeFetchReply reply = fetch is null
                    ? BridgeFetchReply.Fail("Failed to fetch")
                    : await _bridge.FetchAsync(session, fetch);
                return JsonSerializer.Serialize(reply, PageBridgeJsonContext.Default.BridgeFetchReply);
            case "resource":
                BridgeResourceRequest? resource = JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeResourceRequest);
                BridgeResourceReply loaded = resource is null
                    ? new BridgeResourceReply(DataUrl.Empty)
                    : await _bridge.LoadResourceAsync(session, resource);
                return JsonSerializer.Serialize(loaded, PageBridgeJsonContext.Default.BridgeResourceReply);
            default:
                return "{}";
        }
    }

    private async Task HandleBridgeEventAsync(string type, string payload)
    {
        PageSession? session = _session;
        if (session is null)
        {
            return;
        }

        switch (type)
        {
            case "navigate" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeNavigateEvent) is { } navigation:
                await InvokeAsync(() => NavigateFromPageAsync(session, navigation));
                return;
            case "fragment" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeNavigateEvent) is { } fragment
                                 && Uri.TryCreate(fragment.Url, UriKind.Absolute, out Uri? fragmentUrl):
                await InvokeAsync(() => FollowPageHistory(fragmentUrl, replace: false));
                return;
            case "history" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeHistoryEvent) is { } history
                                && Uri.TryCreate(history.Url, UriKind.Absolute, out Uri? historyUrl):
                await InvokeAsync(() => FollowPageHistory(historyUrl, history.Replace));
                return;
            case "console" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeConsoleEvent) is { } message:
                _devTools.Add(new ConsoleLogEntry(ParseLevel(message.Level), message.Message ?? string.Empty, message.Source ?? string.Empty));
                return;
            case "storage" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeStorageEvent) is { } change:
                _storage?.Apply(session.StorageKey, change);
                return;
            case "cookie" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeCookieEvent) is { Value: { } assignment }:
                _bridge.ApplyDocumentCookie(session.DocumentUrl, assignment, DateTimeOffset.UtcNow);
                return;
            case "title" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeTitleEvent) is { } title:
                await InvokeAsync(() =>
                {
                    _pageTitle = title.Title ?? string.Empty;
                    StateHasChanged();
                });
                return;
            case "dialog" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeDialogEvent) is { } dialog:
                await InvokeAsync(() =>
                {
                    _alertOrigin = session.DocumentUrl.Scheme == "file" ? "This page" : session.DocumentUrl.Host;
                    _alertMessage = dialog.Message ?? string.Empty;
                    StateHasChanged();
                });
                return;
            case "key" when JsonSerializer.Deserialize(payload, PageBridgeJsonContext.Default.BridgeKeyEvent) is { } key:
                await InvokeAsync(() => HandleShortcutAsync(key));
                return;
        }
    }

    private async Task NavigateFromPageAsync(PageSession session, BridgeNavigateEvent navigation)
    {
        if (!Uri.TryCreate(navigation.Url, UriKind.Absolute, out Uri? target) || target.Scheme is not ("http" or "https" or "file"))
        {
            return;
        }

        if (target.Scheme == "file" && session.DocumentUrl.Scheme != "file")
        {
            // Chromium: a web page may not link to a local file.
            _devTools.Add(new ConsoleLogEntry(ConsoleLevel.Error,
                $"Not allowed to load local resource: {BrowserUrl.ToDisplayString(target)}", BrowserUrl.ToDisplayString(session.DocumentUrl)));
            return;
        }

        if (navigation.Target is "_blank")
        {
            _devTools.Add(new ConsoleLogEntry(ConsoleLevel.Log, "Links opening a new tab open in this tab (one tab per window).", "browser"));
        }

        _viewSource = false;
        bool post = string.Equals(navigation.Method, "POST", StringComparison.OrdinalIgnoreCase);
        byte[]? body = post && navigation.Body is { Length: > 0 } encoded ? Convert.FromBase64String(encoded) : null;
        await NavigateAsync(
            target,
            navigation.Replace ? HistoryMode.Replace : HistoryMode.Push,
            post ? "POST" : "GET",
            body,
            post ? navigation.ContentType ?? SimulatedMediaTypes.FormUrlEncoded : null,
            navigation.Download);
    }

    /// <summary>A same-document navigation (fragment, <c>pushState</c>): the address changes, the page stays.</summary>
    private void FollowPageHistory(Uri url, bool replace)
    {
        _documentUrl = url;
        _finalUrl = BrowserUrl.ToDisplayString(url);
        _urlInputValue = _finalUrl;
        UpdateHistory(url, replace ? HistoryMode.Replace : HistoryMode.Push);
        StateHasChanged();
    }

    private async Task HandleShortcutAsync(BridgeKeyEvent key)
    {
        string lower = (key.Key ?? string.Empty).ToLowerInvariant();
        if (key.Key == "F5" || (key.Ctrl && lower == "r"))
        {
            await RefreshAsync();
        }
        else if (key.Key == "F12" || (key.Ctrl && key.Shift && lower == "i"))
        {
            ToggleDevTools();
        }
        else if (key.Ctrl && lower == "u")
        {
            await ToggleViewSourceAsync();
        }
        else if (key.Alt && key.Key == "ArrowLeft")
        {
            await GoBack();
        }
        else if (key.Alt && key.Key == "ArrowRight")
        {
            await GoForward();
        }

        StateHasChanged();
    }

    private Task HandleFrameEscapedAsync() => InvokeAsync(() =>
    {
        // The page set window.location (or similar) by script: the frame left the document the
        // browser wrote. Show why instead of an unbridged page.
        Uri? url = _documentUrl;
        ClearView();
        _error = new BrowserErrorView(
            "This page navigated by script",
            "The page changed window.location, which HackerOS Browser does not support yet: links, forms and history.pushState work.",
            BrowserErrorCodes.BlockedByClient,
            ["Reload the page.", "Use a link or a form for navigation."]);
        _devTools.Add(new ConsoleLogEntry(ConsoleLevel.Error,
            "The page navigated its frame by script (window.location); the navigation was blocked.",
            url is null ? string.Empty : BrowserUrl.ToDisplayString(url)));
        StateHasChanged();
    });

    private static ConsoleLevel ParseLevel(string? level) => level switch
    {
        "debug" => ConsoleLevel.Debug,
        "warning" => ConsoleLevel.Warning,
        "error" => ConsoleLevel.Error,
        _ => ConsoleLevel.Log,
    };

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

    /// <summary>
    /// Records a navigation: a new one drops the forward entries, a replacement rewrites the current
    /// entry, and back/forward/reload (traverse) only moved the position, so the list is kept.
    /// </summary>
    private void UpdateHistory(Uri url, HistoryMode mode)
    {
        switch (mode)
        {
            case HistoryMode.Traverse when _historyPos >= 0:
                // Keep the entry, but follow a redirect the server answered this time.
                _history[_historyPos] = url;
                break;
            case HistoryMode.Replace when _historyPos >= 0:
                _history[_historyPos] = url;
                break;
            default:
                if (_historyPos < _history.Count - 1)
                {
                    _history.RemoveRange(_historyPos + 1, _history.Count - _historyPos - 1);
                }

                if (_history.Count == 0 || _history[^1] != url)
                {
                    _history.Add(url);
                }

                _historyPos = _history.Count - 1;
                break;
        }
    }

    // ── Disposal ─────────────────────────────────────────────────────────

    /// <summary>Cancels the current navigation, writes pending storage and forgets session cookies.</summary>
    public async ValueTask DisposeAsync()
    {
        _navigation?.Cancel();
        _navigation?.Dispose();
        _devTools.Changed -= OnDevToolsChanged;

        if (_storage is not null)
        {
            await _storage.FlushAsync();
        }

        // Session cookies are cleared with the window; nothing else holds them.
        _cookies.Clear();
    }
}
