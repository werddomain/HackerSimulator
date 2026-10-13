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

| Projet | Espaces de noms | Pour |
|---|---|---|
| `Shared/HackerOs.App.Abstractions` | `HackerOs.App.Abstractions` | Définir une app (`AppDefinition`, capacités, manifeste). |
| `Shared/HackerOs.Simulation.Abstractions` | `HackerOs.Simulation.Abstractions.*` | Types de données du système simulé (chemins, passerelles, journaux, notifications, processus). |
| `Shared/HackerOs.AppSdk` | `HackerOs.AppSdk`, `.IO`, `.Settings`, `.Apps`, `.Net`, `.Clipboard`, `.Diagnostics`, `.ServiceProcess`, `.Shell`, `.Administration` | Façades `Hack*`, contexte d'exécution, commandes et services. |
| `Shared/HackerOs.AppSdk.Blazor` | `HackerOs.AppSdk.Blazor`, `.Charts`, `.Interop`, `.Settings` | Apps fenêtrées, dialogues, graphiques. |
| `Shared/HackerOs.AppSdk.FileView` | `HackerOs.AppSdk.FileView` | Vue de fichiers réutilisable. |
| `Shared/HackerOs.AppSdk.Terminal` | `HackerOs.AppSdk.Terminal` | Terminal xterm.js. |
| `Shared/HackerOs.AppSdk.Icons` | `HackerOs.AppSdk.Icons` | Icônes. |
| `Shared/HackerOs.AppSdk.VirtualKeyboard` | `HackerOs.AppSdk.VirtualKeyboard` | Clavier virtuel. |

## Définir une app (`HackerOs.App.Abstractions`)

| Type | Rôle |
|---|---|
| `[AppManifest]` | Marque la classe qui décrit l'app ; le build en génère `manifest.json`. |
| `AppDefinition` | Classe de base. Obligatoires : `Id`, `Name`, `Kind`, `EntryPoint`. Facultatifs : `Version`, `Description`, `PublisherId`, `Category`, `LaunchVisibility`, `Icons`, `Capabilities`, `FileHandlers`, `Terminal`, `Components`, `MobileEntryPoint`, `SingleInstancePerUser`, `AutoStart`, `SupportsBack`, `Resources`, `Settings`, `Dependencies`, `Intents`, `DeclaredTopicPermissions`, `Localizations`, `Assets`. |
| `AppKind` | `Window`, `Terminal`, `Service`. |
| `AppLaunchVisibility` | `Visible`, `Hidden` (absente du lanceur). |
| `FileHandler(MediaType, Extensions, Actions, Priority)` | Type de fichier que l'app ouvre. |
| `TerminalCommand(Name, Usage, Aliases)` | Nom de commande du shell. |
| `AppComponent(Contract, Type)` | Composant que d'autres apps peuvent embarquer (ADR 0063) ; voir [`embedded-components.md`](embedded-components.md). |
| `AppSettingsSchemaManifest(SchemaVersion, Fields)`, `AppSettingFieldManifest(Key, ValueType, DefaultValue, Scope, Sensitivity, AllowedValues, Group)` | Schéma des paramètres ; `AppSettingValueType` : `String`, `Integer`, `Boolean`, `Enum`. |
| `AppResourceProfileManifest` | Poids CPU, mémoire, disque et réseau simulés (`None` par défaut). |
| `AppDefinitionMapper.ToManifest(definition)` | Définition → `AppManifest` (utile dans les tests). |
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
| `GameplayDomainAccess` | `gameplay.domain.access` | normale | moteur de jeu |
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
| `HackFileStream` | Flux de lecture, ou d'écriture publié en une écriture atomique à la fermeture | [`hack-file-api.md`](hack-file-api.md) |
| `HackWriteOptions` | `ExpectedRevision` (concurrence optimiste), `CreateMode` | [`hack-file-api.md`](hack-file-api.md) |
| `HackFileSystemWatcher` | `Created`, `Changed`, `Deleted`, `Renamed`, `Error`, `Filter`, `IncludeSubdirectories`, `StartAsync` | [`hack-file-api.md`](hack-file-api.md) |
| `HackTrash` | `MoveToTrashAsync`, `GetItemsAsync`, `RestoreAsync`, `DeleteAsync`, `EmptyAsync`, `TrashDirectory` | [`trash.md`](trash.md) |
| `HackTextContent` | `TryDecode`, `TryReadAsync` (texte ou binaire, comme `file`) | [`text-editor.md`](text-editor.md) |
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
| `HackAppManagement` | `GetAllAsync`, `UninstallAsync`, `ReinstallAsync` ; `HackInstallScope`, `HackAppInstallState` | `apps.manage` | [`app-installation.md`](app-installation.md) |

### Processus et services

| API | Membres | Capacité | Document |
|---|---|---|---|
| `HackProcess` (`.Diagnostics`) | `GetProcessesAsync`, `GetProcessByIdAsync`, `GetCurrentProcessAsync`, `RefreshAsync`, `GetEnvironmentVariablesAsync`, `GetChildren`, `KillAsync`, `CloseMainWindowAsync` ; état, CPU, mémoire, E/S, arguments | `filesystem.system.read` pour `/proc`, `process.list`, `process.manage` | [`task-manager.md`](task-manager.md) |
| `HackServiceController` (`.ServiceProcess`) | `GetServicesAsync`, `RefreshAsync`, `StartAsync`, `StopAsync`, `SetStartTypeAsync`, `Status`, `StartType` | `services.manage` sur les autres apps | [`task-manager.md`](task-manager.md) |

