# SDK des apps et frontière du SDK

## Objet

Une app HackerOS, système ou tierce, n'obtient les services de l'OS que par le
SDK public (principe 3 du plan, ADR 0055). Ce document décrit :

- ce qui forme le SDK et ce qu'une app a le droit de référencer et d'injecter ;
- comment le build vérifie ces règles (codes `HOSAPP0030`, `HOSAPP0031`,
  `HOSSDK0001`, ADR 0059) ;
- la liste d'exemptions temporaire de la phase 4 ;
- les API du SDK, au fur et à mesure que les chantiers `rebuild/4-*` les
  ajoutent (ADR 0056).

Chaque chantier de la phase 4 qui ajoute une API la documente ici, dans la
section [API du SDK](#api-du-sdk).

## Ce qui forme le SDK

| Projet | Contenu |
|---|---|
| `Shared/HackerOs.AppSdk` | `IAppExecutionContext` et ses passerelles, façades statiques `Hack*`, classes de base des points d'entrée (`TerminalAppBase`…). |
| `Shared/HackerOs.AppSdk.Blazor` | Composants et services Blazor des apps fenêtrées (fenêtre, téléchargement…). |
| `Shared/HackerOs.AppSdk.FileView` | Vue de fichiers réutilisable (explorateur, dialogues). |
| `Shared/HackerOs.AppSdk.Icons` | Catalogue d'icônes. |
| `Shared/HackerOs.AppSdk.Terminal` | Panneau de terminal (`TerminalPanel`), qui héberge un shell par `HackShell`, et console à onglets (`TerminalTabs`). |
| `Shared/HackerOs.AppSdk.VirtualKeyboard` | Clavier virtuel. |

Toute bibliothèque `Shared/HackerOs.AppSdk` ou `Shared/HackerOs.AppSdk.*` est
une **bibliothèque SDK** (propriété MSBuild `HackerOsSdkLibrary`, calculée par
`build/HackerOs.Apps.targets`). Un projet de tests nommé de la même façon n'en
est pas une : seul l'emplacement sous `Shared/` compte.

## Règles

### Références de projet (ADR 0055 §2)

| Projet | Peut référencer | Sinon |
|---|---|---|
| Projet d'app (`HackerOsAppProject=true`, tout projet sous `Apps/`) | `Shared/HackerOs.{App,Simulation,Theming,Game}.Abstractions`, les bibliothèques SDK, `Platform/HackerOs.Theming.Blazor`, un autre projet sous `Apps/` | `HOSAPP0030` |
| Bibliothèque SDK | les quatre `*.Abstractions` ci-dessus, une autre bibliothèque SDK | `HOSSDK0001` |

Seules les références **directes** du projet comptent : une référence
transitive est vérifiée dans le projet qui la déclare. Les `PackageReference`
restent libres. La référence d'ordre de build vers le générateur de manifeste,
ajoutée par `HackerOs.Apps.targets`, est ignorée.

### Injection (ADR 0055 §3)

Une app ne peut obtenir par injection qu'un service dont le type est :

1. défini dans une bibliothèque SDK ;
2. un type de MudBlazor (par exemple `MudBlazor.IDialogService`) ;
3. `Microsoft.JSInterop.IJSRuntime`, seulement pour importer son propre module
   JavaScript.

Deux points d'injection sont contrôlés, ce sont les seuls que la plateforme
honore pour une app :

- chaque propriété `[Inject]` de chaque type de l'assembly (une directive
  `@inject` compile en propriété `[Inject]`) ;
- chaque paramètre des constructeurs publics des points d'entrée
  (`EntryPoint`, `MobileEntryPoint`), que l'hôte construit avec
  `ActivatorUtilities` (ADR 0034 §2).

Pour un constructeur de point d'entrée, restent permis : le `AppManifest` que
l'hôte passe explicitement, les types valeur et `string` (jamais enregistrés
comme services : un tel constructeur ne sert qu'aux tests, par exemple un
intervalle de minuterie).

Les types de `Simulation.Abstractions` et `App.Abstractions` restent permis
comme **données** (une propriété non `[Inject]`, un paramètre de méthode).

### Appels JavaScript (ADR 0055 §3)

Un appel `Invoke*Async` sur un `IJSRuntime` doit avoir la forme :

```csharp
_module = await JS.InvokeAsync<IJSObjectReference>(
    "import", "./_content/<projet de l'app>/<fichier>.js");
```

