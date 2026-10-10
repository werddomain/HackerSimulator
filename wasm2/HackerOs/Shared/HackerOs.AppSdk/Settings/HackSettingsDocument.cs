using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.AppSdk.Settings;

/// <summary>
/// One open, layered settings document of an app: typed reads over the merged view of
/// <c>/etc/&lt;appId&gt;/&lt;document&gt;.config</c> and <c>~/.config/&lt;appId&gt;/&lt;document&gt;.config</c>,
/// typed writes to either layer, and change notifications (ADR 0044 section 7).
/// </summary>
/// <remarks>
/// <para>
/// Every read and write goes through the app's own <see cref="IAppFileSystemGateway"/>: the file's
/// permissions decide access. Writing the user layer needs <c>filesystem.private.write</c>; writing
/// the system layer needs the right to write <c>/etc/&lt;appId&gt;/</c> (administrator plus <c>sudo</c>
/// or an approval ticket of ADR 0042), otherwise <see cref="UnauthorizedAccessException"/> is thrown.
/// </para>
/// <para>
/// Reads never fail on a bad file: malformed lines, undeclared keys and ill-typed values are
/// ignored, and each problem is written once per file revision to the app's log through
/// <see cref="IAppExecutionContext.Logging"/>.
/// </para>
/// </remarks>
public sealed class HackSettingsDocument : IAsyncDisposable
{
    private readonly IAppFileSystemGateway _fileSystem;
    private readonly IAppLoggingGateway? _logging;
    private readonly SettingsSchema _schema;
    private readonly VirtualPath _systemPath;
    private readonly VirtualPath _userPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _loggedRevisions = new(StringComparer.Ordinal);
    private SettingsFilesWatcher? _watcher;
    private LayeredConfigSnapshot _snapshot;
    private bool _disposed;

    private HackSettingsDocument(
        IAppFileSystemGateway fileSystem,
        IAppLoggingGateway? logging,
        SettingsSchema schema,
        VirtualPath systemPath,
        VirtualPath userPath)
    {
        _fileSystem = fileSystem;
        _logging = logging;
        _schema = schema;
        _systemPath = systemPath;
        _userPath = userPath;
        _snapshot = LayeredConfigResolver.Resolve(schema, null, null, systemPath.Value, userPath.Value);
    }

    /// <summary>Gets the system layer path, <c>/etc/&lt;appId&gt;/&lt;document&gt;.config</c>.</summary>
    public string SystemPath => _systemPath.Value;

    /// <summary>Gets the user layer path, <c>/home/&lt;user&gt;/.config/&lt;appId&gt;/&lt;document&gt;.config</c>.</summary>
    public string UserPath => _userPath.Value;

    /// <summary>Gets the declared schema of this document.</summary>
    public SettingsSchema Schema => _schema;

    /// <summary>Gets every problem found in the two files at the last read.</summary>
    public IReadOnlyList<LayeredConfigWarning> Warnings => _snapshot.Warnings;

    /// <summary>
    /// Raised after a write by this document or an external change of either file (seen through a
    /// filesystem watch) modified at least one effective value, source layer or lock.
    /// </summary>
    public event EventHandler<HackSettingsChangedEventArgs>? Changed;

