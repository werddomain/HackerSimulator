using System.Text.RegularExpressions;

namespace HackerOs.App.Abstractions;

/// <summary>
/// Validates application manifests before build-time registration or installation.
/// </summary>
public static partial class AppManifestValidator
{
    private const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Validates a manifest without loading or executing its application assembly.
    /// </summary>
    /// <param name="manifest">Manifest to validate.</param>
    /// <returns>Every validation failure found in deterministic order.</returns>
    public static ManifestValidationResult Validate(AppManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<ManifestValidationError> errors = [];

        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            AddError(errors, "manifest.schema.unsupported", "schemaVersion", "Only manifest schema version 1 is supported.");
        }

        ValidateAppId(manifest.Id, "id", errors);
        RequireText(manifest.Name, "name", errors);
        ValidateSemanticVersion(manifest.Version, "version", errors);
        ValidatePublisherId(manifest.PublisherId, errors);
        RequireText(manifest.Description, "description", errors);
        ValidatePlatformAndEntryPoint(manifest, errors);
        ValidateVersionRange(
            manifest.SdkCompatibility.MinimumVersion,
            manifest.SdkCompatibility.MaximumVersion,
            "sdkCompatibility",
            errors);
        if (manifest.OsCompatibility is not null)
        {
            ValidateVersionRange(
                manifest.OsCompatibility.MinimumVersion,
                manifest.OsCompatibility.MaximumVersion,
                "osCompatibility",
                errors);
        }

        ValidateCapabilities(manifest.Capabilities, errors);
        ValidateCapabilityKindCompatibility(manifest.Kind, manifest.Capabilities, errors);
        ValidateDeclaredTopicPermissions(manifest.Id, manifest.DeclaredTopicPermissions, errors);
        ValidateDependencies(manifest.Dependencies, errors);
        ValidateAssets(manifest.Assets, errors);
        ValidatePresentation(manifest.Presentation, manifest.Assets, errors);
        ValidateLocalizations(manifest.Localizations, errors);
        ValidateResources(manifest.Resources, errors);
        if (manifest.Settings is not null)
        {
            ValidateSettingsSchema(manifest.Settings, errors);
        }

        ValidateIntents(manifest.Intents, manifest.Assets, errors);
        if (manifest.Update is not null)
        {
            ValidateUpdate(manifest.Update, errors);
        }

        if (manifest.AutoStart && manifest.Kind != AppKind.Service)
        {
            AddError(errors, "manifest.autoStart.forbidden", "autoStart", "Only service apps can declare autoStart.");
        }

        ValidateKindSpecificFields(manifest, errors);

