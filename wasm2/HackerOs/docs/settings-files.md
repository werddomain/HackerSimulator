# Paramètres en fichiers

Mise en œuvre de la section 5.2 du [plan de refonte](ecosystem-rebuild-plan.md)
selon l'[ADR 0044](adr/0044-settings-as-files.md). Ce document décrit le code
livré ; l'ADR reste la référence des décisions.

## Objectif

Les paramètres des apps et de l'OS sont de vrais fichiers texte du système de
fichiers simulé. L'app Paramètres, une app tierce et `nano /etc/...` lisent et
modifient les mêmes fichiers, et seuls les droits du fichier décident qui peut
écrire. Le magasin IndexedDB `settings` n'est plus utilisé que par la
migration, en lecture seule.

## Emplacements

| Contenu | Couche système | Couche utilisateur |
| --- | --- | --- |
| Réglages d'une app | `/etc/<appId>/<document>.config` | `~/.config/<appId>/<document>.config` |
| Réglages de l'OS (apparence, interface, menu Démarrer) | `/etc/hackeros/<document>.config` | `~/.config/hackeros/<document>.config` |
| Politique (`policy.config`) | `/etc/hackeros/policy.config` | aucune (document `SystemOnly`) |
| Associations de fichiers | `/etc/hackeros/file-associations.json` | `~/.config/hackeros/file-associations.json` |
| Géométrie des fenêtres | aucune | `~/.local/state/hackeros/windows/<appId>.config` |

`~/.local/state/` contient l'état propre à l'appareil : la synchronisation du
système de fichiers l'ignore (`IFileSystemSyncService.IsDeviceLocal`).

Au premier démarrage, `/etc/hackeros/` reçoit des fichiers par défaut
commentés (mode `0644`, propriétaire `root`). Comme ces fichiers listent toutes
les clés, l'origine affichée d'une valeur de l'OS est « système » et non
« intégrée » tant que personne n'a rien changé.

## Architecture

| Couche | Type | Rôle |
| --- | --- | --- |
| Simulation.Abstractions | `ConfigDocumentLines`, `ConfigDocumentEditor` | Lecture ligne par ligne du format `.config` (ADR 0011) et écriture qui ne touche qu'une ligne, en gardant commentaires, lignes vides et clés inconnues. |
| Simulation.Abstractions | `LayeredConfigResolver`, `LayeredConfigSnapshot` | Fusion des couches, verrous, avertissements `settings: <chemin>:<ligne>: <message>`. |
| Simulation.Abstractions | `SettingsTextFile`, `SettingsFilesWatcher` | Lecture, écriture (création des dossiers manquants) et surveillance des deux fichiers d'un document par une passerelle de fichiers. |
| Simulation.Abstractions | `ILegacySettingsStore`, `LegacySettingsRecord` | Contrat de lecture de l'ancien magasin, pour la migration. |
| AppSdk | `HackSettings`, `HackSettingsDocument` | API typée des apps. |
| AppSdk.Blazor | `SettingOriginBadge` | Badge réutilisable « à vous », « défaut système », « intégré », « verrouillé par l'admin ». |
| Platform.Core | `IOsSettingsService`, `OsSettingsService` | Documents de l'OS (`OsSettingsDocument`) et fichiers entiers (`OsSettingsFile`), pour la session courante. |
| Platform.Core | `SettingsStoreToFilesMigration` | Migration unique depuis IndexedDB, puis fichiers par défaut. |
| Infrastructure.Browser | `IndexedDbLegacySettingsStore` | Lecture `getAll` du magasin `settings`, sans changer la version du schéma. |
| Platform.Blazor | `WindowGeometryPersistence` | Restaure et enregistre la géométrie des fenêtres. |

### Ordre des couches

Pour chaque clé déclarée :

1. clé verrouillée dans la section `[locked]` du fichier système : valeur
   système, sinon valeur par défaut ;
2. sinon valeur utilisateur valide ;
3. sinon valeur système valide ;
4. sinon valeur par défaut du schéma.

`[locked]` n'est lu que dans le fichier système ; dans un fichier utilisateur
la section est ignorée avec un avertissement. `locked` est donc un nom de
groupe réservé, refusé par le schéma et le validateur de manifeste.

### Analyse tolérante