Les appels sur le `IJSObjectReference` obtenu sont libres : c'est le code de
l'app. Passer l'`IJSRuntime` à un composant ou à un service d'une bibliothèque
SDK (par exemple `BrowserFileDownloader` de `HackerOs.AppSdk.Blazor`) est
permis : c'est le code du SDK qui fait l'appel.

## Vérification

| Règle | Où | Quand | Code |
|---|---|---|---|
| Références d'une app | cible `ValidateHackerOsSdkBoundary` de `build/HackerOs.Apps.targets` | avant `ResolveProjectReferences`, donc avant la compilation | `HOSAPP0030` |
| Références d'une bibliothèque SDK | même cible | idem | `HOSSDK0001` |
| Injection | `SdkBoundaryInspector` du générateur de manifeste (`Tools/HackerOs.Tools.ManifestGenerator`) | après la compilation, avec la génération de `manifest.json` ; en cas d'erreur, rien n'est écrit | `HOSAPP0031` |
| `IJSRuntime` | test de sources `SdkBoundarySourceTests` (`Tests/HackerOs.Ecosystem.Tests`) | `dotnet test` | échec du test |

Les messages nomment le projet, la référence ou le type en cause, et la
règle de l'ADR :

```text
App.csproj : error HOSAPP0030: [My.App] Forbidden project reference 'Platform/HackerOs.Platform.Core/HackerOs.Platform.Core.csproj'. …
App.csproj : error HOSAPP0031: [My.App.MyWindow] [Inject] property 'Sessions' injects 'HackerOs.Simulation.Abstractions.Sessions.ISessionService' …
```

Le générateur relance l'inspection quand `HackerOs.Apps.targets` change : retirer
une exemption suffit à faire réapparaître les erreurs au build suivant.

## Exemptions temporaires (ADR 0055 §6)

`build/HackerOs.Apps.targets` déclare un élément `HackerOsSdkBoundaryExemption`
par projet encore hors de la frontière, avec l'ADR et le ou les chantiers qui
le corrigeront. Pour un projet exempté, les trois vérifications donnent des
avertissements au lieu d'erreurs, et le test de sources `IJSRuntime` l'ignore.

| Projet | Violations au 2026-10-10 | Chantier qui retire l'exemption |
|---|---|---|
| `HackerOs.Apps.Settings` | `Platform.Core`, `Infrastructure.Browser` ; `AppCatalog` et `ISessionService` (fenêtre, onglet Permissions), `IServiceProvider`, services de synchronisation, `ThemePreferenceService` ; `location.reload`, module IndexedDB d'un autre projet. L'onglet « Installed Apps » est passé au SDK (`4-sdk-user-and-apps`). | `4-sdk-system-admin` |
| `HackerOs.Apps.Diagnostic` | `Platform.Core`, `Infrastructure.Browser` ; `navigator.clipboard` | `4-sdk-system-admin` |

Règles de la liste :

- un chantier qui corrige un projet retire son élément dans le même
  changement ; un projet listé pour deux chantiers sort de la liste avec le
  dernier ;
- **aucun projet ne peut y être ajouté** : le test
  `SdkBoundarySourceTests.Exemption_list_only_contains_projects_from_the_initial_list`
  échoue sinon ;
- `4-sdk-boundary-final` supprime le mécanisme (élément, propriété
  `_HackerOsSdkBoundaryExempt`, option `--boundary-exempt` du générateur).

Sorties de la liste : FileExplorer, TextEditor et CodeEditor
(`4-sdk-user-and-apps`, 2026-10-11).

Les références `Platform.Core` inutilisées de Calculator, HackPaint,
IconViewer, TextEditor et Browser ont été retirées par `4-sdk-boundary` :
Calculator, HackPaint et IconViewer passent sans exemption.

Exemptions déjà retirées : `HackerOs.Apps.Browser` et `HackerOs.Commands`
(`4-sdk-network`, voir [`sdk-network.md`](sdk-network.md)).
`HackerOs.AppSdk.Terminal` (`4-sdk-shell-host`, voir
[`shell-host.md`](shell-host.md)).

## Comment une app obtient un service de l'OS (ADR 0055 §4)

Uniquement par le SDK :

- une **passerelle** sur `IAppExecutionContext`, propre à l'instance, qui
  vérifie la capacité requise à chaque appel. Une nouvelle passerelle est un
  membre de l'interface avec une implémentation par défaut « non prise en
  charge », pour que les implémentations existantes compilent encore ; la
  fabrique de confiance de Platform Core fournit la vraie ;
