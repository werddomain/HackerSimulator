# Référence du SDK HackerOS

## Objet

Liste des API publiques qu'une app peut utiliser, bibliothèque par
bibliothèque, avec la capacité exigée et le document qui les détaille. Pour
apprendre à écrire une app, commencer par le
[guide du développeur](sdk-developer-guide.md). Les règles de la frontière du
SDK (références, injection, JavaScript) sont dans [`app-sdk.md`](app-sdk.md).

Conventions de l'API :

- Les façades `Hack*` sont statiques et lisent le **contexte ambiant**
  (`HackExecutionContext`) que l'OS ouvre autour du code de l'app. Plusieurs
  existent aussi avec un `IAppExecutionContext` explicite en premier
  paramètre, pour le code qui tourne hors de ce contexte.
- Les noms et les comportements suivent `System.IO`, `System.Diagnostics`,
  `System.Net` et `System.ServiceProcess`, avec le préfixe `Hack` et des
  méthodes asynchrones.
- Les chemins acceptent `~` et les chemins relatifs au répertoire courant du
  processus.
- Une capacité manquante lève `HackUnauthorizedAccessException`
  (`RequiredCapability`), ou `AppGatewayAccessDeniedException` pour une
  passerelle utilisée directement.

## Bibliothèques

Toutes sont publiées en paquets NuGet locaux du même nom, à la version de
`build/Versions.props` ; un autre dépôt les consomme comme le décrit
[`sdk-packages.md`](sdk-packages.md).

| Projet | Espaces de noms | Pour |
|---|---|---|
| `Shared/HackerOs.App.Abstractions` | `HackerOs.App.Abstractions` | Définir une app (`AppDefinition`, capacités, manifeste). |
| `Shared/HackerOs.Simulation.Abstractions` | `HackerOs.Simulation.Abstractions.*` | Types de données du système simulé (chemins, passerelles, journaux, notifications, processus). |
| `Shared/HackerOs.AppSdk` | `HackerOs.AppSdk`, `.IO`, `.Settings`, `.Apps`, `.Net`, `.Clipboard`, `.Diagnostics`, `.ServiceProcess`, `.Shell`, `.Administration` | Façades `Hack*`, contexte d'exécution, commandes et services. |
| `Shared/HackerOs.AppSdk.Blazor` | `HackerOs.AppSdk.Blazor`, `.Charts`, `.Interop`, `.Settings` | Apps fenêtrées, dialogues, graphiques. |
| `Shared/HackerOs.AppSdk.FileView` | `HackerOs.AppSdk.FileView` | Vue de fichiers réutilisable. |
| `Shared/HackerOs.AppSdk.Terminal` | `HackerOs.AppSdk.Terminal` | Terminal xterm.js. |
| `Shared/HackerOs.AppSdk.Web` | `HackerOs.AppSdk.Web.Hosting`, `.Http`, `.Routing`, `.Mvc`, `.Views`, `.Antiforgery`, `.Sessions`, `.Diagnostics`, `.Data`, `.Logging` | Sites internes ASP.Hack façon ASP.NET Core MVC ([guide ASP.Hack](asp-hack-developer-guide.md), [`asp-hack-mvc.md`](asp-hack-mvc.md)). |
| `Shared/HackerOs.AppSdk.Web.Blazor` | `HackerOs.AppSdk.Web.Blazor.Sites` | Sites Blazor interactifs dessinés dans le navigateur ([`blazor-sites.md`](blazor-sites.md)). |
| `Shared/HackerOs.AppSdk.Icons` | `HackerOs.AppSdk.Icons` | Icônes. |
| `Shared/HackerOs.AppSdk.VirtualKeyboard` | `HackerOs.AppSdk.VirtualKeyboard` | Clavier virtuel. |

## Définir une app (`HackerOs.App.Abstractions`)

| Type | Rôle |
|---|---|
| `[AppManifest]` | Marque la classe qui décrit l'app ; le build en génère `manifest.json`. `Execution` (`AppExecutionHost`) choisit où tourne un processus sans interface (ADR 0070). |
| `AppExecutionHost`, `AppExecutionHosts` | `Default`, `MainThread`, `Pool`, `Dedicated` ; `DefaultFor(kind)` (commande → `Pool`, service → `Dedicated`, fenêtre → `MainThread`), `Resolve`, `Of(manifest)`, `IsWorker`. Manifeste : `"execution": "mainThread" \| "pool" \| "dedicated"`. Voir [`worker-processes.md`](worker-processes.md). |
| `AppDefinition` | Classe de base. Obligatoires : `Id`, `Name`, `Kind`, `EntryPoint`. Facultatifs : `Version`, `Description`, `PublisherId`, `Category`, `LaunchVisibility`, `Icons`, `Capabilities`, `FileHandlers`, `Terminal`, `Components`, `MobileEntryPoint`, `SingleInstancePerUser`, `AutoStart`, `SupportsBack`, `Window` (`WindowOptions`, ADR 0095, [`window-model.md`](window-model.md)), `Resources`, `Settings`, `Dependencies`, `Intents`, `DeclaredTopicPermissions`, `Localizations`, `Assets`. |
| `AppKind` | `Window`, `Terminal`, `Service`. |
| `AppLaunchVisibility` | `Visible`, `Hidden` (absente du lanceur). |
| `FileHandler(MediaType, Extensions, Actions, Priority)` | Type de fichier que l'app ouvre. |
| `TerminalCommand(Name, Usage, Aliases)` | Nom de commande du shell. |
| `AppComponent(Contract, Type)` | Composant que d'autres apps peuvent embarquer (ADR 0063) ; voir [`embedded-components.md`](embedded-components.md). |
| `AppSettingsSchemaManifest(SchemaVersion, Fields)`, `AppSettingFieldManifest(Key, ValueType, DefaultValue, Scope, Sensitivity, AllowedValues, Group)` | Schéma des paramètres ; `AppSettingValueType` : `String`, `Integer`, `Boolean`, `Enum`. |
| `AppResourceProfileManifest` | Poids CPU, mémoire, disque et réseau simulés (`None` par défaut). |
| `AppDefinitionMapper.ToManifest(definition)` | Définition → `AppManifest` (utile dans les tests). |
| `HackerOsSdk` | `Version` (version du SDK chargé, par exemple `0.9.0-preview.1`), `SemanticVersion`, `IsCompatible(range)` (ADR 0098 §3). `AppDefinition.SdkCompatibility` déclare une plage ; par défaut, la version contre laquelle l'app est compilée est le minimum. |
| `AppManifest` | Manifeste validé, lu par l'OS ; `AppContext.Manifest` à l'exécution. |
| `AppCapabilities` | Catalogue des capacités (voir ci-dessous) ; `Describe`, `IsDangerous`, `GetRisk`, `IsKnown`, `RequiresWindowKind`, `All`. |
| `VirtualPath` | Chemin absolu canonique (`Parse`, `Value`). |

