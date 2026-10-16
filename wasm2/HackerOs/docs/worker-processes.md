# Processus sans interface dans des Web Workers

Mise en œuvre des [ADR 0070](adr/0070-worker-hosted-processes.md) et
[0071](adr/0071-always-ready-worker.md), avec les ajustements de
l'[ADR 0072](adr/0072-worker-processes-implementation-adjustments.md) ;
chantier `rebuild/5-worker-processes` de la phase 5
([`execution-isolation-plan.md`](execution-isolation-plan.md)).

## Rôle

Blazor WebAssembly exécute tout le .NET sur un seul thread : une commande qui
boucle sans rendre la main gelait l'interface, la souris et `kill`. Dans le
navigateur, les commandes du shell et les services tournent maintenant dans des
**Web Workers**, chacun avec son propre runtime .NET :

- une boucle infinie (`stress`) ne gèle plus le bureau ; `echo` répond en
  ~300 ms pendant qu'elle tourne ;
- le gestionnaire de tâches, `ps` et `/proc/hackeros/workers` montrent
  « Not responding » **pendant** le blocage ;
- `kill -9` (et `kill` après le délai de grâce) termine le worker bloqué et le
  remplace aussitôt ;
- le SDK ne change pas : une commande ne sait pas où elle tourne.

Les apps à fenêtre, le shell (`hsh`) et ses commandes intégrées restent sur le
fil principal.

## Où tourne chaque processus

`[AppManifest(Execution = AppExecutionHost.…)]` déclare l'hôte ; le générateur
écrit le résultat dans `manifest.json` (`"execution"`).

| Genre | Défaut (`Default`) | Valeurs permises |
|---|---|---|
| Fenêtre, composant embarqué | `mainThread` | `mainThread` (validateur : `manifest.execution.window`) |
| Commande | `pool` | `pool`, `dedicated`, `mainThread` |
| Service | `dedicated` | `dedicated`, `pool`, `mainThread` |

