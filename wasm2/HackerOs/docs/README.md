# Documentation HackerOS

Point d'entrée de `wasm2/HackerOs/docs/`. Tout ajout, renommage ou retrait de
document dans ce dossier met à jour cet index dans le même changement (voir
[`AGENTS.md`](../../../AGENTS.md)).

## Direction actuelle

| Document | Rôle |
| --- | --- |
| [`ecosystem-rebuild-plan.md`](ecosystem-rebuild-plan.md) | Plan de refonte de l'écosystème validé le 2026-10-10 : API fichier `Hack*`, manifeste en classe C#, paramètres et journaux en fichiers, `/proc` et `/dev`, shell complet, corbeille, permissions dangereuses, apps de base. **À lire en premier.** |
| [`hack-file-api.md`](hack-file-api.md) | API fichier `Hack*` (`HackFile`, `HackDirectory`, `HackPath`, infos, flux, surveillance), exceptions et contexte d'exécution ambiant positionné par l'OS (phase 1, ADR 0043). |
| [`logging.md`](logging.md) | Journaux en fichiers sous `/var/log` (ADR 0045) : format de ligne, `LogFileWriter`, rotation, Error Log Viewer, moteur d'horloge. |
| [`virtual-proc-and-dev.md`](virtual-proc-and-dev.md) | Phase 1, section 5.4 : fournisseurs virtuels `/proc` et `/dev` (contenu, visibilité, montage, `ps` sur `/proc`). |

## Sous-systèmes

| Document | Rôle |
| --- | --- |
| [`app-installation.md`](app-installation.md) | Désinstallation et réinstallation des apps (phase 2, ADR 0048) : fichiers `/etc/hackeros/apps.config` et `~/.config/hackeros/apps.config`, `IAppInstallationService`, filtrage du lancement, migration depuis `IsEnabled`, onglet « Installed Apps ». |
| [`app-model.md`](app-model.md) | Modèle d'app (phase 2, ADR 0047) : classe `AppDefinition` et `[AppManifest]`, `manifest.json` généré au build, agrégation `BuiltInApplications.json` dans l'hôte. |
| [`capabilities.md`](capabilities.md) | Capacités (phase 2, ADR 0049) : catalogue `AppCapabilities`, classement normal/dangereux, `Describe`, attribution à la connexion selon le statut système. |
| [`dangerous-capability-approval.md`](dangerous-capability-approval.md) | Approbation des capacités dangereuses (phase 2, ADR 0049) : `security.config`, `app-permissions.config`, passerelle de lancement, fenêtre d'approbation, mode texte du terminal, vue Permissions, `auth.log`. |
| [`process-environment.md`](process-environment.md) | Environnement de processus (phase 3, ADR 0050 et 0053) : arguments, répertoire courant et variables par processus, `/etc/environment`, `/etc/hostname`, `/proc/<pid>/cmdline`, `environ`, `cwd`, `HackEnvironment`. |
| [`shell-interpreter.md`](shell-interpreter.md) | Interpréteur du shell `hsh` (phase 3, ADR 0050) : grammaire (`\|`, `>`, `>>`, `&&`, `$VAR`, `$?`, `~`), tubes concurrents, redirections, `ShellSession`, sortie au fil de l'eau dans le terminal, `ExecuteCommandIntent` par l'interpréteur. |
| [`settings-files.md`](settings-files.md) | Paramètres en fichiers (plan 5.2, ADR 0044) : emplacements, couches et `[locked]`, API `HackSettings`, `IOsSettingsService`, migration depuis IndexedDB, app Paramètres, tests de contrat. |
| [`shell-commands.md`](shell-commands.md) | Commandes du shell (phase 3, ADR 0051) : `IShellSession` et capacité `shell.control`, `cd`, `pwd`, `clear`, `help`, `export`, `unset`, `env`, `sudo` du catalogue, terminal sans noms codés en dur, séquences d'effacement, `argv[0]` et alias statiques. |
| [`trash.md`](trash.md) | Corbeille (phase 3, ADR 0052) : format freedesktop sous `~/.local/share/Trash`, API `HackTrash`, `rm` vers la corbeille et `rm --permanent`, commande `trash`, `FileView.DeleteRequested` et vue Corbeille de l'explorateur. |

## Décisions d'architecture

| Dossier | Rôle |
| --- | --- |
| [`adr/`](adr/) | ADR acceptés. Un ADR n'est jamais modifié : un nouvel ADR le remplace. La section 10 du plan liste les ADR à écrire. Phase 1 (fichiers) : [0043](adr/0043-hack-file-api-and-ambient-context.md) API `Hack*` et contexte ambiant, [0044](adr/0044-settings-as-files.md) paramètres en fichiers, [0045](adr/0045-logs-as-files.md) journaux en fichiers, [0046](adr/0046-virtual-proc-and-dev-filesystems.md) `/proc` et `/dev` virtuels. Phase 2 (modèle d'app) : [0047](adr/0047-app-definition-and-generated-manifests.md) manifeste en classe C# et `BuiltInApplications.json`, [0048](adr/0048-system-apps-and-uninstall.md) apps système et désinstallation, [0049](adr/0049-dangerous-capabilities-and-admin-approval.md) capacités dangereuses et approbation administrateur. Phase 3 (shell, commandes et corbeille) : [0050](adr/0050-shell-grammar-and-process-environment.md) grammaire du shell et environnement de processus, [0051](adr/0051-unified-commands-project.md) commandes regroupées et accès contrôlé au shell, [0052](adr/0052-trash.md) corbeille freedesktop et `HackTrash`, [0053](adr/0053-hack-environment-async-directory-change.md) changement asynchrone du répertoire courant et lecture de l'environnement de connexion, [0054](adr/0054-shell-exit-status-and-detached-execution.md) `$?` par pipeline, shell détaché et écritures synchrones dans un tube. Phase 4 (SDK et apps de base) : [0055](adr/0055-base-apps-on-public-sdk-only.md) apps sur le SDK public uniquement, vérifié au build, [0056](adr/0056-sdk-apis-for-system-apps.md) API du SDK et capacités qui remplacent les services internes, [0057](adr/0057-proc-metrics-for-task-manager.md) mesures CPU, disque et réseau dans `/proc` et `HackDriveInfo`, [0058](adr/0058-base-app-features.md) fonctionnalités de l'explorateur, du gestionnaire de tâches, de l'éditeur et de la console, avec les chantiers `rebuild/4-*`. |

## Archives

| Dossier | Rôle |
| --- | --- |
| [`old/`](old/) | Documentation antérieure au plan de refonte (état d'implémentation, liste d'intégration, guides de sous-systèmes, pièges courants). Toujours utile comme référence technique ; son index est [`old/README.md`](old/README.md). Les documents encore pertinents seront réécrits ici au fil des phases. |
