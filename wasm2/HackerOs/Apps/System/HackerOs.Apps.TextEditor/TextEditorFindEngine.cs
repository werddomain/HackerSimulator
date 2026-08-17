namespace HackerOs.Apps.TextEditor;

/// <summary>
/// Pure, Blazor/JS-independent match-finding logic for the text editor's find bar, so cycling
/// and wrap-around behavior can be verified without rendering the component.
/// </summary>
public static class TextEditorFindEngine
{
    /// <summary>Finds every case-insensitive occurrence of <paramref name="query"/> in <paramref name="content"/>, in order.</summary>
    public static List<int> FindMatches(string content, string query)
    {
        List<int> indexes = [];
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(content))
        {
            return indexes;
        }

        int idx = 0;
        while ((idx = content.IndexOf(query, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            indexes.Add(idx);
            idx += query.Length;
        }
        return indexes;
    }

    /// <summary>Advances to the next match index, wrapping around; -1 when there are no matches.</summary>
    public static int Next(int currentIndex, int matchCount) =>
        matchCount == 0 ? -1 : (currentIndex + 1) % matchCount;

    /// <summary>Moves to the previous match index, wrapping around; -1 when there are no matches.</summary>
    public static int Previous(int currentIndex, int matchCount) =>
        matchCount == 0 ? -1 : (currentIndex - 1 + matchCount) % matchCount;
}
