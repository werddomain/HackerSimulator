# Fin de la migration wasm v3 : plan des chantiers

**Statut :** proposé le 2026-10-17, **en attente de validation par Benoit**.
Aucun code n'est écrit avant sa validation (réponse de Benoit du 2026-10-17 :
« Plan d'abord »). Les questions ouvertes sont à la section 7.

Ce plan termine ce que l'analyse d'origine
[`doc/wasm/wasm-v3-migration-analyse.md`](../../../doc/wasm/wasm-v3-migration-analyse.md)
(2026-08-01) demandait et qui n'est toujours pas fait. Il s'appuie sur deux
relevés faits le 2026-10-16 sur `web/base` (07c9c77) et revérifiés le
2026-10-17 sur `web/base` (34952e2) : rien n'a changé pour ces points pendant la
phase web.

- `/mnt/project-files/wasm-v3/wasm-v3-remaining-work.md` (23 manques numérotés,
  cités ici « R1 » à « R23 ») ;
- `/mnt/project-files/audits/wasm-v3-migration-analysis-gaps.md`.

## 1. Décision préalable : le jeu vit dans un autre dépôt

Benoit, 2026-10-17 : « Rien en ce qui concerne le jeu, cela sera dans une autre
repository qui va utiliser l'écosystème que nous créons présentement. »

Ce dépôt construit **la plateforme** : l'OS simulé, son SDK et ses apps de base.
Le jeu (missions, contrats, économie, améliorations matérielles, simulation de
sécurité, scripts du joueur, sauvegarde de partie) sera un autre dépôt, client
de cet écosystème comme n'importe quel développeur tiers. Décision consignée
dans l'[ADR 0092](adr/0092-game-in-a-separate-repository.md), qui remplace
l'[ADR 0023](adr/0023-optional-game-domain-and-proxy-fallback.md).

Conséquences pour ce plan :

- la vague 6 (gameplay) de l'analyse est **retirée** ; elle n'est ni à faire
  ici ni comptée comme un manque ;
- le code de jeu actuel (`HackerOs.Game.Core`, `HackerOs.Game.Abstractions`,
  `HackerOs.Game.Tests`, capacité `gameplay.domain.access`) est retiré par le
  chantier `v3/2` ;
- ce que le jeu demandera à l'OS (missions qui écrivent des fichiers, cibles
  réseau, etc.) passera par des **points d'extension génériques du SDK**, jamais
  par une API « jeu » dans ce dépôt ;
- la capacité du dépôt du jeu à consommer l'écosystème (paquets, versions,
  gabarits) devient un objectif de ce plan : chantiers `v3/6` et `v3/15`.

## 2. Ce qui n'est pas repris

| Point | Pourquoi |
|---|---|
| Vague 6 : missions, économie, améliorations, sécurité simulée, scripts du joueur (`P4-W6-*`) | Jeu, autre dépôt (section 1). `doc/wasm/gameplay-v3-analyse.md` reste une référence pour ce dépôt-là. |
| Site ShopZone (`ecommerce-controller.ts`) | Contenu de jeu ; il n'était même pas enregistré dans `src/`. |
| Portées de réglages app/appareil, itinérance, synchro des réglages | Remplacé par l'ADR 0044. |
| Rejet atomique d'un document de réglages invalide | Remplacé par l'ADR 0044 §5. |
| Activer/désactiver une app | Remplacé par désinstaller/réinstaller (ADR 0048). |
| Permissions accordées par utilisateur | Remplacé par l'ADR 0049. |
| Interface de récupération si Paramètres et Terminal sont désactivés (décision différée n°12) | Sans objet : les apps système ne se désinstallent pas (ADR 0048). Constaté dans l'ADR du chantier `v3/1`. |
| Checklist manuelle d'accessibilité | Écartée par Benoit ; seuls des tests automatisés (`v3/13`). |
| Suites de la phase 5 (quotas et tueur OOM, apps à fenêtre dans des workers, workers sur l'hôte Server, COOP/COEP) | Plan séparé (fil « Plan : suites de l'étape 5 »), à faire **après** cette migration (Benoit, 2026-10-17). |
| Installation d'apps au runtime (R1, R2, décisions différées n°2 et n°10) | Recommandé : plan séparé plus tard (question 1). |

