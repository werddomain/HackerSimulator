# Documentation HackerOS

Point d'entrée de `wasm2/HackerOs/docs/`. Tout ajout, renommage ou retrait de
document dans ce dossier met à jour cet index dans le même changement (voir
[`AGENTS.md`](../../../AGENTS.md)).

## Démarrer

| Document | Rôle |
| --- | --- |
| [`project-overview.md`](project-overview.md) | Vue d'ensemble du projet : couches, rôle de chaque projet de la solution, hôtes, système simulé (fichiers, processus, shell, capacités), démarrage, règles vérifiées au build. **Point d'entrée pour comprendre le code.** |
| [`building-and-testing.md`](building-and-testing.md) | Compiler, lancer les trois hôtes, publier la PWA et exécuter les tests ; contournements connus dans un conteneur Linux. |
| [`sdk-developer-guide.md`](sdk-developer-guide.md) | Guide du développeur d'apps : créer une app fenêtrée, une commande du shell ou un service avec le SDK public, `AppDefinition`, services de l'OS, capacités, installation, tests. Exemples compilés. |
| [`sdk-reference.md`](sdk-reference.md) | Référence du SDK : bibliothèques, types de définition, les 32 capacités, `IAppExecutionContext`, chaque façade `Hack*`, points d'entrée, composants Blazor (dialogues, graphiques, vue de fichiers, terminal, icônes, clavier virtuel). |

## Direction actuelle

| Document | Rôle |
| --- | --- |
| [`ecosystem-rebuild-plan.md`](ecosystem-rebuild-plan.md) | Plan de refonte de l'écosystème validé le 2026-10-10 : API fichier `Hack*`, manifeste en classe C#, paramètres et journaux en fichiers, `/proc` et `/dev`, shell complet, corbeille, permissions dangereuses, apps de base. **À lire en premier.** |
| [`hack-file-api.md`](hack-file-api.md) | API fichier `Hack*` (`HackFile`, `HackDirectory`, `HackPath`, infos, flux, surveillance), exceptions et contexte d'exécution ambiant positionné par l'OS (phase 1, ADR 0043). |
| [`logging.md`](logging.md) | Journaux en fichiers sous `/var/log` (ADR 0045) : format de ligne, `LogFileWriter`, rotation, Error Log Viewer, moteur d'horloge. |
| [`virtual-proc-and-dev.md`](virtual-proc-and-dev.md) | Phase 1, section 5.4 : fournisseurs virtuels `/proc` et `/dev` (contenu, visibilité, montage, `ps` sur `/proc`) ; fichiers de mesure de la phase 4 (`stat`, `io`, `diskstats`, `net/dev`, `mounts`). |

## Sous-systèmes

