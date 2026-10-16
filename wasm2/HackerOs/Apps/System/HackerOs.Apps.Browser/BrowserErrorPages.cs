using HackerOs.Apps.Browser.Net;

namespace HackerOs.Apps.Browser;

/// <summary>What the browser's own error page says.</summary>
/// <param name="Title">Headline ("This site can't be reached").</param>
/// <param name="Message">What happened, naming the host or file.</param>
/// <param name="Code">The <c>ERR_*</c> code, or <c>HTTP ERROR 404</c>.</param>
/// <param name="Hints">What the user can try.</param>
public sealed record BrowserErrorView(string Title, string Message, string Code, IReadOnlyList<string> Hints);

/// <summary>
/// The texts of the browser's error pages, modeled on Chromium's: one per <c>net::ERR_*</c> code a
/// request can end with, and the "HTTP ERROR" page of an error status sent with no body
/// (ADR 0075, "erreurs réseau" of chantier <c>web/4-browser-html</c>).
/// </summary>
public static class BrowserErrorPages
{
    /// <summary>Describes a network error of a navigation to <paramref name="url"/>.</summary>
    public static BrowserErrorView ForNetworkError(string code, Uri? url)
    {
        string host = url is null ? "This site" : url.Scheme == "file" ? BrowserUrl.ToLocalPath(url) : url.Host;
        string display = url is null ? string.Empty : BrowserUrl.ToDisplayString(url);
        return code switch
        {
            "ERR_NAME_NOT_RESOLVED" => new("This site can't be reached",
                $"{host}'s server IP address could not be found.", code,
                [$"Check if there is a typo in {host}.", "Check /etc/hosts and the name of the site the app declares.", "If the site is hosted in HackerOS, check that its app or service is installed."]),
            "ERR_CONNECTION_REFUSED" => new("This site can't be reached",
                $"{host} refused to connect.", code,
                ["Check that a process is listening on that port (netstat -tlnp).", "If the site is hosted in HackerOS, start its app or service."]),
            "ERR_HOST_UNREACHABLE" or "ERR_ADDRESS_UNREACHABLE" => new("This site can't be reached",
                $"{host} is unreachable.", code,
                ["Check the network connection (ip addr, ping)."]),
            "ERR_CONNECTION_TIMED_OUT" or "ERR_TIMED_OUT" => new("This site can't be reached",
                $"{host} took too long to respond.", code,
                ["Check the connection.", "Try again later."]),
            "ERR_TOO_MANY_REDIRECTS" => new("This page isn't working",
                $"{host} redirected you too many times.", code,
                ["Try clearing your cookies: close the window, they last as long as the tab."]),
            BrowserErrorCodes.FileNotFound => new("Your file couldn't be accessed",
                $"It may have been moved, edited, or deleted: {host}", code,
                ["Check the path with ls.", "Open the folder (file:///…/) to browse its content."]),
            BrowserErrorCodes.AccessDenied => new("Access to the file was denied",
                $"You don't have permission to read {host}.", code,
                ["Check the file's permissions with ls -l."]),
            BrowserErrorCodes.InvalidUrl => new("This site can't be reached",
                $"The address {display} is not a valid URL.", code, []),
            BrowserErrorCodes.UnknownUrlScheme => new("This page can't be loaded",
                $"The webpage at {display} could not be loaded because the browser does not know its scheme.", code, []),
            BrowserErrorCodes.BlockedLocalResource => new("This page has been blocked",
                $"A web page may not open the local file {display}.", code, []),
            _ => new("This site can't be reached",
                $"The webpage at {display} might be temporarily down or it may have moved permanently to a new web address.", code,
                ["Check the connection.", "Try again later."]),
        };
    }

    /// <summary>Describes an error status that came with no body to show.</summary>
    public static BrowserErrorView ForHttpStatus(int statusCode, Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        string code = $"HTTP ERROR {statusCode}";
        return statusCode switch
        {
            404 or 410 => new($"This {url.Host} page can't be found",
                $"No webpage was found for the web address: {url.AbsoluteUri}", code, ["Check the address for typos."]),
            401 or 403 => new($"Access to {url.Host} was denied",
                "You don't have authorization to view this page.", code, ["Sign in to the site, then try again."]),
            >= 500 => new("This page isn't working",
                $"{url.Host} is currently unable to handle this request.", code, ["Try again later.", "If you run this site, check its logs."]),
            _ => new("This page isn't working",
                $"{url.Host} answered with status {statusCode}.", code, []),
        };
    }
}
