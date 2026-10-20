using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HackerOs.AppSdk.FileView.Tests")]

// The File Explorer provides the OS file control (hackeros.file-view, ADR 0148) on top of this code,
// which becomes its internal implementation (ADR 0155 §1) until the SDK 2.0 moves it there (ADR 0149).
[assembly: InternalsVisibleTo("HackerOs.Apps.FileExplorer")]