Détails : [`app-model.md`](app-model.md).

### Capacités

| Constante | Identifiant | Classe | Donne accès à |
|---|---|---|---|
| `FileSystemPrivateRead` / `Write` | `filesystem.private.read` / `.write` | normale | `~/.config/<appId>/` ; lecture de `/etc/<appId>/` |
| `FileSystemUserSelectedRead` / `Write` | `filesystem.user-selected.read` / `.write` | normale | fichiers choisis par l'utilisateur |
| `FileSystemUserHomeRead` / `Write` | `filesystem.user-home.read` / `.write` | normale | dossier personnel |
| `FileSystemSystemRead` / `Write` | `filesystem.system.read` / `.write` | **dangereuse** | tout le reste (`/etc`, `/var`, `/proc`…) ; écrire `/etc`, `/bin`, `/var` exige aussi un administrateur |
| `AppsLaunch` | `apps.launch` | normale | `HackApps.LaunchAsync`, `OpenFileAsync` |
| `AppsManage` | `apps.manage` | **dangereuse** | `HackAppManagement` |
| `DialogFileOpen`, `DialogFileSave`, `DialogFolderSelect` | `dialogs.file-open`, `.file-save`, `.folder-select` | normale | dialogues de fichiers (fenêtres seulement) |
| `FileAssociationsRead` | `file-associations.read` | normale | associations de fichiers |
| `FileAssociationsWrite` | `file-associations.write` | **dangereuse** | changer les associations de la machine |
| `ProcessList` | `process.list` | normale | voir les processus des autres apps |
| `ProcessManage` | `process.manage` | **dangereuse** | arrêter les processus des autres apps |
| `ServicesManage` | `services.manage` | **dangereuse** | `HackServiceController` sur les services des autres apps |
| `WindowsManage` | `windows.manage` | **dangereuse** | fenêtres des autres apps |
| `NotificationsPost` | `notifications.post` | normale | `AppContext.Notifications` |
| `ClipboardRead` / `Write` | `clipboard.read` / `.write` | normale | `HackClipboard` |
| `NetworkSimulatedRead` / `Write` | `network.simulated.read` / `.write` | normale | réseau simulé |
| `NetworkRealAccess` | `network.real.access` | **dangereuse** | vrai réseau par le serveur relais |
| `NetworkListen` | `network.listen` | normale | `HackHttpListener` sur un préfixe hors de son manifeste |
| `NetworkListenPrivileged` | `network.listen.privileged` | **dangereuse** | écouter un port sous 1024 |
| `ShellControl` | `shell.control` | normale | agir sur le shell appelant (`IShellSession`) |
| `ShellHost` | `shell.host` | **dangereuse** | `HackShell` |
| `PermissionsManage` | `permissions.manage` | **dangereuse** | `HackPermissionAdministration` |
| `ServerConnectionManage` | `server.connection.manage` | **dangereuse** | `HackServerConnection` |
| `SystemPower` | `system.power` | **dangereuse** | `HackSystem` (avec un administrateur) |
| `SystemStorageInspect` | `system.storage.inspect` | **dangereuse** | `HackStorageDiagnostics` |

Une capacité dangereuse d'une app non système n'est accordée qu'après
approbation d'un administrateur. Détails : [`capabilities.md`](capabilities.md),
[`dangerous-capability-approval.md`](dangerous-capability-approval.md).

## Contexte d'exécution (`HackerOs.AppSdk`)

### `IAppExecutionContext`

Construit par l'OS pour chaque instance (`AppContext` dans une fenêtre,
paramètre de `RunCoreAsync` pour un service, `context.App` pour une commande).

| Membre | Rôle |
|---|---|
| `Manifest`, `InstanceId`, `ProcessId`, `SessionId`, `CancellationToken` | Identité de l'instance ; le jeton est annulé à la fermeture, au kill, à la déconnexion. |
| `StopRequested`, `StopRequest` | Demande d'arrêt (`SIGTERM`, ADR 0069), levée une fois avant l'annulation du jeton : `HackStopRequestedEventArgs` (`Reason`, `Deadline`, `Defer`). Préférer `HackProcess.Current`. Après un kill, chaque passerelle lève `HackProcessKilledException`. |
| `UserId`, `UserLoginName`, `UserDisplayName`, `HomeDirectory`, `UserAuthority`, `IsRootElevated` | Utilisateur effectif. |
| `GrantedCapabilities`, `Capabilities` | Capacités accordées à cette instance. |
| `FileSystem`, `Watch` | Système de fichiers brut (préférer `HackFile`…) et surveillance. |
| `Logging` | `Log(severity, message, properties)` → `/var/log/apps/<appId>.log`. |
| `Notifications` | `Post(severity, title, message, actions, expiresAfter)` ; `notifications.post`. |
| `Events` | Bus d'événements : sujets de l'app (`Publish(topic, payload)`, `Subscribe`, `SubscribeChannel`, `RegisterSharedChannel`) et événements de la plateforme (`Subscribe<TEvent>`). |
| `Clock` | Temps simulé : `UtcNow`, `CurrentTick`, `DelayAsync`, `Schedule`. |
| `Processes` | Processus de l'app et des autres (`OwnProcess`, `ListProcesses`, `StopAsync`, `Kill`). Préférer `HackProcess`. |
| `Intents`, `Catalog`, `AppManagement` | Derrière `HackApps` et `HackAppManagement`. |
| `Environment` | Derrière `HackEnvironment`. |
| `Network`, `Drives`, `Clipboard`, `ShellHost`, `Services` | Derrière les façades du même domaine. |
| `Console` | Derrière `HackConsole` (ADR 0064). |
| `SystemSettings`, `PermissionAdministration`, `ServerConnection`, `SystemPower`, `StorageDiagnostics` | Derrière les façades d'administration. |
| `WebDatabase` | Base IndexedDB `hackweb.<appId>` d'une app `Web` (`IAppWebDatabaseGateway`, ADR 0088) ; derrière `HackWebDb` du SDK web. Une autre app reçoit `UnauthorizedAccessException`. |
| `PermissionErrors` | Événement `PermissionDenied` à chaque refus de permission. |
| `OneShotElevation` | Tickets d'élévation à usage unique (ADR 0042). |