Les 63 commandes de `HackerOs.Commands` : 49 en `pool`, 14 en `mainThread`
avec la raison en commentaire dans leur définition (`cd`, `exit`, `export`,
`unset`, `jobs`, `read`, `help`, `which`, `kill` pilotent le shell ; `sudo`
demande un mot de passe à l'agent d'authentification ; `nano`, `top`, `htop`,
`watch` utilisent l'écran alternatif du terminal). Le service d'exemple reste
en `mainThread` : il publie des événements typés, non disponibles dans un
worker (ADR 0072 §3).

`HackerOs.Server` n'a pas de Web Worker .NET : `AppExecutionHost` y est ignoré
et le démarrage écrit dans `/var/log/syslog` « workers: no Web Worker on this
host: AppExecutionHost (pool, dedicated) is ignored, every process runs in the
host process (ADR 0070, section 6) ». `test/test` exécute côté navigateur le
`Program.cs` de l'Ecosystem : il a les workers.

## Architecture

```
 fil principal (OS)                               Web Worker n
 ─────────────────────────────                    ─────────────────────────────
 AppLifecycleOrchestrator                         workerBootstrap.js (dotnetSidecar)
   └ IRemoteProcessHost ── WorkerRemoteProcessHost   └ BrowserWorkerEntryPoint [JSExport]
        └ WorkerPool ── WorkerConnection ◄─ JSON ─►      └ WorkerProcessHost
             │            └ RemoteProcessSession           ├ ProcessControlBlock / ProcessTaskScheduler
             │               (appels servis sous           ├ RemoteAppExecutionContext
             │                le contexte du processus)    │   └ passerelles distantes (WorkerRpcClient)
             └ IWebWorkerRuntime (BrowserWebWorkerRuntime  └ battement de cœur (250 ms)
               → workerHost.js, ou Loopback en test)
```

| Projet | Types |
|---|---|
| `Shared/HackerOs.App.Abstractions` | `AppExecutionHost`, `AppExecutionHosts` (`DefaultFor`, `Resolve`, `Of`, `IsWorker`), `AppManifestAttribute.Execution`, `AppManifest.Execution`. |
| `Shared/HackerOs.Simulation.Abstractions/Workers` | `IWebWorkerRuntime`, `IWebWorker`, `WebWorkerStartOptions` ; `IWorkerPoolStatus`, `WorkerSnapshot`, `WorkerRole`, `WorkerState`. |
| `Platform/HackerOs.Platform.Core` | `IRemoteProcessHost`, `RemoteProcessLaunch`, `RemoteProcessExit`, `IRemoteService`, `ExecutionPlacement` (groupe d'un pipeline), `SystemStartupSignal` ; `AppLifecycleOrchestrator` lance à distance ce que `ShouldRunRemotely` accepte ; `/proc/hackeros/workers`. |
| `Platform/HackerOs.Platform.WorkerHost` | **Protocole** : `WorkerMessages`, `WireCodec` (encodage explicite, sans réflexion), `RemoteErrors`, `RemoteOps`. **Côté worker** : `WorkerProcessHost`, `RemoteAppExecutionContext`, passerelles distantes (`RemoteGateways`, `RemoteFileSystemGateway`, `RemoteStandardStreams`), `WorkerCpuMeter`, `WorkerAssemblyLoader`, `BrowserWorkerEntryPoint`. **Côté OS** : `WorkerPool`, `WorkerConnection`, `RemoteProcessSession`, `WorkerRemoteProcessHost`, `WorkerAwareCpuTimeAccountant`, `WorkerAwareMemoryAccountant`, `AddHackerOsWorkerProcesses`. **Tests** : `LoopbackWebWorkerRuntime`. |
| `Infrastructure/HackerOs.Infrastructure.Browser` | `wwwroot/workerHost.js` (création, `postMessage`, `terminate`, `hardwareConcurrency`, `requestIdleCallback`), `wwwroot/workerBootstrap.js` (runtime .NET du worker), `Workers/BrowserWebWorkerRuntime`. Aucun hôte ne les référence : ils sont importés par le C# (`_content/HackerOs.Infrastructure.Browser/…`). |
| `OS/HackerOs.Ecosystem` | `Program.cs` enregistre `BrowserWebWorkerRuntime` et les workers **dans le navigateur seulement** (`OperatingSystem.IsBrowser()`), en préchargeant `HackerOs.Commands.dll` ; `EcosystemBootCoordinator.MarkSystemStarted` donne le signal de fin de boot. |
| `Apps/System/HackerOs.Apps.SystemMonitor` | `ProcWorkers`/`ParseWorkers`, section « Workers » de l'onglet Performance. |

### Démarrage d'un worker

`workerBootstrap.js` pose `globalThis.dotnetSidecar = true` avant d'importer
`dotnet.js` (sinon le runtime se prend pour un thread `pthread` et attend
indéfiniment), crée le runtime (~0,7 s), branche l'import
`hackeros-worker.post` et appelle `BrowserWorkerEntryPoint.Start`. Le worker
charge ensuite les assemblies demandées (`INTERNAL.loadLazyAssembly`), répond
`hello` et commence son battement de cœur.

### Protocole

Messages JSON avec un champ `t` :

| OS → worker | Rôle |
|---|---|
| `init` | Numéro du worker, période du battement, mesure du CPU. |
| `start` | Lancer un processus : identité, point d'entrée, flux, environnement. |
| `result` | Réponse à un `call` (valeur `v` ou erreur `e`). |
| `stop` | `SIGTERM` : `StopRequested`, puis annulation du jeton (ADR 0069). |
| `int` | Ctrl+C : annulation du jeton, sans demande d'arrêt. |
| `kill` | `SIGKILL` quand le worker répond encore (étape 3) : jeter le processus. |
| `sstop` | `OnStoppingAsync` d'un service (déconnexion, extinction). |
| `clock` | Réponse au battement : l'horloge simulée, pour que celle du worker suive. |
| `perm` | Erreur de permission levée par l'OS sur les passerelles du processus. |
| `ack` | Crédit de sortie : l'OS a écrit `n` caractères. |
| `epipe` | Le lecteur du flux `f` est parti (tube cassé). |
| `hreq` | Requête HTTP pour un socket ouvert par `net.listen` dans le worker (`web/3`). |

| Worker → OS | Rôle |
|---|---|
| `hello` | Runtime et assemblies chargés. |
| `hb` | Battement (250 ms) : temps CPU par processus, mémoire du runtime, temps occupé. |
| `call` | Appel de passerelle qui attend une réponse. |
| `note` | Appel sans réponse (journal, environnement), gardé dans l'ordre. |
| `cancel` | Annuler un `call` en cours. |
| `out` | Sortie standard ou d'erreur d'un processus. |
| `exit` | Fin d'un processus : statut, code, message d'erreur. |
| `killed` | Accusé d'un `kill`. |
| `stopdone`, `sstopped` | Fin du traitement d'un `stop` / `sstop`. |
| `log` | Ligne de diagnostic du worker. |
| `hres` | Réponse à un `hreq` (`web/3`). |

Chaque `call` est servi sur le fil principal par `RemoteProcessSession` **sous
le contexte d'exécution du processus** (`HackExecutionContext.Enter` puis
`EnterKernel`) : les mêmes vérifications de capacités et la même comptabilité
qu'en local. La sortie a un crédit de 64 Kio en vol ; un fichier régulier de
16 Mio au plus est lu d'un coup, au-delà par blocs.

### Pool, worker prêt, débordement (ADR 0070 §2-3, ADR 0071)

| Rôle | Quand |
|---|---|
| `pool` | Un worker par cœur virtuel : `clamp(CpuCapacity, 1, capacité - 1)`, cœurs 0..n-1. |
| `ready` | Toujours un, une fois le système démarré ; remplacé aussitôt qu'il est pris. |
| `dedicated` | Prend le worker prêt pour un processus `dedicated` ; libéré à sa fin. |
| `overflow` | Le worker prêt pris par une commande quand aucun cœur n'est libre ; retiré après 30 s vide. |
| `web` | Un seul pour tous les sites (`AppKind.Web`, ADR 0073 §1) : le premier site prend le worker prêt, les suivants le partagent ; terminé quand son dernier site s'arrête. Requêtes HTTP par `hreq`/`hres` ([`listening-ports-and-dns.md`](listening-ports-and-dns.md)). |

- **Capacité** : `max(2, navigator.hardwareConcurrency)` workers en tout. Au
  plafond, une demande attend qu'un worker se libère et `/var/log/syslog` le
  note une fois (« worker pool saturated »).
- **Démarrage** : au signal de fin de boot (bureau affiché), puis
  `requestIdleCallback` (2 s au plus) : le worker prêt d'abord, puis les cœurs.
  Avant ce signal, une demande démarre son propre worker et l'attend.
- **Libre** : un worker prêt sans processus, ou dont le dernier battement a
  moins de 750 ms **et** est arrivé au moins une période après le dernier
  processus placé sur lui (ADR 0072 §6) : une commande qui boucle dès son
  lancement n'attire pas la suivante derrière elle.
- **Placement** : le cœur libre le moins chargé ; les étapes d'un pipeline
  (`ExecutionPlacement.BeginGroup` dans `ShellInterpreter`) vont sur le même
  worker.

### Battement de cœur et escalade (ADR 0070 §5)

- Toutes les 250 ms, le worker envoie les temps CPU de chaque processus
  (`utime`, `stime`, tranches, plus longue tranche, allocations) mesurés par
  `WorkerCpuMeter` sur le crochet de changement de contexte, la mémoire de son
  runtime et son temps occupé. Ils alimentent `/proc/<pid>/stat`, `status`,
  `/proc/stat` (le cœur du worker) et `/proc/meminfo` par
  `WorkerAwareCpuTimeAccountant` et `WorkerAwareMemoryAccountant`.
- Sans battement depuis 5 s, les processus du worker passent
  **Not responding** et syslog écrit « worker N: not responding (pids …) ».
  Le temps sans battement est compté comme occupé (ADR 0072 §6).
- **Étape 3** : `kill` au worker ; s'il confirme (`killed`) dans la seconde,
  il continue de servir.
- **Étape 4** : sinon (ou après le délai de grâce de `SIGTERM`), l'OS termine
  le worker. Ses autres processus sortent `Killed` avec « [pid] killed: worker
  N terminated to stop pid P » dans syslog, le shell écrit « Killed », et le
  worker prêt reprend le cœur.

### `/proc/hackeros/workers`

Présent seulement quand l'hôte a des workers :

```
capacity 4
pool_size 1
worker role       state          core memory_kb heartbeat_ms busy_ms pids
2      pool       running        0    41216     112          380     57,58
1      ready      idle           -    38912     98           0       -
```

Rôles : `pool`, `overflow`, `dedicated`, `ready`, `web`. États : `starting`, `idle`,
`running`, `not-responding`, `terminated`. Les PID (séparés par des virgules)
sont filtrés par la visibilité de `/proc` du lecteur.

## Usage

Côté app : rien, sinon déclarer l'hôte quand le défaut ne convient pas
(voir [`app-sdk.md`](app-sdk.md#processus-dans-des-web-workers-adr-0070-et-0071)).

Côté hôte :

```csharp
if (OperatingSystem.IsBrowser())
{
    builder.Services.AddSingleton<IWebWorkerRuntime, BrowserWebWorkerRuntime>();
    builder.Services.AddHackerOsWorkerProcesses(o => o with { PreloadAssemblies = ["HackerOs.Commands.dll"] });
}
```

`WorkerPoolOptions` : `HeartbeatPeriod` (250 ms), `UnresponsiveThreshold`
(5 s), `BusyThreshold` (750 ms), `KillAcknowledgeTimeout` (1 s),
`OverflowIdleTimeout` (30 s), `IdleWait` (2 s), `VirtualCores` (du profil
matériel), `PreloadAssemblies`, `MeterCpu`.

Commande de démonstration : `stress [-c N] [-t SECONDES] [--yield]` fait
tourner N boucles de calcul sans rendre la main (`--yield` : avec points
d'arrêt).

## Décisions clés

- Le shell, ses scripts et ses tubes restent sur le fil principal ; seules les
  commandes externes migrent (ADR 0072 §1-2).
- Encodage explicite des messages (`WireCodec`) : pas de sérialisation par
  réflexion dans un runtime élagué.
- `LoopbackWebWorkerRuntime` exécute chaque « worker » sur un thread dédié avec
  son contexte de synchronisation : les tests couvrent le pool, le protocole et
  l'escalade sans navigateur.
- `IAppProcessGateway.KillAsync` (méthode par défaut) pour tuer depuis un worker
  (ADR 0072 §4).

## Tests

- `Tests/HackerOs.Platform.WorkerHost.Tests` (OS complet sans interface, workers
  en boucle locale) :
  - `CommandParityTests` : **les 63 commandes intégrées** exécutées dans les
    deux modes (fil principal, worker) avec la même sortie, le même code et le
    même arbre de fichiers (`HACKEROS_PARITY_DUMP` pour inspecter) ;
  - `WorkerPoolTests` : démarrage avant / après la fin du boot, worker prêt
    pris et remplacé, débordement et retour à la taille, plafond et syslog,
    pipeline sur un seul worker, commande qui boucle dès son lancement,
    `/proc/hackeros/workers` ;
  - `EscalationTests` : `kill -9` d'une boucle (voisins tués, syslog, cœur
    remplacé), « Not responding » pendant le blocage, `SIGTERM` puis
    escalade, étape 3 sans terminer le worker, Ctrl+C, erreur dans un worker ;
  - `DedicatedServiceTests` : service dans un worker dédié, `systemctl stop`.
- `Tests/HackerOs.App.Abstractions.Tests` : `Execution` dans le mappeur, le
  validateur, le sérialiseur et le schéma.
- `Tests/HackerOs.HostSmoke.Tests` (`build/verify-hosts.sh`) : dans les hôtes
  navigateur, pipeline, `/proc/hackeros/workers`, `echo` pendant `stress`,
  « not-responding », `kill -9` et `kill` ; sur le serveur, absence de
  `/proc/hackeros`.

## Liste des tâches du chantier `rebuild/5-worker-processes`

- [x] `AppExecutionHost` dans `[AppManifest]`, le mappeur, le validateur, le schéma et `manifest.json` (défauts de l'ADR 0070 §1).
- [x] Contrats `IWebWorkerRuntime`, `IWorkerPoolStatus` ; `IRemoteProcessHost` dans l'orchestrateur.
- [x] `HackerOs.Platform.WorkerHost` : protocole, hôte de processus du worker, passerelles distantes, flux standard par messages.
- [x] Module JS dans `HackerOs.Infrastructure.Browser/wwwroot` et `BrowserWebWorkerRuntime`.
- [x] Pool par cœur virtuel, placement des pipelines, workers dédiés.
- [x] Worker toujours prêt après le boot, débordement, retour à la taille, plafond et syslog (ADR 0071).
- [x] Battement de cœur, mesures CPU et mémoire, « Not responding », étape 4 de l'escalade.
- [x] `/proc/hackeros/workers` et section « Workers » du gestionnaire de tâches.
- [x] Commandes : hôte déclaré ou défaut ; service d'exemple en `MainThread` (raison en commentaire).
- [x] Serveur : ligne d'information ; `test/test` comme l'Ecosystem.
- [x] Tests de parité des 63 commandes, tests du pool et de l'escalade.
- [x] Vérification des trois hôtes (`build/verify-hosts.sh`).
- [x] Documentation et ADR 0072.
