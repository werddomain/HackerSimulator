# Suites de la phase 5 : limites, OOM killer, fenêtres en worker, hôte Server, isolation cross-origin

**Statut :** plan proposé le 2026-10-17, **à valider par Benoit**. Aucun code n'est
écrit. Benoit a demandé « plan d'abord, mais faire plus tard après la
migration » : ce plan **s'exécute après la fin de la migration wasm v3**,
planifiée dans un document séparé (fil « Plan : fin de la migration wasm v3 »).
Aucun chantier ne commence avant que ce plan-là soit terminé, et les questions
de la section 6 sont tranchées par Benoit avant l'écriture des ADR.

Il reprend les points que [`execution-isolation-plan.md`](execution-isolation-plan.md)
(phase 5, ADR 0066 à 0072) a laissés hors périmètre (sa section 7), ainsi que
ceux que les ADR de la phase 5 et la mise en œuvre des workers ont notés comme
« plus tard ».

## 1. Objet

- **Contraindre** les processus, et plus seulement les mesurer : limites par
  processus façon `ulimit`/`setrlimit`, et un **OOM killer** quand la mémoire du
  système simulé est épuisée.
- **Sortir du fil principal ce qui y reste par nécessité** : services qui ont
  besoin des événements typés ou des abonnements de fichiers, apps tierces, et
  surtout la logique des **apps à fenêtre**.
- Donner à **`HackerOs.Server`** les mesures et une isolation des processus, que
  la phase 5 a coupées sur cet hôte « jusqu'à un ADR propre ».
- Étudier puis activer, en option, l'**isolation cross-origin** (COOP/COEP), qui
  ouvre `SharedArrayBuffer`, la mesure réelle de la mémoire par worker et des
  minuteries précises.

## 2. Constat de départ

Sur `web/base` au 2026-10-17 (phase 5 et phase web fusionnées) :

| Sujet | État | Référence |
|---|---|---|
| Limites | Aucune. La phase 5 a posé « mesurer, pas contraindre » : pas de quantum, pas de limite mémoire, pas d'OOM killer, pas de fenêtre « Forcer à quitter ». | `execution-isolation-plan.md` §3 et §7, ADR 0068 §3 |
| Mesures | CPU (`utime`/`stime`, compositeur) et mémoire en quatre couches, optionnelles au build et dans `appsettings.json`. Compteurs en mémoire, remis à zéro au démarrage. | ADR 0066 à 0068, [`cpu-accounting.md`](cpu-accounting.md), [`memory-accounting.md`](memory-accounting.md) |
| Arrêt | `StopRequested`, `CheckpointAsync`, escalade en quatre étapes ; l'étape 4 (terminer le worker) n'existe que dans un worker. Sur le fil principal, un processus bloqué gèle encore l'OS. | ADR 0069, ADR 0072 §5 |
| Workers | Commandes en pool (49 sur 63) et services en worker dédié, chacun avec son runtime .NET (~15 à 40 Mo par worker). Le pool ne précharge que `HackerOs.Commands.dll`. | ADR 0070 à 0072, [`worker-processes.md`](worker-processes.md) |
| Ce qu'un worker n'offre pas | `HackEvents`, `Watch`, hôte de shell, administration et page HTTP du navigateur simulé lèvent `NotSupportedException`. Le service d'exemple et `hackwebd` restent donc en `MainThread`. | ADR 0072 §3 |
| Apps à fenêtre | Toujours sur le fil principal (pas de DOM dans un worker). Une boucle dans une fenêtre gèle le bureau ; « Not responding » ne s'affiche qu'après la fin de la tranche. | ADR 0070 §1, ADR 0067 §4 |
| `HackerOs.Server` | Mesures coupées par défaut, pas de worker : `AppExecutionHost` est ignoré, une ligne dans `/var/log/syslog`, pas de `/proc/hackeros`. | ADR 0066 §3, ADR 0067 §6, ADR 0070 §6, ADR 0072 §7 |
| Cross-origin | Aucun hôte n'envoie `Cross-Origin-Opener-Policy` ni `Cross-Origin-Embedder-Policy` ; `crossOriginIsolated` est faux, donc pas de `SharedArrayBuffer`, pas de `performance.measureUserAgentSpecificMemory()`, `performance.now()` arrondi. Les échanges avec les workers passent par `postMessage` (crédit de 64 Kio par flux). | ADR 0072 §2 |
| Trimming | `PublishTrimmed=False` : le coût d'un worker en dépend. Le point appartient à la migration wasm v3, pas à ce plan. | `/mnt/project-files/wasm-v3/wasm-v3-remaining-work.md` n° 12 |

