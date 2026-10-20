# Composants intégrables

Une app peut afficher dans sa fenêtre un composant qu'une autre app a
enregistré auprès de l'OS, sans référencer son assemblage, comme Kate affiche
la KPart de Konsole. Décision : [ADR 0063](adr/0063-embeddable-app-components.md).
Le même mécanisme porte les contrôles et dialogues que l'OS fournit à toutes
les apps (le SDK ne contient que leurs contrats) :
[ADR 0148](adr/0148-sdk-contracts-and-os-provided-controls.md), ajusté par
l'[ADR 0153](adr/0153-contract-host-implementation-adjustments.md).

## Enregistrer un composant (app fournisseur)

```csharp
[AppManifest]
public sealed class TerminalApp : AppDefinition
{
    // ...
    public override IReadOnlyList<AppComponent> Components =>
        [new(HackComponentContracts.Terminal, typeof(TerminalEmbed))];
}
```

```razor
@inherits HackEmbeddableComponent
<TerminalPanel @ref="_panel" AppContext="HostContext" ... />

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await ControllerReady.InvokeAsync(_panel);
    }
}
```

Règles (vérifiées par `AppManifestValidator`) : app `Window`, contrat en mots
minuscules séparés par des points, un contrat par app, type dans l'assemblage
du point d'entrée. Le composant dérive de `HackEmbeddableComponent` et reçoit
`Options` et `ControllerReady` ; `HostContext` est le contexte de la fenêtre
hôte.

## Embarquer un composant (app hôte)

```razor
<HackEmbeddedComponent TController="IHackTerminal"
                       Contract="@HackComponentContracts.Terminal"
                       Options="_options"
                       ControllerChanged="terminal => _terminal = terminal">
    <Unavailable Context="reason"><p>@reason</p></Unavailable>
</HackEmbeddedComponent>
```

Garder la même instance d'`Options` d'un rendu à l'autre : une nouvelle
instance est transmise au composant. Le contrôleur reste valable tant que
`HackEmbeddedComponent` est rendu.

## Composant typé (contrat fourni par l'OS)

Pour un contrat de l'OS, le SDK donne un composant typé que l'app utilise
comme n'importe quel composant Blazor. Il dérive de
`HackContractComponent<TController>` et recopie ses paramètres par nom :

```csharp
public sealed class HackFileView : HackContractComponent<IHackFileView>
{
    [Parameter] public string? Path { get; set; }
    [Parameter] public EventCallback<string> Opened { get; set; }

    protected override string Contract => HackComponentContracts.FileView;

    protected override void AddProviderParameters(IDictionary<string, object?> parameters)
    {
        parameters[nameof(Path)] = Path;
        parameters[nameof(Opened)] = Opened;
    }
}
```

Le fournisseur (`HackEmbeddableComponent`) déclare les mêmes noms en
`[Parameter]`. Un paramètre qu'il ne connaît pas encore (app compilée avec un
SDK plus récent) est rangé dans `UnmatchedParameters` au lieu de faire
échouer le rendu ; il ne doit donc pas déclarer son propre
`CaptureUnmatchedValues`. La base expose `ControllerChanged`, `Unavailable`,
`Controller` et le crochet `OnControllerChangedAsync`, et ne transmet un
nouveau jeu de paramètres que si une valeur a changé. En dessous,
`HackEmbeddedComponent<T>` reçoit ces valeurs par son paramètre `Parameters`
(`Options` et `ControllerReady` gardent la priorité).

**Droits.** Le fournisseur est rendu dans la fenêtre de l'hôte, avec
l'`AppContext` de l'hôte ; les callbacks qu'il invoque (`Opened`…)
s'exécutent dans le contexte de l'hôte. Une app ne voit pas un fichier de plus
en affichant le contrôle d'une autre.

## Registre et fournisseurs

- **Apps.** Le catalogue construit le registre au démarrage à partir des
  manifestes (`AppCatalog.ComponentProviders`,
  `GetComponentProviders(contract)`) : apps système d'abord, puis par
  identifiant. `HackComponentHost` saute les apps désinstallées et charge
  l'assemblage du fournisseur au premier usage.
- **Plateforme.** La plateforme inscrit ses propres fournisseurs dans le
  conteneur de services (`services.AddSingleton(new HackPlatformComponent(contrat, typeof(Composant)))`) ;
  ils passent avant toute app, sans chargement, et leur `ProviderAppId` vaut
  `HackComponentResolution.PlatformProviderId` (`"platform"`).
- **Contrats réservés** (`AppComponentContracts.GetProviderScope`) :

