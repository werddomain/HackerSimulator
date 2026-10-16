using HackerOs.AppSdk;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.Net.Hosting;
using HackerOs.App.Abstractions;

namespace HackerOs.Apps.WebServer.Manager;

/// <summary>
/// Web Server Manager (ADR 0077, section 3): the IIS-Manager-like window of the hosted static sites.
/// It lists the system sites, the user's sites and the external sites of web apps; it creates a site
/// from a folder, edits its bindings and default documents, starts and stops it, opens it in the
/// browser and shows its logs. Every change is a write of a <c>.site</c> file, which
/// <c>hackwebd</c> applies at once; the service itself is driven like <c>systemctl</c>.
/// </summary>
/// <remarks>
/// Launched with a folder path as first argument (File Explorer's "Publish as a web site…"), it opens
/// the form of a new site rooted there.
/// </remarks>
public partial class WebServerManagerWindow
{
    private readonly CancellationTokenSource _lifetime = new();
    private WebServerSnapshot? _snapshot;
    private WebSiteRow? _selected;
    private SiteEditor? _editor;
    private DetailTab _tab = DetailTab.Settings;
    private List<string> _errors = [];
    private string? _warning;
    private string? _message;
    private bool _messageIsError;
    private IReadOnlyList<string> _accessLines = [];
    private IReadOnlyList<string> _errorLines = [];

    private enum DetailTab
    {
        Settings,
        Logs
    }

    private IEnumerable<(string Title, string Hint, IReadOnlyList<WebSiteRow> Rows)> Groups
    {
        get
        {
            IReadOnlyList<WebSiteRow> rows = _snapshot?.Rows ?? [];
            yield return ("System", "/etc/hackweb/sites", [.. rows.Where(static row => row.Declaration?.Scope == HackWebSiteScope.System)]);
            yield return ("My sites", "~/.config/hackweb/sites", [.. rows.Where(static row => row.Declaration?.Scope == HackWebSiteScope.User)]);
            yield return ("External", "web apps, read-only", [.. rows.Where(static row => row.State == WebSiteState.External)]);
        }
    }

    private string DaemonText => _snapshot?.Daemon switch
    {
        null => "hackwebd not installed",
        { ProcessId: { } pid } => $"hackwebd running · pid {pid}",
        _ => "hackwebd stopped"
    };

    private string DaemonTitle => _snapshot?.Daemon?.StartType is { } mode ? $"start type: {mode}" : "systemctl status hackwebd";

    private string DaemonClass => _snapshot?.Daemon?.ProcessId is null ? "wsm-daemon-down" : "wsm-daemon-up";

    private string? SelectedUrl => _selected?.MainUrl;

    private string StatusText
    {
        get
        {
            if (_snapshot is null)
            {
                return "Loading…";
            }

            int declared = _snapshot.Rows.Count(static row => row.Declaration is not null);
            int running = _snapshot.Rows.Count(static row => row.State == WebSiteState.Running);
            int external = _snapshot.Rows.Count(static row => row.State == WebSiteState.External);
            return $"{declared} hosted site(s), {running} running · {external} external · {_snapshot.Listeners.Count} listening socket(s)";
        }
    }

    /// <inheritdoc />
    protected override async Task OnAppInitializedAsync()
    {
        await RefreshAsync();
        if (LaunchFile is { } folder)
        {
            await StartNewAsync(folder.Value);
        }
    }