### Façades générales

| API | Membres | Capacité | Document |
|---|---|---|---|
| `HackExecutionContext` | `Current`, `CurrentOrNull`, `WorkingDirectory`, `Enter(context, workingDirectory)` ; `HackExecutionContextMissingException` | — | [`hack-file-api.md`](hack-file-api.md) |
| `HackEnvironment` | `CurrentDirectory`, `SetCurrentDirectoryAsync`, `GetEnvironmentVariable(s)`, `SetEnvironmentVariable`, `GetCommandLineArgs`, `GetFolderPath(HackSpecialFolder)`, `UserName`, `MachineName`, `ProcessId`, `NewLine` | — | [`process-environment.md`](process-environment.md) |
| `HackSpecialFolder` | `UserProfile`, `Desktop`, `MyDocuments`, `ApplicationData`, `LocalApplicationData` | — | [`app-sdk.md`](app-sdk.md#utilisateur-courant-adr-0056-1) |
| `HackUser` | `Current`, `Describe(context)`, `Root` → `HackUserInfo(LoginName, DisplayName, HomeDirectory, IsAdministrator)` | — | [`app-sdk.md`](app-sdk.md#utilisateur-courant-adr-0056-1) |
| `HackConsole` (`IAppConsoleGateway`) | `Out`, `Error`, `In`, `IsAttached`, `Write`, `WriteLine[Async]`, `ReadLineAsync` : flux standard de l'instance ; ceux du shell qui l'a lancée (une app fenêtrée lancée par son nom écrit dans le terminal), `/dev/null` depuis le bureau (ADR 0064) | — | [`shell-interpreter.md`](shell-interpreter.md#apps-lancées-par-leur-nom) |

### Fichiers (`HackerOs.AppSdk.IO`)

| API | Membres | Document |
|---|---|---|
| `HackFile` | `ReadAllText/Lines/BytesAsync`, `WriteAllText/Lines/BytesAsync`, `AppendAllText/LinesAsync`, `ExistsAsync`, `CopyAsync`, `MoveAsync`, `DeleteAsync`, `OpenRead/Write/AppendAsync`, `Get/SetUnixFileModeAsync`, `CreateSymbolicLinkAsync` (ADR 0064, `ln -s`) | [`hack-file-api.md`](hack-file-api.md) |
| `HackDirectory` | `CreateDirectoryAsync`, `ExistsAsync`, `GetFiles/Directories/FileSystemEntriesAsync`, `EnumerateFileSystemEntriesAsync`, `MoveAsync`, `CopyAsync` (récursive), `DeleteAsync` | [`hack-file-api.md`](hack-file-api.md), [`file-explorer.md`](file-explorer.md) |
| `HackPath` | `Combine`, `Join`, `GetFileName[WithoutExtension]`, `GetExtension`, `HasExtension`, `ChangeExtension`, `GetDirectoryName`, `IsPathRooted`, `GetFullPath` | [`hack-file-api.md`](hack-file-api.md) |
| `HackFileInfo`, `HackDirectoryInfo` (`HackFileSystemInfo`) | `GetAsync`, `RefreshAsync`, `Exists`, `Length`, dates, `UnixFileMode`, `Owner`, `Group`, `Revision`, liens symboliques ; opérations d'instance | [`hack-file-api.md`](hack-file-api.md) |
| `HackFileStream` | Flux de lecture, ou d'écriture publié en une écriture atomique à la fermeture ; le tampon d'écriture compte dans `VmData` quand la mémoire est mesurée | [`hack-file-api.md`](hack-file-api.md) |
| `HackWriteOptions` | `ExpectedRevision` (concurrence optimiste), `CreateMode` | [`hack-file-api.md`](hack-file-api.md) |
| `HackFileSystemWatcher` | `Created`, `Changed`, `Deleted`, `Renamed`, `Error`, `Filter`, `IncludeSubdirectories`, `StartAsync` | [`hack-file-api.md`](hack-file-api.md) |
| `HackTrash` | `MoveToTrashAsync`, `GetItemsAsync`, `RestoreAsync`, `DeleteAsync`, `EmptyAsync`, `TrashDirectory` | [`trash.md`](trash.md) |
| `HackTextContent` | `TryDecode`, `TryReadAsync` (texte ou binaire, comme `file`) | [`text-editor.md`](text-editor.md) |
| `HackRecoveryDraft` | `Open(name)`, `SaveAsync`, `TryReadAsync`, `TakeAsync`, `DeleteAsync` : brouillon de travail non enregistré dans `~/.local/state/<appId>/recovery/` (ADR 0069) | [`app-sdk.md`](app-sdk.md#arrêt-propre-et-points-darrêt-adr-0069) |
| `HackDriveInfo` | `GetDrivesAsync` : taille et espace libre (comme `df`) | [`system-metrics.md`](system-metrics.md) |
| Exceptions | `HackIOException`, `HackFileNotFoundException`, `HackDirectoryNotFoundException`, `HackFileConflictException`, `HackUnauthorizedAccessException` | [`hack-file-api.md`](hack-file-api.md) |

Capacités : selon le chemin (`filesystem.private.*`, `.user-home.*`,
`.system.*`), puis les bits de mode du fichier.

### Paramètres (`HackerOs.AppSdk.Settings`)

| API | Membres | Capacité | Document |
|---|---|---|---|
| `HackSettings` | `OpenAsync([context,] documentId)`, `GetAsync<T>`, `SetAsync<T>`, `OpenSystemAsync(documentId)` | `filesystem.private.*` ; documents de l'OS : droits des fichiers | [`settings-files.md`](settings-files.md) |
| `HackSettingsDocument` | `Get<T>`, `GetSource`, `IsLocked`, `SetAsync`, `SetManyAsync`, `ResetAsync`, `RefreshAsync`, `Changed`, `Warnings` | — | [`settings-files.md`](settings-files.md) |
| `HackSettingLockedException` | Clé verrouillée dans `[locked]` | — | [`settings-files.md`](settings-files.md) |

### Apps (`HackerOs.AppSdk.Apps`)

| API | Membres | Capacité | Document |
|---|---|---|---|
| `HackApps` | `GetInstalledAsync`, `GetHandlersAsync(path)`, `LaunchAsync(appId, args)`, `OpenFileAsync(path)`, `Changed` ; `HackAppInfo`, `HackOpenFileResult`, `HackAppLaunchException` | lecture : aucune ; lancement : `apps.launch` | [`app-sdk.md`](app-sdk.md#catalogue-des-apps-adr-0056-2) |
| `HackBrowser` | `OpenAsync(url)`, `OpenDetachedAsync(url)` (fenêtre détachée, ADR 0078) ; `AppId`, `DetachedArgument` (`--detached`) | `apps.launch` | [`detached-display-windows.md`](detached-display-windows.md) |
| `HackAppManagement` | `GetAllAsync`, `UninstallAsync`, `ReinstallAsync` ; `HackInstallScope`, `HackAppInstallState` | `apps.manage` | [`app-installation.md`](app-installation.md) |

### Processus et services

| API | Membres | Capacité | Document |
|---|---|---|---|
| `HackProcess` (`.Diagnostics`) | `GetProcessesAsync`, `GetProcessByIdAsync`, `GetCurrentProcessAsync`, `RefreshAsync`, `GetEnvironmentVariablesAsync`, `GetChildren`, `KillAsync` (`SIGKILL`), `TerminateAsync` (`SIGTERM`, 5 s), `CloseMainWindowAsync` ; état, CPU, mémoire (`WorkingSet64` = `VmRSS`, `PrivateMemorySize64` = `RssAnon`, ADR 0068), E/S, arguments ; `UserProcessorTime`, `PrivilegedProcessorTime`, `TotalProcessorTime`, `Responding` (ADR 0067, [`cpu-accounting.md`](cpu-accounting.md)) | `filesystem.system.read` pour `/proc`, `process.list`, `process.manage` | [`task-manager.md`](task-manager.md) |
| `HackProcess.Current` → `HackCurrentProcess` ; `HackProcess.CheckpointAsync` (`.Diagnostics`) | `Id`, `StopToken`, `IsStopRequested`, `StopRequest`, `StopRequested` ; point d'arrêt : lève si l'arrêt est demandé, rend la main au-delà de 8 ms, sinon sans allocation | — | [`app-sdk.md`](app-sdk.md#arrêt-propre-et-points-darrêt-adr-0069) |
| `HackStopRequestedEventArgs`, `HackProcessKilledException` (`.Diagnostics`) | `Reason`, `RequestedAt`, `Deadline`, `GracePeriod`, `Defer(Task)` ; exception des passerelles d'un processus tué | — | [`app-sdk.md`](app-sdk.md#arrêt-propre-et-points-darrêt-adr-0069) |
| `IAppProcessGateway.KillAsync(pid, reason)` (`Simulation.Abstractions`) | Tuer un processus en attendant l'accusé de l'OS ; utilisé par `HackProcess.KillAsync`, y compris depuis un Web Worker (méthode par défaut qui retombe sur `Kill`) | `process.manage` | [`worker-processes.md`](worker-processes.md) |
| `HackServiceController` (`.ServiceProcess`) | `GetServicesAsync`, `RefreshAsync`, `StartAsync`, `StopAsync`, `SetStartTypeAsync`, `Status`, `StartType` | `services.manage` sur les autres apps | [`task-manager.md`](task-manager.md) |

### Réseau (`HackerOs.AppSdk.Net`)

| API | Membres | Document |
|---|---|---|
| `HackDns` | `GetHostAddressesAsync`, `GetHostEntryAsync` | [`sdk-network.md`](sdk-network.md) |
| `HackPing` | `SendAsync` → `HackPingReply(Address, Status, RoundtripTime, Ttl)` | [`sdk-network.md`](sdk-network.md) |
| `HackHttpClient` | `GetAsync`, `GetStringAsync`, `GetByteArrayAsync`, `GetFromJsonAsync`, `PostAsync` (formulaire ou contenu), `PostAsJsonAsync`, `PutAsync`, `PatchAsync`, `DeleteAsync`, `SendAsync`, `DefaultRequestHeaders`, `Cookies` (`HackCookieContainer` : `Add`, `Remove`, `GetCookies`, `GetAllCookies`, `Clear`) ; `HackHttpResponse` (`ContentType`, `MediaType`, `Content`, `ContentBytes`, `GetHeader`, `ReadFromJson`), `HackHttpRequestException` | [`sdk-network.md`](sdk-network.md), [`http-bodies.md`](http-bodies.md) |
| `HackHttpContent` | `HackStringContent`, `HackByteArrayContent`, `HackFormUrlEncodedContent`, `HackJsonContent` ; `ContentType`, `MediaType`, `CharSet`, `Length`, `ReadAsString`, `ReadAsByteArray` | [`http-bodies.md`](http-bodies.md) |
| `HackTcpClient` | `ProbeAsync` (ports), `DetectOperatingSystemAsync`, `ConnectAsync` → `HackNetworkStream` | [`sdk-network.md`](sdk-network.md) |
| `HackNetworkStream` | `ReadAsync`, `WriteAsync`, `RemoteEndPoint` (flux TCP relayé) | [`server-network-relay.md`](server-network-relay.md) |
| `HackUdpClient` | `ConnectAsync`, `SendAsync`, `ReceiveAsync` → `HackUdpReceiveResult` | [`server-network-relay.md`](server-network-relay.md) |
| `HackHttpListener` | `Prefixes`, `StartAsync`, `GetContextAsync`, `StopAsync`, `Close` ; `HackHttpListenerContext` (`Request`, `Response`), `HackHttpListenerRequest` (`HttpMethod`, `Url`, `Headers`, `QueryString`, `Cookies`, `Body`, `ReadForm`, `RemoteEndPoint`), `HackHttpListenerResponse` (`StatusCode`, `ContentType`, `Headers`, `OutputStream`, `RedirectLocation`, `Close`) | [`listening-ports-and-dns.md`](listening-ports-and-dns.md) |
| `HackIPGlobalProperties` | `GetActiveTcpListenersAsync` → `HackTcpListenerInfo`, `GetActiveTcpListenerEndPointsAsync` | [`listening-ports-and-dns.md`](listening-ports-and-dns.md) |
| `HackWebSite`, `HackWebSiteFile`, `HackWebSiteDeclaration`, `HackWebSiteScope` (`.Net.Hosting`) | Fichiers `.site` des sites statiques : `SystemSitesDirectory`, `GetUserSitesDirectory`, `Parse`, `Validate`, `Serialize`, `TryParseBinding`, `ReadAllAsync` | [`static-sites.md`](static-sites.md) |
| `WebServerAppBase` (`HackerOs.AppSdk`) | `Urls`, `ProcessRequestAsync`, `OnListeningAsync` : base d'une app web (`AppKind.Web`) | [`listening-ports-and-dns.md`](listening-ports-and-dns.md) |

Capacités : `network.simulated.*` ; le repli sur le vrai réseau est décidé par
l'OS et exige `network.real.access` ; écouter hors de ses `Urls` exige
`network.listen` (et `network.listen.privileged` sous 1024).

### Presse-papiers (`HackerOs.AppSdk.Clipboard`)

`HackClipboard` : `GetTextAsync`, `SetTextAsync`, `ContainsTextAsync`,
`GetFileDropListAsync`, `SetFileDropListAsync` (copier ou couper des
fichiers), `ContainsFileDropListAsync`, `ClearAsync`, `Changed`. Capacités
`clipboard.read` et `clipboard.write`. Le texte est partagé avec la vraie
machine selon `/etc/hackeros/clipboard.config`. Voir [`file-explorer.md`](file-explorer.md)
et [`clipboard.md`](clipboard.md).

### Composants intégrables (`HackerOs.AppSdk.Components`)

| Type | Rôle | Document |
|---|---|---|
| `HackComponentContracts` | Noms des contrats : `Terminal` (`hackeros.terminal`). | [`embedded-components.md`](embedded-components.md) |
| `IHackTerminal`, `HackTerminalOptions`, `HackTerminalCommandResult`, `HackTerminalOutputEventArgs` | Piloter un terminal embarqué : `RunCommandAsync`, `SetWorkingDirectoryAsync`, `SendInputAsync`, `ReadOutput`, `ClearAsync`, `FocusAsync`, `WorkingDirectory`, `Title`, `IsRunning`, `IsBusy`, `OutputReceived`, `CommandCompleted`, `StateChanged`, `Exited`. | [`embedded-components.md`](embedded-components.md) |

### Shell (`HackerOs.AppSdk.Shell` et `HackerOs.AppSdk`)

| API | Rôle | Capacité | Document |
|---|---|---|---|
| `HackShell.StartAsync(options)` → `IHackShell` | Démarrer un `hsh` : `ExecuteAsync`, `CompleteAsync`, `WorkingDirectory`, `ExitStatus`, `Prompt`, `HasExited`, historique, `GetInstalledCommands`, `NeedsMoreInput` (ADR 0064, invite `>`) | `shell.host` | [`shell-host.md`](shell-host.md) |
| `HackShellOptions`, `HackShellExecution`, `HackShellCompletion`, `HackCredentialPrompt` | Options, flux d'une exécution, complétion, saisie des mots de passe | — | [`shell-host.md`](shell-host.md), [`terminal-console.md`](terminal-console.md) |
| `IShellSession` (`TerminalExecutionContext.Shell`) | Pour une commande : `WorkingDirectory`, `Environment`, `History`, `ChangeDirectoryAsync`, `SetVariable`, `UnsetVariable`, `GetCommands`, `Exit` ; ADR 0064 : `GetVariable`, `AssignVariable`, `RunAsync`, `Jobs` (`ShellJobInfo`), `KillJob` | `shell.control` | [`shell-commands.md`](shell-commands.md) |
| `IFullScreenTerminalSession` (`TerminalExecutionContext.FullScreen`) | Écran alternatif : `Viewport`, `EnterAlternateScreenAsync`, `RenderAsync`, `ReadKeyAsync`, `LeaveAlternateScreenAsync` | — | [`terminal-emulator.md`](terminal-emulator.md) |
| `ShellErrorText` | Message court d'une erreur de fichier, pour une commande | — | [`app-sdk.md`](app-sdk.md) |

### Administration (`HackerOs.AppSdk.Administration`)

| API | Membres | Capacité |
|---|---|---|
| `HackPermissionAdministration` | `GetPolicyAsync`, `SetPolicyAsync`, `GetOverviewAsync`, `SetMachineDecisionAsync`, `RemoveOwnDenialAsync` | `permissions.manage` |
| `HackServerConnection` | `GetStateAsync`, `ConnectAsync`, `CreateAccountAsync`, `DisconnectAsync`, `SyncAsync`, `GetConflictsAsync`, `GetRelayPolicyAsync` | `server.connection.manage` |
| `HackSystem` | `RebootAsync`, `FactoryResetAsync` | `system.power` + administrateur |
| `HackStorageDiagnostics` | `GetSnapshotAsync`, `GetWebSiteDatabasesAsync`, `DeleteWebSiteDatabaseAsync` (bases `hackweb.*` des sites, ADR 0088 §4) | `system.storage.inspect` |

Détails : [`system-administration.md`](system-administration.md),
[`admin-authentication-agent.md`](admin-authentication-agent.md).

## Points d'entrée

| Classe | Projet | À surcharger | Document |
|---|---|---|---|
| `WindowAppBase` | `AppSdk.Blazor` | `OnAppInitialized[Async]`, `OnAppParametersSet[Async]`, `OnAppAfterRender[Async]`, `ShouldRenderApp`, `OnPermissionErrorRaised`, `OnAppStopRequestedAsync` (ADR 0069 : la fenêtre se ferme après la tâche renvoyée), crochets de fenêtre `OnWindowActivated[Async]`, `OnWindowDeactivated[Async]`, `OnWindowStateChanged[Async]`, `OnWindowResized[Async]`, état `IsWindowActive`, `WindowState`, `WindowSize`, `TrackDisplaySurface` (ADR 0095, [`window-model.md`](window-model.md)) ; fournit `AppContext`, `Manifest`, `LaunchArguments`, `LaunchFile`, `OpenFileAsync`, `SaveFileAsync`, `SelectFolderAsync`, `MessageBox`, `TextInput` | [guide §3](sdk-developer-guide.md#3-une-app-fenêtrée) |
| `HackComponentBase` | `AppSdk.Blazor` | Composant enfant d'une fenêtre qui appelle `Hack*` | [`hack-file-api.md`](hack-file-api.md) |
| `TerminalAppBase` | `AppSdk` | `ExecuteAsync(TerminalExecutionContext, CancellationToken)` → code de sortie ; le contexte donne `Arguments`, `StandardInput/Output/Error`, `WorkingDirectory`, `Environment`, `App`, `Shell`, `FullScreen` | [guide §4](sdk-developer-guide.md#4-une-commande-du-shell) |
| `WebAppDefinition` | `AppSdk.Web` | Classe `[AppManifest]` `partial` d'un site : `Urls`, `Configure(HackWebApplicationBuilder)` (services, middlewares, `MapControllers`, `MapGet`...) ; le build génère l'`EntryPoint` (`<Nom>WebHost : HackWebHost`) et la table de routes des contrôleurs (`HackController`, `HackApiController`) | [`asp-hack-mvc.md`](asp-hack-mvc.md) |
| `ServiceAppBase` | `AppSdk` | `RunCoreAsync(context, sessionCancellationToken)`, `OnStoppingAsync(ServiceStopReason, token)` (appelé une fois, dès `StopRequested` ; `ServiceStopReason.Terminated` pour `kill`, « End task », `systemctl stop`) | [guide §5](sdk-developer-guide.md#5-un-service) |

Interfaces qu'un composant fenêtré peut implémenter :

| Interface | Rôle |
|---|---|
| `IWindowCloseGuard` | `ConfirmCloseAsync` : autoriser ou refuser la fermeture (modifications non enregistrées). |
| `IAppBackHandler` | `CanNavigateBack`, `CanNavigateBackChanged`, `NavigateBackAsync` → `AppBackResult` ; avec `SupportsBack => true`. |

## Sites web (ASP.Hack)

Pour apprendre : [guide du développeur ASP.Hack](asp-hack-developer-guide.md),
qui suit l'exemple `Apps/Samples/HackerOs.Samples.Intranet`.

### Démarrage (`HackerOs.AppSdk.Web.Hosting`)

| Type | Membres | Document |
|---|---|---|
| `WebAppDefinition` | `Urls`, `BlazorRoot`, `Capabilities` (jamais `filesystem.*`, `HOSAPP0050`), `Configure(builder)` ; `Kind` `Web`, `Category` `internet`, `LaunchVisibility` `Hidden` | [`asp-hack-mvc.md`](asp-hack-mvc.md) |
| `HackWebApplicationBuilder` | `Services`, `Json` (`SerializerOptions`, camelCase), `Mvc` (`Filters`), `Antiforgery`, `Logging` (`EnableDebugLog(level)`, `LogRequests`), `Build()` ; `ILogger<T>` injectable | [`asp-hack-mvc.md`](asp-hack-mvc.md), [`asp-hack-site-data.md`](asp-hack-site-data.md#journal-de-débogage) |
| `HackWebApplication` | `Use`, `UseMiddleware<T>`, `UseExceptionPage`, `UseStatusCodePages`, `UseCookieSession`, `UseStaticFiles(configure)`, `Map`, `MapMethods`, `MapGet`/`MapPost`/`MapPut`/`MapPatch`/`MapDelete`, `MapFallback`, `MapControllers`, `MapControllerRoute`, `MapDefaultControllerRoute`, `ProcessAsync(HackWebRequestData)`, `UnhandledException` | [`asp-hack-mvc.md`](asp-hack-mvc.md) |
| `HackWebHost` | Classe d'entrée générée (`<Définition>WebHost`) ; `CreateApplication(definition, routes, urls, views)` construit le site sans OS (tests) | [guide §9](asp-hack-developer-guide.md#9-tester-un-site) |
| `HackStaticFileOptions` | `Assembly`, `ResourcePrefix`, `RequestPath`, `DefaultFileNames`, `CacheControl` du `wwwroot` embarqué | [`asp-hack-razor-views.md`](asp-hack-razor-views.md) |
| `HackBlazorSiteEndpoints` | Page hôte, `blazor.hack.js` et `blazor.boot.json` d'un site à `BlazorRoot` ; `IsHostPage(html)` | [`blazor-sites.md`](blazor-sites.md) |

### Requête et réponse (`.Http`, `.Sessions`)

| Type | Membres |
|---|---|
| `HackHttpContext` | `Request`, `Response`, `RequestServices` (portée de la requête), `Session`, `HasSession`, `Items`, `Endpoint`, `RequestAborted`, `TraceIdentifier` |
| `HackHttpRequest` | `Method`, `Path`, `PathAndQuery`, `Query`, `Form`, `Headers`, `Cookies`, `ContentType`, `RouteValues`, `ReadBodyAsString()` |
| `HackHttpResponse` | `StatusCode`, `Headers`, `ContentType`, `Cookies`, `WriteAsync` |
| `HackWebRequestData`, `HackWebResponseData` | Échange indépendant du transport (`Create(method, url, ...)`, `ReadAsString()`) |
| `HackSession` | `GetString`/`SetString`, `GetInt32`/`SetInt32`, `TryGetValue`, `Set`, `Remove`, `Clear`, `Keys`, `Id`, `IsNew` ; cookie `.HackSession`, en mémoire du site |

### Contrôleurs (`.Mvc`)

| Type | Rôle |
|---|---|
| `HackControllerBase` | `HttpContext`, `Request`, `Response`, `RouteValues`, `ModelState`, `Url` ; résultats `Ok`, `NoContent`, `StatusCode`, `Content`, `File`, `Redirect`, `RedirectPermanent`, `LocalRedirect`, `RedirectToAction`, `Created`, `CreatedAtAction`, `NotFound`, `BadRequest`, `Unauthorized`, `Forbid`, `Conflict`, `Problem`, `ValidationProblem` ; `TryValidateModel(model, prefix)` (ADR 0087 §3) |
| `HackController` | Pages : `View`, `PartialView`, `View<TView>`, `ViewData`, `Json`, `OnActionExecuting`/`OnActionExecuted` |
| `HackApiController` | API : corps JSON par défaut, `400` automatique, problèmes RFC 9457 ; route d'attribut obligatoire (`HOSWEB0007`) |
| Attributs | `[Route]`, `[HttpGet]`/`[HttpPost]`/`[HttpPut]`/`[HttpPatch]`/`[HttpDelete]` (`Name`, `Order`), `[ActionName]`, `[NonAction]`, `[NonController]`, `[FromRoute]`, `[FromQuery]`, `[FromForm]`, `[FromHeader]`, `[FromBody]`, `[FromServices]` |
| Filtres | `IHackActionFilter`, `IHackAsyncActionFilter`, `HackActionFilterAttribute`, `HackMvcOptions.Filters` |
| `HackResults` | Résultats des routes minimales : `Ok`, `Text`, `Json` (avec `JsonTypeInfo`), `StatusCode`, `NoContent`, `NotFound`, `BadRequest`, `Created`, `Redirect`, `File`, `Problem` |
| `IHackUrlHelper` | `Action`, `RouteUrl`, `Content("~/...")`, `IsLocalUrl` |

### Vues et anti-falsification (`.Views`, `.Antiforgery`)

| Type | Rôle | Document |
|---|---|---|
| `HackView`, `HackView<TModel>`, `HackLayout` | Bases des vues `.razor` : `Model`, `ViewData` (`Title`), `ViewContext`, `HttpContext`, `Request`, `ModelState`, `Url`, `Layout` ; `Body` pour un layout | [`asp-hack-razor-views.md`](asp-hack-razor-views.md) |
| `HackLink`, `HackForm`, `HackAntiforgeryToken`, `HackValidationMessage`, `HackValidationSummary`, `HackSection`, `HackRenderSection`, `HackPartial`, `HackRoute.Values` | Helpers des vues `.razor` | [`asp-hack-razor-views.md`](asp-hack-razor-views.md) |
| `HackRazorPage<TModel>` | Base des vues `.cshtml` : `Model`, `ViewData`, `Context`, `Layout`, `RenderBody`, `RenderSection(Async)`, `IsSectionDefined` ; `Html` (`ActionLink`, `BeginForm`, `TextBoxFor`, `LabelFor`, `ValidationMessageFor`, `ValidationSummary`, `PartialAsync`, `Raw`...), `Json.Serialize`, `Component.InvokeAsync<T>`, `Url` | [`asp-hack-cshtml-views.md`](asp-hack-cshtml-views.md) |
| `[ValidateAntiForgeryToken]`, `[IgnoreAntiforgeryToken]`, `HackAntiforgery` | Jeton de formulaire (`__RequestVerificationToken` ou en-tête `RequestVerificationToken`) et cookie `.HackAntiforgery` | [`asp-hack-razor-views.md`](asp-hack-razor-views.md) |

Une vue est rendue en HTML statique dans l'iframe du navigateur : pas de CSS
scopé ni d'événement Blazor ; le CSS et le JavaScript viennent du `wwwroot`.

### Données et journal (`.Data`, `.Logging`)

| Type | Rôle |
|---|---|
| `AddHackWebDb(services, schema)` | Enregistre `HackWebDb`, la base `hackweb.<appId>` du site, avec ses versions ; en mémoire (`hackweb.in-memory`) hors de l'OS |
| `HackWebDbSchemaBuilder`, `HackWebDbVersionBuilder` | `Version(n, v => ...)` ; `CreateStore(name, keyPath, autoIncrement)`, `DeleteStore`, `CreateIndex(store, name, keyPath, unique, multiEntry)`, `DeleteIndex` |
| `HackWebDb` | `Name`, `Version`, `Store<T>(name)`, `BeginTransaction(stores)`, `OpenAsync` |
| `HackWebDbStore<T>`, `HackWebDbIndex<T>` | `GetAsync`, `GetAllAsync`, `GetAllKeysAsync`, `CountAsync` ; magasin : `AddAsync`, `PutAsync`, `DeleteAsync`, `ClearAsync`, `Index(name)` |
| `HackWebDbTransaction`, `HackWebDbTransactionStore<T>` | Lot d'écritures (`Add`, `Put`, `Delete`, `Clear`) envoyé par `CommitAsync`, tout ou rien |
| `HackWebDbKey`, `HackWebDbKeyRange` | Clés (nombre, chaîne, `Array(...)`) et plages (`Only`, `LowerBound`, `UpperBound`, `Bound`) |
| `HackWebDbException` (`HackerOs.AppSdk.WebData`) | `Name` d'une `DOMException` IndexedDB : `ConstraintError`, `NotFoundError`, `DataError`, `VersionError`… |
| `HackWebDbInMemoryBackend` (`HackerOs.AppSdk.WebData`) | Passerelle en mémoire pour les tests |
| `HackWebLoggingOptions`, `IHackWebDebugLogSink`, `HackWebDebugLogMemorySink` | Journal de débogage `/var/log/hackweb/<appId>.log` ; récepteur de test |

Guide : [`asp-hack-site-data.md`](asp-hack-site-data.md).

## Composants et services Blazor

### Dialogues (`HackerOs.AppSdk.Blazor`)

| Type | Champs |
|---|---|
| `OpenFileDialogRequest` → `OpenFileDialogResult` | `Title`, `InitialDirectory`, `Filters` (`FileDialogFilter(DisplayName, Extensions, MediaTypes)`), `AllowMultiple`, `RequestedAccess` (`Read`, `ReadWrite`) ; résultat `Status`, `Resources` |
| `SaveFileDialogRequest` → `SaveFileDialogResult` | `Title`, `InitialDirectory`, `SuggestedFileName`, `DefaultExtension`, `Filters`, `ConfirmOverwrite` ; résultat `Status`, `Resource` |
| `SelectFolderDialogRequest` → `SelectFolderDialogResult` | `Title`, `InitialDirectory`, `AllowCreateFolder` |
| `MessageBoxDialogRequest` → `MessageBoxDialogResult` | `Title`, `Content`, `DialogType` (`MessageBoxType`) ; résultat `MessageBoxResult` |
| `TextInputDialogRequest` → `TextInputDialogResult` | `Title`, `Content`, `DefaultValue`, `Placeholder` ; résultat `Status` (`TextInputStatus`) et `Value` |
| `IDialogService`, `IFileDialogService` | Services injectés dans `WindowAppBase` (`Dialogs`, `FileDialogs`) ; `CredentialPromptAsync` en plus |

`FileDialogStatus.Selected` indique un choix ; tout autre statut est une
annulation.

### Autres composants de `HackerOs.AppSdk.Blazor`

| Type | Rôle | Document |
|---|---|---|
| `HackLineChart` (`.Charts`) | Graphique SVG : `Series` (`HackChartSeries(Name, Values)`), `Title`, `Capacity`, `Maximum`, `MinimumScale`, `ValueFormatter`, `Filled`, `ShowLegend`, `TimeSpanLabel`, `Size`, `Class` ; couleurs `--hos-chart-series-0..7` | [`task-manager.md`](task-manager.md) |
| `SettingOriginBadge` (`.Settings`) | Badge d'origine d'un paramètre (défaut, système, utilisateur, verrouillé) | [`settings-files.md`](settings-files.md) |
| `BrowserFileDownloader` (`.Interop`) | `DownloadFileFromStreamAsync(fileName, stream)` : télécharger vers le vrai disque | [`file-explorer.md`](file-explorer.md) |
| `BrowserClipboard` (`.Interop`) | `[Obsolete]` (ADR 0065) : utiliser `HackClipboard.SetTextAsync`, recopié vers l'hôte | [`clipboard.md`](clipboard.md) |
| `IDisplaySurfaceService`, `IDisplaySurface`, `DisplaySurfaceContent` (`.Display`) | Fenêtres détachées du navigateur hôte (ADR 0078) : `OpenAsync(DisplaySurfaceOptions)` → `DisplaySurfaceOpenResult` (`Opened`, `Blocked`, `Unavailable`), `Surfaces`, `SurfacesChanged`, `CloseAllAsync` ; surface `Id`, `Title`, `Width`, `Height`, `HasFocus`, `State`, `Changed`, `SetTitleAsync`, `FocusAsync`, `CloseAsync` ; `<DisplaySurfaceContent Surface="…">` y dessine son contenu (`Surface` facultatif : sans surface ouverte, le contenu reste en place) | [`detached-display-windows.md`](detached-display-windows.md) |
| `HackEmbeddedComponent<TController>` (`.Components`) | Affiche le composant d'une autre app pour un contrat : `Contract`, `Options`, `ControllerChanged`, `Unavailable` ; `HackEmbeddableComponent` est la base du composant fourni (`Options`, `ControllerReady`, `HostContext`) | [`embedded-components.md`](embedded-components.md) |

### Sites Blazor (`HackerOs.AppSdk.Web.Blazor.Sites`)

| Type | Rôle |
|---|---|
| `WebAppDefinition.BlazorRoot` (`AppSdk.Web`) | Composant racine du site ; produit `web.blazor` dans le manifeste, la page hôte et `blazor.boot.json`. |
| `HackSiteRouter` | Routeur du site : `AppAssembly`, `DefaultLayout`, `NotFound` ; gabarits `@page` de Blazor, valeurs de route vers les `[Parameter]` de la page, `@layout` imbriqués. |
| `HackSiteLayoutBase`, `HackNavLink`, `HackPageTitle` | Mise en page (`Body`), lien marqué `active` (`Match`), titre de l'onglet. |
| `HackWebSite` (cascadé) | `Navigation` (`NavigationManager` de l'onglet : `NavigateTo`, `LocationChanged`, `IsWithinSite`), `Http` (`HackHttpClient` avec `BaseAddress` et les cookies de l'onglet), `AppId`, `Name`, `Title`. |
| Liens | Un `<a href>` du site navigue sans requête ; un lien dont `target` n'est pas `_self` (`target="_top"`) charge un nouveau document : c'est ainsi qu'une page Blazor rejoint les pages MVC du même site (ADR 0087 §1). |
| `IHackWebSiteHost`, `IHackWebSiteProcess`, `HackWebSiteView` | Côté navigateur : démarre le processus de rendu d'une page hôte et dessine le site dans la zone de page. |

Détails : [`blazor-sites.md`](blazor-sites.md).

### Vue de fichiers (`HackerOs.AppSdk.FileView`)

| Type | Rôle |
|---|---|
| `FileView` | Liste de fichiers. Paramètres : `FileSystem`, `Watch`, `Intents`, `InitialDirectory`, `Mode` (`FileViewMode`), `FolderActivation`, `IconProvider`, `ContextMenuProviders`, `AllowMultiSelect`, `AllowDragDrop`, `Columns`, `Filter`, `CanPaste`, `CutPaths`, rappels `DeleteRequested`, `DropRequested`, `ClipboardRequested`. Méthodes `NavigateAsync`, `RefreshAsync` ; propriétés `CurrentDirectory`, `Items`, `SelectedItem(s)` ; événements `Navigating`, `OnPathChange`, `Opening`, `Opened`, `Renaming`, `Renamed`, `Creating`, `Created`, `Deleting`, `Deleted`, `Moving`, `Moved`, `Copying`, `Copied`, `SelectionChanging`, `SelectionChanged`, `ContextMenuOpening` (les formes `-ing` sont annulables). |
| `FileViewTree`, `FileViewBreadcrumb`, `FileViewDropZone`, `FileViewContextMenu` | Arbre de dossiers, fil d'Ariane, zone de dépôt, menu contextuel. |
| `IFileViewContextMenuProvider`, `IShellIconProvider`, `ShellIcon` | Extensions : entrées de menu, icônes. |
| `FileTransfer` | `CopyAsync`, `MoveAsync`, `TransferAsync`, `ImportAsync` (fichiers de l'hôte), `MoveToTrashAsync`, `GetCopyName` ; conflits de noms à la Nautilus. |

Détails : [`file-explorer.md`](file-explorer.md).

### Terminal (`HackerOs.AppSdk.Terminal`)

| Composant | Paramètres | Document |
|---|---|---|
| `TerminalPanel` | `CallerAppId`, `InitialUsername`, `InitialCwd`, `UseVirtualKeyboard`, `ShowBanner`, `FocusOnStart`, `OnExitRequested`, `OnTitleChanged` ; héberge un `hsh` (capacité `shell.host`) ; implémente `IHackTerminal` (`RunCommandAsync`, `SetWorkingDirectoryAsync`, `SendInputAsync`, `ReadOutput`, `ClearAsync`, événements) | [`terminal-emulator.md`](terminal-emulator.md), [`shell-host.md`](shell-host.md) |
| `TerminalTabs` | `CallerAppId`, `InitialCwd`, `OnLastTabClosed` ; un `hsh` par onglet | [`terminal-console.md`](terminal-console.md) |

### Icônes et clavier virtuel

| Composant | Paramètres |
|---|---|
| `HackerIcon` (`AppSdk.Icons`) | `Library` (`IconLibrary` : `Bootstrap`, `FontAwesome`, `Lucide`, `SimpleIcons`), `Name`, `Icon`, `Size`, `Title` ; voir [`old/icon-library.md`](old/icon-library.md). |
| `VirtualKeyboard`, `VirtualKeyboardHost` (`AppSdk.VirtualKeyboard`) | `Options` (`VirtualKeyboardOptions` : rangées modificateurs, symboles, flèches, libellés), `OnKeyPressed` (`VirtualKeyPressedEvent`), `IsKeyboardVisible`, `ChildContent`. |

## Tâches de ce document

- [x] Lister les bibliothèques du SDK et leurs espaces de noms.
- [x] Types de définition d'une app et table complète des 31 capacités.
- [x] Membres de `IAppExecutionContext`.
- [x] Chaque façade `Hack*` avec ses membres, sa capacité et son document.
- [x] Classes de base des points d'entrée et interfaces des fenêtres.
- [x] Composants Blazor : dialogues, graphiques, vue de fichiers, terminal, icônes, clavier virtuel.
- [x] Hôte d'exécution (`AppExecutionHost`, ADR 0070) et `IAppProcessGateway.KillAsync`.
- [x] Sites web ASP.Hack : démarrage, requête, contrôleurs, vues, anti-falsification, liens des sites Blazor (`web/11`).
- [x] Base `HackWebDb`, journal de débogage d'un site, passerelle `WebDatabase`, bases des sites dans `HackStorageDiagnostics` (`web/12`).
