using HackerOs.Apps.Browser;
using Xunit;

namespace HackerOs.Apps.Browser.Tests;

public sealed class BrowserWindowTests
{
    [Fact]
    public void ResolveSubmitUrl_RelativePostPath_ResolvesAgainstCurrentHost()
    {
        string result = BrowserWindow.ResolveSubmitUrl("https://hackersearch.net/results?q=x", "/login");

        Assert.Equal("https://hackersearch.net/login", result);
    }

    [Fact]
    public void ResolveSubmitUrl_AbsolutePostPath_IsUsedAsIs()
    {
        string result = BrowserWindow.ResolveSubmitUrl("https://hackersearch.net/", "https://other-host.example/submit");

        Assert.Equal("https://other-host.example/submit", result);
    }

    [Fact]
    public void ResolveSubmitUrl_NoCurrentPage_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BrowserWindow.ResolveSubmitUrl(string.Empty, "/login"));
    }

    [Fact]
    public void ResolveSubmitUrl_MalformedCurrentUrl_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BrowserWindow.ResolveSubmitUrl("not-a-valid-url", "/login"));
    }

    [Fact]
    public void ResolveSubmitUrl_PreservesHttpsScheme_NotOriginalPath()
    {
        string result = BrowserWindow.ResolveSubmitUrl("https://hackmail.com/inbox/message/42", "/submit");

        Assert.Equal("https://hackmail.com/submit", result);
    }
}