Une ligne mal formée, une clé inconnue ou une valeur invalide est ignorée ; la
couche suivante s'applique. Chaque problème donne un avertissement envoyé à
`IAppLoggingGateway` (pour une app) ou à `ILogger` (pour l'OS), une seule fois
par révision de fichier. L'écriture dans `/var/log` relève du chantier des
journaux (ADR 0045).

### Droits

| Opération | Capacité de l'app | Droits du fichier |
| --- | --- | --- |
| Lire `~/.config/<appId>/` | `filesystem.private.read` | ceux de l'utilisateur |
| Écrire `~/.config/<appId>/` | `filesystem.private.write` | ceux de l'utilisateur |
| Lire `/etc/<appId>/` | `filesystem.private.read` | `0644` : lisible par tous |
| Écrire `/etc/...` | `filesystem.system.write` | autorité Administrateur, bits de mode, ticket d'approbation de l'ADR 0042 consommé à chaque appel |

Une autre app n'a aucun accès au dossier privé `~/.config/<appId>/` d'une app.

## Utilisation

### Dans une app

Le schéma vient de `AppManifest.Settings` (clés, types, valeurs par défaut,
groupes). L'app passe explicitement son contexte en phase 1 ; des surcharges
sans contexte s'ajouteront quand le contexte ambiant de l'ADR 0043 existera.

```csharp
await using HackSettingsDocument settings = await HackSettings.OpenAsync(AppContext);
EditorTheme theme = settings.Get<EditorTheme>("theme");      // enum : "high-contrast" <-> HighContrast
bool locked = settings.IsLocked("autosave");
SettingLayer source = settings.GetSource("theme");          // Default, System ou User
settings.Changed += (_, e) => Refresh(e.ChangedKeys);       // édition externe (nano, autre fenêtre)
await settings.SetAsync("Editor.tab-size", 2);              // couche utilisateur
await settings.ResetAsync("theme");                         // retire la valeur utilisateur

int size = await HackSettings.GetAsync<int>(AppContext, "Editor.tab-size"); // lecture ponctuelle
```

- Une clé non déclarée ou un type incompatible lève `ArgumentException`.
- Écrire une clé verrouillée lève `HackSettingLockedException` (avec le chemin
  du fichier système).
- Une erreur de fichier est traduite en `UnauthorizedAccessException`,
  `FileNotFoundException` ou `IOException`.
- `SetAsync(key, value, SettingLayer.System)` écrit `/etc/<appId>/` si l'app a
  la capacité et l'utilisateur les droits du fichier.

`IAppExecutionContext.Settings` (l'ancienne passerelle `IAppSettingsGateway`)
est retiré ; `IAppExecutionContext.HomeDirectory` donne le dossier personnel.

### Dans l'OS

`IOsSettingsService` lit un `OsSettingsDocument` (`ReadAsync` donne un
`LayeredConfigSnapshot`), écrit une ou plusieurs clés en une seule édition
(`SetAsync`, `SetManyAsync`), retire une clé (`ResetAsync`), lit et écrit des
fichiers entiers (`ReadFileAsync`, `ReadValidatedFileAsync`, `WriteFileAsync`)
et publie `DocumentChanged`. Une écriture système passe le `writer` de l'app
appelante, pour que ses droits s'appliquent. Les documents de l'OS sont
déclarés dans `AppearanceSettingsDocuments`, `UiPlatformPreferenceSettingsDocuments`,
`StartMenuSettingsDocuments`, `PolicySettingsDocuments` et
`FileAssociationSettingsDocuments`.

### App Paramètres

L'app propose deux cibles : « Moi seulement » (couche utilisateur) et « Tous
les utilisateurs (défaut système) ». Chaque réglage affiche son origine ; un
réglage verrouillé est désactivé pour la cible utilisateur. Écrire la couche
système demande l'approbation administrateur ; un refus affiche un message au
lieu d'une erreur. « Synchroniser » pousse et tire le système de fichiers puis
relit l'apparence.

## Migration

`SettingsStoreToFilesMigration` lit l'ancien magasin une seule fois :

- au démarrage, les documents de l'OS (`OsAdmin`) deviennent des fichiers de
  `/etc/hackeros/`. Un document invalide est remplacé par la valeur par
  défaut. Le marqueur `/var/lib/hackeros/migrations/settings-to-files` empêche
  une seconde copie, donc les modifications de l'administrateur survivent aux
  redémarrages ;
- à la connexion, seules les valeurs de l'utilisateur connecté sont copiées :
  réglages d'app vers `~/.config/<appId>/`, épingles du menu Démarrer vers
  `~/.config/hackeros/start-menu.json`, géométrie vers `~/.local/state/`. Les
  anciens dossiers `~/.config/apps/<appId>` sont déplacés vers
  `~/.config/<appId>`. Le marqueur
  `~/.local/state/hackeros/migrations/settings-to-files` évite d'écraser ce
  que l'utilisateur a changé depuis.

Une erreur de lecture du magasin est journalisée et n'empêche ni le démarrage
ni la connexion. Le schéma IndexedDB ne change pas.

## Décisions de mise en œuvre

- Une écriture de plusieurs clés (`SetManyAsync`) se fait en une seule édition
  du fichier, pour qu'un observateur ne voie jamais un état à moitié écrit.
- Une app peut créer `~/.config/<appId>/` sans droit de lecture sur
  `~/.config` : si l'inspection d'un dossier parent est refusée, on le suppose
  existant et c'est la création du dossier final qui décide.
- La synchronisation des paramètres (`ISettingsSyncService`) disparaît : les
  fichiers sont synchronisés avec le reste du système de fichiers (l'ADR 0044
  remplace l'ADR 0029).

## Tests de contrat

Dans `Tests/HackerOs.Platform.Core.Tests/Settings/` :

- `LayeredConfigResolverTests` : couches, verrous, analyse tolérante, texte
  des avertissements, groupe réservé ;
- `ConfigDocumentEditorTests` : écriture qui préserve le texte humain ;
- `HackSettingsTests` : API du SDK sur le vrai système de fichiers, propriétaire
  et mode, verrou, réinitialisation, avertissements, droits sur `/etc`,
  isolement entre apps, notification de changement ;
- `SettingsStoreToFilesMigrationTests` : fichiers par défaut, copie unique,
  copie par utilisateur, magasin illisible.

## Liste des tâches

- [x] Moteur de couches, verrous et analyse tolérante
- [x] API typée `HackSettings` dans le SDK
- [x] Service `IOsSettingsService` et documents de l'OS en fichiers
- [x] Géométrie des fenêtres dans `~/.local/state`, exclue de la synchronisation
- [x] Migration depuis le magasin IndexedDB
- [x] App Paramètres : cible de couche, origine et verrou par réglage
- [x] Retrait de `IAppSettingsGateway`, de la projection et de la synchronisation des paramètres
- [x] Tests de contrat
