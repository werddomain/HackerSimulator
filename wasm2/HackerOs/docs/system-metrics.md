# Mesures système : CPU, disque, réseau et espace disque

Mise en œuvre de l'[ADR 0057](adr/0057-proc-metrics-for-task-manager.md),
chantier `rebuild/4-proc-metrics` de la phase 4 (tableau des chantiers de
l'[ADR 0058](adr/0058-base-app-features.md)). Les fichiers produits sont décrits
dans [`virtual-proc-and-dev.md`](virtual-proc-and-dev.md) ; ce document décrit
les compteurs qui les alimentent.

## Rôle

Le gestionnaire de tâches ([`task-manager.md`](task-manager.md), chantier
`4-task-manager`), `ps` et toute app tierce lisent les mesures du système dans
`/proc`, comme sous Linux :

| Fichier ou API | Mesure | Source |
| --- | --- | --- |
| `/proc/stat` | temps CPU cumulé par cœur, en `USER_HZ` | **simulé** : simulateur de ressources |
| `/proc/<pid>/io` | octets lus et écrits par un processus | **compté** : service de fichiers et passerelle réseau |
| `/proc/diskstats` | requêtes, secteurs et temps du disque `hda` | **compté** : transactions IndexedDB du système de fichiers |
| `/proc/net/dev` | octets et paquets de `lo` et `eth0` | **compté** : passerelle réseau des apps |
| `/proc/mounts` | table des montages | routeur de montages |
| `HackDriveInfo` | taille et espace libre de `/` | profil matériel, contenu stocké, quota du navigateur |

Un `cat` d'un gros fichier fait monter `rchar` du processus et les secteurs lus
de `hda` ; une page chargée par le navigateur fait monter `eth0`. Le CPU reste
simulé : le navigateur ne mesure pas le CPU par app.

La phase 5 remplacera le CPU et la mémoire simulés par une mesure réelle,
optionnelle (ADR 0066 à 0068, [`execution-isolation-plan.md`](execution-isolation-plan.md)).

### Deux sources pour le CPU et la mémoire d'un processus (ADR 0066)

| Source | Quand | Indicateur |
| --- | --- | --- |
| **simulée** : `IResourceSimulator`, profil `ResourceProfile` de l'app | mesure coupée au build (`HackerOsCpuAccounting`, `HackerOsMemoryAccounting`) ou dans `appsettings.json` (`HackerOs:ResourceAccounting`), mesure pas encore implémentée, processus du contenu de jeu | `CpuSource: simulated`, `MemSource: simulated` ; mention « estimated » dans le gestionnaire de tâches |
| **mesurée** : `ICpuTimeAccountant`, `IMemoryAccountant` alimentés par le crochet de changement de contexte | mesure livrée et activée, et le comptable répond pour ce processus | `CpuSource: measured`, `MemSource: measured` |

Le fournisseur `/proc` reçoit les deux comptables par `ProcMetricsSources`
(`CpuAccountant`, `MemoryAccountant`) et retombe sur le simulateur dès qu'un
comptable ne répond pas. Le chantier `5-accounting-switches` livre les
interrupteurs, le crochet et les comptables nuls : tout reste `simulated`
jusqu'aux chantiers `5-cpu-accounting` et `5-memory-accounting`. Détails :
[`resource-accounting.md`](resource-accounting.md).

Les compteurs vivent en mémoire, partent de zéro au démarrage et ne sont jamais
persistés.

## Architecture

### Contrats

`Shared/HackerOs.Simulation.Abstractions/Metrics/SystemMetricsContracts.cs` :

| Contrat | Rôle |
| --- | --- |
| `IProcessIoAccounting`, `ProcessIoCounters` | compteurs de `/proc/<pid>/io` ; `Retain` oublie les processus terminés |
| `IBlockDeviceStatistics`, `BlockDeviceCounters`, `BlockIoDirection` | statistiques de `hda` (majeur 8, mineur 0) ; `Begin` puis `Complete` par requête |
| `INetworkInterfaceStatistics`, `NetworkInterfaceCounters`, `NetworkInterfaceNames` | compteurs de `lo` et `eth0` |
| `ICpuTimeAccounting`, `CpuTimes` | temps `user`, `nice`, `system`, `idle` par cœur |
| `IStorageUsageProvider`, `StorageUsage` | espace occupé et quota du navigateur |

`VirtualHardwareProfile.StorageCapacityBytes` (8 Gio par défaut,
`DefaultStorageCapacityBytes`) donne la taille du disque.

### Implémentations

| Type | Projet | Rôle |
| --- | --- | --- |
| `ProcessIoAccounting`, `BlockDeviceStatistics`, `NetworkInterfaceStatistics`, `CpuTimeAccounting` | `Platform.Core/Metrics` | compteurs en mémoire, sous verrou |
| `ResourceSamplingService` | `Platform.Core/Processes` | le « tick » de l'ordonnanceur : une fois par seconde simulée, échantillonne chaque processus actif et cumule le temps CPU |
| `FileSystemService` | `Platform.Core/FileSystem` | compte les lectures et écritures de chaque processus |
| `AppNetworkGateway` + `NetworkTrafficMeter` | `Platform.Core/Network` | compte le trafic de chaque app |
| `ProcFileSystemProvider` + `ProcMetricsSources` | `Platform.Core/FileSystem/Virtual` | rend les fichiers |
| `AppDriveGateway` | `Platform.Core/Execution` | `statfs` de l'OS derrière `HackDriveInfo` |
| `IndexedDbInteropAdapter` | `Infrastructure.Browser/Interop` | chronomètre chaque transaction et la signale à `hda` |
| `IndexedDbStorageUsageProvider` | `Infrastructure.Browser/FileSystem` | espace occupé (contenu dédupliqué) et quota (`navigator.storage.estimate()`) |

Branchement : `EcosystemServiceCollectionExtensions` enregistre les compteurs en
`Scoped` (une instance par machine, comme le gestionnaire de processus), les
passe au fournisseur `/proc` (`ProcMetricsSources`), au service de fichiers, au
fournisseur IndexedDB et à `AppExecutionContextFactory`
(`SystemMetricsServices`). `EcosystemBootCoordinator` démarre
`ResourceSamplingService` juste après le moteur d'horloge.

## Comment chaque mesure est prise

### CPU (`/proc/stat`)

Avant ce chantier, rien n'appelait `IResourceSimulator.Tick` dans un hôte réel :
`status`, `meminfo` et `loadavg` restaient à zéro. `ResourceSamplingService`
s'inscrit sur l'horloge simulée et, chaque seconde simulée :

1. lit les processus actifs ;
2. demande un échantillon au simulateur (`Tick`) ;
3. ajoute l'intervalle à `CpuTimeAccounting` ;
4. oublie les compteurs d'E/S des processus disparus.

`CpuTimeAccounting` donne à chaque cœur `100 × secondes` unités par intervalle.
Un processus commence sur le cœur `pid mod cœurs` et déborde sur les suivants
quand il est plein, comme un ordonnanceur avec affinité : deux processus
occupés tombent sur des cœurs différents. Le temps va dans `system` pour les
processus de l'OS (services déclarés système par l'hôte et processus inconnus
du catalogue), dans `user` sinon ; le reste du cœur va dans `idle`.

