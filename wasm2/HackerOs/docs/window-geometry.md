# Géométrie des fenêtres enregistrée et restaurée

## Objet

Une fenêtre d'app rouvre là où l'utilisateur l'a laissée : même taille, même
position, et agrandie si elle l'était. Mis en œuvre par le chantier
`v3.1/2-window-geometry` selon l'[ADR 0116](adr/0116-window-geometry-restore.md)
(série [`v3-1-series-plan.md`](v3-1-series-plan.md)), qui met en œuvre le §10
de l'[ADR 0044](adr/0044-settings-as-files.md).

Comme sous KWin ou Mutter, c'est le gestionnaire de fenêtres qui retient la
place, pas l'app : HackerOS tient la géométrie en C# (ADR 0009), il l'écrit donc
lui-même.

## Fichier d'état

Un fichier par app et par utilisateur, propre à l'appareil, jamais synchronisé :
`~/.local/state/hackeros/windows/<appId>.config`.

```ini
# Window geometry of org.hackeros.text-editor (ADR 0116)
x=312
y=96
width=900
height=640
maximized=false
```

- `x`, `y`, `width`, `height` : géométrie **normale** en pixels CSS, culture
  invariante. Une fenêtre agrandie y garde sa géométrie de restauration.
- `maximized` : facultatif, `false` s'il manque. Seul `true` agrandit : une
  faute de frappe laisse la fenêtre normale.
- Un fichier sans les quatre nombres est ignoré : la fenêtre s'ouvre à sa place
  de cascade.
- Oublier la place d'une app : `rm ~/.local/state/hackeros/windows/<appId>.config`.

## Architecture

| Classe (Platform.Blazor, `Windows/`) | Rôle |
|---|---|
| `WindowGeometrySettings` | Chemin du fichier, `Parse` et `Serialize`. |
| `SavedWindowGeometry` | Bornes normales et état agrandi lus ou à écrire. |
| `WindowGeometryPersistence` | Lit et écrit le fichier par `IOsSettingsService`, sous l'identité de l'utilisateur ; n'écrit que si le contenu change ; un échec (pas de session, dossier en lecture seule) est ignoré. |
| `WindowGeometryRecorder` | Écoute `WindowRuntime.EventsApplied`, garde en mémoire la dernière géométrie normale de chaque fenêtre suivie et l'écrit après 500 ms sans changement, ou tout de suite à la fermeture. |
| `WindowLaunchCoordinator` | `PresentAsync` lit le fichier avant d'ouvrir la fenêtre, corrige la géométrie (`RestoredBounds`), agrandit si besoin, puis confie la fenêtre au recorder. |

Les trois services sont `Scoped`, comme `WindowRuntime`
(`EcosystemServiceCollectionExtensions`).

### Écriture

- Déclencheurs : tout lot de faits de `WindowRuntime` qui change la géométrie
  normale ou l'état agrandi d'une fenêtre suivie (déplacement, fin de
  redimensionnement, agrandir, restaurer, changement de zone de travail). Le
  recorder relit l'état du runtime après chaque lot plutôt que chaque fait, pour
  enregistrer les bornes réellement appliquées.
- Regroupement : un glisser produit un déplacement par mouvement du pointeur ;
  les changements d'une même app sont écrits une fois, 500 ms après le dernier.
- Fermeture : écriture immédiate de la dernière géométrie connue.
- Les écritures d'une même app passent l'une après l'autre ; ouvrir une fenêtre
  lance tout de suite l'écriture qui attendait encore et attend la fin des
  écritures de l'app avant de lire le fichier. Une fenêtre rouverte juste après
  sa fermeture, ou une deuxième fenêtre ouverte pendant le regroupement, voit
  donc la dernière place.
