# Modèle de fenêtre : événements, tailles déclarées et clavier

Chantier `v3/3-window-model` du [plan de fin de migration wasm v3](wasm-v3-completion-plan.md),
mise en œuvre de l'[ADR 0095](adr/0095-window-events-sizes-and-keyboard.md).

## Objet

- Une app fenêtrée apprend ce qui arrive à sa fenêtre : elle gagne ou perd le focus, elle est
  réduite, agrandie ou restaurée, elle change de taille.
- Une app fenêtrée déclare la taille de sa fenêtre (initiale, minimale, maximale, redimensionnable)
  dans sa classe `AppDefinition` au lieu du 800×560 / 360×240 codé en dur.
- Le gestionnaire de fenêtres se pilote au clavier comme un bureau Linux : changer de fenêtre,
  fermer, menu de fenêtre, déplacer et redimensionner au clavier, réduire, agrandir, ancrer à
  gauche ou à droite, plein écran.

## 1. Événements de fenêtre (ADR 0095 §1)

### Pour l'app

`WindowAppBase` (SDK Blazor) expose :

| Membre | Rôle |
| --- | --- |
| `OnWindowActivated[Async]()` | La fenêtre reçoit le focus. |
| `OnWindowDeactivated[Async]()` | La fenêtre perd le focus (autre fenêtre, réduction, plus aucune fenêtre au premier plan). |
| `OnWindowStateChanged[Async](previous, current)` | `HackWindowState` : `Normal`, `Minimized`, `Maximized`. |
| `OnWindowResized[Async](HackWindowSize size)` | Taille de la zone client (sans la barre de titre), en pixels CSS, **une seule fois à la fin** d'un redimensionnement (geste à la souris, clavier, agrandissement, restauration, changement de la zone de travail). |
| `IsWindowActive`, `WindowState`, `WindowSize` | Dernier état connu, à lire dans le rendu. |
| `TrackDisplaySurface(IDisplaySurface)` | Fait suivre une fenêtre détachée (ADR 0078/0081) : son focus, sa perte de focus et sa taille déclenchent les mêmes crochets. `HackWindowSize.DisplaySurfaceId` indique alors la surface. Le résultat se libère quand la surface est rattachée. |

Ordre garanti :

- les crochets d'une fenêtre passent dans une file à elle, un à la fois, dans l'ordre des faits ;
- un changement d'état précède le redimensionnement qu'il provoque (agrandir donne
  `OnWindowStateChanged(Normal, Maximized)` puis `OnWindowResized`) ;
- quand l'app est rendue dans une fenêtre déjà agrandie ou déjà au premier plan, elle reçoit
  `OnWindowStateChanged(Normal, état actuel)` puis `OnWindowActivated` ;
- la version synchrone est appelée avant la version `Async`, sur le répartiteur du composant, dans
  le contexte ambiant de l'app (les façades `Hack*` marchent) ; un nouveau rendu suit.

Un crochet qui lève une exception est écrit dans le journal de l'app
(`/var/log/apps/<appId>.log`, niveau `Error`, préfixe `window:`) ; la transition de la fenêtre a
déjà eu lieu et continue.

```razor
@inherits WindowAppBase

<p class="@(IsWindowActive ? "active" : "idle")">@WindowState</p>

@code {
    protected override Task OnWindowDeactivatedAsync() => PauseAnimationAsync();
    protected override void OnWindowResized(HackWindowSize size) => _columns = (int)(size.Width / 120);
}
```

### Architecture

- `WindowRuntime` (Windowing.Core) produit déjà des faits (`WindowFocusedEvent`,
  `WindowVisualStateChangedEvent`, `WindowViewportChangedEvent`, `WindowClosedEvent`). Il publie
  maintenant chaque lot par l'événement `EventsApplied`, avant `StateChanged`.
- Nouveau fait : `CompleteWindowResizeCommand` → `WindowResizeCompletedEvent(id, bounds)`. Le
  chrome (`WindowChrome.razor.js`) l'envoie quand un geste de redimensionnement se termine ; le
  clavier l'envoie quand on valide un redimensionnement.
- `WindowAppEventDispatcher` (Platform.Blazor, un par session) traduit ces faits en appels de
  `IWindowAppEventTarget` (interface du SDK, implémentée par `WindowAppBase`). `WindowAppRenderer`
  y inscrit le composant de l'app. La taille est mesurée dans le navigateur
  (`IWindowClientAreaProbe`, `windowClientArea.js`, deux images après le rendu) ; à défaut, elle
  est estimée depuis la taille de la fenêtre moins le chrome.
- `DesktopShell` et `MobileShell` passent le répartiteur à `WindowAppRenderer`. Sur mobile, les
  fenêtres sont plein écran : les crochets d'activation et d'état restent valables.

## 2. Tailles déclarées (ADR 0095 §2)