| Document | Rôle |
| --- | --- |
| [`admin-authentication-agent.md`](admin-authentication-agent.md) | Agent d'authentification de l'OS (phase 4, ADR 0055 §5), façon `polkit` : une API qui exige un administrateur le fait confirmer dans une fenêtre de la plateforme, l'app ne voit jamais le mot de passe, issue dans `auth.log`. |
| [`app-installation.md`](app-installation.md) | Désinstallation et réinstallation des apps (phase 2, ADR 0048) : fichiers `/etc/hackeros/apps.config` et `~/.config/hackeros/apps.config`, `IAppInstallationService`, filtrage du lancement, migration depuis `IsEnabled`, onglet « Installed Apps ». |
| [`app-sdk.md`](app-sdk.md) | Frontière du SDK des apps (phase 4, ADR 0055 et 0059 ; pour écrire une app, voir le guide et la référence ci-dessus) : ce qui forme le SDK, références et injections permises, vérification au build (`HOSAPP0030`, `HOSAPP0031`, `HOSSDK0001`), test `IJSRuntime`, fin des exemptions temporaires (mécanisme supprimé par `rebuild/4-sdk-boundary-final`), API ajoutées par les chantiers `rebuild/4-*` (`HackUser`, `HackEnvironment.GetFolderPath`, `HackApps`, `HackAppManagement`, `HackDriveInfo`, `HackProcess`, `HackServiceController`, `HackLineChart`, `HackClipboard`, `HackDirectory.CopyAsync`, `HackTextContent`…). |
| [`sdk-network.md`](sdk-network.md) | Phase 4, ADR 0056 section 6 : `HackDns`, `HackPing`, `HackHttpClient`, `HackTcpClient`, passerelle `IAppNetworkGateway`, repli sur le réseau réel décidé par l'OS, migration du navigateur et de `ping`, `curl`, `cat`, `nmap`. |
| [`app-model.md`](app-model.md) | Modèle d'app (phase 2, ADR 0047) : classe `AppDefinition` et `[AppManifest]`, `manifest.json` généré au build, agrégation `BuiltInApplications.json` dans l'hôte. |
| [`capabilities.md`](capabilities.md) | Capacités (phase 2, ADR 0049) : catalogue `AppCapabilities`, classement normal/dangereux, `Describe`, attribution à la connexion selon le statut système. |
| [`file-explorer.md`](file-explorer.md) | Explorateur de fichiers (phase 4, ADR 0056 §12 et 0058 §1) : presse-papiers de session `HackClipboard` (format GNOME), couper/copier/coller, conflits de noms à la Nautilus, `FileTransfer`, glisser-déposer (fond, arbre, fil d'Ariane, Places, Corbeille, fichiers de l'hôte), `FileViewBreadcrumb`, `FileViewDropZone`. |
| [`code-editor.md`](code-editor.md) | Éditeur de code façon VS Code (ADR 0020 et 0062) : Monaco avec un modèle par onglet, dossier ouvert (`CodeEditorWorkspace`), explorateur avec création de fichiers et de dossiers, recherche dans tout le dossier (casse, mot entier, regex, globs), ouverture rapide Ctrl+P, terminaux `hsh` en onglets, barre latérale et panneau redimensionnables, raccourcis. |
| [`dangerous-capability-approval.md`](dangerous-capability-approval.md) | Approbation des capacités dangereuses (phase 2, ADR 0049) : `security.config`, `app-permissions.config`, passerelle de lancement, fenêtre d'approbation, mode texte du terminal, vue Permissions, `auth.log`. |
| [`process-environment.md`](process-environment.md) | Environnement de processus (phase 3, ADR 0050 et 0053) : arguments, répertoire courant et variables par processus, `/etc/environment`, `/etc/hostname`, `/proc/<pid>/cmdline`, `environ`, `cwd`, `HackEnvironment`. |
| [`shell-interpreter.md`](shell-interpreter.md) | Interpréteur du shell `hsh` (phase 3, ADR 0050) : grammaire (`\|`, `>`, `>>`, `&&`, `$VAR`, `$?`, `~`), tubes concurrents, redirections, `ShellSession`, sortie au fil de l'eau dans le terminal, `ExecuteCommandIntent` par l'interpréteur. |
| [`settings-files.md`](settings-files.md) | Paramètres en fichiers (plan 5.2, ADR 0044) : emplacements, couches et `[locked]`, API `HackSettings`, `IOsSettingsService`, migration depuis IndexedDB, app Paramètres, tests de contrat. |
| [`shell-host.md`](shell-host.md) | Hôte de shell (phase 4, ADR 0056 §7) : `HackShell`, `IHackShell`, `HackCredentialPrompt`, capacité dangereuse `shell.host`, shell `hsh` en processus enfant du terminal, complétion des commandes et des chemins, `TerminalPanel` sur le SDK seul. |
| [`shell-commands.md`](shell-commands.md) | Commandes du shell (phase 3, ADR 0051) : `IShellSession` et capacité `shell.control`, `cd`, `pwd`, `clear`, `help`, `export`, `unset`, `env`, `sudo`, `exit` du catalogue, terminal sans noms codés en dur, séquences d'effacement, `argv[0]` et alias statiques. |
| [`system-administration.md`](system-administration.md) | Administration du système dans le SDK (phase 4, ADR 0056 §8–11 et ADR 0060) : `HackSettings.OpenSystemAsync`, `HackPermissionAdministration`, `HackServerConnection`, `HackSystem`, `HackStorageDiagnostics`, capacités dangereuses associées, séquence d'arrêt, Paramètres et Diagnostic sur le SDK. |
| [`system-metrics.md`](system-metrics.md) | Mesures système (phase 4, ADR 0057) : compteurs CPU simulés, entrées-sorties par processus, disque `hda` et réseau `eth0` réellement comptés, `ResourceSamplingService`, `/proc/stat`, `/proc/<pid>/io`, `/proc/diskstats`, `/proc/net/dev`, `/proc/mounts`, `HackDriveInfo`. |
| [`task-manager.md`](task-manager.md) | Gestionnaire de tâches (phase 4, ADR 0056 §4–5 et ADR 0058 §2) : `HackProcess` et `HackServiceController` lus dans `/proc`, onglets Processes, Performance, Disk, Network et Services, tri, filtre, sélection multiple, « End task », détail d'un processus, graphiques SVG `HackLineChart`, rafraîchissement d'une seconde suspendu fenêtre réduite. |
| [`terminal-emulator.md`](terminal-emulator.md) | Émulateur du terminal (ADR 0061) : xterm.js embarqué dans `HackerOs.AppSdk.Terminal`, discipline de ligne en C# (`TerminalInputDecoder`, `TerminalLineEditor`, `TerminalAnsi`), couleurs ANSI, raccourcis clavier, écran alternatif pour `nano`, mise à jour du bundle. |
| [`terminal-console.md`](terminal-console.md) | Console (phase 4, ADR 0058 §4) : complétion des chemins par le shell, `DisplayCandidates`, commande `exit` et `IShellSession.Exit`, Ctrl+D, onglets `TerminalTabs` (un `hsh` par onglet, raccourcis, titre), fenêtre fermée avec le dernier onglet. |
| [`text-editor.md`](text-editor.md) | Éditeur de texte (phase 4, ADR 0058 §3) : ouvrir depuis l'explorateur (`LaunchFile`), enregistrer sur place ou sous un autre nom avec détection des conflits, texte détecté par `HackTextContent`, rechercher et remplacer (Ctrl+F, Ctrl+H), options « Match case » et « Whole word », `TextEditorFindEngine`. |
| [`trash.md`](trash.md) | Corbeille (phase 3, ADR 0052) : format freedesktop sous `~/.local/share/Trash`, API `HackTrash`, `rm` vers la corbeille et `rm --permanent`, commande `trash`, `FileView.DeleteRequested` et vue Corbeille de l'explorateur. |

