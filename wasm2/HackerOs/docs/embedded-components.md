# Composants intégrables

Une app peut afficher dans sa fenêtre un composant qu'une autre app a
enregistré auprès de l'OS, sans référencer son assemblage, comme Kate affiche
la KPart de Konsole. Décision : [ADR 0063](adr/0063-embeddable-app-components.md).

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

## Contrats

| Contrat | Contrôleur | Options | Fournisseur |
| --- | --- | --- | --- |
| `hackeros.terminal` (`HackComponentContracts.Terminal`) | `IHackTerminal` | `HackTerminalOptions` (`WorkingDirectory`, `ShowBanner`, `FocusOnStart`) | Terminal (`TerminalEmbed`) |

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
| `IHackComponentHost`, `HackComponentResolution`, `HackEmbeddableComponent`, `HackEmbeddedComponent<T>` | `HackerOs.AppSdk.Blazor` (`.Components`) | Côté Blazor du SDK. |
| `HackComponentHost` | `HackerOs.Platform.Blazor` (`.Components`) | Trouve le fournisseur installé (apps système d'abord), charge son assemblage par `AppLifecycleOrchestrator.EnsureDescriptorAsync`, vérifie le type. |
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