## 3. Principes

Ceux de la phase 5 restent, sauf le premier, que ce plan remplace en partie.

1. **Contraindre seulement là où la contrainte est réelle.** Dans un worker,
   une limite dépassée termine vraiment le processus. Sur le fil principal, une
   limite passe par la demande d'arrêt et l'escalade (ADR 0069), et la
   documentation dit qu'elle est « au mieux ». On n'affiche jamais une
   protection que l'OS ne peut pas tenir.
2. **Optionnel, comme les mesures.** Chaque contrainte se coupe dans
   `appsettings.json` (réglage d'hôte) ou dans `/etc` (réglage de l'OS simulé,
   ADR 0044) selon sa nature ; coupée, on revient au comportement actuel. Une
   limite fondée sur une mesure est inactive quand cette mesure est coupée
   (`CpuSource`/`MemSource: simulated`).
3. **Mimic the Metal.** `setrlimit`, `ulimit`, `/proc/<pid>/limits`,
   `/etc/security/limits.conf`, `SIGXCPU`, `oom_score`, `oom_score_adj`, la
   ligne « Out of memory: Killed process » du noyau, un serveur d'affichage qui
   reçoit les dessins d'un client (X11, Wayland).
4. **Le SDK ne change pas pour les apps existantes.** Une app existante garde
   son comportement sans changer une ligne. Les nouveautés (processus de
   travail d'une fenêtre, réaction à une limite) sont des ajouts.
5. **Les apps existantes suivent chaque nouveauté dans le même chantier**
   (section 5.1), comme en phase 5.
6. **Les trois hôtes restent opérationnels après chaque chantier.**
   `build/verify-hosts.sh` passe avant toute fusion, avec les options du
   chantier activées et coupées. L'Ecosystem fonctionne toujours sans serveur
   et hors ligne (AGENTS.md, section I).
7. **Aucune dépendance au jeu.** Le contenu de jeu vivra dans un autre dépôt qui
   utilisera cet écosystème (décision de Benoit du 2026-10-17) ; ce plan ne
   prévoit rien pour lui.

## 4. Registre des décisions proposées

Chaque ligne devient une décision d'ADR une fois les questions de la section 6
tranchées. Les numéros d'ADR sont attribués au moment de l'écriture (section 7).

| # | Décision proposée | Question |
|---|---|---|
| 1 | Passerelles distantes pour `HackEvents` et `Watch` ; le service d'exemple et `hackwebd` passent en worker ; le pool charge à la demande l'assembly de n'importe quelle commande ou service, plus seulement `HackerOs.Commands.dll`. | — |
| 2 | Limites par processus façon `setrlimit` : `RLIMIT_CPU` (`SIGXCPU` puis `SIGKILL`), une limite mémoire sur `VmRSS` mesuré, `RLIMIT_NOFILE`, `RLIMIT_NPROC`. Commande `ulimit` (intégrée au shell), `prlimit`, `/proc/<pid>/limits`, `/etc/security/limits.conf`. Héritées par les enfants. | Q1 |
| 3 | OOM killer : quand la mémoire disponible du système simulé passe sous un seuil, l'OS choisit la victime par `oom_score` (résident + `oom_score_adj`), la tue et l'écrit dans le journal du noyau. Actif seulement avec la mémoire mesurée. | Q2, Q3 |
| 4 | Garde du vrai navigateur : quand le tas du runtime principal ou d'un worker approche la limite réelle de la page, l'OOM killer agit même si `MemTotal` simulé n'est pas atteint. | Q2 |
| 5 | Pas de quantum préemptif ; `nice` et `renice` règlent seulement l'ordre de la file du pool et le choix du worker. | Q4 |
| 6 | Fenêtre « (Not responding) » : titre, fenêtre grisée et boîte « Attendre / Forcer à quitter » quand une fenêtre reste sans réponse, dès que le bureau peut se redessiner (fenêtre dont la logique est en worker, ou fin d'une longue tranche). | — |
| 7 | Apps à fenêtre : d'abord un **processus de travail** déclaré par l'app (`AppDefinition`), qui tourne en worker et parle à sa fenêtre par un canal typé ; ensuite une preuve de faisabilité d'un **rendu Blazor dans un worker** dont les lots de rendu sont appliqués par le fil principal, façon serveur d'affichage. | Q5 |
| 8 | `HackerOs.Server` : mesures sur un hôte multithread (tranches par thread, `idle` = cœurs × temps écoulé − somme des tranches), workers sur des threads dédiés qui réutilisent le protocole et la boucle locale des tests, `/proc/hackeros/workers`. | Q7 |
| 9 | Isolation cross-origin optionnelle : en-têtes COOP/COEP envoyés par les hôtes et par le service worker de la PWA (donc hors ligne aussi), détection par `crossOriginIsolated`, repli exact sur le comportement actuel. | Q6 |
| 10 | Avec l'isolation : tubes en anneaux de `SharedArrayBuffer` entre workers et fil principal, appels synchrones du worker vers l'OS (`Atomics.wait` dans le worker seulement), mémoire réelle par worker par `measureUserAgentSpecificMemory()`, minuteries à 5 µs. Pas de threads .NET WebAssembly. | Q6, Q8 |

## 5. Chantiers

Branches `exec/<n>-<topic>`, chacune partant de la branche d'intégration en
usage au moment de l'exécution, et sa PR la visant (question Q9). Les ADR de la
phase sont écrits et acceptés avant le code (`exec/0-adr`), comme pour les
phases précédentes.

| Branche | Contenu | Dépend de |
|---|---|---|
| `exec/0-adr` | ADR des décisions 1 à 10, une fois les questions tranchées ; mise à jour de ce plan et de l'index. | fin de la migration wasm v3 |
| `exec/1-remote-gateways` | Décision 1 : passerelles distantes `HackEvents` (abonnement côté worker, livraison par message, désabonnement à la fin du processus) et `Watch` ; tests de parité pour ces deux passerelles ; service d'exemple et `hackwebd` en worker ; chargement à la demande de toute assembly d'app dans le pool ; une commande d'un projet d'exemple tiers exécutée en worker. | `0` |
| `exec/2-process-limits` | Décision 2 : contrat de limites dans Simulation Abstractions, lecture de `/etc/security/limits.conf` (et `limits.d/`), héritage au `fork`, `ulimit` et `prlimit`, `/proc/<pid>/limits`, `SIGXCPU`, application stricte en worker et au mieux sur le fil principal, sortie 152 (`SIGXCPU`) et 137 ; erreur `EMFILE`/`EAGAIN` pour `NOFILE`/`NPROC` par les passerelles. | `0` |
| `exec/3-oom-killer` | Décisions 3 et 4 : seuil de mémoire disponible, `oom_score`, `oom_score_adj` (fichier modifiable, `-1000` exempte, réservé à l'administrateur pour baisser), choix et mise à mort, ligne du noyau dans `/var/log/kern.log` et `syslog`, `dmesg`, notification à l'utilisateur pour une fenêtre tuée, garde du vrai navigateur, compteur `oom_kill` dans `/proc/vmstat`. | `2` |
| `exec/4-not-responding-ui` | Décisions 5 et 6 : fenêtre « (Not responding) » et boîte « Attendre / Forcer à quitter », `nice`/`renice` et colonne « NI » de `ps`/`top`, ordre de la file du pool. | `0` |
| `exec/5-window-worker-process` | Décision 7, premier volet : processus de travail d'une app à fenêtre (déclaration dans `AppDefinition`, lancement et arrêt liés à la fenêtre, canal typé, même identité et mêmes capacités que la fenêtre), exemple dans `Apps/Samples`, migration des traitements longs des apps de base qui s'y prêtent. | `1` |
| `exec/6-remote-renderer-poc` | Décision 7, second volet : preuve de faisabilité d'un `Renderer` Blazor dans un worker, lots de rendu sérialisés vers le fil principal, événements du DOM renvoyés au worker ; mesure du coût. Résultat : un ADR « on y va » ou « on s'arrête ». Rien n'est livré aux apps si la preuve échoue. | `5` |
| `exec/7-window-workers` | Seulement si `6` réussit : `AppExecutionHost.Dedicated` permis pour une fenêtre sans interop JavaScript propre, gestion du clavier, du focus et des dialogues, tests sur la Calculatrice. | `6` |
| `exec/8-server-host` | Décision 8 : ADR propre à `HackerOs.Server`, mesures activées par défaut si le test de surcoût passe, workers sur threads, escalade sans étape 4 (un thread .NET ne se tue pas) et ce que l'OS affiche alors. | `1` |
| `exec/9-cross-origin-isolation` | Décisions 9 et 10 : audit de compatibilité (iframes des sites ASP.Hack et du navigateur, fenêtres détachées, repli sur le réseau réel, workers de Monaco, téléchargements, PWA), en-têtes dans les trois hôtes et le service worker, repli, puis tubes partagés, appels synchrones, mémoire réelle par worker et minuteries. | `3`, `5` |
| `exec/10-final` | Vérification de bout en bout, documents à jour (`system-metrics.md`, `task-manager.md`, `worker-processes.md`, `app-sdk.md`, `sdk-reference.md`, `linux-commands.md`, `virtual-proc-and-dev.md`, `building-and-testing.md`), index, cette liste. | tous |

Ordre :

1. `exec/0-adr`.
2. En parallèle : `exec/1-remote-gateways`, `exec/2-process-limits`,
   `exec/4-not-responding-ui`.
3. En parallèle : `exec/3-oom-killer`, `exec/5-window-worker-process`,
   `exec/8-server-host`.
4. `exec/6-remote-renderer-poc`, puis `exec/7-window-workers` s'il est décidé.
5. `exec/9-cross-origin-isolation` (il profite des limites et de la mesure
   réelle par worker, et doit vérifier les fenêtres en worker s'il y en a).
6. `exec/10-final`.

### 5.1 Ce que chaque chantier fait pour les apps existantes

| Chantier | Migration des apps existantes |
|---|---|
| `exec/1-remote-gateways` | Revoir chaque app en `MainThread` pour cause de passerelle manquante (service d'exemple, `hackwebd`) et la passer en worker quand la raison disparaît ; mettre à jour le commentaire de raison des autres. |
| `exec/2-process-limits` | Chaque commande et service traite `EMFILE`/`EAGAIN` comme une erreur d'E/S ordinaire (message et code de sortie). Les commandes longues ont déjà `CheckpointAsync` (phase 5) : vérifier qu'une limite CPU les arrête proprement. |
| `exec/3-oom-killer` | Les apps qui gardent un état (éditeurs, CodeEditor, explorateur, Paramètres) gardent leur brouillon de récupération quand elles sont tuées par l'OOM killer comme par « Force kill » ; les services système critiques reçoivent un `oom_score_adj` négatif dans leur définition. |
| `exec/4-not-responding-ui` | Aucune modification d'app ; le gestionnaire de tâches et `top` affichent « NI ». |
| `exec/5-window-worker-process` | Inventaire des traitements longs des apps à fenêtre (recherche du CodeEditor, zip et copies de l'explorateur, calculs de Hack Paint) ; chacun passe dans un processus de travail ou garde sa place avec la raison écrite. |
| `exec/7-window-workers` | Inventaire des apps à fenêtre sans interop JavaScript propre ; celles qui passent les tests en worker changent d'`AppExecutionHost`, les autres gardent `MainThread` avec la raison. |
| `exec/8-server-host` | Aucune modification d'app ; les tests de parité des commandes tournent aussi contre l'hôte Server. |
| `exec/9-cross-origin-isolation` | Chaque app qui charge une ressource externe (navigateur, CodeEditor) est vérifiée avec l'isolation activée ; ce qui casse est corrigé dans le chantier ou empêche d'activer l'isolation par défaut. |

### 5.2 Vérification des hôtes

`build/verify-hosts.sh` (phase 5) gagne, au fil des chantiers :

- un parcours avec des limites basses (`ulimit -t 1` puis `stress`, sortie 152) ;
- un parcours de l'OOM killer avec une `MemTotal` simulée réduite ;
- sur l'hôte Server, `/proc/hackeros/workers` présent et `kill -9` d'une boucle ;
- un parcours de l'Ecosystem publié avec et sans isolation cross-origin, avec
  `crossOriginIsolated` vérifié dans la page.

## 6. Questions ouvertes (à trancher par Benoit)

Chaque question a une recommandation (⭐). Un mot par question suffit.

**Q1. Limites sur le fil principal**
- Au mieux ⭐ : `SIGTERM` puis l'escalade de l'ADR 0069 (jusqu'à l'étape 3) ;
  `/proc/<pid>/limits` et la documentation disent que la limite n'est stricte
  qu'en worker.
- Déplacer : un processus qui reçoit une limite est relancé en worker quand il
  le peut. Plus compliqué, et impossible pour une fenêtre.

**Q2. Mémoire totale de l'OOM killer**
- Simulée ⭐ : `MemTotal` du profil matériel simulé (comme aujourd'hui), plus la
  garde du vrai navigateur (décision 4).
- Réelle : la mémoire que le navigateur accorde à la page. Peu lisible et
  différente d'une machine à l'autre.

**Q3. OOM killer actif par défaut**
- Oui quand la mémoire est mesurée ⭐ ; jamais quand elle est simulée (on ne tue
  pas sur des valeurs inventées).
- Non : désactivé tant que l'utilisateur ne l'active pas dans `/etc`.

**Q4. Quantum imposé**
- Non ⭐ : impossible à préempter sans threads ; `nice` ne règle que la file du
  pool (décision 5).
- Oui : couper une tranche par `CheckpointAsync` forcé. Ne marche que pour le
  code qui appelle `CheckpointAsync`, donc trompeur.

**Q5. Apps à fenêtre dans un worker**
- Processus de travail d'abord, puis preuve de faisabilité du rendu ⭐ : la
  décision d'aller plus loin se prend sur le résultat de la preuve.
- Processus de travail seulement : on n'étudie pas le rendu en worker.
- Rendu en worker directement : risqué, le format des lots de rendu de Blazor
  côté serveur est interne au framework.

**Q6. Isolation cross-origin**
- Optionnelle, activée par défaut si l'audit passe ⭐ : en-têtes par le service
  worker (fonctionne hors ligne et sur un hébergement statique qui ne permet pas
  d'en-têtes), `COEP: credentialless` là où le navigateur le permet, sinon
  `require-corp` ; repli exact quand `crossOriginIsolated` est faux.
- Optionnelle, coupée par défaut : activée dans `appsettings.json` seulement.
- Non : on reste sur `postMessage`.

**Q7. Isolation sur l'hôte Server**
- Threads d'abord ⭐ : même protocole, réutilise la boucle locale des tests ;
  un thread bloqué ne peut pas être tué, l'OS l'affiche « Not responding » et
  le journal propose de redémarrer l'hôte.
- Processus enfants : un vrai `kill`, mais un exécutable de plus à publier et à
  démarrer ; à reprendre dans un ADR séparé si les threads ne suffisent pas.

**Q8. Threads .NET WebAssembly (`WasmEnableThreads`)**
- Non ⭐ : Blazor ne les prend pas en charge et ils interdisent de bloquer le
  fil principal ; `SharedArrayBuffer` sert seulement entre les workers et la
  couche JavaScript de l'OS.
- Étudier : une preuve de faisabilité dans le chantier 9.

**Q9. Branches**
- `exec/<n>-<topic>` depuis la branche d'intégration en usage à ce moment ⭐
  (aujourd'hui `web/base`), PR vers elle, fusion par le fil quand la build et
  les tests passent en local ; jamais dans `main`.
- Une nouvelle branche d'intégration `exec/base`.

## 7. ADR à écrire

Dans `exec/0-adr`, numéros pris au moment de l'écriture (le prochain libre
selon `docs/adr/`) :

| ADR | Contenu | Complète ou remplace |
|---|---|---|
| Passerelles distantes d'événements et de surveillance | décision 1 | remplace l'ADR 0072 §3 en partie, complète 0070 |
| Limites par processus | décision 2 | remplace le principe « mesurer, pas contraindre » de la phase 5 pour les limites, complète 0067, 0068, 0069 |
| OOM killer | décisions 3 et 4 | remplace l'ADR 0068 §3 |
| Priorité et fenêtre sans réponse | décisions 5 et 6 | complète l'ADR 0067 §4 |
| Processus de travail des apps à fenêtre | décision 7, premier volet | complète l'ADR 0070 §1 |
| Rendu de fenêtre dans un worker | décision 7, second volet, après la preuve | remplace l'ADR 0070 §1 (fenêtres) si on y va |
| Mesures et workers sur `HackerOs.Server` | décision 8 | remplace l'ADR 0066 §3 (défaut Server), 0067 §6, 0070 §6, 0072 §7 |
| Isolation cross-origin | décisions 9 et 10 | complète l'ADR 0072 §2 (tubes) et l'ADR 0068 (mémoire réelle par worker) |

## 8. Liste des tâches

### Préparation
- [x] Écrire ce plan (2026-10-17, fil « Plan : suites de l'étape 5 »).
- [ ] Faire valider le plan et trancher les questions Q1 à Q9 avec Benoit.
- [ ] Attendre la fin de la migration wasm v3.
- [ ] `exec/0-adr` : écrire et faire accepter les ADR de la section 7.

### Passerelles distantes
- [ ] `HackEvents` et `Watch` en worker, tests de parité.
- [ ] Service d'exemple et `hackwebd` en worker.
- [ ] Pool ouvert à toute assembly de commande ou de service.

### Limites et OOM killer
- [ ] Contrat de limites, `limits.conf`, héritage, `ulimit`, `prlimit`, `/proc/<pid>/limits`.
- [ ] `SIGXCPU`, limite mémoire, `EMFILE`/`EAGAIN`.
- [ ] `oom_score`, `oom_score_adj`, choix et mise à mort, journal du noyau, `dmesg`.
- [ ] Garde du vrai navigateur.

### Fenêtres
- [ ] « (Not responding) » et « Attendre / Forcer à quitter », `nice`/`renice`.
- [ ] Processus de travail d'une app à fenêtre, exemple, migration des apps de base.
- [ ] Preuve de faisabilité du rendu en worker et ADR de décision.
- [ ] Fenêtres en worker (si décidé).

### Hôte Server
- [ ] ADR de l'hôte, mesures, workers sur threads, `/proc/hackeros/workers`.

### Isolation cross-origin
- [ ] Audit de compatibilité.
- [ ] En-têtes dans les hôtes et le service worker, repli.
- [ ] Tubes partagés, appels synchrones, mémoire réelle par worker, minuteries.

### Fin
- [ ] `build/verify-hosts.sh` étendu (section 5.2), vérification finale, documents et index.

## 9. Hors périmètre

- Tout ce que planifie la migration wasm v3 (trimming, localisation, gabarits
  `dotnet new`, sauvegarde et restauration, etc.).
- Le contenu de jeu, qui vivra dans un autre dépôt.
- Plusieurs utilisateurs ou plusieurs circuits simultanés sur `HackerOs.Server`
  (l'hôte reste mono-locataire, ADR 0027).
- Déplacer le shell (`hsh`) et ses scripts dans un worker (ADR 0072 §1 reste
  valable).
- Compteurs de CPU et de mémoire persistants d'un démarrage à l'autre.

## 10. Références

- [`execution-isolation-plan.md`](execution-isolation-plan.md) (phase 5) et ses
  ADR [0066](adr/0066-optional-resource-accounting.md),
  [0067](adr/0067-measured-cpu-time.md),
  [0068](adr/0068-measured-process-memory.md),
  [0069](adr/0069-graceful-stop-and-checkpoints.md),
  [0070](adr/0070-worker-hosted-processes.md),
  [0071](adr/0071-always-ready-worker.md),
  [0072](adr/0072-worker-processes-implementation-adjustments.md).
- [`worker-processes.md`](worker-processes.md),
  [`cpu-accounting.md`](cpu-accounting.md),
  [`memory-accounting.md`](memory-accounting.md),
  [`resource-accounting.md`](resource-accounting.md),
  [`ecosystem-rebuild-plan.md`](ecosystem-rebuild-plan.md).