## Décisions d'architecture

| Dossier | Rôle |
| --- | --- |
| [`adr/`](adr/) | ADR acceptés. Un ADR n'est jamais modifié : un nouvel ADR le remplace. La section 10 du plan liste les ADR à écrire. Phase 1 (fichiers) : [0043](adr/0043-hack-file-api-and-ambient-context.md) API `Hack*` et contexte ambiant, [0044](adr/0044-settings-as-files.md) paramètres en fichiers, [0045](adr/0045-logs-as-files.md) journaux en fichiers, [0046](adr/0046-virtual-proc-and-dev-filesystems.md) `/proc` et `/dev` virtuels. Phase 2 (modèle d'app) : [0047](adr/0047-app-definition-and-generated-manifests.md) manifeste en classe C# et `BuiltInApplications.json`, [0048](adr/0048-system-apps-and-uninstall.md) apps système et désinstallation, [0049](adr/0049-dangerous-capabilities-and-admin-approval.md) capacités dangereuses et approbation administrateur. Phase 3 (shell, commandes et corbeille) : [0050](adr/0050-shell-grammar-and-process-environment.md) grammaire du shell et environnement de processus, [0051](adr/0051-unified-commands-project.md) commandes regroupées et accès contrôlé au shell, [0052](adr/0052-trash.md) corbeille freedesktop et `HackTrash`, [0053](adr/0053-hack-environment-async-directory-change.md) changement asynchrone du répertoire courant et lecture de l'environnement de connexion, [0054](adr/0054-shell-exit-status-and-detached-execution.md) `$?` par pipeline, shell détaché et écritures synchrones dans un tube. Phase 4 (SDK et apps de base) : [0055](adr/0055-base-apps-on-public-sdk-only.md) apps sur le SDK public uniquement, vérifié au build, [0056](adr/0056-sdk-apis-for-system-apps.md) API du SDK et capacités qui remplacent les services internes, [0057](adr/0057-proc-metrics-for-task-manager.md) mesures CPU, disque et réseau dans `/proc` et `HackDriveInfo`, [0058](adr/0058-base-app-features.md) fonctionnalités de l'explorateur, du gestionnaire de tâches, de l'éditeur et de la console, avec les chantiers `rebuild/4-*`, [0059](adr/0059-sdk-boundary-diagnostic-codes.md) numéros des codes de la frontière du SDK (`HOSAPP0030`, `HOSAPP0031`), [0060](adr/0060-system-administration-sdk-adjustments.md) ajustements de l'API d'administration (`SystemPower`, `HackPermissionDecision`, droits d'écriture des documents de l'OS, `SetManyAsync`, presse-papiers de l'hôte pour Diagnostic). Après la refonte : [0061](adr/0061-xterm-terminal-emulator.md) xterm.js comme émulateur du terminal, [0062](adr/0062-code-editor-workbench.md) éditeur de code en atelier à la VS Code (remplace une puce de l'ADR 0058 §4). |

## Archives

| Dossier | Rôle |
| --- | --- |
| [`old/`](old/) | Documentation antérieure au plan de refonte (état d'implémentation, liste d'intégration, guides de sous-systèmes, pièges courants). Toujours utile comme référence technique ; son index est [`old/README.md`](old/README.md). Les documents encore pertinents seront réécrits ici au fil des phases. |