    /// <summary>Opens a document, reads both layers and starts watching them.</summary>
    internal static async ValueTask<HackSettingsDocument> OpenAsync(
        IAppExecutionContext context,
        SettingsSchema schema,
        string documentId,
        CancellationToken cancellationToken)
    {
        string appId = context.Manifest.Id;
        string home = context.HomeDirectory.TrimEnd('/');
        HackSettingsDocument document = new(
            context.FileSystem,
            TryGetLogging(context),
            schema,
            VirtualPath.Parse($"/etc/{appId}/{documentId}.config"),
            VirtualPath.Parse($"{home}/.config/{appId}/{documentId}.config"));

        await document.ReloadAsync(raiseChanged: false, cancellationToken).ConfigureAwait(false);

        IAppFileSystemWatchGateway? watch = TryGetWatch(context);
        if (watch is not null)
        {
            document._watcher = new SettingsFilesWatcher(
                watch,
                [document._systemPath, document._userPath],
                () => new ValueTask(document.ReloadAsync(raiseChanged: true, CancellationToken.None)));
            await document._watcher.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        return document;
    }

    /// <summary>Gets the effective value of a declared key.</summary>
    /// <typeparam name="T">
    /// <see cref="string"/> (any field), <see cref="long"/> or <see cref="int"/> (integer field),
    /// <see cref="bool"/> (boolean field), or a C# enum whose member names match the field's allowed
    /// values ignoring case, <c>-</c> and <c>_</c> (enum field).
    /// </typeparam>
    /// <param name="qualifiedKey"><c>key</c> or <c>Group.key</c>, as declared.</param>
    /// <exception cref="ArgumentException">The key is not declared in the schema.</exception>
    /// <exception cref="InvalidCastException"><typeparamref name="T"/> does not fit the field's type.</exception>
    public T Get<T>(string qualifiedKey)
    {
        SettingFieldDeclaration field = RequireField(qualifiedKey);
        return SettingValueConverter.FromSetting<T>(field, _snapshot.Values[field.QualifiedKey]);
    }

    /// <summary>Gets the layer that supplied the effective value of a declared key.</summary>
    /// <param name="qualifiedKey"><c>key</c> or <c>Group.key</c>, as declared.</param>
    /// <exception cref="ArgumentException">The key is not declared in the schema.</exception>
    public SettingLayer GetSource(string qualifiedKey) => _snapshot.Sources[RequireField(qualifiedKey).QualifiedKey];

    /// <summary>Gets whether the system layer locks a declared key.</summary>
    /// <param name="qualifiedKey"><c>key</c> or <c>Group.key</c>, as declared.</param>
    /// <exception cref="ArgumentException">The key is not declared in the schema.</exception>
    public bool IsLocked(string qualifiedKey) => _snapshot.LockedKeys.Contains(RequireField(qualifiedKey).QualifiedKey);

    /// <summary>Writes a typed value to one layer, preserving every other line of the file.</summary>
    /// <param name="qualifiedKey"><c>key</c> or <c>Group.key</c>, as declared.</param>
    /// <param name="value">New value; see <see cref="Get{T}"/> for the accepted types.</param>
    /// <param name="target"><see cref="SettingLayer.User"/> (default) or <see cref="SettingLayer.System"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException">The key is undeclared or the value is invalid for its type.</exception>
    /// <exception cref="HackSettingLockedException">The user layer is targeted and the key is locked.</exception>
    /// <exception cref="UnauthorizedAccessException">The file's permissions deny the write.</exception>
    /// <exception cref="IOException">The write failed for another filesystem reason.</exception>
    public async ValueTask SetAsync<T>(
        string qualifiedKey,
        T value,
        SettingLayer target = SettingLayer.User,
        CancellationToken cancellationToken = default)
    {
        SettingFieldDeclaration field = RequireField(qualifiedKey);
        string serialized = SettingValueConverter.ToSetting(field, value);
        if (!SettingValueCodec.TryNormalize(field, serialized, out string normalized))
        {
            throw new ArgumentException(
                $"Value '{serialized}' is invalid for setting '{field.QualifiedKey}' (expected {SettingValueCodec.DescribeExpected(field)}).",
                nameof(value));
        }

        await EditLayerAsync(
            field,
            target,
            content => ConfigDocumentEditor.SetValue(content, field.Group, field.Key, normalized),
            createIfMissing: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a key from one layer, so the next layer (or the default) applies again.</summary>
    /// <param name="qualifiedKey"><c>key</c> or <c>Group.key</c>, as declared.</param>
    /// <param name="target"><see cref="SettingLayer.User"/> (default) or <see cref="SettingLayer.System"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException">The key is not declared in the schema.</exception>
    /// <exception cref="UnauthorizedAccessException">The file's permissions deny the write.</exception>
    public async ValueTask ResetAsync(
        string qualifiedKey,
        SettingLayer target = SettingLayer.User,
        CancellationToken cancellationToken = default)
    {
        SettingFieldDeclaration field = RequireField(qualifiedKey);
        await EditLayerAsync(
            field,
            target,
            content => ConfigDocumentEditor.RemoveValue(content, field.Group, field.Key),
            createIfMissing: false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Re-reads both files now, raising <see cref="Changed"/> when something changed.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default) =>
        new(ReloadAsync(raiseChanged: true, cancellationToken));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_watcher is not null)
        {
            await _watcher.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask EditLayerAsync(
        SettingFieldDeclaration field,
        SettingLayer target,
        Func<string?, string> edit,
        bool createIfMissing,
        CancellationToken cancellationToken)
    {
        VirtualPath path = target switch
        {
            SettingLayer.User => _userPath,
            SettingLayer.System => _systemPath,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Only the User and System layers can be written.")
        };

        await ReloadAsync(raiseChanged: true, cancellationToken).ConfigureAwait(false);
        // Only a write that sets a value is pointless on a locked key; removing a stale user value is not.
        bool setsValue = createIfMissing;
        if (target == SettingLayer.User && setsValue && _snapshot.LockedKeys.Contains(field.QualifiedKey))
        {
            throw new HackSettingLockedException(field.QualifiedKey, SystemPath);
        }

        FileSystemError? error = await SettingsTextFile
            .EditAsync(_fileSystem, path, edit, createIfMissing, cancellationToken)
            .ConfigureAwait(false);
        if (error is not null && !(error.Code == FileSystemErrorCode.NotFound && !createIfMissing))
        {
            throw SettingsErrorTranslator.ToException(error);
        }

        await ReloadAsync(raiseChanged: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReloadAsync(bool raiseChanged, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        LayeredConfigSnapshot previous;
        LayeredConfigSnapshot next;
        try
        {
            SettingsFileReadResult system = await SettingsTextFile.ReadAsync(_fileSystem, _systemPath, cancellationToken).ConfigureAwait(false);
            SettingsFileReadResult user = await SettingsTextFile.ReadAsync(_fileSystem, _userPath, cancellationToken).ConfigureAwait(false);
            if (system.Error is not null)
            {
                throw SettingsErrorTranslator.ToException(system.Error);
            }

            if (user.Error is not null)
            {
                throw SettingsErrorTranslator.ToException(user.Error);
            }

            next = LayeredConfigResolver.Resolve(_schema, system.Content, user.Content, SystemPath, UserPath);
            previous = _snapshot;
            _snapshot = next;
            LogNewWarnings(next, system.Revision, user.Revision);
        }
        finally
        {
            _gate.Release();
        }

        if (raiseChanged)
        {
            IReadOnlyList<string> changed = Diff(previous, next);
            if (changed.Count > 0)
            {
                Changed?.Invoke(this, new HackSettingsChangedEventArgs(changed));
            }
        }
    }

    private void LogNewWarnings(LayeredConfigSnapshot snapshot, long? systemRevision, long? userRevision)
    {
        if (_logging is null || snapshot.Warnings.Count == 0)
        {
            return;
        }

        // One warning per problem, per file and per revision: re-reading an unchanged bad file
        // (every watch event, every write to the other layer) must not flood the log.
        foreach (LayeredConfigWarning warning in snapshot.Warnings)
        {
            long? revision = warning.Path == SystemPath ? systemRevision : userRevision;
            if (_loggedRevisions.Add($"{warning.Path}@{revision}:{warning.LineNumber}:{warning.Message}"))
            {
                _logging.Log(DiagnosticSeverity.Warning, warning.ToLogLine());
            }
        }
    }

    private static IReadOnlyList<string> Diff(LayeredConfigSnapshot previous, LayeredConfigSnapshot next)
    {
        List<string> changed = [];
        foreach ((string key, string value) in next.Values)
        {
            if (!previous.Values.TryGetValue(key, out string? before)
                || before != value
                || previous.Sources[key] != next.Sources[key]
                || previous.LockedKeys.Contains(key) != next.LockedKeys.Contains(key))
            {
                changed.Add(key);
            }
        }

        return changed;
    }

    private SettingFieldDeclaration RequireField(string qualifiedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedKey);
        return _schema.Fields.TryGetValue(qualifiedKey, out SettingFieldDeclaration? field)
            ? field
            : throw new ArgumentException($"Setting '{qualifiedKey}' is not declared in this app's settings schema.", nameof(qualifiedKey));
    }

    private static IAppLoggingGateway? TryGetLogging(IAppExecutionContext context)
    {
        try
        {
            return context.Logging;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static IAppFileSystemWatchGateway? TryGetWatch(IAppExecutionContext context)
    {
        try
        {
            return context.Watch;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