Le simulateur tire l'usage de chaque processus de son `ResourceProfile`, c'est-à-dire
des poids `Resources` déclarés par le manifeste (ADR 0012). Jusqu'au chantier
`4-task-manager`, `AppLifecycleOrchestrator` lançait chaque processus avec
`ResourceProfile.None` : le CPU et la mémoire restaient à zéro malgré
l'échantillonnage. L'orchestrateur passe maintenant le profil du manifeste
(voir [`task-manager.md`](task-manager.md)).

Une seconde d'échantillonnage garde l'historique borné du simulateur
(120 échantillons) sur deux minutes, assez pour les graphiques.

### Entrées-sorties par processus (`/proc/<pid>/io`)

`FileSystemService` est le seul chemin vers les fichiers, pour les passerelles
des apps comme pour les commandes, et le seul endroit qui connaît le montage
d'un chemin. Quand l'appelant a un PID (`AppOperationContext.ProcessId`) :

- **Lecture** : le flux rendu est enveloppé ; chaque appel de lecture compte un
  `syscr` et les octets rendus dans `rchar`. Un `head -c 10` d'un gros fichier
  coûte dix octets.
- **Écriture** : la source de contenu est enveloppée ; les octets que le
  fournisseur en tire vont dans `wchar`, avec un `syscw` par écriture.
