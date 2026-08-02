using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core;

/// <summary>
/// Stores canonical settings documents in memory while enforcing production authorization semantics.
/// </summary>
/// <remarks>
/// This implementation supports headless tests and Phase 1 behavior. Browser
/// persistence will implement the same contracts without changing callers.
/// </remarks>
public sealed class InMemorySettingsDocumentService : ISettingsDocumentService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DocumentState> _documents = new(StringComparer.Ordinal);

    /// <summary>Initializes the service from validated document definitions.</summary>
    /// <param name="definitions">Documents available in a clean OS profile.</param>
    /// <exception cref="ArgumentException">A definition is duplicated or its initial content is invalid.</exception>
    public InMemorySettingsDocumentService(IEnumerable<SettingsDocumentDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (SettingsDocumentDefinition definition in definitions)
        {
            IReadOnlyList<string> errors = definition.Validator.Validate(definition.InitialContent);
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    $"Initial settings document '{definition.Path}' is invalid: {string.Join("; ", errors)}",
                    nameof(definitions));
            }

            if (!_documents.TryAdd(
                    definition.Path.Value,
                    new DocumentState(definition, definition.InitialContent, 1)))
            {
                throw new ArgumentException(
                    $"Settings document '{definition.Path}' is defined more than once.",
                    nameof(definitions));
            }
        }
    }

    /// <summary>Raised after a new settings revision commits successfully.</summary>
    public event EventHandler<SettingsDocumentChangedEventArgs>? DocumentChanged;

    /// <inheritdoc />
    public ValueTask<SettingsReadResult> ReadAsync(
        VirtualPath path,
        AppOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_documents.TryGetValue(path.Value, out DocumentState? state))
            {
                return ValueTask.FromResult(new SettingsReadResult(SettingsReadStatus.NotFound, ErrorCode: "settings.not-found"));
            }

            if (!IsAuthorized(
                    context,
                    state.Definition.ReadCapability,
                    state.Definition.MinimumReadAuthority))
            {
                return ValueTask.FromResult(new SettingsReadResult(SettingsReadStatus.Denied, ErrorCode: "settings.read-denied"));
            }

            return ValueTask.FromResult(new SettingsReadResult(
                SettingsReadStatus.Success,
                CreateSnapshot(state)));
        }
    }

    /// <inheritdoc />
    public ValueTask<SettingsWriteResult> WriteAsync(
        SettingsWriteRequest request,
        AppOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        SettingsDocumentSnapshot? committed = null;
        SettingsWriteResult result;

        lock (_sync)
        {
            if (!_documents.TryGetValue(request.Path.Value, out DocumentState? state))
            {
                return ValueTask.FromResult(new SettingsWriteResult(
                    SettingsWriteStatus.NotFound,
                    Errors: ["settings.not-found"]));
            }

            if (!IsAuthorized(
                    context,
                    state.Definition.WriteCapability,
                    state.Definition.MinimumWriteAuthority))
            {
                return ValueTask.FromResult(new SettingsWriteResult(
                    SettingsWriteStatus.Denied,
                    Errors: ["settings.write-denied"]));
            }

            if (state.Revision != request.ExpectedRevision)
            {
                return ValueTask.FromResult(new SettingsWriteResult(
                    SettingsWriteStatus.Conflict,
                    Errors: ["settings.revision-conflict"]));
            }

            IReadOnlyList<string> validationErrors = state.Definition.Validator.Validate(request.Content);
            if (validationErrors.Count > 0)
            {
                return ValueTask.FromResult(new SettingsWriteResult(
                    SettingsWriteStatus.Invalid,
                    Errors: validationErrors));
            }

            state.Content = request.Content;
            state.Revision++;
            committed = CreateSnapshot(state);
            result = new SettingsWriteResult(SettingsWriteStatus.Success, committed);
        }

        DocumentChanged?.Invoke(
            this,
            new SettingsDocumentChangedEventArgs(
                committed!,
                context.AppId,
                context.UserId,
                context.EffectiveAuthority));

        return ValueTask.FromResult(result);
    }

    private static bool IsAuthorized(
        AppOperationContext context,
        string requiredCapability,
        AppAuthority requiredAuthority) =>
        context.HasCapability(requiredCapability)
        && AppAuthorityPolicy.Satisfies(context.EffectiveAuthority, requiredAuthority);

    private static SettingsDocumentSnapshot CreateSnapshot(DocumentState state) =>
        new(
            state.Definition.Path,
            state.Content,
            state.Definition.MediaType,
            state.Revision);

    private sealed class DocumentState(
        SettingsDocumentDefinition definition,
        string content,
        long revision)
    {
        public SettingsDocumentDefinition Definition { get; } = definition;

        public string Content { get; set; } = content;

        public long Revision { get; set; } = revision;
    }
}

/// <summary>
/// Reports an audited settings revision after a successful commit.
/// </summary>
/// <param name="Document">Committed settings snapshot.</param>
/// <param name="AppId">Application responsible for the write.</param>
/// <param name="UserId">Acting authenticated user.</param>
/// <param name="Authority">Effective authority used for the write.</param>
public sealed class SettingsDocumentChangedEventArgs(
    SettingsDocumentSnapshot document,
    string appId,
    string userId,
    AppAuthority authority) : EventArgs
{
    /// <summary>Gets the committed settings snapshot.</summary>
    public SettingsDocumentSnapshot Document { get; } = document;

    /// <summary>Gets the application responsible for the write.</summary>
    public string AppId { get; } = appId;

    /// <summary>Gets the acting authenticated user.</summary>
    public string UserId { get; } = userId;

    /// <summary>Gets the effective authority used for the write.</summary>
    public AppAuthority Authority { get; } = authority;
}