### Réseau (`HackerOs.AppSdk.Net`)

| API | Membres | Document |
|---|---|---|
| `HackDns` | `GetHostAddressesAsync`, `GetHostEntryAsync` | [`sdk-network.md`](sdk-network.md) |
| `HackPing` | `SendAsync` → `HackPingReply(Address, Status, RoundtripTime, Ttl)` | [`sdk-network.md`](sdk-network.md) |
| `HackHttpClient` | `GetAsync`, `GetStringAsync`, `PostAsync`, `SendAsync`, `DefaultRequestHeaders`, `Cookies` ; `HackHttpResponse`, `HackHttpRequestException` | [`sdk-network.md`](sdk-network.md) |
| `HackTcpClient` | `ProbeAsync` (ports), `DetectOperatingSystemAsync` | [`sdk-network.md`](sdk-network.md) |

Capacités : `network.simulated.*` ; le repli sur le vrai réseau est décidé par
l'OS et exige `network.real.access`.

### Presse-papiers (`HackerOs.AppSdk.Clipboard`)

`HackClipboard` : `GetTextAsync`, `SetTextAsync`, `ContainsTextAsync`,
`GetFileDropListAsync`, `SetFileDropListAsync` (copier ou couper des
fichiers), `ContainsFileDropListAsync`, `ClearAsync`, `Changed`. Capacités
`clipboard.read` et `clipboard.write`. Voir [`file-explorer.md`](file-explorer.md).

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
| `HackServerConnection` | `GetStateAsync`, `ConnectAsync`, `CreateAccountAsync`, `DisconnectAsync`, `SyncAsync`, `GetConflictsAsync` | `server.connection.manage` |
| `HackSystem` | `RebootAsync`, `FactoryResetAsync` | `system.power` + administrateur |
| `HackStorageDiagnostics` | `GetSnapshotAsync` | `system.storage.inspect` |

Détails : [`system-administration.md`](system-administration.md),
[`admin-authentication-agent.md`](admin-authentication-agent.md).

## Points d'entrée

| Classe | Projet | À surcharger | Document |
|---|---|---|---|
| `WindowAppBase` | `AppSdk.Blazor` | `OnAppInitialized[Async]`, `OnAppParametersSet[Async]`, `OnAppAfterRender[Async]`, `ShouldRenderApp`, `OnPermissionErrorRaised` ; fournit `AppContext`, `Manifest`, `LaunchArguments`, `LaunchFile`, `OpenFileAsync`, `SaveFileAsync`, `SelectFolderAsync`, `MessageBox`, `TextInput` | [guide §3](sdk-developer-guide.md#3-une-app-fenêtrée) |
| `HackComponentBase` | `AppSdk.Blazor` | Composant enfant d'une fenêtre qui appelle `Hack*` | [`hack-file-api.md`](hack-file-api.md) |
| `TerminalAppBase` | `AppSdk` | `ExecuteAsync(TerminalExecutionContext, CancellationToken)` → code de sortie ; le contexte donne `Arguments`, `StandardInput/Output/Error`, `WorkingDirectory`, `Environment`, `App`, `Shell`, `FullScreen` | [guide §4](sdk-developer-guide.md#4-une-commande-du-shell) |
| `ServiceAppBase` | `AppSdk` | `RunCoreAsync(context, sessionCancellationToken)`, `OnStoppingAsync(ServiceStopReason, token)` | [guide §5](sdk-developer-guide.md#5-un-service) |

Interfaces qu'un composant fenêtré peut implémenter :

| Interface | Rôle |
|---|---|
| `IWindowCloseGuard` | `ConfirmCloseAsync` : autoriser ou refuser la fermeture (modifications non enregistrées). |
| `IAppBackHandler` | `CanNavigateBack`, `CanNavigateBackChanged`, `NavigateBackAsync` → `AppBackResult` ; avec `SupportsBack => true`. |

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
| `BrowserClipboard` (`.Interop`) | `WriteTextAsync` : presse-papiers du vrai navigateur | [`system-administration.md`](system-administration.md) |
| `HackEmbeddedComponent<TController>` (`.Components`) | Affiche le composant d'une autre app pour un contrat : `Contract`, `Options`, `ControllerChanged`, `Unavailable` ; `HackEmbeddableComponent` est la base du composant fourni (`Options`, `ControllerReady`, `HostContext`) | [`embedded-components.md`](embedded-components.md) |

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
- [x] Types de définition d'une app et table complète des 32 capacités.
- [x] Membres de `IAppExecutionContext`.
- [x] Chaque façade `Hack*` avec ses membres, sa capacité et son document.
- [x] Classes de base des points d'entrée et interfaces des fenêtres.
- [x] Composants Blazor : dialogues, graphiques, vue de fichiers, terminal, icônes, clavier virtuel.