## 3. Principes

1. **Mimic the Metal.** La langue suit `LANG` et `/etc/locale.conf` ; une
   suppression synchronisée est une pierre tombale ; un service a un état
   `active`/`failed` comme sous systemd ; une sauvegarde est une archive qu'on
   peut copier.
2. **SDK public seulement** pour les apps de base (ADR 0055) ; toute nouvelle
   possibilité passe par une façade `Hack*` documentée dans `sdk-reference.md`.
3. **Pensé pour un dépôt tiers.** Chaque ajout au SDK doit pouvoir servir au
   dépôt du jeu sans connaître le jeu.
4. **Les trois hôtes restent opérationnels** après chaque chantier
   (`build/verify-hosts.sh`) et l'Ecosystem marche sans serveur.
5. **ADR avant le code**, documentation et index `docs/README.md` à jour dans
   chaque chantier.
6. **Porte de fusion :** compilation et tests verts en local, tant que GitHub
   Actions ne tourne plus (blocage de facturation côté compte, hors de ce plan).

## 4. Chantiers

Branche d'intégration proposée : `v3/base` (question 5). Chaque chantier part
de `v3/base`, s'appelle `v3/<n>-<sujet>` et sa PR vise `v3/base`. Rien n'est
fusionné dans `main` sans Benoit.

