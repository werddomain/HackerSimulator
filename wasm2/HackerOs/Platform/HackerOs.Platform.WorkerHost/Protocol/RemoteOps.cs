namespace HackerOs.Platform.WorkerHost.Protocol;

/// <summary>
/// Names of the remote gateway operations (<c>o</c> of a <c>call</c> or <c>note</c> message), one per
/// SDK gateway member a worker can reach (ADR 0070, section 4). The OS-side dispatcher serves each
/// one through the process's trusted context, with the same capability checks as in-process.
/// </summary>
internal static class RemoteOps
{
    // Filesystem (IAppFileSystemGateway): read(2) is a handle opened once, then read in chunks.
    public const string FsRead = "fs.read";
    public const string FsReadChunk = "fs.chunk";
    public const string FsReadClose = "fs.close";
    public const string FsEnumerate = "fs.enum";
    public const string FsCreate = "fs.create";
    public const string FsWrite = "fs.write";
    public const string FsMove = "fs.move";
    public const string FsCopy = "fs.copy";
    public const string FsDelete = "fs.delete";
    public const string FsStat = "fs.stat";
    public const string FsSetPermissions = "fs.chmod";

    // Standard input (pipe or terminal of the shell).
    public const string StdinRead = "in.read";

    // Process environment (IAppEnvironmentGateway): local copy in the worker, written through.
    public const string EnvSetDirectory = "env.cd";
    public const string EnvSetVariable = "env.set";

    // Logging, notifications: fire-and-forget.
    public const string Log = "log";
    public const string NotificationPost = "notify.post";

    // Clock.
    public const string ClockDelay = "clock.delay";

    // Processes (IAppProcessGateway).
    public const string ProcessStop = "proc.stop";
    public const string ProcessKill = "proc.kill";

    // Network (IAppNetworkGateway).
    public const string NetResolve = "net.resolve";
    public const string NetReverse = "net.reverse";
    public const string NetPing = "net.ping";
    public const string NetHttp = "net.http";
    public const string NetTcp = "net.tcp";
    public const string NetOs = "net.os";

    // Listening sockets (ADR 0074, section 2): requests then travel as hreq/hres messages.
    public const string NetListen = "net.listen";
    public const string NetUnlisten = "net.unlisten";
    public const string NetListeners = "net.listeners";

    // App catalog, services, drives, intents, clipboard.
    public const string CatalogInstalled = "cat.installed";
    public const string CatalogHandlers = "cat.handlers";
    public const string ServiceStart = "svc.start";
    public const string ServiceStop = "svc.stop";
    public const string ServiceGetMode = "svc.getmode";
    public const string ServiceSetMode = "svc.setmode";
    public const string Drives = "drv.list";
    public const string IntentLaunch = "int.launch";
    public const string IntentOpenFile = "int.open";
    public const string ClipboardGetText = "clip.get";
    public const string ClipboardSetText = "clip.set";
    public const string ClipboardGetFiles = "clip.getfiles";
    public const string ClipboardSetFiles = "clip.setfiles";
    public const string ClipboardClear = "clip.clear";
}