    /// <summary>Reads everything again, keeping the selection.</summary>
    private async Task RefreshAsync()
    {
        try
        {
            _snapshot = await WebSiteStore.LoadAsync(_lifetime.Token);
            if (_selected is { } selected)
            {
                _selected = _snapshot.Rows.FirstOrDefault(row => row.Name == selected.Name && row.Declaration?.Path == selected.Declaration?.Path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowMessage($"Cannot read the sites: {exception.Message}", error: true);
        }
    }

    private bool IsSelected(WebSiteRow row) =>
        _selected is { } selected && selected.Name == row.Name && selected.Declaration?.Path == row.Declaration?.Path;

    private async Task SelectAsync(WebSiteRow row)
    {
        _selected = row;
        _tab = DetailTab.Settings;
        _message = null;
        _editor = row.Declaration is { } declaration ? SiteEditor.From(declaration) : null;
        await ValidateAsync();
    }

    private Task NewSiteAsync() => StartNewAsync(HackUser.Current.HomeDirectory + "/www");

    private async Task StartNewAsync(string folder)
    {
        _snapshot ??= await WebSiteStore.LoadAsync(_lifetime.Token);
        HackWebSite suggested = WebSiteStore.Suggest(folder, _snapshot);
        _selected = null;
        _tab = DetailTab.Settings;
        _editor = SiteEditor.FromNew(suggested);
        ShowMessage($"New site for {folder}: check the name and the address, then Save.", error: false);
        await ValidateAsync();
    }

    private void CancelNew()
    {
        _editor = null;
        _message = null;
    }

    private async Task SetScopeAsync(HackWebSiteScope scope)
    {
        if (_editor is null)
        {
            return;
        }

        _editor.Scope = scope;
        await ValidateAsync();
    }

    private void Validate()
    {
        if (_editor is null)
        {
            _errors = [];
            return;
        }

        _errors = [.. HackWebSiteFile.Validate(_editor.ToSite(), _editor.Scope)];
    }

    private async Task ValidateAsync()
    {
        Validate();
        _warning = _editor is { Scope: HackWebSiteScope.System } editor
            ? await WebSiteStore.CheckWwwDataAsync(editor.Root, _lifetime.Token)
            : null;
    }

    private async Task BrowseRootAsync()
    {
        if (_editor is null)
        {
            return;
        }

        SelectFolderDialogResult result = await SelectFolderAsync(new SelectFolderDialogRequest
        {
            Title = "Site root folder",
            InitialDirectory = TryParse(_editor.Root),
            AllowCreateFolder = true
        });
        if (result.Status == FileDialogStatus.Selected && result.Resource is { } folder)
        {
            _editor.Root = folder.Path.Value;
            await ValidateAsync();
        }
    }

    private async Task SaveAsync()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        await ValidateAsync();
        if (_errors.Count > 0)
        {
            return;
        }

        try
        {
            string path = await WebSiteStore.SaveAsync(editor.ToSite(), editor.Scope, editor.OriginalPath, _lifetime.Token);
            ShowMessage($"Saved {path}. hackwebd applies it now.", error: false);
            await RefreshAsync();
            _selected = _snapshot?.Rows.FirstOrDefault(row => row.Declaration?.Path == path);
            _editor = _selected?.Declaration is { } declaration ? SiteEditor.From(declaration) : null;
        }
        catch (UnauthorizedAccessException)
        {
            ShowMessage(editor.Scope == HackWebSiteScope.System
                ? "Writing /etc/hackweb/sites needs an administrator. Once one has approved the action, press Save again."
                : "Access denied while writing the site file.", error: true);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            ShowMessage($"Cannot save: {exception.Message}", error: true);
        }
    }

    private async Task ToggleEnabledAsync()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        editor.Enabled = !editor.WasEnabled;
        await SaveAsync();
    }

    private async Task DeleteAsync()
    {
        if (_selected?.Declaration is not { } declaration)
        {
            return;
        }

        MessageBoxDialogResult answer = await MessageBox(
            "Delete site",
            $"Delete {declaration.Path}? hackwebd stops serving '{declaration.Name}' at once. The site's files are kept.",
            MessageBoxType.YesNo);
        if (answer.Result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await WebSiteStore.DeleteAsync(declaration, _lifetime.Token);
            ShowMessage($"Deleted {declaration.Path}.", error: false);
            _selected = null;
            _editor = null;
            await RefreshAsync();
        }
        catch (UnauthorizedAccessException)
        {
            ShowMessage("Deleting a system site needs an administrator. Once one has approved the action, press Delete again.", error: true);
        }
        catch (IOException exception)
        {
            ShowMessage($"Cannot delete: {exception.Message}", error: true);
        }
    }

