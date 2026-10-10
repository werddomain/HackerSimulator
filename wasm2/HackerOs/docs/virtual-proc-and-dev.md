# `/proc` et `/dev` virtuels

Mise en œuvre de la section 5.4 du [plan de refonte](ecosystem-rebuild-plan.md)
(décision D20) selon l'[ADR 0046](adr/0046-virtual-proc-and-dev-filesystems.md).

## Rôle

HackerOS expose l'état du système comme Linux : `/proc` décrit les processus,
la mémoire, le CPU et la charge ; `/dev` offre les périphériques `null`, `zero`,
`random` et `urandom`. Les deux montages sont **calculés à chaque lecture** et
**jamais écrits dans IndexedDB**.

## Architecture

Code : `Platform/HackerOs.Platform.Core/FileSystem/Virtual/`.

| Type | Rôle |
| --- | --- |
| `IVirtualFileSystemProvider` | Marqueur d'un fournisseur calculé, jamais persisté. `FileSystemService` n'émet aucun `FileSystemChangeEvent` pour ses chemins. |
| `VirtualFileSystemProvider` | Base commune : identité stable (SHA-256 de `"<providerId>:<chemin>"`), révision = `CurrentTick + 1`, refus `PermissionDenied` de toute création, suppression, déplacement, copie et `chmod`. |
| `ProcFileSystemProvider` (`procfs`) | Monté sur `/proc`. Lit `IProcessManager`, `IResourceSimulator`, `VirtualHardwareProfile` et `ISimulationClock`. |
| `DevFileSystemProvider` (`devfs`) | Monté sur `/dev`. Quatre entrées `CharacterDevice` en `0666`. |

Branchement : `EcosystemServiceCollectionExtensions` passe les deux montages au
`FileSystemMountRouter`, pour les deux hôtes WASM. Le gestionnaire de processus
est résolu **à la demande** : il dépend du service de session, qui dépend du
`FileSystemSeeder`, qui atteint `/proc` par le routeur. Une résolution immédiate
fermerait un cycle.

Points de montage : le routeur ne fusionne pas les montages dans le listage du
parent. `FileSystemSeeder` reçoit donc le routeur et crée `/proc` (`0555`) et
`/dev` (`0755`), vides, **directement dans le fournisseur racine**. `ls /` les
montre, puis le fournisseur virtuel les masque.

Contrats touchés :

- `FileSystemEntryKind.CharacterDevice` et `CharacterDeviceMetadata`
  (`Shared/HackerOs.Simulation.Abstractions/FileSystem/FileSystemEntryMetadata.cs`).
  Les fournisseurs IndexedDB et mémoire refusent de créer ce type
  (`UnsupportedOperation`). `ls -l` l'affiche avec `c` (et les liens avec `l`).
- `AppOperationContext.ProcessId` (`long?`), renseigné par
  `AppExecutionContextFactory`. Il sert à `/proc/self` et à la visibilité.

## Contenu

### `/proc`

| Chemin | Type et mode | Contenu |
| --- | --- | --- |
| `/proc/<pid>/` | dossier `0555`, propriétaire = utilisateur du processus | — |
| `/proc/<pid>/status` | fichier `0444` | voir ci-dessous |
| `/proc/<pid>/cmdline` | fichier `0444` | `appId\0` (les arguments arrivent en phase 3) |
| `/proc/<pid>/environ` | fichier `0400` | vide en phase 1 |
| `/proc/<pid>/cwd` | lien `0777` | `/home/<login>` en phase 1 |
| `/proc/self` | lien `0777` | `/proc/<pid de l'appelant>`, absent sans processus |
| `/proc/meminfo` | fichier `0444` | `MemTotal`, `MemFree`, `MemAvailable` en kB |
| `/proc/cpuinfo` | fichier `0444` | un bloc par cœur (`ceil(CpuCapacity)`) |
| `/proc/uptime` | fichier `0444` | `<secondes depuis le démarrage> <secondes d'inactivité>` |
| `/proc/loadavg` | fichier `0444` | `<1 min> <5 min> <15 min> <actifs>/<total> <dernier pid>` |

Exemple de `/proc/42/status` :

```text
Name:	code-editor-del
State:	R (running)
Pid:	42
PPid:	7
Uid:	aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
AppId:	org.hackeros.apps.code-editor-deluxe
Kind:	window
Session:	bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
VmRSS:	3072 kB
Cpu:	12.5
```

`Name` est le dernier segment de l'`appId`, tronqué à 15 caractères. `Cpu` est
un pourcentage d'un cœur (`CpuUsage × 100`), sans le signe `%`, pour rester
facile à analyser.

Choix de calcul :