        return new ManifestValidationResult(errors);
    }

    /// <summary>
    /// Validates the mutually exclusive <see cref="AppManifest.EntryPoint"/> (legacy) /
    /// <see cref="AppManifest.Platform"/> (<c>MOB-003</c>) declaration, per plan §4.2.
    /// </summary>
    private static void ValidatePlatformAndEntryPoint(AppManifest manifest, List<ManifestValidationError> errors)
    {
        bool hasEntryPoint = manifest.EntryPoint is not null;
        bool hasPlatform = manifest.Platform is not null;

        if (!hasEntryPoint && !hasPlatform)
        {
            AddError(errors, "manifest.platform.required", "entryPoint", "Either 'entryPoint' or 'platform' must be declared.");
            return;
        }

        if (hasEntryPoint && hasPlatform)
        {
            AddError(errors, "manifest.platform.ambiguous", "platform", "Declare either 'entryPoint' or 'platform', not both.");
            return;
        }

        if (hasEntryPoint)
        {
            RequireText(manifest.EntryPoint!.Assembly, "entryPoint.assembly", errors);
            RequireText(manifest.EntryPoint!.Type, "entryPoint.type", errors);
            return;
        }

        ValidatePlatform(manifest.Platform!, errors);
    }

    private static void ValidatePlatform(AppManifestPlatform platform, List<ManifestValidationError> errors)
    {
        if (platform.Supported is null || platform.Supported.Count == 0)
        {
            AddError(errors, "manifest.platform.supported.empty", "platform.supported", "At least one supported platform is required.");
            return;
        }

        ValidateUniqueValues(platform.Supported, "platform.supported", errors);

        HashSet<AppPlatformId> supportedPlatforms = [];
        foreach (string rawPlatformId in platform.Supported)
        {
            if (!AppPlatformId.TryParse(rawPlatformId, out AppPlatformId platformId))
            {
                AddError(errors, "manifest.platform.id.invalid", "platform.supported", $"'{rawPlatformId}' is not a valid platform identifier.");
                continue;
            }

            supportedPlatforms.Add(platformId);
        }

        if (platform.EntryPoints is null || platform.EntryPoints.Count == 0)
        {
            AddError(errors, "manifest.platform.entryPoints.empty", "platform.entryPoints", "At least one entry point is required.");
            return;
        }

        Dictionary<AppPlatformId, int> coverage = [];
        foreach (AppPlatformEntryPointManifest entryPoint in platform.EntryPoints)
        {
            RequireText(entryPoint.Assembly, "platform.entryPoints.assembly", errors);
            RequireText(entryPoint.Type, "platform.entryPoints.type", errors);

            if (entryPoint.Platforms is null || entryPoint.Platforms.Count == 0)
            {
                AddError(errors, "manifest.platform.entryPoint.platforms.empty", "platform.entryPoints.platforms", "An entry point must cover at least one platform.");
                continue;
            }

            foreach (string rawPlatformId in entryPoint.Platforms)
            {
                if (!AppPlatformId.TryParse(rawPlatformId, out AppPlatformId platformId))
                {
                    AddError(errors, "manifest.platform.id.invalid", "platform.entryPoints.platforms", $"'{rawPlatformId}' is not a valid platform identifier.");
                    continue;
                }

                if (!supportedPlatforms.Contains(platformId))
                {
                    AddError(
                        errors,
                        "manifest.platform.entryPoint.platform.unsupported",
                        "platform.entryPoints.platforms",
                        $"Entry point references platform '{platformId}', which is not in 'platform.supported'.");
                    continue;
                }

                coverage[platformId] = coverage.GetValueOrDefault(platformId) + 1;
            }
        }

        foreach ((AppPlatformId platformId, int entryPointCount) in coverage)
        {
            if (entryPointCount > 1)
            {
                AddError(
                    errors,
                    "manifest.platform.entryPoint.platform.duplicate",
                    "platform.entryPoints",
                    $"Platform '{platformId}' is covered by more than one entry point.");
            }
        }

        foreach (AppPlatformId platformId in supportedPlatforms)
        {
            if (!coverage.ContainsKey(platformId))
            {
                AddError(
                    errors,
                    "manifest.platform.coverage.missing",
                    "platform.entryPoints",
                    $"Supported platform '{platformId}' is not covered by any entry point.");
            }
        }
    }

    /// <summary>
    /// ADR 0070, section 1: a window app needs the DOM and only runs on the main thread; the
    /// <c>default</c> placeholder of the attribute is never a manifest value.
    /// </summary>
    private static void ValidateExecution(AppManifest manifest, List<ManifestValidationError> errors)
    {
        if (manifest.Execution is not { } execution)
        {
            return;
        }

        if (!Enum.IsDefined(execution) || execution == AppExecutionHost.Default)
        {
            AddError(errors, "manifest.execution.invalid", "execution", "Execution must be mainThread, pool, dedicated or web.");
        }
        else if (manifest.Kind == AppKind.Window && execution != AppExecutionHost.MainThread)
        {
            AddError(errors, "manifest.execution.window", "execution", "Window apps run on the main thread: they need the DOM.");
        }
        else if (manifest.Kind == AppKind.Web && execution is not (AppExecutionHost.Web or AppExecutionHost.MainThread))
        {
            // ADR 0073, section 1: the server code of every site shares the web worker.
            AddError(errors, "manifest.execution.web", "execution", "Web apps run in the shared web worker (web) or on the main thread.");
        }
        else if (manifest.Kind != AppKind.Web && execution == AppExecutionHost.Web)
        {
            AddError(errors, "manifest.execution.web-only", "execution", "Only web apps run in the shared web worker.");
        }
    }

    /// <summary>
    /// Checks the web part of ADR 0073: a web app declares at least one valid, distinct URL; no
    /// other kind declares any; and a site never asks for the file system (its data lives in the
    /// host browser's IndexedDB, section 3).
    /// </summary>
    private static void ValidateWeb(AppManifest manifest, List<ManifestValidationError> errors)
    {
        if (manifest.Kind != AppKind.Web)
        {
            if (manifest.Web is not null)
            {
                AddError(errors, "manifest.web.forbidden", "web", "Only web apps can declare web URLs.");
            }

            return;
        }

        if (manifest.Web is not { Urls.Count: > 0 } web)
        {
            AddError(errors, "manifest.web.urls.required", "web.urls", "Web apps must declare at least one URL, e.g. 'http://intranet.hack/'.");
        }
        else
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (string url in web.Urls)
            {
                if (!WebAppUrls.TryParse(url, allowWildcardHost: false, out WebBinding binding, out string? error))
                {
                    AddError(errors, "manifest.web.url.invalid", "web.urls", error!);
                }
                else if (!seen.Add(binding.ToString()))
                {
                    AddError(errors, "manifest.web.url.duplicate", "web.urls", $"URL '{url}' is declared twice.");
                }
            }
        }

        if (manifest.Web?.Blazor is { } blazor)
        {
            // ADR 0084, section 1: lazy loading fetches the entry-point assembly only, as for components.
            RequireText(blazor.Type, "web.blazor.type", errors);
            if (!string.Equals(blazor.Assembly, manifest.EntryPoint?.Assembly, StringComparison.Ordinal))
            {
                AddError(errors, "manifest.web.blazor.assembly", "web.blazor.assembly", $"The Blazor root '{blazor.Type}' must live in the app's entry-point assembly.");
            }
        }

        foreach (string capability in manifest.Capabilities)
        {
            if (capability.StartsWith("filesystem.", StringComparison.Ordinal))
            {
                AddError(errors, "manifest.web.filesystem", "capabilities", $"A web site cannot request '{capability}': its data lives in the browser's database, not in the OS file system (ADR 0073).");
            }
        }
    }

    private static void ValidateKindSpecificFields(AppManifest manifest, List<ManifestValidationError> errors)
    {
        if (manifest.Kind == AppKind.Terminal)
        {
            if (manifest.Terminal is null)
            {
                AddError(errors, "manifest.terminal.required", "terminal", "Terminal apps must declare command metadata.");
            }
            else
            {
                ValidateCommandName(manifest.Terminal.Name, "terminal.name", errors);
                ValidateUniqueValues(manifest.Terminal.Aliases, "terminal.aliases", errors);
                foreach (string alias in manifest.Terminal.Aliases)
                {
                    ValidateCommandName(alias, "terminal.aliases", errors);
                }

                RequireText(manifest.Terminal.Usage, "terminal.usage", errors);
            }
        }
        else if (manifest.Terminal is not null)
        {
            AddError(errors, "manifest.terminal.forbidden", "terminal", "Only terminal apps can declare command metadata.");
        }

        if (manifest.Kind != AppKind.Window && manifest.FileHandlers.Count > 0)
        {
            AddError(errors, "manifest.fileHandlers.forbidden", "fileHandlers", "Only window apps can declare file handlers.");
        }

        if (manifest.Kind != AppKind.Window && manifest.SupportsBack)
        {
            AddError(errors, "manifest.supportsBack.forbidden", "supportsBack", "Only window apps can declare Back navigation support.");
        }

        ValidateExecution(manifest, errors);
        ValidateWeb(manifest, errors);
        ValidateComponents(manifest, errors);

        foreach (FileHandlerManifest handler in manifest.FileHandlers)
        {
            if (string.IsNullOrWhiteSpace(handler.MediaType) && handler.Extensions.Count == 0)
            {
                AddError(errors, "manifest.fileHandler.type.required", "fileHandlers", "A file handler needs a media type or extension.");
            }

            if (handler.Priority < 0)
            {
                AddError(errors, "manifest.fileHandler.priority.invalid", "fileHandlers.priority", "File handler priority must not be negative.");
            }

            ValidateUniqueValues(handler.Extensions, "fileHandlers.extensions", errors);
            ValidateUniqueValues(handler.Actions, "fileHandlers.actions", errors);
        }
    }

    /// <summary>
    /// Checks the embeddable components of ADR 0063: only window apps render inside other windows,
    /// a contract is registered once per app, and the component lives in the app's entry-point
    /// assembly, because lazy loading fetches exactly that one DLL.
    /// </summary>
    private static void ValidateComponents(AppManifest manifest, List<ManifestValidationError> errors)
    {
        if (manifest.Components is not { Count: > 0 } components)
        {
            return;
        }

        if (manifest.Kind != AppKind.Window)
        {
            AddError(errors, "manifest.components.forbidden", "components", "Only window apps can register embeddable components.");
        }

        ValidateUniqueValues(components.Select(component => component.Contract ?? string.Empty), "components.contract", errors);
        HashSet<string> entryAssemblies = new(StringComparer.Ordinal);
        if (manifest.EntryPoint is not null)
        {
            entryAssemblies.Add(manifest.EntryPoint.Assembly);
        }

        foreach (AppPlatformEntryPointManifest entryPoint in manifest.Platform?.EntryPoints ?? [])
        {
            entryAssemblies.Add(entryPoint.Assembly);
        }

        foreach (AppComponentManifest component in components)
        {
            if (!ComponentContractPattern().IsMatch(component.Contract ?? string.Empty))
            {
                AddError(errors, "manifest.component.contract.invalid", "components.contract", $"Component contract '{component.Contract}' must be lower-case dotted words, e.g. 'hackeros.terminal'.");
            }

            RequireText(component.Type, "components.type", errors);
            if (!entryAssemblies.Contains(component.Assembly ?? string.Empty))
            {
                AddError(errors, "manifest.component.assembly.invalid", "components.assembly", $"Component '{component.Contract}' must live in the app's entry-point assembly.");
            }
        }
    }

    private static void ValidateAssets(IReadOnlyList<AssetManifest> assets, List<ManifestValidationError> errors)
    {
        ValidateUniqueValues(assets.Select(asset => asset.Path), "assets.path", errors);

        foreach (AssetManifest asset in assets)
        {
            ValidatePackageRelativePath(asset.Path, "assets.path", errors);
            if (!Enum.IsDefined(asset.Kind))
            {
                AddError(errors, "manifest.asset.kind.invalid", "assets.kind", $"Asset kind for '{asset.Path}' is not recognized.");
            }

            if (!Sha256Pattern().IsMatch(asset.Sha256 ?? string.Empty))
            {
                AddError(errors, "manifest.asset.hash.invalid", "assets.sha256", $"Asset '{asset.Path}' needs a lowercase 64-character hexadecimal SHA-256 hash.");
            }
        }
    }

    private static void ValidatePresentation(
        PresentationManifest presentation,
        IReadOnlyList<AssetManifest> assets,
        List<ManifestValidationError> errors)
    {
        RequireText(presentation.Category, "presentation.category", errors);
        if (!Enum.IsDefined(presentation.LaunchVisibility))
        {
            AddError(errors, "manifest.presentation.launchVisibility.invalid", "presentation.launchVisibility", "Launch visibility is not recognized.");
        }

        HashSet<string> assetPaths = new(assets.Select(asset => asset.Path), StringComparer.Ordinal);
        foreach (string iconAssetPath in presentation.IconAssetPaths)
        {
            ValidatePackageRelativePath(iconAssetPath, "presentation.iconAssetPaths", errors);
            if (!assetPaths.Contains(iconAssetPath))
            {
                AddError(errors, "manifest.presentation.icon.undeclared", "presentation.iconAssetPaths", $"Icon asset '{iconAssetPath}' is not declared in assets.");
            }
        }
    }

    private static void ValidateLocalizations(IReadOnlyList<LocalizationManifest> localizations, List<ManifestValidationError> errors)
    {
        ValidateUniqueValues(localizations.Select(localization => localization.Culture), "localizations.culture", errors);

        foreach (LocalizationManifest localization in localizations)
        {
            RequireText(localization.Culture, "localizations.culture", errors);
            ValidatePackageRelativePath(localization.ResourcePath, "localizations.resourcePath", errors);
        }
    }

    private static void ValidateResources(AppResourceProfileManifest resources, List<ManifestValidationError> errors)
    {
        ValidateWeightPair(resources.BaselineCpuWeight, resources.BurstCpuWeight, "resources.cpuWeight", errors);
        ValidateWeightPair(resources.BaselineMemoryWeight, resources.BurstMemoryWeight, "resources.memoryWeight", errors);
        ValidateWeightPair(resources.BaselineStorageIoWeight, resources.BurstStorageIoWeight, "resources.storageIoWeight", errors);
        ValidateWeightPair(resources.BaselineNetworkIoWeight, resources.BurstNetworkIoWeight, "resources.networkIoWeight", errors);
    }

    private static void ValidateWeightPair(double baseline, double burst, string path, List<ManifestValidationError> errors)
    {
        if (baseline is < 0 or > 1)
        {
            AddError(errors, "manifest.resources.weight.range", $"{path}.baseline", "Weight must be within [0, 1].");
        }

        if (burst is < 0 or > 1)
        {
            AddError(errors, "manifest.resources.weight.range", $"{path}.burst", "Weight must be within [0, 1].");
        }

        if (burst < baseline)
        {
            AddError(errors, "manifest.resources.weight.burstBelowBaseline", path, "Burst weight must be at least the baseline weight.");
        }
    }

    private static void ValidateSettingsSchema(AppSettingsSchemaManifest settings, List<ManifestValidationError> errors)
    {
        if (settings.SchemaVersion < 1)
        {
            AddError(errors, "manifest.settings.schemaVersion.invalid", "settings.schemaVersion", "Settings schema version must be at least 1.");
        }

        ValidateUniqueValues(
            settings.Fields.Select(field => field.Group is null ? field.Key : $"{field.Group}.{field.Key}"),
            "settings.fields.key",
            errors);

        foreach (AppSettingFieldManifest field in settings.Fields)
        {
            RequireText(field.Key, "settings.fields.key", errors);
            if (!Enum.IsDefined(field.ValueType))
            {
                AddError(errors, "manifest.settings.field.valueType.invalid", "settings.fields.valueType", $"Setting '{field.Key}' has an unrecognized value type.");
            }

            if (!Enum.IsDefined(field.Scope))
            {
                AddError(errors, "manifest.settings.field.scope.invalid", "settings.fields.scope", $"Setting '{field.Key}' has an unrecognized scope.");
            }

            if (!Enum.IsDefined(field.Sensitivity))
            {
                AddError(errors, "manifest.settings.field.sensitivity.invalid", "settings.fields.sensitivity", $"Setting '{field.Key}' has an unrecognized sensitivity.");
            }

            // ADR 0044: [locked] is the system layer's reserved lock section, never a data group.
            if (string.Equals(field.Group, "locked", StringComparison.Ordinal))
            {
                AddError(errors, "manifest.settings.field.group.reserved", "settings.fields.group", $"Setting '{field.Key}' uses the reserved group name 'locked'.");
            }

            if (field.ValueType == AppSettingValueType.Enum && (field.AllowedValues is null || field.AllowedValues.Count == 0))
            {
                AddError(errors, "manifest.settings.field.allowedValues.required", "settings.fields.allowedValues", $"Enum setting '{field.Key}' must declare at least one allowed value.");
            }
        }
    }

    private static void ValidateIntents(
        IReadOnlyList<AppIntentDeclarationManifest> intents,
        IReadOnlyList<AssetManifest> assets,
        List<ManifestValidationError> errors)
    {
        ValidateUniqueValues(intents.Select(intent => intent.IntentId), "intents.intentId", errors);

        HashSet<string> assetPaths = new(assets.Select(asset => asset.Path), StringComparer.Ordinal);
        foreach (AppIntentDeclarationManifest intent in intents)
        {
            RequireText(intent.IntentId, "intents.intentId", errors);
            if (intent.PayloadSchemaAssetPath is not null)
            {
                ValidatePackageRelativePath(intent.PayloadSchemaAssetPath, "intents.payloadSchemaAssetPath", errors);
                if (!assetPaths.Contains(intent.PayloadSchemaAssetPath))
                {
                    AddError(errors, "manifest.intent.payloadSchema.undeclared", "intents.payloadSchemaAssetPath", $"Payload schema asset '{intent.PayloadSchemaAssetPath}' is not declared in assets.");
                }
            }
        }
    }

    private static void ValidateUpdate(UpdateManifest update, List<ManifestValidationError> errors)
    {
        ValidateUniqueValues(update.MigrationIds, "update.migrationIds", errors);
        foreach (string migrationId in update.MigrationIds)
        {
            RequireText(migrationId, "update.migrationIds", errors);
        }

        if (update.MinimumUpgradableVersion is not null)
        {
            ValidateSemanticVersion(update.MinimumUpgradableVersion, "update.minimumUpgradableVersion", errors);
        }
    }

    private static void ValidatePackageRelativePath(string value, string path, List<ManifestValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, "manifest.value.required", path, "A non-empty value is required.");
            return;
        }

        bool isInvalid = value.StartsWith('/')
            || value.Contains('\\')
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('?')
            || value.Contains('#')
            || value.Contains("://", StringComparison.Ordinal);

        if (isInvalid)
        {
            AddError(
                errors,
                "manifest.path.invalid",
                path,
                "Paths must be package-relative, use '/' separators, and must not contain '..', backslashes, queries, fragments, or external URLs.");
        }
    }

    private static void ValidateDependencies(
        IReadOnlyList<AppDependencyManifest> dependencies,
        List<ManifestValidationError> errors)
    {
        ValidateUniqueValues(dependencies.Select(dependency => dependency.AppId), "dependencies", errors);

        foreach (AppDependencyManifest dependency in dependencies)
        {
            ValidateAppId(dependency.AppId, "dependencies.appId", errors);
            ValidateVersionRange(dependency.MinimumVersion, dependency.MaximumVersion, "dependencies.version", errors);
        }
    }

    private static void ValidateCapabilities(
        IReadOnlyList<string> capabilities,
        List<ManifestValidationError> errors)
    {
        ValidateUniqueValues(capabilities, "capabilities", errors);

        foreach (string capability in capabilities)
        {
            if (!AppCapabilities.IsKnown(capability) && !TopicPermissions.IsWellFormed(capability))
            {
                AddError(
                    errors,
                    "manifest.capability.unknown",
                    "capabilities",
                    $"Capability '{capability}' is not recognized by this App SDK version and is not a " +
                    "well-formed topic permission.");
            }
        }
    }

    private static void ValidateDeclaredTopicPermissions(
        string appId,
        IReadOnlyList<TopicPermissionDeclarationManifest> declarations,
        List<ManifestValidationError> errors)
    {
        // A manifest deserialized from JSON that predates this field (every manifest.json shipped before
        // this change) leaves it null rather than running the record's [] field initializer, the same as
        // every other optional array-typed manifest property would if a caller omitted it.
        declarations ??= [];

        ValidateUniqueValues(declarations.Select(static declaration => declaration.Id), "declaredTopicPermissions", errors);

        foreach (TopicPermissionDeclarationManifest declaration in declarations)
        {
            RequireText(declaration.Description, "declaredTopicPermissions.description", errors);

            if (!TopicPermissions.IsWellFormed(declaration.Id))
            {
                AddError(
                    errors,
                    "manifest.topicPermission.malformed",
                    "declaredTopicPermissions",
                    $"'{declaration.Id}' is not a well-formed topic permission identifier.");
                continue;
            }

            if (!TopicPermissions.IsOwnedByApp(declaration.Id, appId))
            {
                AddError(
                    errors,
                    "manifest.topicPermission.notOwned",
                    "declaredTopicPermissions",
                    $"'{declaration.Id}' must be rooted under this app's own topic namespace ('app/{appId}/...').");
            }
        }
    }

    private static void ValidateCapabilityKindCompatibility(
        AppKind kind,
        IReadOnlyList<string> capabilities,
        List<ManifestValidationError> errors)
    {
        if (kind == AppKind.Window)
        {
            return;
        }

        foreach (string capability in capabilities)
        {
            if (AppCapabilities.RequiresWindowKind(capability))
            {
                AddError(
                    errors,
                    "manifest.capability.incompatible",
                    "capabilities",
                    $"Capability '{capability}' renders modal UI and requires a window app.");
            }
        }
    }

    private static void ValidateVersionRange(
        string minimumVersion,
        string? maximumVersion,
        string path,
        List<ManifestValidationError> errors)
    {
        bool minimumIsValid = SemanticVersion.TryParse(minimumVersion, out SemanticVersion minimum);
        if (!minimumIsValid)
        {
            AddError(errors, "manifest.version.invalid", $"{path}.minimumVersion", "A semantic version with major, minor, and patch is required.");
        }

        if (maximumVersion is null)
        {
            return;
        }

        bool maximumIsValid = SemanticVersion.TryParse(maximumVersion, out SemanticVersion maximum);
        if (!maximumIsValid)
        {
            AddError(errors, "manifest.version.invalid", $"{path}.maximumVersion", "A semantic version with major, minor, and patch is required.");
        }
        else if (minimumIsValid && maximum.CompareTo(minimum) < 0)
        {
            AddError(errors, "manifest.version.range.invalid", path, "Maximum version must not be lower than minimum version.");
        }
    }

    private static void ValidateSemanticVersion(string value, string path, List<ManifestValidationError> errors)
    {
        if (!SemanticVersion.TryParse(value, out _))
        {
            AddError(errors, "manifest.version.invalid", path, "A semantic version with major, minor, and patch is required.");
        }
    }

    private static void ValidateAppId(string value, string path, List<ManifestValidationError> errors)
    {
        if (!AppIdPattern().IsMatch(value ?? string.Empty))
        {
            AddError(errors, "manifest.appId.invalid", path, "Use a lowercase reverse-domain ID with at least three segments.");
        }
    }

    private static void ValidatePublisherId(string value, List<ManifestValidationError> errors)
    {
        if (!PublisherIdPattern().IsMatch(value ?? string.Empty))
        {
            AddError(errors, "manifest.publisherId.invalid", "publisherId", "Use a lowercase reverse-domain publisher ID.");
        }
    }

    private static void ValidateCommandName(string value, string path, List<ManifestValidationError> errors)
    {
        if (!CommandNamePattern().IsMatch(value ?? string.Empty))
        {
            AddError(errors, "manifest.command.invalid", path, "Command names must use lowercase letters, digits, and hyphens.");
        }
    }

    private static void RequireText(string value, string path, List<ManifestValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, "manifest.value.required", path, "A non-empty value is required.");
        }
    }

    private static void ValidateUniqueValues(IEnumerable<string> values, string path, List<ManifestValidationError> errors)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            if (!seen.Add(value))
            {
                AddError(errors, "manifest.value.duplicate", path, $"Duplicate value '{value}' is not allowed.");
            }
        }
    }

    private static void AddError(List<ManifestValidationError> errors, string code, string path, string message) =>
        errors.Add(new ManifestValidationError(code, path, message));

    [GeneratedRegex("^[a-z][a-z0-9-]*(?:\\.[a-z][a-z0-9-]*){2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex AppIdPattern();

    [GeneratedRegex("^[a-z][a-z0-9-]*(?:\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PublisherIdPattern();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CommandNamePattern();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*(?:\\.[a-z0-9]+(?:-[a-z0-9]+)*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ComponentContractPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}