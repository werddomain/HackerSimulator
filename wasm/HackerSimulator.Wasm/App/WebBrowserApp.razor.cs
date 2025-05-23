using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using HackerSimulator.Wasm.Web;

namespace HackerSimulator.Wasm.Apps
{
    public partial class WebBrowserApp : Windows.WindowBase, IAsyncDisposable
    {
        [Inject] private HackerHttpClient Http { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private string _currentUrl = "https://hackersearch.net";
        private readonly List<string> _history = new();
        private int _historyIndex = -1;
        private readonly Dictionary<string, string> _bookmarks = new();
        private bool _showBookmarksMenu;
        private string _newBookmarkName = string.Empty;
        private string _newBookmarkUrl = string.Empty;
        private bool _loading;

        private readonly string _frameId = "frame" + Guid.NewGuid().ToString("N");
        private DotNetObjectReference<WebBrowserApp>? _objRef;
        private bool _jsInit;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Browser";
            _objRef = DotNetObjectReference.Create(this);
            _bookmarks["HackerSearch"] = "https://hackersearch.net";
            _bookmarks["HackMail"] = "https://hackmail.com";
            _bookmarks["CryptoBank"] = "https://cryptobank.com";
            _bookmarks["DarkNet Market"] = "https://darknet.market";
            _bookmarks["Hacker Forum"] = "https://hackerz.forum";
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                await Navigate(_currentUrl);
            }
        }

        private async Task EnsureJs()
        {
            if (_jsInit) return;
            var script = @"window.hackerBrowserLoadContent = function(frameId, html, dotNetRef) {\n" +
                "  var frame = document.getElementById(frameId);\n" +
                "  if(!frame) return;\n" +
                "  frame.srcdoc = html;\n" +
                "  setTimeout(function(){\n" +
                "    var doc = frame.contentDocument || frame.contentWindow.document;\n" +
                "    if(!doc) return;\n" +
                "    var title = doc.querySelector('title');\n" +
                "    if(title){ dotNetRef.invokeMethodAsync('UpdateTitle', title.textContent); }\n" +
                "    doc.querySelectorAll('a[href]').forEach(function(a){\n" +
                "      var href = a.getAttribute('href');\n" +
                "      if(href && href !== '#'){\n" +
                "        a.addEventListener('click', function(e){\n" +
                "          e.preventDefault();\n" +
                "          dotNetRef.invokeMethodAsync('NavigateFromJs', href);\n" +
                "        });\n" +
                "      }\n" +
                "    });\n" +
                "    doc.querySelectorAll('form').forEach(function(f){\n" +
                "      f.addEventListener('submit', function(e){\n" +
                "        e.preventDefault();\n" +
                "        alert('Form submission is not implemented yet.');\n" +
                "      });\n" +
                "    });\n" +
                "  },0);\n" +
                "};";
            await JS.InvokeVoidAsync("eval", script);
            _jsInit = true;
        }

        private async Task SetContent(string html)
        {
            await EnsureJs();
            await JS.InvokeVoidAsync("hackerBrowserLoadContent", _frameId, html, _objRef);
        }

        private bool IsSecure => _currentUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        private bool IsBackDisabled => _historyIndex <= 0;
        private bool IsForwardDisabled => _historyIndex >= _history.Count - 1;

        public async Task Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
            _currentUrl = url;
            if (_historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(url);
            _historyIndex = _history.Count - 1;
            _loading = true;
            StateHasChanged();
            var resp = await Http.SendAsync(url);
            _loading = false;
            await SetContent(resp.Content);
        }

        private async Task NavigateWithoutHistory(string url)
        {
            _currentUrl = url;
            _loading = true;
            StateHasChanged();
            var resp = await Http.SendAsync(url);
            _loading = false;
            await SetContent(resp.Content);
        }

        private Task Back()
        {
            if (_historyIndex <= 0) return Task.CompletedTask;
            _historyIndex--;
            return NavigateWithoutHistory(_history[_historyIndex]);
        }

        private Task Forward()
        {
            if (_historyIndex >= _history.Count - 1) return Task.CompletedTask;
            _historyIndex++;
            return NavigateWithoutHistory(_history[_historyIndex]);
        }

        private Task Refresh() => NavigateWithoutHistory(_currentUrl);

        private async Task OnUrlKey(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
                await Navigate(_currentUrl);
        }

        private async Task AddBookmark()
        {
            var name = await JS.InvokeAsync<string?>("prompt", "Enter a name for this bookmark:", new Uri(_currentUrl).Host);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _bookmarks[name] = _currentUrl;
            }
        }

        private void ToggleBookmarksMenu() => _showBookmarksMenu = !_showBookmarksMenu;

        private void DeleteBookmark(string name)
        {
            if (_bookmarks.ContainsKey(name))
            {
                _bookmarks.Remove(name);
            }
        }

        private void AddBookmarkFromMenu()
        {
            var name = _newBookmarkName.Trim();
            var url = _newBookmarkUrl.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
            _bookmarks[name] = url;
            _newBookmarkName = string.Empty;
            _newBookmarkUrl = string.Empty;
        }

        [JSInvokable]
        public Task NavigateFromJs(string url) => Navigate(url);

        [JSInvokable]
        public void UpdateTitle(string title)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Browser" : $"{title} - Browser";
            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            _objRef?.Dispose();
            await Task.CompletedTask;
        }
    }
}
