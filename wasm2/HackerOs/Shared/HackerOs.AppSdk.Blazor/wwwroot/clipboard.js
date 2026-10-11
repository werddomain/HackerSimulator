// Host clipboard helper of HackerOs.AppSdk.Blazor (see Interop/BrowserClipboard.cs).
// Copies text to the clipboard of the real browser, so it can be pasted outside HackerOS. The
// simulated clipboard of HackerOS (IAppClipboardGateway) is a different thing and never touches it.
export async function writeText(text) {
    await navigator.clipboard.writeText(text);
}