| Contrat | Fournisseur admis | Refus |
| --- | --- | --- |
| `hackeros.terminal` | toute app fenêtrée | — |
| `hackeros.file-view`, `hackeros.file-dialogs` | apps système | `HOSAPP0025` au build de l'hôte ; à l'exécution, inscription écartée et `catalog.component.reserved` dans `/var/log/syslog` |
| `hackeros.context-menu` | plateforme | `manifest.component.contract.platform` (`HOSAPP0009` au build de l'app) |

- **Indisponible.** Un contrat réservé sans fournisseur qui se charge est une
  erreur de l'OS : `component.unavailable` est écrit dans `/var/log/syslog`
  (source `components`). Un contrat ouvert sans fournisseur est seulement
  montré à l'utilisateur (`Unavailable`).

## Contrats

| Contrat | Contrôleur | Options | Fournisseur |
| --- | --- | --- | --- |
| `hackeros.terminal` (`HackComponentContracts.Terminal`) | `IHackTerminal` | `HackTerminalOptions` (`WorkingDirectory`, `ShowBanner`, `FocusOnStart`) | Terminal (`TerminalEmbed`) |
| `hackeros.file-view` (`HackComponentContracts.FileView`) | `IHackFileView` (chantier 3 du plan) | — | Explorateur de fichiers |
| `hackeros.file-dialogs` (`HackComponentContracts.FileDialogs`) | dialogues de fichiers (chantier 3) | — | Explorateur de fichiers |
| `hackeros.context-menu` (`HackComponentContracts.ContextMenu`) | `IHackContextMenu`, composant `HackContextMenu` ([`context-menu.md`](context-menu.md)) | — | Plateforme (`PlatformContextMenu`, qui inscrit le menu ; `ContextMenuOverlay` le dessine, ADR 0154) |

### `IHackTerminal`

| Membre | Rôle |
| --- | --- |
| `RunCommandAsync(ligne, jeton)` | Affiche la ligne à l'invite, l'exécute après les touches déjà tapées, renvoie `HackTerminalCommandResult(CommandLine, ExitStatus, Output)`. Le jeton interrompt la commande (statut 130). |
| `SetWorkingDirectoryAsync(chemin)` | `cd` visible dans le terminal. |
| `SendInputAsync(texte)` | Octets du clavier (`\r` = Entrée, `\u0003` = Ctrl+C). |
| `ReadOutput(max)` | Texte affiché récemment, sans séquences de contrôle (256 Kio gardés). |
| `ClearAsync()`, `FocusAsync()` | Effacer l'écran, prendre le focus. |
| `WorkingDirectory`, `Title`, `IsRunning`, `IsBusy` | État. |
| `OutputReceived`, `CommandCompleted`, `StateChanged`, `Exited` | Événements sur le thread de rendu. |

Le shell est un enfant du processus de l'hôte : l'hôte doit demander
`shell.host`.

## Architecture

| Élément | Projet | Rôle |
| --- | --- | --- |
| `AppComponent`, `AppComponentManifest`, `AppManifest.Components` | `HackerOs.App.Abstractions` | Déclaration et manifeste (`components`, omis si vide). |
| `HackComponentContracts`, `IHackTerminal`, `HackTerminalOptions`, `HackTerminalCommandResult` | `HackerOs.AppSdk` (`.Components`) | Contrats. |
| `AppComponentContracts`, `AppComponentProviderScope`, `AppManifestValidator.ValidateComponentProviders` | `HackerOs.App.Abstractions` | Contrats réservés et qui peut les fournir (ADR 0148 §3.3). |
| `IHackComponentHost`, `HackComponentResolution`, `HackEmbeddableComponent`, `HackEmbeddedComponent<T>`, `HackContractComponent<T>` | `HackerOs.AppSdk.Blazor` (`.Components`) | Côté Blazor du SDK. |
| `AppCatalog.ComponentProviders`, `AppComponentProvider` | `HackerOs.Platform.Core` | Registre des fournisseurs lu au démarrage. |
| `HackComponentHost`, `HackPlatformComponent` | `HackerOs.Platform.Blazor` (`.Components`) | Fournisseurs de la plateforme d'abord, puis le premier fournisseur installé qui se charge (`AppLifecycleOrchestrator.EnsureDescriptorAsync`), vérifie le type, écrit au journal un contrat réservé indisponible. |
| `BuiltInApplicationsAggregator.FindReservedComponentContracts` | `HackerOs.Tools.ManifestGenerator` | `HOSAPP0025` au build de l'hôte. |
| `TerminalEmbed` | `HackerOs.Apps.Terminal` | Fournisseur de `hackeros.terminal`. |

## Tâches

- [x] `Components` dans `AppDefinition`, le mappeur, `AppManifest`, le schéma,
  le sérialiseur, le canonique de test et le validateur.
- [x] Contrats `IHackTerminal` et `HackTerminalOptions`.
- [x] `HackEmbeddableComponent`, `HackEmbeddedComponent<T>`,
  `IHackComponentHost`.
- [x] `HackComponentHost` et `EnsureDescriptorAsync` (chargement paresseux).
- [x] `TerminalPanel` implémente `IHackTerminal` (file d'entrée, transcription,
  résultat des commandes) ; `TerminalEmbed` enregistré par l'app Terminal.
- [x] L'éditeur embarque le terminal par contrat dans son dock.
- [x] Tests : manifeste, validateur, mappeur, registre, nettoyage des
  séquences.
- [x] Chantier 1 du plan du bureau (ADR 0148, ADR 0153) : composant typé
  `HackContractComponent<T>` et `Parameters`, `UnmatchedParameters`, registre
  dans `AppCatalog`, contrats réservés (`HOSAPP0025`,
  `catalog.component.reserved`, `manifest.component.contract.platform`),
  `HackPlatformComponent`, `component.unavailable` au journal ; tests du
  validateur, du catalogue, de l'agrégation, du registre et du composant typé.