- **Stockage** : `read_bytes` et `write_bytes` reçoivent les mêmes octets
  quand le montage n'est pas virtuel (`/proc` et `/dev` en sont exclus).

`AppNetworkGateway` ajoute le trafic réseau à `rchar` et `wchar`, jamais à
`read_bytes` ni à `write_bytes`. Une opération système sans processus n'est
comptée nulle part. `cancelled_write_bytes` reste à `0`.

### Disque (`/proc/diskstats`)

Le fournisseur IndexedDB passe `IBlockDeviceStatistics` à ses trois
adaptateurs (métadonnées en lecture, métadonnées en écriture, contenu).
`IndexedDbInteropAdapter.ExecuteAsync` marque la requête en vol, la chronomètre
et la termine :

- transaction en lecture seule : une lecture ; en lecture-écriture : une
  écriture ;
- taille : les octets du fichier pour le contenu, un bloc de 4 Kio pour une
  transaction de métadonnées (la taille d'une mise à jour d'inode sous Linux) ;
- une transaction en échec compte dans le temps occupé, pas dans les requêtes
  terminées.

`BlockDeviceStatistics` tient `io_ticks` (temps avec au moins une requête en
vol) et `time_in_queue` (temps pondéré par la profondeur de file) comme le
noyau : à chaque changement de file, il ajoute le temps écoulé depuis le
précédent. Les champs « fusionnées » restent à `0` : il n'y a pas de file à
fusionner.

### Réseau (`/proc/net/dev`)

`AppNetworkGateway` reste le point de passage unique ([`sdk-network.md`](sdk-network.md)).
Chaque méthode publique appelle son implémentation, puis `NetworkTrafficMeter`
compte l'échange sur `eth0` :

| Opération | Envoyé | Reçu |
| --- | --- | --- |
| DNS (`Resolve`, `ReverseLookup`) | IP + UDP + question RFC 1035 | question + 16 octets par adresse |
| Ping | 84 octets (IP + ICMP + 56) | 84 octets si l'hôte répond |
| HTTP | IP + TCP + ligne de requête, en-têtes, cookies, formulaire | IP + TCP + ligne d'état, en-têtes, corps ; un aller-retour de plus par redirection |
| Sondage TCP | un SYN de 40 octets par port | un SYN-ACK ou RST par port non filtré |
| Détection d'OS | 16 sondes | 16 réponses si l'hôte est connu |

Un paquet est une requête ou une réponse (ADR 0057). Un hôte introuvable,
injoignable, un délai dépassé ou une connexion refusée comptent la requête et
une erreur de réception (`errs`). Un appel refusé faute de capacité ne sort
pas : rien n'est compté. `lo` reste à zéro.

### Montages (`/proc/mounts`)

`MountDescription.From` décrit chaque montage du routeur : `procfs` →
`proc /proc proc ro`, `devfs` → `devfs /dev devfs rw`, tout autre fournisseur →
`/dev/hda <point> idbfs rw`. Le fournisseur `/proc` lit le routeur à la demande,
parce que le routeur le contient.

### Espace disque (`HackDriveInfo`)

