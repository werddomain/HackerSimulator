using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Tests.Support;

/// <summary>In-memory <see cref="IAppSettingsGateway"/> double that enforces real optimistic-revision writes.</summary>
public sealed class FakeAppSettingsGateway : IAppSettingsGateway
{
    private readonly Dictionary<string, (string Content, long Revision)> _documents = new(StringComparer.Ordinal);

    public FakeAppSettingsGateway WithDocument(string path, string content, long revision = 1)
    {
        _documents[VirtualPath.Parse(path).Value] = (content, revision);
        return this;
    }

    public string? ContentOf(string path) =>
        _documents.TryGetValue(VirtualPath.Parse(path).Value, out (string Content, long Revision) doc) ? doc.Content : null;

    public ValueTask<SettingsReadResult> ReadAsync(VirtualPath path, CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(path.Value, out (string Content, long Revision) doc))
        {
            return ValueTask.FromResult(new SettingsReadResult(SettingsReadStatus.NotFound, ErrorCode: "settings.not-found"));
        }

        return ValueTask.FromResult(new SettingsReadResult(
            SettingsReadStatus.Success, new SettingsDocumentSnapshot(path, doc.Content, "application/json", doc.Revision)));
    }

    public ValueTask<SettingsWriteResult> WriteAsync(SettingsWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(request.Path.Value, out (string Content, long Revision) doc))
        {
            return ValueTask.FromResult(new SettingsWriteResult(SettingsWriteStatus.NotFound, Errors: ["settings.not-found"]));
        }

        if (doc.Revision != request.ExpectedRevision)
        {
            return ValueTask.FromResult(new SettingsWriteResult(SettingsWriteStatus.Conflict, Errors: ["settings.revision-conflict"]));
        }

        long newRevision = doc.Revision + 1;
        _documents[request.Path.Value] = (request.Content, newRevision);
        return ValueTask.FromResult(new SettingsWriteResult(
            SettingsWriteStatus.Success, new SettingsDocumentSnapshot(request.Path, request.Content, "application/json", newRevision)));
    }
}
