namespace HackerOs.App.Abstractions;

/// <summary>
/// Converts an <see cref="AppDefinition"/> into the schema-version-1 <see cref="AppManifest"/>
/// (ADR 0047 §1). Pure and deterministic: it only reads the definition's getters, its
/// <see cref="AppManifestAttribute"/> and the entry-point types' names, never loads or activates anything.
/// </summary>
public static class AppDefinitionMapper
{
    /// <summary>
    /// Maps <paramref name="definition"/> using the <c>major.minor.0</c> version of the
    /// <c>HackerOs.App.Abstractions</c> assembly loaded in the current process as
    /// <c>sdkCompatibility.minimumVersion</c>.
    /// </summary>
    /// <param name="definition">Definition to map.</param>
    /// <returns>The equivalent manifest. Not validated: call <see cref="AppManifestValidator.Validate"/>.</returns>
    public static AppManifest ToManifest(AppDefinition definition) =>
        ToManifest(definition, CurrentSdkMinimumVersion);

    /// <summary>
    /// Maps <paramref name="definition"/> with an explicit SDK minimum version. The manifest generator
    /// uses this overload with the App Abstractions version the app was <i>compiled</i> against.
    /// </summary>
    /// <param name="definition">Definition to map.</param>
    /// <param name="sdkMinimumVersion">Value written to <c>sdkCompatibility.minimumVersion</c>.</param>
    /// <returns>The equivalent manifest. Not validated: call <see cref="AppManifestValidator.Validate"/>.</returns>
    public static AppManifest ToManifest(AppDefinition definition, string sdkMinimumVersion)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(sdkMinimumVersion);

        Type entryPoint = definition.EntryPoint
            ?? throw new InvalidOperationException($"'{definition.GetType().FullName}' returned a null EntryPoint.");
        Type? mobileEntryPoint = definition.MobileEntryPoint;
        TerminalCommand? terminal = definition.Terminal;
        IReadOnlyList<AppComponent> components = definition.Components;
        IReadOnlyList<string> urls = definition.Urls;
        Type? blazorRoot = definition.BlazorRoot;

        return new AppManifest
        {
            SchemaVersion = 1,
            Id = definition.Id,
            Name = definition.Name,
            Version = definition.Version,
            PublisherId = definition.PublisherId,
            Description = definition.Description,
            Kind = definition.Kind,
            EntryPoint = mobileEntryPoint is null ? ToEntryPoint(entryPoint) : null,
            Platform = mobileEntryPoint is null ? null : ToPlatform(entryPoint, mobileEntryPoint),
            SdkCompatibility = new AppSdkCompatibilityManifest(sdkMinimumVersion),
            Presentation = new PresentationManifest(definition.Category, definition.LaunchVisibility, [.. definition.Icons]),
            Localizations = [.. definition.Localizations],
            Capabilities = [.. definition.Capabilities],
            DeclaredTopicPermissions = [.. definition.DeclaredTopicPermissions],
            Resources = definition.Resources,
            Settings = definition.Settings,
            Intents = [.. definition.Intents],
            Dependencies = [.. definition.Dependencies],
            Assets = [.. definition.Assets],
            FileHandlers = [.. definition.FileHandlers.Select(handler => new FileHandlerManifest(
                handler.MediaType, [.. handler.Extensions], [.. handler.Actions], handler.Priority))],
            Terminal = terminal is null
                ? null
                : new TerminalCommandManifest(terminal.Name, [.. terminal.Aliases ?? []], terminal.Usage),
            // Omitted when empty, so manifests of apps without components keep their bytes.
            Components = components.Count == 0
                ? null
                : [.. components.Select(component => new AppComponentManifest(
                    component.Contract, AssemblyFileName(component.Type), TypeName(component.Type)))],
            SingleInstancePerUser = definition.SingleInstancePerUser,
            AutoStart = definition.AutoStart,
            SupportsBack = definition.SupportsBack,
            // ADR 0070, section 1: always written resolved, so the host never guesses a default.
            Execution = AppExecutionHosts.Resolve(definition.Kind, DeclaredExecution(definition)),
            // ADR 0073: only a web app has a web part; omitted otherwise so other manifests keep their bytes.
            Web = definition.Kind == AppKind.Web || urls.Count > 0 || blazorRoot is not null
                ? new WebAppManifest(
                    [.. urls.Select(WebAppUrls.Normalize)],
                    // ADR 0084: omitted without a Blazor root, so MVC-only sites keep their bytes.
                    blazorRoot is null ? null : new WebBlazorManifest(AssemblyFileName(blazorRoot), TypeName(blazorRoot)))
                : null
        };
    }

    /// <summary>
    /// Reads <see cref="AppManifestAttribute.Execution"/> from the definition's class, or
    /// <see langword="null"/> when the class is not marked (a definition built by hand in a test).
    /// </summary>
    private static AppExecutionHost? DeclaredExecution(AppDefinition definition) =>
        Attribute.GetCustomAttribute(definition.GetType(), typeof(AppManifestAttribute), inherit: false) is AppManifestAttribute attribute
            ? attribute.Execution
            : null;

    /// <summary>
    /// Formats an App Abstractions assembly version as the <c>major.minor.0</c> SDK minimum version
    /// (ADR 0047 §1: patch releases never raise an app's minimum).
    /// </summary>
    /// <param name="version">Assembly version, or <see langword="null"/> for <c>1.0.0</c>.</param>
    /// <returns>The semantic version string.</returns>
    public static string FormatSdkMinimumVersion(Version? version) =>
        version is null ? "1.0.0" : $"{version.Major}.{Math.Max(version.Minor, 0)}.0";

    private static string CurrentSdkMinimumVersion =>
        FormatSdkMinimumVersion(typeof(AppDefinition).Assembly.GetName().Version);

    private static AppEntryPointManifest ToEntryPoint(Type type) =>
        new(AssemblyFileName(type), TypeName(type));

    /// <summary>
    /// Builds the <c>platform</c> form. When the mobile entry point is the desktop type, one entry
    /// covers both platforms (a shared component); otherwise each platform gets its own entry.
    /// </summary>
    private static AppManifestPlatform ToPlatform(Type desktop, Type mobile)
    {
        string desktopId = WellKnownAppPlatforms.Desktop.Value;
        string mobileId = WellKnownAppPlatforms.Mobile.Value;
        AppPlatformEntryPointManifest[] entryPoints = desktop == mobile
            ? [new([desktopId, mobileId], AssemblyFileName(desktop), TypeName(desktop))]
            :
            [
                new([desktopId], AssemblyFileName(desktop), TypeName(desktop)),
                new([mobileId], AssemblyFileName(mobile), TypeName(mobile))
            ];
        return new AppManifestPlatform([desktopId, mobileId], entryPoints);
    }

    private static string AssemblyFileName(Type type) => type.Assembly.GetName().Name + ".dll";

    private static string TypeName(Type type) =>
        type.FullName ?? throw new InvalidOperationException($"Entry-point type '{type}' has no full name (generic or nested open type).");
}