`AppDriveGateway` rend un `HackDriveSnapshot` par montage non virtuel
(aujourd'hui `/`) :

- **taille** : `StorageCapacityBytes`, ramenée au quota du navigateur quand il
  est plus petit ;
- **occupé** : somme des longueurs des contenus distincts référencés par une
  entrée de fichier (deux fichiers identiques comptent une fois, comme leurs
  blocs dédupliqués) ;
- **libre** : taille − occupé, jamais négatif.

Aucune capacité n'est demandée, comme `df`.

## Utilisation

```csharp
// Usage CPU entre deux lectures, comme top.
string before = await HackFile.ReadAllTextAsync("/proc/stat");
await Task.Delay(1000);
string after = await HackFile.ReadAllTextAsync("/proc/stat");

foreach (HackDriveInfo drive in await HackDriveInfo.GetDrivesAsync())
{
    Console.WriteLine($"{drive.Name} {drive.DriveFormat} {drive.AvailableFreeSpace}/{drive.TotalSize}");
}
```

Lire `/proc` demande `filesystem.system.read`. `/proc/<pid>/io` est en `0400` :
seuls le propriétaire et l'autorité `System` le lisent.

## Décisions clés et précisions par rapport à l'ADR 0057

- **Comptage des fichiers dans `FileSystemService`.** L'ADR parle de « la
  passerelle fichier » : c'est le service derrière toutes les passerelles
  (`AppFileSystemGateway`, commandes, `HackFile`). Lui seul sait si un chemin
  touche le stockage ou un montage virtuel.
- **`ResourceSamplingService` est nouveau.** L'ADR suppose que le simulateur
  avance à chaque tick ; aucun code ne l'appelait. L'échantillonnage se fait
  chaque seconde simulée, pas chaque tick de 100 ms, pour garder deux minutes
  d'historique.
- **`hda` ne compte que le système de fichiers.** Les autres magasins IndexedDB
  (utilisateurs, groupes, autorisations, synchronisation) ne sont pas des
  fichiers ; ils ne passent pas par cet adaptateur instrumenté.
- **Tailles des métadonnées et des paquets estimées.** IndexedDB ne donne pas la
  taille de ses écritures de métadonnées, ni le réseau simulé celle de ses
  paquets : un bloc de 4 Kio par transaction de métadonnées, et des en-têtes IP,
  TCP, UDP et ICMP standard autour des données réellement échangées.
- **Temps du disque en temps réel.** Les millisecondes de `diskstats` sont
  mesurées sur l'horloge réelle : c'est le vrai temps passé par IndexedDB.
- **`HackDriveInfo` lit ses valeurs une fois.** Le navigateur ne répond qu'en
  asynchrone ; `GetDrivesAsync` rend des instances figées, à redemander pour des
  valeurs fraîches.
- **Espace occupé sans les blocs orphelins.** Les blocs en attente de nettoyage
  ne sont plus référencés par aucun fichier et ne sont pas comptés.

## Tests

- `Tests/HackerOs.Platform.Core.Tests/FileSystem/Virtual/ProcMetricsTests.cs` :
  format exact de `stat`, `diskstats`, `net/dev`, `mounts` ; débordement d'un
  cœur à l'autre ; échantillonnage par seconde simulée ; `io` compté par le
  service de fichiers, sans octets de stockage pour `/proc` ; `io` privé ;
  temps occupé pondéré par la file.
- `Tests/HackerOs.Platform.Core.Tests/Execution/AppDriveGatewayTests.cs` :
  taille, quota, espace libre jamais négatif, `HackDriveInfo` dans le contexte
  ambiant.
- `Tests/HackerOs.Network.Tests/Wave4NetworkTests.ProcMetrics.cs` : DNS, ping,
  HTTP et refus de capacité comptés (ou non) sur `eth0` et dans `rchar`/`wchar`.
- `Tests/HackerOs.Infrastructure.Browser.Tests/IndexedDbFileSystemReaderTests.cs` :
  espace occupé dédupliqué, transactions signalées à `hda`.

## Liste des tâches du chantier `rebuild/4-proc-metrics`

- [x] Contrats `Metrics` dans Simulation Abstractions ; `VirtualHardwareProfile.StorageCapacityBytes`.
- [x] Compteurs en mémoire dans `Platform.Core/Metrics`.
- [x] `ResourceSamplingService` démarré au boot : le simulateur avance enfin.
- [x] Comptage des fichiers par processus dans `FileSystemService`.
- [x] Comptage du trafic dans `AppNetworkGateway` (`NetworkTrafficMeter`).
- [x] Comptage des transactions IndexedDB du système de fichiers pour `hda`.
- [x] `/proc/stat`, `/proc/<pid>/io`, `/proc/diskstats`, `/proc/net/dev`, `/proc/mounts`.
- [x] `HackDriveInfo`, `IAppDriveGateway`, `IAppExecutionContext.Drives`, `AppDriveGateway`, `IndexedDbStorageUsageProvider`.
- [x] Câblage dans l'hôte Ecosystem.
- [x] Tests.
- [x] Documentation : ce document, `virtual-proc-and-dev.md`, `app-sdk.md`, `sdk-network.md`, index.
