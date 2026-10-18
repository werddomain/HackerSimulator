namespace HackerOs.Apps.Diagnostic;

/// <summary>
/// The page's cross-origin isolation as the first lines of <c>/proc/hackeros/workers</c> describe it
/// (ADR 0131, section 5): the inspector shows it next to the storage, with the reason when the page
/// is not isolated.
/// </summary>
/// <param name="IsIsolated">The page's <c>crossOriginIsolated</c>.</param>
/// <param name="Reason">The short reason (<c>setting</c>, <c>missing-headers</c>, <c>insecure-context</c>, <c>browser</c>, <c>not-browser</c>), or <c>-</c>.</param>
/// <param name="Fields">Every <c>key: value</c> line of the header, in order, for the tooltip.</param>
internal sealed record IsolationStatus(bool IsIsolated, string Reason, IReadOnlyList<KeyValuePair<string, string>> Fields)
{
    /// <summary>The file read, the same one <c>cat</c> shows.</summary>
    public const string ProcPath = "/proc/hackeros/workers";

    /// <summary>
    /// Reads the <c>key: value</c> lines at the top of the file (the table that follows has no colon);
    /// <see langword="null"/> when the file has no <c>cross-origin-isolated</c> line.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static IsolationStatus? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<KeyValuePair<string, string>> fields = [];
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                break; // "capacity 4": the worker part starts.
            }

            fields.Add(new(line[..colon].Trim(), line[(colon + 1)..].Trim()));
        }

        string? isolated = Value(fields, "cross-origin-isolated");
        return isolated is null
            ? null
            : new IsolationStatus(isolated == "yes", Value(fields, "isolation-reason") ?? "-", fields);

        static string? Value(List<KeyValuePair<string, string>> fields, string key) =>
            fields.FirstOrDefault(field => field.Key == key).Value;
    }
}