    private async Task OpenInBrowserAsync()
    {
        if (SelectedUrl is not { } url)
        {
            return;
        }

        try
        {
            await HackApps.LaunchAsync("org.hackeros.browser", [url], _lifetime.Token);
        }
        catch (Exception exception) when (exception is HackAppLaunchException or UnauthorizedAccessException)
        {
            ShowMessage($"Cannot open the browser: {exception.Message}", error: true);
        }
    }

    private async Task ShowLogsAsync()
    {
        if (_editor is null)
        {
            return;
        }

        _tab = DetailTab.Logs;
        (_accessLines, _errorLines) = await WebSiteStore.ReadLogsAsync(_editor.OriginalName ?? _editor.Name, cancellationToken: _lifetime.Token);
    }

    private Task StartDaemonAsync() => ControlDaemonAsync(static (service, token) => service.StartAsync(token), "started");

    private Task StopDaemonAsync() => ControlDaemonAsync(static (service, token) => service.StopAsync(token), "stopped");

    private Task RestartDaemonAsync() => ControlDaemonAsync(static async (service, token) =>
    {
        await service.StopAsync(token);
        await service.StartAsync(token);
    }, "restarted");

    private async Task ControlDaemonAsync(Func<AppSdk.ServiceProcess.HackServiceController, CancellationToken, Task> action, string done)
    {
        if (_snapshot?.Daemon is not { } daemon)
        {
            return;
        }

        try
        {
            await action(daemon, _lifetime.Token);
            ShowMessage($"hackwebd {done}.", error: false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            ShowMessage($"hackwebd: {exception.Message}", error: true);
        }

        await RefreshAsync();
    }

    private void ShowMessage(string message, bool error)
    {
        _message = message;
        _messageIsError = error;
    }

    private static string StateLabel(WebSiteState state) => state switch
    {
        WebSiteState.Running => "running",
        WebSiteState.Partial => "partly bound",
        WebSiteState.Down => "not listening",
        WebSiteState.Stopped => "stopped",
        WebSiteState.Invalid => "invalid file",
        _ => "external"
    };

    private static VirtualPath? TryParse(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? null : VirtualPath.Parse(HackPath.GetDirectoryName(path) ?? "/");
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Cancels the reads still running when the window closes.</summary>
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>The form's fields, as text, before they become a <see cref="HackWebSite"/>.</summary>
    private sealed class SiteEditor
    {
        public string Name { get; set; } = string.Empty;

        public string Root { get; set; } = string.Empty;

        public string Bindings { get; set; } = string.Empty;

        public string DefaultDocuments { get; set; } = string.Empty;

        public bool DirectoryBrowsing { get; set; }

        public bool Enabled { get; set; } = true;

        public bool WasEnabled { get; private init; }

        public HackWebSiteScope Scope { get; set; } = HackWebSiteScope.User;

        public string? OriginalPath { get; private init; }

        public string? OriginalName { get; private init; }

        public static SiteEditor From(HackWebSiteDeclaration declaration)
        {
            HackWebSite site = declaration.Site ?? new HackWebSite { Name = declaration.Name };
            return new SiteEditor
            {
                Name = site.Name,
                Root = site.Root,
                Bindings = string.Join('\n', site.Bindings),
                DefaultDocuments = string.Join(", ", site.DefaultDocuments),
                DirectoryBrowsing = site.DirectoryBrowsing,
                Enabled = site.Enabled,
                WasEnabled = site.Enabled,
                Scope = declaration.Scope,
                OriginalPath = declaration.Path,
                OriginalName = declaration.Name
            };
        }

        public static SiteEditor FromNew(HackWebSite site) => new()
        {
            Name = site.Name,
            Root = site.Root,
            Bindings = string.Join('\n', site.Bindings),
            DefaultDocuments = string.Join(", ", site.DefaultDocuments),
            DirectoryBrowsing = site.DirectoryBrowsing,
            Enabled = true,
            Scope = HackWebSiteScope.User
        };

        public HackWebSite ToSite() => new()
        {
            Name = Name.Trim(),
            Root = Root.Trim(),
            Bindings = [.. Bindings.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)],
            DefaultDocuments = [.. DefaultDocuments.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)],
            DirectoryBrowsing = DirectoryBrowsing,
            Enabled = Enabled
        };
    }
}
