using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Clipboard;
using HackerOs.Platform.Core.Clipboard;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Trusted <see cref="IAppClipboardGateway"/> (ADR 0056, section 12): one app instance's access to
/// its own session's <see cref="SystemClipboard"/>, checking <c>clipboard.read</c> on every read and
/// <c>clipboard.write</c> on every write.
/// </summary>
internal sealed class AppClipboardGateway : IAppClipboardGateway
{
    private readonly SystemClipboard _clipboard;
    private readonly ICapabilityChecker _capabilities;
    private readonly SessionId _session;
    private readonly AppAmbientContextBinding _ambient = new();
    private readonly Lock _gate = new();
    private EventHandler? _changed;

    /// <summary>Creates the gateway for one app instance of <paramref name="session"/>.</summary>
    public AppClipboardGateway(SystemClipboard clipboard, ICapabilityChecker capabilities, SessionId session)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _session = session;
    }

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add
        {
            lock (_gate)
            {
                bool first = _changed is null;
                _changed += value;
                if (first && _changed is not null)
                {
                    _clipboard.Changed += OnClipboardChanged;
                }
            }
        }
        remove
        {
            lock (_gate)
            {
                bool had = _changed is not null;
                _changed -= value;
                if (had && _changed is null)
                {
                    _clipboard.Changed -= OnClipboardChanged;
                }
            }
        }
    }

    /// <summary>Binds the app context <see cref="Changed"/> handlers run under; called once by the factory.</summary>
    internal void BindAmbientContext(HackerOs.AppSdk.IAppExecutionContext context) => _ambient.Bind(context);

    /// <inheritdoc />
    public ValueTask<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.ClipboardRead);
        return ValueTask.FromResult(_clipboard.Get(_session, SystemClipboard.TextMediaType));
    }

    /// <inheritdoc />
    public ValueTask SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.ClipboardWrite);
        _clipboard.SetText(_session, text);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<HackClipboardFileList?> GetFileDropListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.ClipboardRead);
        return ValueTask.FromResult(HackClipboardFileList.TryParseGnomeCopiedFiles(
            _clipboard.Get(_session, HackClipboardFileList.GnomeCopiedFilesMediaType)));
    }

    /// <inheritdoc />
    public ValueTask SetFileDropListAsync(HackClipboardFileList files, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.ClipboardWrite);
        foreach (string path in files.Paths)
        {
            // Only canonical absolute paths reach the shared clipboard: another app resolves them as is.
            VirtualPath.Parse(path);
        }

        _clipboard.SetFileDropList(_session, files);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.ClipboardWrite);
        _clipboard.Clear(_session);
        return ValueTask.CompletedTask;
    }

    private void OnClipboardChanged(object? sender, SessionId session)
    {
        if (session != _session)
        {
            return;
        }

        EventHandler? handlers;
        lock (_gate)
        {
            handlers = _changed;
        }

        if (handlers is not null)
        {
            _ambient.Wrap(() => handlers(this, EventArgs.Empty))();
        }
    }
}