- `MemAvailable` vaut `MemFree` : la simulation n'a pas de cache à libérer.
- L'inactivité d'`uptime` est cumulée sur tous les cœurs, comme sous Linux :
  `uptime × (cœurs − usage moyen)`, l'usage moyen venant de l'historique système
  borné.
- `loadavg` fait la moyenne de `TotalCpuUsage` sur les échantillons de
  l'historique compris dans chaque fenêtre. L'historique étant borné, les
  fenêtres de 5 et 15 minutes se réduisent aux échantillons disponibles.
- Horodatages : démarrage du processus pour `/proc/<pid>`, démarrage de l'OS
  (`maintenant − tick × durée du tick`) sinon ; le contenu des fichiers est daté
  de maintenant. La taille des fichiers est toujours `0`.

Visibilité : seuls les processus actifs apparaissent. L'appelant voit toujours
son propre processus, et les autres avec `process.list` ou l'autorité `System`.
Un PID invisible répond `NotFound`.

### `/dev`

| Chemin | Lecture | Écriture |
| --- | --- | --- |
| `/dev/null` | fin de flux immédiate | absorbée |
| `/dev/zero` | flux infini de `0x00` | absorbée |
| `/dev/random`, `/dev/urandom` | flux infini de `RandomNumberGenerator` | absorbée |

Les flux infinis rendent au plus 4 Kio par lecture et respectent l'annulation :
un lecteur borne lui-même sa lecture. En lecture asynchrone, ils cèdent la main
au navigateur tous les 16 blocs, pour qu'un `cat /dev/zero` laisse l'interface
répondre et puisse être interrompu. Une écriture vide le flux reçu, réussit
sans changer de révision et n'émet aucun événement.

## Utilisation

Tout passe par l'API fichier ordinaire (`IFileSystemService`, puis
`IAppFileSystemGateway` côté app). Lire `/proc` ou `/dev` demande
`filesystem.system.read` ; écrire dans `/dev` demande `filesystem.system.write`.

`ps` lit maintenant `/proc` : il liste `/proc`, lit chaque `/proc/<pid>/status`
et prend l'heure de création de `/proc/<pid>` comme heure de démarrage. Son
manifeste déclare `filesystem.system.read` en plus de `process.list`. La colonne
`STAT` affiche la lettre Linux (`R`, `S`…). `kill` garde `IAppProcessGateway`
pour agir.

## Décisions clés

- **Migration de `ps` avancée.** L'ADR 0046 (section 5) plaçait cette migration
  en phase 3. Elle est faite dès la phase 1, comme le demande la section 5.4 du
  plan, parce qu'elle ne dépend que de `/proc`. La refonte des commandes de la
  phase 3 la conserve. Le gestionnaire de tâches reste en phase 4.
- **Pas d'événements de changement** : le marqueur `IVirtualFileSystemProvider`
  est vérifié au seul endroit qui publie, `FileSystemService`.
- **Mutations refusées par le fournisseur lui-même**, en plus des bits de mode :
  même l'autorité `System`, qui ignore les modes, reçoit `PermissionDenied`.

## Tests

- `Tests/HackerOs.Platform.Core.Tests/FileSystem/Virtual/` : listage de `/`,
  formats exacts de `status`, `meminfo`, `cpuinfo`, `uptime` et `loadavg`,
  visibilité selon `process.list`, `/proc/self`, refus des mutations, `/dev/null`,
  lecture bornée de `/dev/zero`, annulation, refus de `CharacterDevice` par le
  stockage, et aucune écriture dans le dépôt racine.
- `Tests/HackerOs.Commands.Wave5.Tests/PsCommandTests.cs` : `ps` contre le vrai
  fournisseur `/proc`.

## Liste des tâches

- [x] Ajouter `FileSystemEntryKind.CharacterDevice` et `CharacterDeviceMetadata`.
- [x] Faire refuser `CharacterDevice` par les fournisseurs IndexedDB et mémoire.
- [x] Ajouter `AppOperationContext.ProcessId` et le renseigner.
- [x] Écrire la base `VirtualFileSystemProvider` et le marqueur `IVirtualFileSystemProvider`.
- [x] Écrire `ProcFileSystemProvider`.
- [x] Écrire `DevFileSystemProvider`.
- [x] Couper les événements de changement pour les montages virtuels.
- [x] Créer les points de montage `/proc` et `/dev` dans `FileSystemSeeder`.
- [x] Monter les deux fournisseurs dans `EcosystemServiceCollectionExtensions`.
- [x] Afficher `c` et `l` dans `ls -l`.
- [x] Faire lire `/proc` par `ps`.
- [x] Écrire les tests de contrat.