```csharp
public override WindowOptions? Window => new()
{
    DefaultWidth = 720, DefaultHeight = 520,
    MinWidth = 480, MinHeight = 360,
    MaxWidth = null, MaxHeight = null,   // pas de limite
    Resizable = true,
};
```

- `WindowOptions` (App.Abstractions) : tailles extérieures (barre de titre comprise) en pixels
  CSS. Sans déclaration : `WindowOptions.Default`, 800×560 et minimum 360×240, comme avant.
- Manifeste : objet facultatif `window` (`defaultWidth`, `defaultHeight`, `minWidth`, `minHeight`,
  `maxWidth`, `maxHeight`, `resizable`), produit par le générateur et absent si l'app ne déclare
  rien. Un champ omis prend sa valeur par défaut. Le schéma `manifest.schema.v1.json` interdit
  `window` hors des apps de genre `window`.
- Codes du validateur :

| Code | Règle |
| --- | --- |
| `manifest.window.forbidden` | `window` sur une app qui n'est pas de genre `window`. |
| `manifest.window.size.invalid` | Une taille nulle ou négative. |
| `manifest.window.minimum.exceedsDefault` | Minimum plus grand que la taille initiale. |
| `manifest.window.default.exceedsMaximum` | Taille initiale plus grande que le maximum. |

- Ouverture (`WindowLaunchCoordinator.InitialBounds`) : taille initiale réduite à la zone de
  travail, mais jamais sous le minimum ; une fenêtre dont le minimum dépasse l'écran déborde,
  comme sur un vrai petit écran (`WindowRuntime.Constrain` garde le minimum).
- Agrandir ignore le maximum. `Resizable = false` : pas de poignées, pas de bouton Agrandir, et
  les commandes clavier redimensionner, agrandir et ancrer ne font rien.
- `WindowGeometryPersistence` (`~/.local/state/hackeros/windows/<appId>.config`) est enregistré
  mais aucun shell ne relit encore la géométrie à l'ouverture : ce chantier n'y touche pas.
- Le shell mobile ignore ces tailles.

## 3. Commandes clavier (ADR 0095 §3)

### Fichier `keybindings.config`

Lu par `WindowKeyboardCommands` avec les couches habituelles
([`settings-files.md`](settings-files.md)) : `/etc/hackeros/keybindings.config` (écrit au
premier démarrage, tout en commentaires), puis `~/.config/hackeros/keybindings.config`. Une
modification du fichier s'applique tout de suite. Plusieurs combinaisons séparées par des virgules ;
une valeur vide retire la touche.

