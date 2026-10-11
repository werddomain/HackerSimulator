# Guide du développeur d'apps HackerOS

## Objet

Ce guide explique comment écrire une app pour HackerOS avec le SDK public :
une fenêtre, une commande du shell ou un service. Il part de zéro et va
jusqu'à l'installation et aux tests. La liste complète des API est dans la
[référence du SDK](sdk-reference.md) ; les règles de la frontière du SDK sont
dans [`app-sdk.md`](app-sdk.md).

Tous les exemples de ce guide ont été compilés et le test de la dernière
section exécuté contre le code de `rebuild/base` (2026-10-11).

## Les trois genres d'app

| Genre (`AppKind`) | Point d'entrée | Exemples dans l'OS |
|---|---|---|
| `Window` | composant Razor dérivé de `WindowAppBase` | Text Editor, File Explorer, Calculator |
| `Terminal` | classe dérivée de `TerminalAppBase`, lancée par le shell | `ls`, `cat`, `grep`, `nano` |
| `Service` | classe dérivée de `ServiceAppBase`, sans interface, liée à la session | services démarrés à la connexion |

Une app est décrite par une classe `[AppManifest]` dérivée d'`AppDefinition`.
Le build en tire `manifest.json` ; l'OS ne lit que ce fichier, jamais la classe.

Des apps complètes servent de modèles dans `Apps/Samples/` (une par genre) et
`Apps/System/` (les apps de base, écrites avec le même SDK public).

## 1. Créer le projet

Un projet d'app vit sous `Apps/` : c'est ce qui en fait un projet d'app
(`HackerOsAppProject`), avec génération du manifeste et contrôle de la
frontière du SDK.

`Apps/Samples/Example.Notes/Example.Notes.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.10" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\HackerOs.App.Abstractions\HackerOs.App.Abstractions.csproj" />
    <ProjectReference Include="..\..\..\Shared\HackerOs.Simulation.Abstractions\HackerOs.Simulation.Abstractions.csproj" />
    <ProjectReference Include="..\..\..\Shared\HackerOs.AppSdk\HackerOs.AppSdk.csproj" />
    <ProjectReference Include="..\..\..\Shared\HackerOs.AppSdk.Blazor\HackerOs.AppSdk.Blazor.csproj" />
  </ItemGroup>

</Project>
```

- Une commande ou un service sans interface peut utiliser
  `Microsoft.NET.Sdk` et se passer de `HackerOs.AppSdk.Blazor`.
- Références permises : les quatre `Shared/*.Abstractions`, les bibliothèques
  `Shared/HackerOs.AppSdk*`, `Platform/HackerOs.Theming.Blazor` et d'autres
  projets sous `Apps/`. Toute autre référence de projet échoue au build
  (`HOSAPP0030`). Les `PackageReference` (MudBlazor par exemple) sont libres.
- Ajouter le projet à `HackerOs.sln` (`dotnet sln add`).

## 2. Décrire l'app : `AppDefinition`

```csharp
using HackerOs.App.Abstractions;

namespace Example.Notes;

[AppManifest]
public sealed class NotesApp : AppDefinition
{
    public override string Id => "com.example.notes";
    public override string Name => "Notes";
    public override AppKind Kind => AppKind.Window;
    public override Type EntryPoint => typeof(NotesWindow);
    public override string PublisherId => "pub.example";
    public override string Description => "Prise de notes dans ~/Documents";
    public override string Category => "productivity";

    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
        AppCapabilities.FileSystemPrivateRead,    // lire ses paramètres
        AppCapabilities.FileSystemPrivateWrite,   // écrire ses paramètres
        AppCapabilities.DialogFileSave,           // « Enregistrer sous »
        AppCapabilities.NotificationsPost,
    ];

    // Ouvrir un .txt ou un .md depuis l'explorateur ou avec `open` lance cette app.
    public override IReadOnlyList<FileHandler> FileHandlers =>
        [new("text/plain", [".txt", ".md"], ["open", "edit"])];

    // Schéma des paramètres : /etc/com.example.notes/settings.config puis ~/.config/com.example.notes/settings.config.
    public override AppSettingsSchemaManifest? Settings => new(
        SchemaVersion: 1,
        Fields:
        [
            new("font-size", AppSettingValueType.Integer, "14", AppSettingScope.AppUser, AppSettingSensitivity.Public),
            new("theme", AppSettingValueType.Enum, "dark", AppSettingScope.AppUser, AppSettingSensitivity.Public, ["dark", "light"]),
        ]);
}
```