| Branche | Contenu | Manques couverts | Dépend de |
|---|---|---|---|
| `v3/1-adr` | ADR des chantiers 3 à 15 (numéros pris à partir du premier libre). Constate aussi les décisions différées sans objet (n°12) et la taille maximale des fichiers déjà fixée par `FileContentStoragePolicy` (16 Mio, blocs de 256 Kio, analyse §20.5). | R22 (en partie) | validation |
| `v3/2-game-removal` | Retrait de `Game/HackerOs.Game.Core`, `Shared/HackerOs.Game.Abstractions`, `Tests/HackerOs.Game.Tests` de la solution et des règles de la frontière du SDK ; retrait de `AppCapabilities.GameplayDomainAccess` (32 → 31 capacités) ; docs (`project-overview`, `capabilities`, `sdk-reference`, `app-sdk`). | — | `1` |
| `v3/3-window-model` | Événements de fenêtre dans `WindowAppBase` (focus, perte du focus, réduction, agrandissement, restauration, redimensionnement) ; tailles initiale, minimale et maximale déclarées dans `AppDefinition` au lieu de `360×240` en dur (`WindowLaunchCoordinator.cs:77`) ; opérations au clavier de l'ADR 0009 : Alt+Tab, Alt+F4, Super+flèches, déplacer et redimensionner au clavier (menu de fenêtre Alt+Espace). | R7, R8 | `1` |
| `v3/4-intents` | `RevealFileIntent` réel (l'explorateur ouvre le dossier et sélectionne le fichier) ; `Edit` atteignable par `IAppIntentGateway` ; intents `OpenUri` (navigateur par défaut, `xdg-open`) et `ShowAppSettings` ; intents déclarés par les apps et routés par le dispatcher au lieu de `intent.unknown` ; commande `xdg-open`. | R6 | `1` |
| `v3/5-services` | Santé des services (`ServiceHealth` : `active`, `activating`, `failed`, raison) remontée par `HackServiceController`, `systemctl status` et le gestionnaire de tâches ; démarrage au boot de l'OS (avant la connexion) ou à la connexion, déclaré dans le manifeste ; message clair quand la désinstallation d'une app arrête un processus actif. | R9, R23 | `1` |
| `v3/6-sdk-versioning` | Versions sur les bibliothèques du SDK ; paquets NuGet du SDK et de la plateforme dans un flux local `artifacts/local-nupkg` pour qu'un autre dépôt construise une app ou un OS ; plage `SdkCompatibility` vérifiée au chargement (`AppCatalog`) ; noms de commandes en double rejetés au build et au chargement ; hash Sha256 des assets vérifiés ; `BuildProfileManifest` retiré (remplacé par l'ADR 0047/0048) ; gabarits `dotnet new` (app à fenêtre, commande, service, site ASP.Hack). | R3, R4, R5, R20, R21 (en partie) | `1` |
| `v3/7-storage-backup` | Sauvegarde et restauration de l'OS exposées (`IndexedDbBackupService` enregistré, page dans Paramètres ou Diagnostic, commande `hackbackup`, archive dans `~`) ; comportement « espace faible » (`IsLowSpace` : notification, écriture refusée avec `ENOSPC`, `df`) ; fixtures IndexedDB des versions précédentes et test de montée de version ; cache PWA corrompu détecté et rechargé, test de quota. | R13, R14, R15 | `1` |
| `v3/8-settings-security` | Valeurs `SettingSensitivity` masquées à la lecture par un non-administrateur et dans les exports ; chiffrement des réglages sensibles (décision différée n°9, ADR) ; journal d'audit des écritures protégées dans `/etc` (`/var/log/audit.log`, façon `auditd`). | R16, R17, R22 (n°9) | `1` |
| `v3/9-sync` | Suppressions synchronisées (pierres tombales au lieu de `IsTombstone: false` en dur) ; résolution des conflits appelée et proposée à l'utilisateur ; synchro périodique ; client du domaine AppCatalog. | R18 | `1` |
| `v3/10-server-proxy` | Relais du serveur optionnel : corps des requêtes HTTP transmis, flux TCP, UDP, contrôle du grant côté serveur, quotas et politique dans `appsettings` (tout autoriser, désactiver les quotas), avertissements au démarrage. | R19 | `1` |
| `v3/11-apps` | Hack Paint ouvre et enregistre des images dans les fichiers de l'OS (PNG, dialogues, « Ouvrir avec ») ; éditeur de thèmes porté depuis `src/` (édition, aperçu, documentation, thème enregistré dans `~/.config`) ; revérification des points ouverts du Code Editor (assets hors ligne, gros fichiers, nettoyage des workers). | migration §2 | `1`, `3` |
| `v3/12-localization` | `IStringLocalizer` dans les apps et la plateforme, ressources `.resx` ; langue de l'utilisateur dans `~/.config/locale.conf`, langue du système dans `/etc/locale.conf`, variable `LANG` dans l'environnement des processus (ADR 0050) ; passerelle de langue dans `IAppExecutionContext` ; `Localizations` du manifeste utilisé (nom des apps dans le lanceur) ; traduction des apps de base et de la plateforme ; correction de `docs/old/localization.md`, qui décrit un état inexistant. | R10 | `3`, `4`, `5`, `7`, `11` |
| `v3/13-accessibility` | Préférences d'accessibilité en fichiers (contraste élevé, taille du texte, mouvement réduit) lues par le thème et exposées par le SDK ; scans axe ajoutés pour Terminal, Browser, Calculator, System Monitor et les apps des chantiers 7 et 11 ; parcours au clavier testé en E2E. | R11 | `3`, `12` |
| `v3/14-trimming` | `PublishTrimmed=True` dans l'Ecosystem, avertissements de trimming corrigés dans tous les projets livrés, publication Release trimmée testée en E2E (boot, apps, sites). AOT hors périmètre. | R12 | `2` à `13` |
| `v3/15-sdk-freeze` | `PublicAPI.Shipped.txt` pour toutes les bibliothèques du SDK ; gel du SDK 1.0 ; test d'une app compilée contre une version antérieure du SDK ; guide « construire sur HackerOS depuis un autre dépôt ». | R21 | `14` |
| `v3/16-final` | Vérification finale (trois hôtes, toutes les suites), statut ajouté à `doc/wasm/wasm-v3-migration-analyse.md`, mise à jour de `docs/old/implementation-status.md`, `project-overview.md`, `sdk-reference.md`. | — | tous |

### Ordre et parallélisme

1. `v3/1-adr`.
2. **Vague 1**, en parallèle (zones de code distinctes) : `2`, `3`, `4`, `5`,
   `6`, `7`, `8`, `9`, `10`. Recommandé : quatre fils à la fois au plus, en
   commençant par `2`, `3`, `6` et `7`. `3` et `6` touchent tous deux
   `AppDefinition` : le second à fusionner reprend l'autre avant.
