using System.Globalization;
using System.Net;
using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Clipboard;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.Net;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.WorkerHost.Protocol;

/// <summary>
/// Explicit wire format of every value a remote gateway sends or receives (ADR 0070, section 4).
/// </summary>
/// <remarks>
/// <para>
/// Written by hand rather than with reflection-based serialization: the contracts validate in their
/// constructors and several are built only through factories (<see cref="FileSystemResult{T}"/>,
/// <see cref="FileSystemTransactionResult"/>, <see cref="HackNetworkResult{T}"/>), and the project
/// is trim-analyzed. Each type is rebuilt on the other side through its public constructor or
/// factory, so a value that crosses the boundary is exactly as valid as the original.
/// </para>
/// <para>Enums travel as their numeric values; times as ISO 8601 round-trip strings.</para>
/// </remarks>
internal static class WireCodec
{
    // ---- Primitives ---------------------------------------------------------------------------

    public static void WriteTime(Utf8JsonWriter writer, string name, DateTimeOffset value) =>
        writer.WriteString(name, value.ToString("O", CultureInfo.InvariantCulture));

    public static DateTimeOffset ReadTime(JsonElement element, string name) =>
        DateTimeOffset.Parse(element.GetString(name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    public static string[] ReadStrings(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement array) && array.ValueKind == JsonValueKind.Array
            ? [.. array.EnumerateArray().Select(static item => item.GetString() ?? string.Empty)]
            : [];

    public static void WritePairs(Utf8JsonWriter writer, string name, IEnumerable<KeyValuePair<string, string>> pairs)
    {
        writer.WriteStartArray(name);
        foreach (KeyValuePair<string, string> pair in pairs)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(pair.Key);
            writer.WriteStringValue(pair.Value);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }

    public static KeyValuePair<string, string>[] ReadPairs(JsonElement element) =>
        [.. element.EnumerateArray().Select(static pair => new KeyValuePair<string, string>(pair[0].GetString()!, pair[1].GetString()!))];

    public static void WriteDictionary(Utf8JsonWriter writer, string name, IEnumerable<KeyValuePair<string, string>> values)
    {
        writer.WriteStartObject(name);
        foreach (KeyValuePair<string, string> pair in values)
        {
            writer.WriteString(pair.Key, pair.Value);
        }

        writer.WriteEndObject();
    }

    public static Dictionary<string, string> ReadDictionary(JsonElement element, string name)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        if (element.TryGetProperty(name, out JsonElement map) && map.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in map.EnumerateObject())
            {
                values[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return values;
    }

    // ---- Filesystem: values --------------------------------------------------------------------

    public static VirtualPath ReadPath(JsonElement element, string name) => VirtualPath.Parse(element.GetString(name));

    public static VirtualPath? ReadOptionalPath(JsonElement element, string name) =>
        element.GetOptionalString(name) is { } value ? VirtualPath.Parse(value) : null;

    public static void WriteMetadata(Utf8JsonWriter writer, string name, FileSystemEntryMetadata metadata)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("k", (int)metadata.Kind);
        writer.WriteString("id", metadata.Id.ToString());
        writer.WriteString("o", metadata.OwnerId);
        writer.WriteString("g", metadata.GroupId);
        writer.WriteNumber("m", metadata.Permissions.Mode);
        WriteTime(writer, "c", metadata.Timestamps.CreatedAtUtc);
        WriteTime(writer, "w", metadata.Timestamps.ContentModifiedAtUtc);
        WriteTime(writer, "x", metadata.Timestamps.MetadataChangedAtUtc);
        writer.WriteNumber("r", metadata.Revision);
        switch (metadata)
        {
            case FileMetadata file:
                writer.WriteNumber("l", file.Length);
                writer.WriteString("mt", file.MediaType);
                break;
            case SymbolicLinkMetadata link:
                writer.WriteString("tg", link.Target);
                break;
        }

        writer.WriteEndObject();
    }

    public static FileSystemEntryMetadata ReadMetadata(JsonElement element)
    {
        FileSystemEntryId id = FileSystemEntryId.Parse(element.GetString("id"));
        string owner = element.GetString("o");
        string group = element.GetString("g");
        FileSystemPermissions permissions = FileSystemPermissions.FromMode((ushort)element.GetInt32("m"));
        FileSystemTimestamps timestamps = new(ReadTime(element, "c"), ReadTime(element, "w"), ReadTime(element, "x"));
        long revision = element.GetInt64("r");
        return (FileSystemEntryKind)element.GetInt32("k") switch
        {
            FileSystemEntryKind.File => new FileMetadata(id, owner, group, permissions, timestamps, revision, element.GetInt64("l"), element.GetString("mt")),
            FileSystemEntryKind.Directory => new DirectoryMetadata(id, owner, group, permissions, timestamps, revision),
            FileSystemEntryKind.SymbolicLink => new SymbolicLinkMetadata(id, owner, group, permissions, timestamps, revision, element.GetString("tg")),
            FileSystemEntryKind.CharacterDevice => new CharacterDeviceMetadata(id, owner, group, permissions, timestamps, revision),
            FileSystemEntryKind kind => throw new JsonException($"Unknown entry kind {kind}.")
        };
    }

    public static void WriteSnapshot(Utf8JsonWriter writer, string name, FileSystemEntrySnapshot snapshot)
    {
        writer.WriteStartObject(name);
        writer.WriteString("p", snapshot.Path.Value);
        WriteMetadata(writer, "md", snapshot.Metadata);
        writer.WriteEndObject();
    }

    public static FileSystemEntrySnapshot ReadSnapshot(JsonElement element) =>
        new(ReadPath(element, "p"), ReadMetadata(element.GetProperty("md")));

    public static void WriteError(Utf8JsonWriter writer, string name, FileSystemError error)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("op", (int)error.Operation);
        writer.WriteNumber("c", (int)error.Code);
        if (error.Path is { } path)
        {
            writer.WriteString("p", path.Value);
        }

        if (error.RelatedPath is { } related)
        {
            writer.WriteString("rp", related.Value);
        }

        if (error.RequiredCapability is { } capability)
        {
            writer.WriteString("cap", capability);
        }

        writer.WriteEndObject();
    }

    public static FileSystemError ReadError(JsonElement element) => new(
        (FileSystemOperation)element.GetInt32("op"),
        (FileSystemErrorCode)element.GetInt32("c"),
        ReadOptionalPath(element, "p"),
        ReadOptionalPath(element, "rp"),
        element.GetOptionalString("cap"));

    public static void WriteDescriptor(Utf8JsonWriter writer, string name, FileSystemContentDescriptor descriptor)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("k", (int)descriptor.Kind);
        writer.WriteString("mt", descriptor.MediaType);
        if (descriptor.EncodingName is { } encoding)
        {
            writer.WriteString("enc", encoding);
        }

        writer.WriteEndObject();
    }