| Action (clé) | `[linux]` (sous verrouillage du clavier) | `[fallback]` (toujours) |
| --- | --- | --- |
| `switch-next` | `Alt+Tab` | `` Alt+` `` (`Alt+Backquote`) |
| `switch-previous` | `Alt+Shift+Tab` | `Alt+Shift+Backquote` |
| `close` | `Alt+F4` | `Alt+Q` |
| `window-menu` | `Alt+Space` | `Alt+F3` |
| `move` | `Alt+F7` | `Alt+F7` |
| `resize` | `Alt+F8` | `Alt+F8` |
| `minimize` | | `Alt+F9` |
| `maximize` | `Super+Up` | |
| `restore-or-minimize` | `Super+Down` | |
| `toggle-maximize` | | `Alt+F10` |
| `snap-left` / `snap-right` | `Super+Left` / `Super+Right` | `Alt+Shift+Left` / `Alt+Shift+Right` |
| `toggle-fullscreen` | | `Alt+F11` |

Les touches Linux ne sont actives que si le clavier est verrouillé (`navigator.keyboard.lock()`,
appelé en plein écran) : sans cela, le système hôte les intercepte. Les touches de remplacement
marchent dans un onglet ordinaire. Une combinaison du gestionnaire de fenêtres passe avant l'app
qui a le focus. Une lettre est reconnue par sa touche physique quand Alt change le caractère
(Option+Q sur macOS).

### Comportement

- **Changer de fenêtre** : tant que Alt est tenu, un sélecteur liste les fenêtres (non réduites
  d'abord, de la plus récente à la plus ancienne) et la sélection avance à chaque appui ; relâcher
  Alt passe à la fenêtre choisie (restaurée si elle était réduite). Échap annule.
- **Déplacer / redimensionner** : un bandeau indique le mode ; les flèches déplacent ou
  redimensionnent de 10 px (1 px avec Ctrl), Entrée garde, Échap remet la fenêtre où elle était.
- **Ancrer** : moitié gauche ou droite de la zone de travail.
- **Menu de fenêtre** : `Alt+Space`/`Alt+F3`, bouton de menu dans la barre de titre, ou clic
  droit sur la barre de titre. Il contient toutes les actions avec leur raccourci actif :
  Agrandir/Restaurer, Réduire, Déplacer, Redimensionner, Ancrer à gauche/droite, Plein écran,
  Fermer. Flèches, Entrée et Échap y fonctionnent.
- **Fermer** suit le chemin du bouton Fermer (`IWindowCloseGuard` compris).
- **Focus clavier** : la fenêtre qui devient la fenêtre active (sélecteur, fermeture de celle du
  dessus, barre des tâches) reçoit aussi le focus clavier : le dernier contrôle qui l'avait dans
  cette fenêtre, sinon son premier élément focalisable, sinon la fenêtre elle-même. Rien ne bouge
  si le focus y est déjà (clic, app qui se focalise seule). `WindowHost` le fait au rendu
  (`WindowHost.razor.js`, chantier `v3/13`).

### Architecture

- `WindowKeyBindingsSettingsDocuments`, `KeyChord`, `WindowKeyboardAction` (Platform.Core) :
  schéma du document, analyse des combinaisons, contenu par défaut.
- `WindowKeyboardCommands` (Platform.Blazor, un par session) : toutes les décisions (modes,
  sélecteur, menu, déplacements) en C#, testées sans navigateur.
- `WindowManagerOverlays` (`.razor`, `.razor.cs`, `.razor.css`, `.razor.js`) : dessine le
  sélecteur, le bandeau et le menu. Son JS écoute `keydown` en phase de capture, reconnaît les
  combinaisons envoyées par le C#, appelle le C# et bloque la touche ; il gère le plein écran et
  le verrouillage du clavier.

## Décisions de mise en œuvre

- **Plein écran ajouté** (`toggle-fullscreen`, `Alt+F11`, entrée du menu) : le verrouillage du
  clavier n'existe qu'en plein écran, il fallait un moyen d'y entrer.
- **`HackWindowSize.DisplaySurfaceId`** : distingue le redimensionnement d'une fenêtre détachée de
  celui de la fenêtre du bureau.
- **Taille mesurée** dans le navigateur, estimée seulement en repli (tests, rendu serveur).
- **`/etc/hackeros/keybindings.config` livré en commentaires** : les valeurs actives viennent du
  code tant que personne n'écrit le fichier, comme les autres documents de l'OS.
- **Ancrer ne garde pas de géométrie de restauration** : la fenêtre ancrée est une fenêtre normale.
- **Touches dans une iframe** (navigateur, sites) : elles restent dans l'iframe, la page ne les voit
  pas ; il faut d'abord rendre le focus au bureau.
- **Correction** : réduire une fenêtre en arrière-plan levait une exception dans `WindowRuntime`
  (il tentait de lui donner le focus). Elle est maintenant réduite sans toucher au focus.

## Tests

- `WindowAppBaseWindowEventsTests` (SDK Blazor) : ordre des crochets, erreur journalisée,
  surfaces détachées.
- `WindowAppEventDispatcherTests`, `WindowKeyboardCommandsTests`, `WindowLaunchCoordinatorTests`
  (Platform.Blazor) : traduction des faits, une seule fin de redimensionnement, sélecteur, menu,
  modes clavier, tailles initiales.
- `WindowKeyBindingsTests` (Platform.Core) : analyse des combinaisons et du fichier en couches.
- `WindowRuntimeTests` (Windowing.Core) : fin de redimensionnement, minimum qui déborde, réduction
  en arrière-plan.
- Validateur, sérialiseur, mappeur et schéma (App.Abstractions).
- E2E `WindowKeyboardCommandsTests` (UI.E2E) : `` Alt+` `` change de fenêtre et `Alt+Q` ferme la
  fenêtre active dans un onglet Chromium.

## Liste des tâches

- [x] Crochets de fenêtre dans `WindowAppBase` et contrats du SDK (`HackWindowState`, `HackWindowSize`).
- [x] Faits du runtime : lot publié, fin de redimensionnement, correction de la réduction en arrière-plan.
- [x] Répartiteur `WindowAppEventDispatcher` et mesure de la zone client.
- [x] Fenêtres détachées suivies (`TrackDisplaySurface`, navigateur).
- [x] `WindowOptions` dans `AppDefinition`, manifeste, schéma, validateur, sérialiseur strict.
- [x] Tailles appliquées à l'ouverture ; bouton Agrandir masqué si non redimensionnable.
- [x] `keybindings.config` (schéma, couches, défaut commenté, rechargement).
- [x] `WindowKeyboardCommands` : sélecteur, fermer, menu, déplacer, redimensionner, réduire, agrandir, ancrer, plein écran.
- [x] Menu de fenêtre (clavier, bouton, clic droit) et surcouches.
- [x] App d'exemple WindowApp : tailles déclarées et journal des événements.
- [x] Tests unitaires et E2E.
- [x] Documentation et index.