Seuls `Id`, `Name`, `Kind` et `EntryPoint` sont obligatoires ; les autres
membres ont une valeur par défaut (tableau complet dans
[`app-model.md`](app-model.md#appdefinition-et-appmanifest)). Les membres les
plus utiles :

| Membre | Rôle |
|---|---|
| `Capabilities` | Droits demandés, pris dans `AppCapabilities` ([référence](sdk-reference.md#capacités)). Ne demander que le nécessaire : une capacité dangereuse impose une approbation administrateur au premier lancement. |
| `FileHandlers` | Types de fichiers que l'app sait ouvrir ; le chemin arrive dans `LaunchFile`. |
| `Terminal` | Pour une commande : nom, usage et alias (`new("wc-notes", "wc-notes [-l] FILE...", ["wcn"])`). |
| `Settings` | Schéma des paramètres lus par `HackSettings`. |
| `SingleInstancePerUser` | Une seule fenêtre ; un second lancement l'active. |
| `AutoStart` | Pour un service : démarré à la connexion. |
| `LaunchVisibility` | `Hidden` pour ne pas apparaître dans le menu Démarrer (commandes, services). |
| `Icons`, `Category`, `Version` | Présentation dans le lanceur. |

Règles imposées par le générateur : classe publique, non abstraite, avec un
constructeur sans paramètre ; getters purs (le générateur instancie la classe
deux fois et compare) ; point d'entrée dans le même assembly. Un projet peut
contenir plusieurs classes `[AppManifest]` : le build écrit alors
`manifests/<appId>.json` au lieu de `manifest.json`.

Le fichier généré est **suivi par git** : un changement de capacité se voit
dans la revue. Ne jamais le modifier à la main, le build l'écrase.

## 3. Une app fenêtrée

Le point d'entrée est un composant Razor qui hérite de `WindowAppBase`. CSS et
JavaScript vont dans des fichiers à côté du composant, jamais en ligne (le
build refuse `style="..."`, `<style>`, `<script>` et `onclick="..."`).

`NotesWindow.razor` :

```razor
@inherits WindowAppBase
@using HackerOs.AppSdk.Blazor

<div class="notes">
    <header class="notes-toolbar">
        <span class="notes-path">@_path</span>
        <button class="notes-button" @onclick="SaveAsync">Enregistrer</button>
        <button class="notes-button" @onclick="SaveAsAsync">Enregistrer sous…</button>
    </header>
    <textarea class="notes-text notes-size-@_fontSize" @bind="_text"></textarea>
    <footer class="notes-status">@_status</footer>
</div>
```

`NotesWindow.razor.cs` :

```csharp
using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.Settings;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Notifications;

namespace Example.Notes;

public partial class NotesWindow
{
    private string _path = "";
    private string _text = "";
    private string _status = "";
    private int _fontSize = 14;

    protected override async Task OnAppInitializedAsync()
    {
        // Ouverte depuis l'explorateur ou par `open notes.md` : le fichier est le premier argument.
        _path = LaunchFile?.Value
            ?? HackPath.Combine(HackEnvironment.GetFolderPath(HackSpecialFolder.MyDocuments), "notes.txt");

        if (await HackFile.ExistsAsync(_path))
        {
            _text = await HackFile.ReadAllTextAsync(_path);
        }

        _fontSize = await HackSettings.GetAsync<int>("font-size");
    }

    private async Task SaveAsync()
    {
        try
        {
            await HackFile.WriteAllTextAsync(_path, _text);
            AppContext.Logging.Log(DiagnosticSeverity.Information, $"Saved {_path}");   // /var/log/apps/com.example.notes.log
            _status = "Enregistré.";
        }
        catch (HackUnauthorizedAccessException ex)
        {
            _status = $"Accès refusé ({ex.RequiredCapability ?? "droits du fichier"}).";
        }
        catch (HackIOException ex)
        {
            _status = $"Erreur : {ex.Message}";
        }
    }

    private async Task SaveAsAsync()
    {
        SaveFileDialogResult result = await SaveFileAsync(new SaveFileDialogRequest
        {
            Title = "Enregistrer la note",
            SuggestedFileName = HackPath.GetFileName(_path),
            DefaultExtension = ".txt",
        });

        if (result.Status == FileDialogStatus.Selected && result.Resource is not null)
        {
            _path = result.Resource.Path.Value;
            await SaveAsync();
            AppContext.Notifications.Post(NotificationSeverity.Success, "Notes", $"Note enregistrée dans {_path}");
        }
    }
}
```

`NotesWindow.razor.css` utilise les jetons du thème (`--hos-*`), pour suivre
le thème choisi par l'utilisateur :

```css
.notes {
    display: flex;
    flex-direction: column;
    height: 100%;
    background: var(--hos-bg-base);
    color: var(--hos-text-primary);
    font-family: var(--hos-font-ui);
}

.notes-text {
    flex: 1;
    font-family: var(--hos-font-mono);
    background: var(--hos-input-bg);
    color: var(--hos-input-color);
    border: 1px solid var(--hos-border);
}
```

### Ce que fournit `WindowAppBase`

| Membre | Rôle |
|---|---|
| `AppContext` | Contexte d'exécution de l'instance (`IAppExecutionContext`) : passerelles, identité, capacités accordées. |
| `LaunchArguments`, `LaunchFile` | Arguments du processus sans `argv[0]` ; premier argument s'il est un chemin absolu. |
| `OnAppInitialized[Async]`, `OnAppParametersSet[Async]`, `OnAppAfterRender[Async]`, `ShouldRenderApp` | Cycle de vie. Les méthodes Blazor (`OnInitializedAsync`…) sont scellées : on surcharge celles-ci. |
| `OpenFileAsync`, `SaveFileAsync`, `SelectFolderAsync` | Dialogues de fichiers de l'OS (capacités `dialogs.file-open`, `dialogs.file-save`, `dialogs.folder-select`) ; le résultat porte le chemin choisi (`Resource.Path`, `Resources`). |
| `MessageBox`, `TextInput` | Boîte de message et saisie de texte. |
| `OnPermissionErrorRaised` | Surcharger pour afficher soi-même une erreur de permission (mettre `ErrorHandled = true`) au lieu de la notification de l'OS. |

Le composant peut aussi implémenter :

- `IWindowCloseGuard` (`ConfirmCloseAsync`) pour demander confirmation avant
  fermeture, par exemple s'il reste des modifications non enregistrées
  (CodeEditor le fait) ;
- `IAppBackHandler` pour gérer le bouton Retour (mobile et barre de titre) ;
  déclarer alors `SupportsBack => true`.

### Contexte ambiant et composants enfants

Les façades `Hack*` savent quelle app les appelle grâce à un contexte ambiant
que l'OS ouvre autour du cycle de vie, des gestionnaires d'événements
(`@onclick`…) et du rendu de `WindowAppBase`. Un **composant enfant** qui
appelle `Hack*` hérite de `HackComponentBase` : il reçoit le même contexte en
cascade. Une méthode `[JSInvokable]` ou un rappel de minuterie .NET (hors
`AppContext.Clock`) ouvre le contexte lui-même :

```csharp
using (HackExecutionContext.Enter(AppContext))
{
    await HackFile.WriteAllTextAsync("~/autosave.txt", _text);
}
```

Sans contexte, l'API lève `HackExecutionContextMissingException` ; elle ne
retombe jamais sur une identité système.

### JavaScript

À n'utiliser que pour une API du navigateur inaccessible en C#. Le module est
un fichier `MonComposant.razor.js` à côté du composant (ou sous `wwwroot/` du
projet), importé ainsi et uniquement ainsi (vérifié au build, `HOSAPP0031`) :

```csharp
[Inject] private IJSRuntime JS { get; set; } = default!;

_module = await JS.InvokeAsync<IJSObjectReference>(
    "import", "./_content/Example.Notes/NotesWindow.razor.js");
```

Ne jamais ajouter de `<script>` dans un hôte.

## 4. Une commande du shell

Une commande reçoit ses arguments, ses flux et le répertoire courant du shell.
Elle rend un code de sortie (0 = succès), lu par `$?` et `&&`.

```csharp
[AppManifest]
public sealed class WordCountApp : AppDefinition
{
    public override string Id => "com.example.wc-notes";
    public override string Name => "wc-notes";
    public override AppKind Kind => AppKind.Terminal;
    public override Type EntryPoint => typeof(WordCountCommand);
    public override string PublisherId => "pub.example";
    public override AppLaunchVisibility LaunchVisibility => AppLaunchVisibility.Hidden;
    public override TerminalCommand? Terminal => new("wc-notes", "wc-notes [-l] FILE...", ["wcn"]);
    public override IReadOnlyList<string> Capabilities => [AppCapabilities.FileSystemUserHomeRead];
}

public sealed class WordCountCommand(AppManifest manifest) : TerminalAppBase(manifest)
{
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        bool lines = context.Arguments.Contains("-l");
        string[] files = [.. context.Arguments.Where(argument => !argument.StartsWith('-'))];
        if (files.Length == 0)
        {
            // Pas de fichier : lire l'entrée standard, pour que `cat a.txt | wc-notes` marche.
            string input = await context.StandardInput.ReadToEndAsync(cancellationToken);
            await context.StandardOutput.WriteLineAsync(Count(input, lines).ToString());
            return 0;
        }

        int exitCode = 0;
        foreach (string file in files)
        {
            try
            {
                // Un chemin relatif part du répertoire courant du shell.
                string text = await HackFile.ReadAllTextAsync(file, cancellationToken);
                await context.StandardOutput.WriteLineAsync($"{Count(text, lines),8} {file}");
            }
            catch (HackFileNotFoundException)
            {
                await context.StandardError.WriteLineAsync($"wc-notes: {file}: No such file or directory");
                exitCode = 1;
            }
        }

        return exitCode;
    }

    private static int Count(string text, bool lines) => lines
        ? text.Split('\n').Length
        : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
```

`TerminalExecutionContext` donne aussi `Environment` (variables du
processus), `App` (le contexte d'app), `Shell` (`IShellSession`, pour une
commande qui agit sur son shell comme `cd` ou `export`, capacité
`shell.control`) et `FullScreen` (écran alternatif plein terminal, comme
`nano`). Les erreurs vont sur `StandardError`, les messages dans le style des
outils GNU. Voir [`shell-commands.md`](shell-commands.md) et
[`shell-interpreter.md`](shell-interpreter.md).

## 5. Un service

Un service tourne en arrière-plan pendant la session, sans fenêtre. Il observe
le jeton d'annulation (déconnexion, arrêt, désactivation) et peut libérer ses
ressources dans `OnStoppingAsync`.

```csharp
[AppManifest]
public sealed class CleanupApp : AppDefinition
{
    public override string Id => "com.example.tmp-cleanup";
    public override string Name => "Temp Cleanup";
    public override AppKind Kind => AppKind.Service;
    public override Type EntryPoint => typeof(CleanupService);
    public override string PublisherId => "pub.example";
    public override AppLaunchVisibility LaunchVisibility => AppLaunchVisibility.Hidden;
    public override bool AutoStart => true;
    public override IReadOnlyList<string> Capabilities =>
        [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite];
}

public sealed class CleanupService(AppManifest manifest) : ServiceAppBase(manifest)
{
    protected override async Task RunCoreAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken)
    {
        string tmp = HackPath.GetFullPath("~/tmp");
        try
        {
            while (!sessionCancellationToken.IsCancellationRequested)
            {
                if (await HackDirectory.ExistsAsync(tmp, sessionCancellationToken))
                {
                    foreach (string file in await HackDirectory.GetFilesAsync(tmp))
                    {
                        await HackFile.DeleteAsync(file, sessionCancellationToken);
                    }

                    context.Logging.Log(DiagnosticSeverity.Information, $"Cleaned {tmp}");
                }

                // Le temps simulé de l'OS, pas Task.Delay.
                await context.Clock.DelayAsync(TimeSpan.FromHours(1), sessionCancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Déconnexion ou arrêt : fin normale.
        }
    }
}
```

Le service apparaît dans l'onglet Services du gestionnaire de tâches et dans
`/proc`. Il communique avec les autres apps par les sujets du bus
d'événements (`context.Events.Publish(topic, payload)` dans son propre espace
de noms, ADR 0038) : voir `Apps/Samples/HackerOs.Samples.ServiceApp`.

## 6. Utiliser les services de l'OS

Une app n'obtient un service de l'OS que par le SDK : une **façade statique**
`Hack*` (forme recommandée, elle lit le contexte ambiant) ou une **passerelle**
de `AppContext`. L'injection d'un service de la plateforme est refusée au
build (`HOSAPP0031`). Les tâches courantes :

| Besoin | API | Capacité |
|---|---|---|
| Lire, écrire, copier, déplacer des fichiers | `HackFile`, `HackDirectory`, `HackPath`, `HackFileInfo` | `filesystem.user-home.*` pour `~`, `filesystem.system.*` (dangereuses) ailleurs |
| Mettre à la corbeille | `HackTrash.MoveToTrashAsync` | celles du fichier |
| Surveiller un dossier | `HackFileSystemWatcher` | celles du dossier |
| Paramètres de l'app | `HackSettings.OpenAsync`, `GetAsync`, `SetAsync` | `filesystem.private.read` / `.write` |
| Journaliser | `AppContext.Logging.Log` → `/var/log/apps/<appId>.log` | aucune |
| Notifier l'utilisateur | `AppContext.Notifications.Post` | `notifications.post` |
| Utilisateur, dossiers spéciaux, variables, arguments | `HackUser`, `HackEnvironment` | aucune |
| Lancer une app, ouvrir un fichier avec son app | `HackApps.LaunchAsync`, `HackApps.OpenFileAsync` | `apps.launch` |
| Presse-papiers | `HackClipboard` | `clipboard.read` / `.write` |
| Réseau simulé (et réel si l'OS l'autorise) | `HackDns`, `HackPing`, `HackHttpClient`, `HackTcpClient` | `network.simulated.*`, `network.real.access` (dangereuse) |
| Processus et services | `HackProcess`, `HackServiceController` | `process.list`, `process.manage`, `services.manage` |
| Héberger un shell (terminal, éditeur de code) | `HackShell.StartAsync` | `shell.host` (dangereuse) |
| Temps simulé, minuteries | `AppContext.Clock` | aucune |

Les chemins acceptent `~` et les chemins relatifs (au répertoire courant du
processus). Les erreurs suivent `System.IO` : `HackFileNotFoundException`,
`HackDirectoryNotFoundException`, `HackFileConflictException` (révision
concurrente), `HackIOException`, `HackUnauthorizedAccessException` (avec
`RequiredCapability`).

Exemples détaillés par domaine : [`hack-file-api.md`](hack-file-api.md),
[`settings-files.md`](settings-files.md), [`logging.md`](logging.md),
[`trash.md`](trash.md), [`process-environment.md`](process-environment.md),
[`sdk-network.md`](sdk-network.md), [`task-manager.md`](task-manager.md),
[`shell-host.md`](shell-host.md), [`system-administration.md`](system-administration.md).

### Capacités dangereuses

`filesystem.system.*`, `apps.manage`, `process.manage`, `services.manage`,
`network.real.access`, `shell.host`, `permissions.manage`… sont
**dangereuses** : pour une app non système, l'OS demande à un administrateur
de les approuver au premier lancement, et l'app peut être lancée sans elles si
l'administrateur refuse. Le code doit donc s'attendre à
`HackUnauthorizedAccessException` et dégrader proprement. Détails :
[`capabilities.md`](capabilities.md),
[`dangerous-capability-approval.md`](dangerous-capability-approval.md).

## 7. Installer l'app dans l'OS

Ajouter une `ProjectReference` dans `OS/HackerOs.Ecosystem/HackerOs.Ecosystem.csproj` :

```xml
<ProjectReference Include="..\..\Apps\Samples\Example.Notes\Example.Notes.csproj" />
```

C'est tout. Au build, l'hôte agrège le manifeste dans
`BuiltInApplications.json` et charge l'assembly de l'app au premier lancement.
`IsSystemApp="true"` sur la référence fait de l'app une app système (non
désinstallable, capacités dangereuses accordées d'office) : réservé aux apps
de base de l'OS.

## 8. Tester une app

`Tests/HackerOs.Tests.Support` fournit `MinimalAppExecutionContext` et des
passerelles factices (`FakeAppFileSystemGateway`, `FakeAppClockGateway`,
`FakeAppLoggingGateway`, `FakeAppIntentGateway`, `FakeAppEnvironmentGateway`,
`FakeShellSession`…). Le manifeste vient de la définition elle-même.

```csharp
public sealed class WordCountCommandTests
{
    private static readonly AppManifest Manifest = AppDefinitionMapper.ToManifest(new WordCountApp());

    [Fact]
    public async Task Counts_the_words_of_a_file()
    {
        FakeAppFileSystemGateway files = new FakeAppFileSystemGateway()
            .WithDirectory("/home/alice")
            .WithFile("/home/alice/notes.txt", "un deux trois");
        MinimalAppExecutionContext app = new(Manifest, files, userId: "alice");
        StringWriter output = new();
        TerminalExecutionContext terminal = new(
            app, ["notes.txt"], TextReader.Null, output, TextWriter.Null, "/home/alice",
            new Dictionary<string, string>());

        // L'OS ouvre le contexte ambiant autour d'ExecuteAsync ; un test le fait lui-même.
        using (HackExecutionContext.Enter(app, "/home/alice"))
        {
            int exitCode = await new WordCountCommand(Manifest).ExecuteAsync(terminal, CancellationToken.None);
            Assert.Equal(0, exitCode);
        }

        Assert.Equal("       3 notes.txt", output.ToString().TrimEnd());
    }
}
```

Le projet de tests référence le projet de l'app et `HackerOs.Tests.Support`
(xUnit 2.9). Les composants fenêtrés se testent avec bUnit (voir
`Tests/HackerOs.Apps.*.Tests`).

## 9. Liste de contrôle avant de livrer

- [ ] Le projet est sous `Apps/`, dans `HackerOs.sln`, et ne référence que le SDK.
- [ ] La classe `[AppManifest]` ne demande que les capacités nécessaires.
- [ ] `manifest.json` (ou `manifests/*.json`) régénéré est committé.
- [ ] Pas de CSS ni de JavaScript en ligne ; styles sur les jetons `--hos-*`.
- [ ] Les erreurs de permission et de fichier sont gérées et affichées.
- [ ] Des tests couvrent la logique de l'app.
- [ ] L'app fonctionne avec le seul hôte Ecosystem, sans serveur.
- [ ] Un document décrit l'app sous `docs/`, ajouté à l'index [`README.md`](README.md).

## Tâches de ce document

- [x] Présenter les trois genres d'app et leurs points d'entrée.
- [x] Création du projet, références permises.
- [x] `AppDefinition` : membres utiles, règles du générateur.
- [x] Exemple complet d'app fenêtrée (fichiers, paramètres, journal, dialogue, notification, CSS).
- [x] Exemple de commande du shell (arguments, flux, code de sortie).
- [x] Exemple de service (boucle, horloge simulée, annulation).
- [x] Table des services de l'OS et capacités associées.
- [x] Installation dans l'hôte et test unitaire d'une commande.
- [x] Compiler les exemples et exécuter le test contre `rebuild/base`.