- une **façade statique** `Hack*` qui lit le contexte ambiant (ADR 0043) et
  appelle la passerelle. C'est la forme recommandée.

Une capacité manquante lève `HackUnauthorizedAccessException` et passe par
`IAppPermissionErrorGateway`.

## API du SDK

Façades statiques existantes au début de la phase 4 :

| Façade | Rôle | Document |
|---|---|---|
| `HackFile`, `HackDirectory`, `HackPath` | fichiers | [`hack-file-api.md`](hack-file-api.md) |
| `HackExecutionContext` | contexte ambiant | [`hack-file-api.md`](hack-file-api.md) |
| `HackEnvironment` | arguments, répertoire courant, variables | [`process-environment.md`](process-environment.md) |
| `HackSettings` | paramètres en fichiers | [`settings-files.md`](settings-files.md) |
| `HackTrash` | corbeille | [`trash.md`](trash.md) |
| `HackDns`, `HackPing`, `HackHttpClient`, `HackTcpClient` | réseau, repli réel décidé par l'OS (`4-sdk-network`) | [`sdk-network.md`](sdk-network.md) |
| `HackDriveInfo` | taille et espace libre des disques (`4-proc-metrics`) | [`system-metrics.md`](system-metrics.md) |
| `HackClipboard` | presse-papiers de session, texte et liste de fichiers (`4-file-explorer`) | [`file-explorer.md`](file-explorer.md) |
| `HackTextContent` | texte ou binaire décidé sur le contenu, comme `file` (`4-file-explorer`) | [`text-editor.md`](text-editor.md) |

Passerelles de `IAppExecutionContext` : `FileSystem`, `Events`,
`Notifications`, `Logging`, `Clock`, `Processes`, `Intents`, `Watch`,
`Services`, `PermissionErrors`, `Environment`, `Catalog` et `AppManagement`
(ADR 0056, `4-sdk-user-and-apps`), `Network` (`4-sdk-network`), `ShellHost`
(`4-sdk-shell-host`), `Drives` (ADR 0057, `4-proc-metrics`),
`Clipboard` (ADR 0056 §12, `4-file-explorer`).

`ShellErrorText` (texte court d'une erreur de fichier, pour les commandes comme
`cd`) est aussi dans le SDK, à côté de `IShellSession` (`4-sdk-network`).

### Utilisateur courant (ADR 0056 §1)

```csharp
HackUserInfo me = HackUser.Current;     // LoginName, DisplayName, HomeDirectory, IsAdministrator
string docs = HackEnvironment.GetFolderPath(HackSpecialFolder.MyDocuments);   // /home/benoit/Documents
HackUserInfo cached = HackUser.Describe(AppContext);   // sans contexte ambiant (rendu d'un composant)
```

- `HackUser.Current` décrit l'utilisateur **effectif** : `HackUser.Root`
  (`root`, `/root`, administrateur) pour un processus lancé par `sudo`.
- `HackSpecialFolder` : `UserProfile` (`~`), `Desktop`, `MyDocuments`
  (`~/Documents`), `ApplicationData` (`~/.config`), `LocalApplicationData`
  (`~/.local/share`). Le chemin est calculé, pas vérifié.
- Aucune capacité. `IAppExecutionContext` gagne `UserLoginName` et
  `UserDisplayName` (implémentations par défaut ; la fabrique les remplit
  depuis le principal).
- Remplace `ISessionService.CurrentPrincipal` dans FileExplorer, TextEditor et
  CodeEditor. Comme `BuildRenderTree` ne tourne pas dans le contexte ambiant,
  un composant qui affiche le dossier personnel utilise
  `HackUser.Describe(AppContext)`.

### Catalogue des apps (ADR 0056 §2)

```csharp
IReadOnlyList<HackAppInfo> apps = await HackApps.GetInstalledAsync();
IReadOnlyList<HackAppInfo> editors = await HackApps.GetHandlersAsync("~/notes.md");
await HackApps.LaunchAsync("org.hackeros.text-editor", ["/home/benoit/notes.md"]);
HackOpenFileResult opened = await HackApps.OpenFileAsync("~/notes.md");   // Opened, ChooserRequired (+ CandidateAppIds), NoHandler
```

