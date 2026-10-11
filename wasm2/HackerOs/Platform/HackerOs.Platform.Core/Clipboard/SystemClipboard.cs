using HackerOs.AppSdk.Clipboard;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Clipboard;

/// <summary>
/// The OS clipboard (ADR 0056, section 12): one selection per session, owned by the platform and
/// shared by every app of that session, like the X11 <c>CLIPBOARD</c> selection a desktop's clipboard
/// manager keeps alive. Content is held as MIME-typed targets, as X11 offers them: <c>text/plain</c>
/// always, plus <c>x-special/gnome-copied-files</c> for a file list. It lives in memory only: like a
/// real clipboard, it does not survive a reboot.
/// </summary>
/// <remarks>
/// Apps never see this class; each instance gets an <see cref="Execution.AppClipboardGateway"/> that
/// checks <c>clipboard.read</c>/<c>clipboard.write</c> and only reaches its own session.
/// </remarks>
public sealed class SystemClipboard : IDisposable
{
    /// <summary>The <c>text/plain</c> target.</summary>
    public const string TextMediaType = "text/plain";

    private readonly Lock _gate = new();
    private readonly Dictionary<SessionId, IReadOnlyDictionary<string, string>> _selections = [];
    private readonly IDisposable? _logoutSubscription;

    /// <summary>Creates an empty clipboard.</summary>
    /// <param name="eventBus">
    /// When supplied, a session's clipboard is emptied at its logout, as the X server drops the
    /// selections of a closed desktop session.
    /// </param>
    public SystemClipboard(IEventBus? eventBus = null)
    {
        _logoutSubscription = eventBus?.Subscribe<SessionLoggedOutEvent>(logout => Clear(logout.SessionId));
    }

    /// <summary>Raised after a session's clipboard changed, with that session's ID. Handlers run on the writer's call stack.</summary>
    public event EventHandler<SessionId>? Changed;

    /// <summary>Gets one target of a session's clipboard, or <see langword="null"/> when it is not offered.</summary>
    /// <param name="session">Owning session.</param>
    /// <param name="mediaType">Target MIME type.</param>
    public string? Get(SessionId session, string mediaType)
    {
        lock (_gate)
        {
            return _selections.TryGetValue(session, out IReadOnlyDictionary<string, string>? targets)
                && targets.TryGetValue(mediaType, out string? content)
                    ? content
                    : null;
        }
    }

    /// <summary>Replaces a session's clipboard with text.</summary>
    /// <param name="session">Owning session.</param>
    /// <param name="text">Copied text.</param>
    public void SetText(SessionId session, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Replace(session, new Dictionary<string, string>(StringComparer.Ordinal) { [TextMediaType] = text });
    }

    /// <summary>
    /// Replaces a session's clipboard with a file list, offered both as
    /// <c>x-special/gnome-copied-files</c> and as <c>text/plain</c> (the paths, one per line).
    /// </summary>
    /// <param name="session">Owning session.</param>
    /// <param name="files">Files cut or copied.</param>
    public void SetFileDropList(SessionId session, HackClipboardFileList files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Replace(session, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HackClipboardFileList.GnomeCopiedFilesMediaType] = files.ToGnomeCopiedFiles(),
            [TextMediaType] = files.ToText()
        });
    }

    /// <summary>Empties a session's clipboard, for instance after cut files were pasted or at logout.</summary>
    /// <param name="session">Owning session.</param>
    public void Clear(SessionId session)
    {
        bool removed;
        lock (_gate)
        {
            removed = _selections.Remove(session);
        }

        if (removed)
        {
            Changed?.Invoke(this, session);
        }
    }

    private void Replace(SessionId session, IReadOnlyDictionary<string, string> targets)
    {
        lock (_gate)
        {
            _selections[session] = targets;
        }

        Changed?.Invoke(this, session);
    }

    /// <inheritdoc />
    public void Dispose() => _logoutSubscription?.Dispose();
}