- Jamais enregistré : l'état réduit (la fenêtre garde ce qu'elle avait avant) et
  la moitié d'écran d'une fenêtre ancrée (elle garde sa géométrie d'avant
  l'ancrage et rouvre comme une fenêtre normale).

### Lecture (ADR 0116 §4)

1. Pas de fichier, fichier invalide ou `RememberGeometry = false` : place de
   cascade, taille déclarée (`InitialBounds`, comportement d'avant).
2. Taille ramenée entre le minimum et le maximum que l'app déclare **aujourd'hui**
   (une fenêtre de taille fixe prend sa taille déclarée), puis à la zone de
   travail sans descendre sous le minimum.
3. Position corrigée : au moins 48 px de la barre de titre visibles
   horizontalement, le haut de la fenêtre dans la zone de travail.
   `WindowRuntime` applique ensuite sa propre contrainte et garde une fenêtre
   normale entière dans la zone de travail.
4. Deuxième fenêtre de la même app : décalée de 28 px par fenêtre déjà ouverte.
5. `maximized=true` : la fenêtre s'ouvre agrandie, la géométrie lue devient sa
   géométrie de restauration. Une fenêtre de taille fixe ne rouvre jamais agrandie.

## Usage pour une app

Rien à faire : toute app de genre `Window` retient sa géométrie. Pour refuser
(une calculatrice, un assistant qui doit toujours s'ouvrir au même endroit) :

```csharp
public override WindowOptions? Window => new()
{
    DefaultWidth = 320, DefaultHeight = 480,
    RememberGeometry = false,
};
```

`WindowOptions.RememberGeometry` est un ajout du SDK 1.1
(`PublicAPI.Unshipped.txt` de `HackerOs.App.Abstractions`). Le générateur écrit
`window.rememberGeometry` dans `manifest.json` seulement quand il vaut `false` :
les manifestes des apps qui gardent le défaut restent identiques octet pour
octet. Le schéma v1 l'accepte comme champ facultatif, le désérialiseur strict
aussi.

## Ce qui n'est pas couvert

- Interface mobile : plein écran, elle ignore ce fichier.
- Fenêtres détachées (ADR 0078, 0081) : placées par le système hôte.
- Fenêtres de l'OS qui ne sont pas des apps (boîtes de dialogue de fichiers,
  agent d'authentification, « Ouvrir avec ») : jamais suivies.
- Pas de bouton « oublier la position » dans Paramètres.

## Décisions de mise en œuvre

- **`PresentAsync` remplace `Present`** : lire le fichier est asynchrone. Le
  shell présente parfois deux fois le même processus (son propre lancement et le
  passage du processus à `Running`) ; pendant la lecture, le second appel rend la
  main sans ouvrir de deuxième fenêtre, et l'existence de la fenêtre est
  revérifiée après la lecture.
- **Ancrage reconnu à sa forme** : `WindowRuntime` ne garde pas d'état « ancré »
  (window-model.md). Une fenêtre normale qui couvre exactement la moitié gauche ou
  droite de la zone de travail est traitée comme ancrée et n'est pas enregistrée.
- **`SaveAsync` sans `isEligible`** : c'est le coordinateur qui décide quelles
  fenêtres suivre (`Track`) ; la ligne retirée est marquée `*REMOVED*` dans
  `PublicAPI.Unshipped.txt` de Platform.Blazor (hors promesse du SDK, ADR 0114 §2).
- **Échecs journalisés** : `WindowGeometryPersistence` écrit un avertissement
  quand le fichier ne peut être lu ou écrit, puis continue.
- **Contenu comparé avant écriture** : fermer une fenêtre qui n'a pas bougé ne
  réécrit pas le fichier et ne réveille pas les observateurs.
- **En-tête commenté** dans le fichier, comme les autres documents de l'OS.

## Tests

- `WindowGeometryPersistenceTests` (Platform.Blazor) : chemin, aller-retour,
  état agrandi, `maximized` facultatif, fichier invalide, pas de réécriture,
  pas de session.
- `WindowGeometryRestoreTests` (Platform.Blazor) : réouverture au même endroit,
  nouvelle session, agrandie, réduite ou ancrée, regroupement d'un glisser,
  écriture en attente vidée à l'ouverture, `RememberGeometry = false`, fichier
  invalide, deuxième instance, double
  présentation, écran plus petit, position hors écran, tailles déclarées
  changées, fenêtre fixe, fenêtre de l'OS non suivie.
- `AppManifestJsonSerializerTests`, `ManifestSchemaConformanceTests`
  (App.Abstractions) : `rememberGeometry` écrit seulement à `false`, relu, accepté
  par le schéma.
- E2E `WindowGeometryRestoreTests` (UI.E2E) : glisser la barre de titre et le
  coin d'une fenêtre, la fermer, la rouvrir au même endroit, recharger la page,
  se reconnecter et la retrouver au même endroit.

## Liste des tâches

- [x] `WindowOptions.RememberGeometry`, projection JSON, schéma, désérialiseur strict, `PublicAPI.Unshipped.txt`.
- [x] Clé `maximized` et `SavedWindowGeometry` ; `SaveAsync` sans `isEligible`, écrit seulement si le contenu change.
- [x] `WindowGeometryRecorder` : suivi, regroupement de 500 ms, écriture à la fermeture, réduction et ancrage ignorés.
- [x] `WindowLaunchCoordinator.PresentAsync` : lecture, correction, deuxième instance, agrandie, garde contre la double présentation.
- [x] `DesktopShell` et injection de dépendances.
- [x] Tests unitaires et E2E.
- [x] `window-model.md`, `settings-files.md`, `sdk-developer-guide.md`, `sdk-reference.md`, `app-sdk.md`, bilan v3, plan v3.1, index.