- `HackAppInfo` : `Id`, `Name`, `Description`, `Kind`, `Category`, `Icon`,
  `Version`, `PublisherId`, `IsSystemApp`, `IsHidden`, `FileHandlers`.
- `GetInstalledAsync` : apps installées pour l'utilisateur, triées par nom ;
  une app désinstallée (ADR 0048) n'y figure pas. **Aucune capacité.**
- `GetHandlersAsync` : apps fenêtrées installées qui déclarent l'extension ou
  le type MIME ; le défaut de `/etc/hackeros/file-associations.json` en tête.
- `LaunchAsync` et `OpenFileAsync` passent par `IAppIntentGateway`
  (`apps.launch`). Échec : `HackAppLaunchException` (app inconnue,
  désinstallée, ou erreur au démarrage).
- `HackApps.Changed` (ou `AppContext.Catalog.Changed`, à préférer quand on se
  désabonne dans `Dispose`, hors contexte ambiant) signale une installation ou
  une désinstallation ; les gestionnaires s'exécutent dans le contexte de
  l'app abonnée.
- Passerelle : `IAppCatalogGateway Catalog` (`AppCatalogGateway`, Platform Core).

### Gestion des apps (ADR 0056 §3)

```csharp
IReadOnlyList<HackAppInstallInfo> all = await HackAppManagement.GetAllAsync();   // App, State, UninstallClosure
IReadOnlyList<string> removed = await HackAppManagement.UninstallAsync("org.hackeros.calculator", HackInstallScope.User);
await HackAppManagement.ReinstallAsync("org.hackeros.calculator", HackInstallScope.Machine);
```

- **Capacité `apps.manage`** (dangereuse) pour tous les membres. Paramètres la
  déclare désormais.
- `HackAppInstallState` : `Installed` (y compris une app système, signalée par
  `App.IsSystemApp`), `UninstalledForUser`, `UninstalledForMachine`.
- Portée `Machine` : l'agent d'authentification de l'OS demande un
  administrateur ([`admin-authentication-agent.md`](admin-authentication-agent.md)),
  puis l'OS écrit `/etc/hackeros/apps.config` lui-même. Refus ou annulation :
  `HackUnauthorizedAccessException`.
- App système, ou fermeture qui en contient une : `InvalidOperationException`,
  avant toute demande de mot de passe. Réinstaller pour soi une app
  désinstallée par l'administrateur : `InvalidOperationException`. App
  inconnue : `ArgumentException`.
- Passerelle : `IAppManagementGateway AppManagement` (`AppManagementGateway`,
  sur `IAppInstallationService`).

### Réseau (ADR 0056 §6)

`HackDns`, `HackPing`, `HackHttpClient` et `HackTcpClient`, passerelle
`IAppNetworkGateway Network`, repli réel décidé par l'OS : voir
[`sdk-network.md`](sdk-network.md).

### Disques (ADR 0057 §7)