    public static FileSystemContentDescriptor ReadDescriptor(JsonElement element) =>
        (FileSystemContentKind)element.GetInt32("k") == FileSystemContentKind.Text
            ? FileSystemContentDescriptor.Text(element.GetString("mt"), element.GetOptionalString("enc") ?? "utf-8")
            : FileSystemContentDescriptor.Binary(element.GetString("mt"));

    /// <summary>Writes a result of type <typeparamref name="T"/>: <c>ok</c>, then <c>v</c> or <c>err</c>.</summary>
    public static void WriteResult<T>(Utf8JsonWriter writer, FileSystemResult<T> result, Action<Utf8JsonWriter, T> writeValue)
        where T : notnull
    {
        writer.WriteBoolean("ok", result.Succeeded);
        if (result.Succeeded)
        {
            writeValue(writer, result.Value!);
        }
        else
        {
            WriteError(writer, "err", result.Error!);
        }
    }

    public static FileSystemResult<T> ReadResult<T>(JsonElement element, Func<JsonElement, T> readValue)
        where T : notnull =>
        element.GetFlag("ok")
            ? FileSystemResult<T>.Success(readValue(element))
            : FileSystemResult<T>.Failure(ReadError(element.GetProperty("err")));

    public static void WriteDirectory(Utf8JsonWriter writer, FileSystemDirectorySnapshot snapshot)
    {
        writer.WriteString("p", snapshot.Path.Value);
        writer.WriteNumber("r", snapshot.Revision);
        writer.WriteStartArray("e");
        foreach (FileSystemDirectoryItem item in snapshot.Entries)
        {
            writer.WriteStartObject();
            writer.WriteString("n", item.Name.Value);
            WriteMetadata(writer, "md", item.Metadata);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public static FileSystemDirectorySnapshot ReadDirectory(JsonElement element) => new(
        ReadPath(element, "p"),
        element.GetInt64("r"),
        [.. element.GetProperty("e").EnumerateArray().Select(static item =>
            new FileSystemDirectoryItem(FileSystemEntryName.Parse(item.GetString("n")), ReadMetadata(item.GetProperty("md"))))]);

    public static void WriteMutation(Utf8JsonWriter writer, FileSystemMutationResult result)
    {
        FileSystemTransactionResult transaction = result.Transaction;
        writer.WriteString("tx", transaction.TransactionId.ToString("N"));
        writer.WriteNumber("s", (int)transaction.Status);
        WriteStrings(writer, "a", transaction.AffectedEntryIds.Select(static id => id.ToString()));
        if (transaction.Error is { } error)
        {
            WriteError(writer, "err", error);
        }

        if (result.Entry is { } entry)
        {
            WriteSnapshot(writer, "en", entry);
        }
    }

    public static FileSystemMutationResult ReadMutation(JsonElement element)
    {
        Guid id = Guid.ParseExact(element.GetString("tx"), "N");
        FileSystemTransactionResult transaction = (FileSystemTransactionStatus)element.GetInt32("s") switch
        {
            FileSystemTransactionStatus.Committed => FileSystemTransactionResult.Committed(
                id, ReadStrings(element, "a").Select(FileSystemEntryId.Parse)),
            FileSystemTransactionStatus.Cancelled => FileSystemTransactionResult.Cancelled(id, ReadError(element.GetProperty("err"))),
            _ => FileSystemTransactionResult.Rejected(id, ReadError(element.GetProperty("err")))
        };
        FileSystemEntrySnapshot? entry = element.GetOptional("en") is { } snapshot ? ReadSnapshot(snapshot) : null;
        return new FileSystemMutationResult(transaction, entry);
    }

    // ---- Filesystem: requests -----------------------------------------------------------------

    public static void WriteRead(Utf8JsonWriter writer, FileSystemReadRequest request)
    {
        writer.WriteString("p", request.Path.Value);
        writer.WriteNumber("lb", (int)request.LinkBehavior);
    }

    public static FileSystemReadRequest ReadRead(JsonElement element) =>
        new(ReadPath(element, "p"), (FileSystemLinkBehavior)element.GetInt32("lb"));

    public static void WriteEnumerate(Utf8JsonWriter writer, FileSystemEnumerateRequest request)
    {
        writer.WriteString("p", request.Path.Value);
        writer.WriteNumber("lb", (int)request.LinkBehavior);
    }

    public static FileSystemEnumerateRequest ReadEnumerate(JsonElement element) =>
        new(ReadPath(element, "p"), (FileSystemLinkBehavior)element.GetInt32("lb"));

    public static void WriteStat(Utf8JsonWriter writer, FileSystemStatRequest request)
    {
        writer.WriteString("p", request.Path.Value);
        writer.WriteNumber("lb", (int)request.LinkBehavior);
    }

    public static FileSystemStatRequest ReadStat(JsonElement element) =>
        new(ReadPath(element, "p"), (FileSystemLinkBehavior)element.GetInt32("lb"));

    public static void WriteCreate(Utf8JsonWriter writer, FileSystemCreateRequest request)
    {
        writer.WriteString("p", request.Path.Value);
        writer.WriteNumber("k", (int)request.Kind);
        writer.WriteNumber("m", request.Permissions.Mode);
        if (request.ExpectedParentRevision is { } revision)
        {
            writer.WriteNumber("pr", revision);
        }

        if (request.SymbolicLinkTarget is { } target)
        {
            writer.WriteString("tg", target);
        }
    }

    public static FileSystemCreateRequest ReadCreate(JsonElement element) => new(
        ReadPath(element, "p"),
        (FileSystemEntryKind)element.GetInt32("k"),
        FileSystemPermissions.FromMode((ushort)element.GetInt32("m")),
        element.GetOptional("pr")?.GetInt64(),
        element.GetOptionalString("tg"));

    public static void WriteWrite(Utf8JsonWriter writer, FileSystemWriteRequest request, FileSystemContentDescriptor descriptor, byte[] content)
    {
        writer.WriteString("p", request.Path.Value);
        if (request.ExpectedRevision is { } revision)
        {
            writer.WriteNumber("er", revision);
        }

        WriteDescriptor(writer, "d", descriptor);
        writer.WriteBase64String("b", content);
    }

    public static (FileSystemWriteRequest Request, FileSystemContentDescriptor Descriptor, byte[] Content) ReadWrite(JsonElement element) => (
        new FileSystemWriteRequest(ReadPath(element, "p"), element.GetOptional("er")?.GetInt64()),
        ReadDescriptor(element.GetProperty("d")),
        element.GetProperty("b").GetBytesFromBase64());

    public static void WriteMove(Utf8JsonWriter writer, FileSystemMoveRequest request)
    {
        writer.WriteString("s", request.SourcePath.Value);
        writer.WriteString("d", request.DestinationPath.Value);
        writer.WriteNumber("er", request.ExpectedEntryRevision);
        writer.WriteNumber("sr", request.ExpectedSourceParentRevision);
        writer.WriteNumber("dr", request.ExpectedDestinationParentRevision);
    }

    public static FileSystemMoveRequest ReadMove(JsonElement element) => new(
        ReadPath(element, "s"), ReadPath(element, "d"),
        element.GetInt64("er"), element.GetInt64("sr"), element.GetInt64("dr"));

    public static void WriteCopy(Utf8JsonWriter writer, FileSystemCopyRequest request)
    {
        writer.WriteString("s", request.SourcePath.Value);
        writer.WriteString("d", request.DestinationPath.Value);
        writer.WriteNumber("er", request.ExpectedEntryRevision);
        writer.WriteNumber("dr", request.ExpectedDestinationParentRevision);
    }

    public static FileSystemCopyRequest ReadCopy(JsonElement element) => new(
        ReadPath(element, "s"), ReadPath(element, "d"), element.GetInt64("er"), element.GetInt64("dr"));

    public static void WriteDelete(Utf8JsonWriter writer, FileSystemDeleteRequest request)
    {
        writer.WriteString("p", request.Path.Value);
        writer.WriteNumber("er", request.ExpectedEntryRevision);
        writer.WriteNumber("pr", request.ExpectedParentRevision);
        writer.WriteBoolean("rc", request.Recursive);
    }

    public static FileSystemDeleteRequest ReadDelete(JsonElement element) => new(
        ReadPath(element, "p"), element.GetInt64("er"), element.GetInt64("pr"), element.GetFlag("rc"));

    public static void WriteSetPermissions(Utf8JsonWriter writer, FileSystemSetPermissionsRequest request)
    {
        writer.WriteString("p", request.Path.Value);
        writer.WriteNumber("m", request.Permissions.Mode);
        writer.WriteNumber("er", request.ExpectedRevision);
        writer.WriteNumber("lb", (int)request.LinkBehavior);
    }

    public static FileSystemSetPermissionsRequest ReadSetPermissions(JsonElement element) => new(
        ReadPath(element, "p"),
        FileSystemPermissions.FromMode((ushort)element.GetInt32("m")),
        element.GetInt64("er"),
        (FileSystemLinkBehavior)element.GetInt32("lb"));

    // ---- Network ------------------------------------------------------------------------------

    public static void WriteNetwork<T>(Utf8JsonWriter writer, HackNetworkResult<T> result, Action<Utf8JsonWriter, T> writeValue)
    {
        writer.WriteNumber("ne", (int)result.Error);
        if (result.Message is { } message)
        {
            writer.WriteString("nm", message);
        }

        if (result.Succeeded && result.Value is { } value)
        {
            writeValue(writer, value);
        }
    }

    public static HackNetworkResult<T> ReadNetwork<T>(JsonElement element, Func<JsonElement, T> readValue)
    {
        HackNetworkError error = (HackNetworkError)element.GetInt32("ne");
        return error == HackNetworkError.None
            ? HackNetworkResult<T>.Success(readValue(element))
            : HackNetworkResult<T>.Failure(error, element.GetOptionalString("nm"));
    }

    public static void WriteHostEntry(Utf8JsonWriter writer, HackHostEntry entry)
    {
        writer.WriteString("h", entry.HostName);
        WriteStrings(writer, "ad", entry.AddressList.Select(static address => address.ToString()));
        WriteStrings(writer, "al", entry.Aliases);
    }

    public static HackHostEntry ReadHostEntry(JsonElement element) => new(
        element.GetString("h"),
        [.. ReadStrings(element, "ad").Select(IPAddress.Parse)],
        ReadStrings(element, "al"));

    public static void WritePingReply(Utf8JsonWriter writer, HackPingReply reply)
    {
        if (reply.Address is { } address)
        {
            writer.WriteString("ad", address.ToString());
        }

        writer.WriteNumber("st", (int)reply.Status);
        writer.WriteNumber("rt", reply.RoundtripTime.Ticks);
        writer.WriteNumber("ttl", reply.Ttl);
    }

    public static HackPingReply ReadPingReply(JsonElement element) => new(
        element.GetOptionalString("ad") is { } address ? IPAddress.Parse(address) : null,
        (HackIPStatus)element.GetInt32("st"),
        TimeSpan.FromTicks(element.GetInt64("rt")),
        element.GetInt32("ttl"));

    private static void WriteCookies(Utf8JsonWriter writer, string name, IEnumerable<HackCookie> cookies)
    {
        writer.WriteStartArray(name);
        foreach (HackCookie cookie in cookies)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(cookie.Domain);
            writer.WriteStringValue(cookie.Name);
            writer.WriteStringValue(cookie.Value);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }

    private static HackCookie[] ReadCookies(JsonElement element) =>
        [.. element.EnumerateArray().Select(static cookie => new HackCookie(cookie[0].GetString()!, cookie[1].GetString()!, cookie[2].GetString()!))];

    public static void WriteHttpRequest(Utf8JsonWriter writer, HackHttpRequest request)
    {
        writer.WriteString("me", request.Method);
        writer.WriteString("u", request.RequestUri.OriginalString);
        WritePairs(writer, "hd", request.Headers);
        if (request.Form is { } form)
        {
            WritePairs(writer, "fm", form);
        }

        WriteCookies(writer, "ck", request.Cookies);
    }

    public static HackHttpRequest ReadHttpRequest(JsonElement element) => new(
        element.GetString("me"),
        new Uri(element.GetString("u"), UriKind.Absolute),
        ReadPairs(element.GetProperty("hd")),
        element.GetOptional("fm") is { } form ? ReadPairs(form) : null,
        ReadCookies(element.GetProperty("ck")));

    /// <summary>
    /// Writes a response without its structured <see cref="HackHttpResponse.Page"/>: only the browser
    /// window app renders it, and window apps never run in a worker (ADR 0070, section 1).
    /// </summary>
    public static void WriteHttpResponse(Utf8JsonWriter writer, HackHttpResponse response)
    {
        writer.WriteNumber("sc", response.StatusCode);
        writer.WriteString("rp", response.ReasonPhrase);
        writer.WriteString("u", response.RequestUri.OriginalString);
        WritePairs(writer, "hd", response.Headers);
        writer.WriteBase64String("b", response.Content);
        writer.WriteNumber("rc", response.RedirectCount);
        WriteCookies(writer, "sk", response.SetCookies);
    }

    public static HackHttpResponse ReadHttpResponse(JsonElement element) => new(
        element.GetInt32("sc"),
        element.GetString("rp"),
        new Uri(element.GetString("u"), UriKind.Absolute),
        ReadPairs(element.GetProperty("hd")),
        element.GetProperty("b").GetBytesFromBase64(),
        Page: null,
        element.GetInt32("rc"),
        ReadCookies(element.GetProperty("sk")));

    public static void WritePortProbes(Utf8JsonWriter writer, IReadOnlyList<HackPortProbeResult> probes)
    {
        writer.WriteStartArray("pp");
        foreach (HackPortProbeResult probe in probes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("po", probe.Port);
            writer.WriteNumber("st", (int)probe.State);
            if (probe.ServiceName is { } service)
            {
                writer.WriteString("sn", service);
            }

            if (probe.ServiceVersion is { } version)
            {
                writer.WriteString("sv", version);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public static IReadOnlyList<HackPortProbeResult> ReadPortProbes(JsonElement element) =>
        [.. element.GetProperty("pp").EnumerateArray().Select(static probe => new HackPortProbeResult(
            probe.GetInt32("po"), (HackPortState)probe.GetInt32("st"), probe.GetOptionalString("sn"), probe.GetOptionalString("sv")))];

    public static void WriteFingerprint(Utf8JsonWriter writer, HackOsFingerprint? fingerprint)
    {
        if (fingerprint is null)
        {
            return;
        }

        writer.WriteStartObject("os");
        writer.WriteString("n", fingerprint.Name);
        writer.WriteString("v", fingerprint.Version);
        writer.WriteNumber("a", fingerprint.Accuracy);
        writer.WriteEndObject();
    }

    public static HackOsFingerprint? ReadFingerprint(JsonElement element) =>
        element.GetOptional("os") is { } os ? new HackOsFingerprint(os.GetString("n"), os.GetString("v"), os.GetInt32("a")) : null;

    // ---- Apps, services, drives, intents ---------------------------------------------------------

    public static void WriteApps(Utf8JsonWriter writer, IReadOnlyList<HackAppInfo> apps)
    {
        writer.WriteStartArray("apps");
        foreach (HackAppInfo app in apps)
        {
            writer.WriteStartObject();
            writer.WriteString("id", app.Id);
            writer.WriteString("n", app.Name);
            writer.WriteString("d", app.Description);
            writer.WriteNumber("k", (int)app.Kind);
            writer.WriteString("c", app.Category);
            if (app.Icon is { } icon)
            {
                writer.WriteString("i", icon);
            }

            writer.WriteString("v", app.Version);
            writer.WriteString("pb", app.PublisherId);
            writer.WriteBoolean("sys", app.IsSystemApp);
            writer.WriteBoolean("hid", app.IsHidden);
            writer.WriteStartArray("fh");
            foreach (HackAppFileHandler handler in app.FileHandlers)
            {
                writer.WriteStartObject();
                if (handler.MediaType is { } mediaType)
                {
                    writer.WriteString("mt", mediaType);
                }

                WriteStrings(writer, "ex", handler.Extensions);
                WriteStrings(writer, "ac", handler.Actions);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public static IReadOnlyList<HackAppInfo> ReadApps(JsonElement element) =>
        [.. element.GetProperty("apps").EnumerateArray().Select(static app => new HackAppInfo(
            app.GetString("id"),
            app.GetString("n"),
            app.GetString("d"),
            (AppKind)app.GetInt32("k"),
            app.GetString("c"),
            app.GetOptionalString("i"),
            app.GetString("v"),
            app.GetString("pb"),
            app.GetFlag("sys"),
            app.GetFlag("hid"),
            [.. app.GetProperty("fh").EnumerateArray().Select(static handler => new HackAppFileHandler(
                handler.GetOptionalString("mt"), ReadStrings(handler, "ex"), ReadStrings(handler, "ac")))]))];

    public static void WriteServiceResult(Utf8JsonWriter writer, ServiceControlResult result)
    {
        writer.WriteNumber("oc", (int)result.Outcome);
        if (result.ErrorCode is { } code)
        {
            writer.WriteString("ec", code);
        }
    }

    public static ServiceControlResult ReadServiceResult(JsonElement element) =>
        new((ServiceControlOutcome)element.GetInt32("oc"), element.GetOptionalString("ec"));

    public static void WriteDrives(Utf8JsonWriter writer, IReadOnlyList<HackDriveSnapshot> drives)
    {
        writer.WriteStartArray("dr");
        foreach (HackDriveSnapshot drive in drives)
        {
            writer.WriteStartObject();
            writer.WriteString("n", drive.Name);
            writer.WriteString("f", drive.DriveFormat);
            writer.WriteString("l", drive.VolumeLabel);
            writer.WriteNumber("t", drive.TotalSize);
            writer.WriteNumber("a", drive.AvailableFreeSpace);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public static IReadOnlyList<HackDriveSnapshot> ReadDrives(JsonElement element) =>
        [.. element.GetProperty("dr").EnumerateArray().Select(static drive => new HackDriveSnapshot(
            drive.GetString("n"), drive.GetString("f"), drive.GetString("l"), drive.GetInt64("t"), drive.GetInt64("a")))];

    public static void WriteLaunchResult(Utf8JsonWriter writer, AppIntentLaunchResult result)
    {
        writer.WriteNumber("oc", (int)result.Outcome);
        if (result.ErrorCode is { } code)
        {
            writer.WriteString("ec", code);
        }
    }

    public static AppIntentLaunchResult ReadLaunchResult(JsonElement element) =>
        new((AppIntentLaunchOutcome)element.GetInt32("oc"), element.GetOptionalString("ec"));

    public static void WriteOpenFileResult(Utf8JsonWriter writer, AppIntentOpenFileResult result)
    {
        writer.WriteNumber("oc", (int)result.Outcome);
        if (result.CandidateAppIds is { } candidates)
        {
            WriteStrings(writer, "ca", candidates);
        }

        if (result.ErrorCode is { } code)
        {
            writer.WriteString("ec", code);
        }
    }

    public static AppIntentOpenFileResult ReadOpenFileResult(JsonElement element) => new(
        (AppIntentOpenFileOutcome)element.GetInt32("oc"),
        element.TryGetProperty("ca", out _) ? ReadStrings(element, "ca") : null,
        element.GetOptionalString("ec"));

    public static void WriteClipboardFiles(Utf8JsonWriter writer, HackClipboardFileList? files)
    {
        if (files is null)
        {
            return;
        }

        writer.WriteStartObject("cf");
        writer.WriteNumber("op", (int)files.Operation);
        WriteStrings(writer, "ps", files.Paths);
        writer.WriteEndObject();
    }

    public static HackClipboardFileList? ReadClipboardFiles(JsonElement element) =>
        element.GetOptional("cf") is { } files
            ? new HackClipboardFileList((HackClipboardFileOperation)files.GetInt32("op"), ReadStrings(files, "ps"))
            : null;

    // ---- Processes ------------------------------------------------------------------------------

    public static void WriteProcess(Utf8JsonWriter writer, string name, ProcessRecord process)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("pid", process.Pid.Value);
        if (process.ParentPid is { } parent)
        {
            writer.WriteNumber("pp", parent.Value);
        }

        writer.WriteString("app", process.AppId);
        writer.WriteString("in", process.AppInstanceId.Value.ToString("N"));
        writer.WriteNumber("k", (int)process.Kind);
        writer.WriteString("u", process.UserId.Value.ToString("N"));
        writer.WriteString("s", process.SessionId.Value.ToString("N"));
        writer.WriteNumber("st", (int)process.State);
        ResourceProfile profile = process.ResourceProfile;
        writer.WriteStartArray("rp");
        foreach (double weight in new[]
                 {
                     profile.BaselineCpuWeight, profile.BurstCpuWeight, profile.BaselineMemoryWeight, profile.BurstMemoryWeight,
                     profile.BaselineStorageIoWeight, profile.BurstStorageIoWeight, profile.BaselineNetworkIoWeight, profile.BurstNetworkIoWeight
                 })
        {
            writer.WriteNumberValue(weight);
        }

        writer.WriteEndArray();
        if (process.ServiceHealth is { } health)
        {
            writer.WriteNumber("h", (int)health);
        }

        WriteTime(writer, "cr", process.CreatedAtUtc);
        if (process.StartedAtUtc is { } started)
        {
            WriteTime(writer, "sa", started);
        }

        if (process.StoppedAtUtc is { } stopped)
        {
            WriteTime(writer, "so", stopped);
        }

        if (process.ExitCode is { } code)
        {
            writer.WriteNumber("ec", code);
        }

        if (process.ExitReason is { } reason)
        {
            writer.WriteNumber("er", (int)reason);
        }

        writer.WriteEndObject();
    }

    public static ProcessRecord ReadProcess(JsonElement element)
    {
        double[] weights = [.. element.GetProperty("rp").EnumerateArray().Select(static weight => weight.GetDouble())];
        return new ProcessRecord(
            ProcessId.FromInt64(element.GetInt64("pid")),
            element.GetOptional("pp") is { } parent ? ProcessId.FromInt64(parent.GetInt64()) : null,
            element.GetString("app"),
            AppInstanceId.FromGuid(Guid.ParseExact(element.GetString("in"), "N")),
            (AppKind)element.GetInt32("k"),
            LocalUserId.FromGuid(Guid.ParseExact(element.GetString("u"), "N")),
            SessionId.FromGuid(Guid.ParseExact(element.GetString("s"), "N")),
            (ProcessState)element.GetInt32("st"),
            new ResourceProfile(weights[0], weights[1], weights[2], weights[3], weights[4], weights[5], weights[6], weights[7]),
            element.GetOptional("h") is { } health ? (ServiceHealth)health.GetInt32() : null,
            ReadTime(element, "cr"),
            element.TryGetProperty("sa", out _) ? ReadTime(element, "sa") : null,
            element.TryGetProperty("so", out _) ? ReadTime(element, "so") : null,
            element.GetOptional("ec")?.GetInt32(),
            element.GetOptional("er") is { } reason ? (ProcessExitReason)reason.GetInt32() : null);
    }
}