3. **Vague 2** : `11` (après `3`), puis `12`, qui touche toutes les chaînes
   affichées et passe donc après les chantiers qui en ajoutent.
4. `13`, puis `14`, puis `15`, puis `16`, l'un après l'autre (chacun touche
   l'ensemble de la solution).

Le dépôt du jeu peut commencer dès la fusion de `v3/6` (paquets en version
préliminaire) ; il passe au SDK 1.0 après `v3/15`.

### ADR prévus (écrits par `v3/1`)

Fenêtres (événements, tailles, clavier ; complète l'ADR 0009) ; intents ajoutés
et intents d'app ; santé et démarrage des services ; versions, paquets et
vérifications du manifeste (complète l'ADR 0019) ; sauvegarde et espace faible ;
réglages sensibles chiffrés et audit ; suppressions synchronisées (complète les
ADR 0025 et 0030) ; relais TCP/UDP du serveur ; localisation ; préférences
d'accessibilité ; trimming ; décisions différées sans objet et taille maximale
des fichiers. Les numéros sont attribués au moment de l'écriture (premier
numéro libre ; plusieurs fils écrivent des ADR en parallèle).

## 5. Hors périmètre

- Tout le contenu de jeu (section 1).
- Installation d'apps au runtime, format et signature des paquets, magasin
  d'apps, si la question 1 confirme le report.
- Suites de la phase 5 : plan séparé.
- AOT, compilation d'apps dans l'OS.

## 6. Risques

- **Localisation** : chantier le plus long (toutes les chaînes de la plateforme
  et des 13 apps système). Peut être découpé en un fil d'infrastructure puis un
  fil par groupe d'apps.
- **Trimming** : les assemblies chargées paresseusement et la réflexion du
  générateur de manifestes peuvent casser en Release seulement ; c'est pour ça
  que le chantier vient tard, une fois les API stables, et qu'il ajoute un test
  E2E sur la publication trimmée.
- **Tests sans CI** : la porte est locale (section 3, principe 6). Les suites
  PWA ne tournent pas en conteneur (pas d'IPv6).

## 7. Questions ouvertes

| # | Question | Recommandation |
|---|---|---|
| 1 | Installation d'apps au runtime (paquets, signature, transaction, migrations de données) : dans ce plan ou plus tard ? | **Plus tard**, dans un plan à part après celui-ci : c'est le plus gros morceau restant et rien d'autre n'en dépend. |
| 2 | Comment le dépôt du jeu consomme-t-il l'écosystème ? | **Paquets NuGet** (flux local, puis GitHub Packages quand la CI remarchera), plutôt qu'un sous-module git. |
| 3 | Langues de la localisation ? | **Anglais et français**, anglais par défaut ; les autres langues de `docs/old/localization.md` ne sont pas reprises. |
| 4 | Porter l'éditeur de thèmes de `src/` ? | **Oui**, comme app système sur le SDK public. |
| 5 | Branche d'intégration ? | **`v3/base`**, créée depuis `web/base` une fois `web/12` à `web/15` fusionnés. |

## 8. Liste des tâches

### Préparation
- [x] Relevé de ce qui reste (2026-10-16), revérifié sur `web/base` 34952e2.
- [x] Décision « le jeu dans un autre dépôt » consignée (ADR 0092).
- [x] Ce plan.
- [ ] Validation du plan et réponses aux questions par Benoit.
- [ ] Création de `v3/base`.

### Chantiers
- [ ] `v3/1-adr`
- [ ] `v3/2-game-removal`
- [ ] `v3/3-window-model`
- [ ] `v3/4-intents`
- [ ] `v3/5-services`
- [ ] `v3/6-sdk-versioning`
- [ ] `v3/7-storage-backup`
- [ ] `v3/8-settings-security`
- [ ] `v3/9-sync`
- [ ] `v3/10-server-proxy`
- [ ] `v3/11-apps`
- [ ] `v3/12-localization`
- [ ] `v3/13-accessibility`
- [ ] `v3/14-trimming`
- [ ] `v3/15-sdk-freeze`
- [ ] `v3/16-final`