`HackDriveInfo.GetDrivesAsync()` rend les montages qui ont une taille
(aujourd'hui `/`), sur le modèle de `System.IO.DriveInfo` : `Name`,
`DriveFormat` (`idbfs`), `DriveType` (`Fixed`), `VolumeLabel` (`hda`),
`TotalSize`, `AvailableFreeSpace`, `TotalFreeSpace`, `RootDirectory`. Aucune
capacité, comme `df`. Passerelle `IAppDriveGateway Drives`, implémentée par
`AppDriveGateway` dans Platform Core. Les valeurs sont lues une fois, à l'appel.
Les mesures CPU, disque et réseau, elles, sont des fichiers de `/proc` : voir
[`system-metrics.md`](system-metrics.md).

### Processus et services (ADR 0056 §4 et §5)

| API | Passerelle | Capacité | Remplace | Document |
|---|---|---|---|---|
| `HackProcess` (`GetProcessesAsync`, `GetProcessByIdAsync`, `GetCurrentProcessAsync`, `RefreshAsync`, `GetEnvironmentVariablesAsync`, `KillAsync`, `CloseMainWindowAsync`), `HackProcessState`, `HackProcessIoCounters` | lecture : `IAppExecutionContext.FileSystem` sur `/proc` ; action : `IAppExecutionContext.Processes` (`IAppProcessGateway`) | `filesystem.system.read` pour lire `/proc` ; `process.list` (normale) pour voir les autres processus ; `process.manage` (dangereuse) pour arrêter un autre processus | `IProcessManager`, `AppLifecycleOrchestrator`, `IEventBus` | [`task-manager.md`](task-manager.md) |
| `HackServiceController` (`GetServicesAsync`, `RefreshAsync`, `StartAsync`, `StopAsync`, `SetStartTypeAsync`), `HackServiceControllerStatus` | `Catalog` (liste), `/proc` (état), `Services` (`IAppServiceControlGateway`) | aucune pour lister ; `services.manage` (dangereuse) pour agir sur le service d'une autre app ou lire son type de démarrage | `AppCatalog` filtré sur `AppKind.Service`, orchestrateur | [`task-manager.md`](task-manager.md) |

Les données d'un processus viennent de `/proc`, exactement ce que `ps` lit ; il
n'y a pas d'abonnement aux événements de processus : un gestionnaire de tâches
relit `/proc` chaque seconde.

### Graphiques (ADR 0058 §2)

`HackLineChart` (`HackerOs.AppSdk.Blazor.Charts`) trace en SVG natif une ou
plusieurs `HackChartSeries`, la valeur la plus récente à droite, avec une
légende et une échelle automatique ; ni bibliothèque ni JavaScript. Les
couleurs viennent du thème (`--hos-chart-series-0` à `7`). Voir
[`task-manager.md`](task-manager.md).

### Presse-papiers (ADR 0056 §12)

```csharp
await HackClipboard.SetFileDropListAsync(["~/rapport.txt"], HackClipboardFileOperation.Cut);
HackClipboardFileList? files = await HackClipboard.GetFileDropListAsync();   // Operation + Paths
string? text = await HackClipboard.GetTextAsync();
```

- Un presse-papiers par **session**, partagé par toutes ses apps (`SystemClipboard`,
  Platform Core), vidé à la déconnexion ; liste de fichiers au format GNOME
  `x-special/gnome-copied-files`, lisible aussi en texte (les chemins).
- **Capacités** `clipboard.read` (lecture) et `clipboard.write` (écriture,
  vidage), normales. `Changed` ne demande aucune capacité.
- `GetFileReferencesAsync` / `SetFileReferencesAsync` : `[Obsolete]`, formes de
  « copier ». L'ancienne classe en mémoire `AppClipboardGateway` du SDK est
  supprimée (la vraie est dans Platform Core).
- Passerelle : `IAppClipboardGateway Clipboard` (`AppClipboardGateway`, Platform Core).
- `HackDirectory.CopyAsync` (copie récursive) et `FileTransfer`
  (`HackerOs.AppSdk.FileView`, transferts de l'explorateur) sont décrits dans
  [`file-explorer.md`](file-explorer.md).

Les autres API de l'ADR 0056 (`HackPermissionAdministration`,
`HackServerConnection`, `HackSystem`, `HackStorageDiagnostics`…) sont ajoutées ici par leurs chantiers.

### Hôte de shell (ADR 0056 §7)

| API | Passerelle | Capacité | Remplace | Document |
|---|---|---|---|---|
| `HackShell.StartAsync`, `IHackShell` (`ExecuteAsync`, `CompleteAsync`, `GetInstalledCommands`, `WorkingDirectory`, `ExitStatus`, `Prompt`, historique), `HackShellOptions`, `HackShellCompletion`, `HackCredentialPrompt` | `IAppExecutionContext.ShellHost` (`IAppShellHostGateway`) | `shell.host` (dangereuse) | `ShellInterpreter`, `ISessionService`, `IElevationService` dans `TerminalPanel` | [`shell-host.md`](shell-host.md) |

`4-terminal` ajoute la complétion des chemins, `HackShellCompletion.DisplayCandidates`,
`IShellSession.Exit` (commande `exit`) et le composant d'onglets `TerminalTabs`
de `HackerOs.AppSdk.Terminal` ([`terminal-console.md`](terminal-console.md)).

Le shell est un processus `hsh` enfant de l'app ; ses commandes sont ses
enfants et gardent leurs propres capacités. Le terminal lit les identifiants
(`HackCredentialPrompt`), l'OS les vérifie.

### Arguments de lancement d'une fenêtre

`WindowAppBase.LaunchArguments` rend les arguments du processus sans `argv[0]`
(comme `Main(string[] args)`), et `WindowAppBase.LaunchFile` le premier s'il est
un chemin absolu. Ouvrir un fichier depuis l'explorateur ou avec `open` lance
l'app associée avec ce chemin : une app de document lit son fichier initial là
(voir [`text-editor.md`](text-editor.md)).

## Décisions

- Codes `HOSAPP0030` et `HOSAPP0031` au lieu de `HOSAPP0002` et `HOSAPP0003`,
  déjà pris par l'ADR 0047 (ADR 0059).
- Exemption par projet, pas par référence : plus simple à retirer, et un
  chantier corrige un projet entier.
- Les avertissements d'un projet exempté ne sont pas transformés en erreurs :
  `TreatWarningsAsErrors` ne s'applique qu'au compilateur C#, pas aux
  avertissements MSBuild.
- Le contrôle d'injection compare les types par nom (assembly, nom complet) :
  l'assembly de l'app est chargée dans un contexte isolé.

- `HackAppInfo` ajoute `PublisherId` et `IsHidden` à la liste de l'ADR 0056 :
  l'onglet « Installed Apps » affiche l'éditeur, et un lanceur doit savoir
  qu'une app est cachée. `HackApps.OpenFileAsync` rend un résultat plutôt que
  rien, parce que l'explorateur doit proposer un choix quand plusieurs apps
  conviennent. Les méthodes de `HackAppManagement` rendent les ids écrits.
- Un composant enfant qui n'hérite pas de `HackComponentBase` appelle les
  passerelles de `AppContext` directement (`AppContext.Catalog`,
  `AppContext.AppManagement`) : ses gestionnaires d'événements ne sont pas dans
  le contexte ambiant.

## Liste des tâches du chantier `rebuild/4-sdk-boundary`

- [x] Cible MSBuild `ValidateHackerOsSdkBoundary` (`HOSAPP0030`, `HOSSDK0001`), références directes seulement.
- [x] Propriété `HackerOsSdkLibrary` pour les bibliothèques SDK.
- [x] `SdkBoundaryInspector` dans le générateur (`HOSAPP0031`), option `--boundary-exempt`.
- [x] Liste `HackerOsSdkBoundaryExemption` (9 projets) et test qui l'empêche de grandir.
- [x] Test de sources `IJSRuntime` dans `HackerOs.Ecosystem.Tests`.
- [x] Retrait des références `Platform.Core` inutilisées (Calculator, HackPaint, IconViewer, TextEditor, Browser).
- [x] Projets de test `ManifestGenerator.Fixtures.ForbiddenReference` et `ForbiddenInjection`, tests unitaires de l'inspecteur.
- [x] ADR 0059 (numéros des codes), ce document, `app-model.md`, plan et index à jour.

## Liste des tâches du chantier `rebuild/4-sdk-user-and-apps`

- [x] `HackUser`, `HackUserInfo`, `HackEnvironment.GetFolderPath`, `HackSpecialFolder`.
- [x] `IAppExecutionContext` : `UserLoginName`, `UserDisplayName`, passerelles `Catalog` et `AppManagement`.
- [x] `HackApps`, `HackAppInfo`, `IAppCatalogGateway` et `AppCatalogGateway`.
- [x] `HackAppManagement`, `IAppManagementGateway` et `AppManagementGateway` (`apps.manage`).
- [x] Agent d'authentification de l'OS, courtier et fenêtre ([`admin-authentication-agent.md`](admin-authentication-agent.md)).
- [x] Migration de FileExplorer (« Open with », dossier personnel), TextEditor et CodeEditor ; FileExplorer perd sa référence à `Platform.Core`.
- [x] Onglet « Installed Apps » de Paramètres sur le SDK ; `apps.manage` déclarée par Paramètres.
- [x] Exemptions retirées : FileExplorer, TextEditor, CodeEditor ; Paramètres reste pour `4-sdk-system-admin`.
- [x] Tests : agent, passerelles, `HackUser`, fenêtre.

## Liste des tâches du chantier `rebuild/4-file-explorer`

- [x] `HackClipboard`, `HackClipboardFileList`, passerelle `IAppExecutionContext.Clipboard` ; `SystemClipboard` et `AppClipboardGateway` dans Platform Core.
- [x] `HackDirectory.CopyAsync`, `FileTransfer` et les ajouts de `FileView` ([`file-explorer.md`](file-explorer.md)).
- [x] Explorateur sur ces API ; capacités `clipboard.read`, `clipboard.write`, `notifications.post` déclarées. Aucune exemption à retirer (FileExplorer était déjà sorti de la liste).
