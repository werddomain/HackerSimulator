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
| [`app-model.md`](app-model.md) | Modèle d'app (phase 2, ADR 0047) : classe `AppDefinition` et `[AppManifest]`, `manifest.json` généré au build, agrégation `BuiltInApplications.json` dans l'hôte. |
| [`settings-files.md`](settings-files.md) | Paramètres en fichiers (plan 5.2, ADR 0044) : emplacements, couches et `[locked]`, API `HackSettings`, `IOsSettingsService`, migration depuis IndexedDB, app Paramètres, tests de contrat. |

## Décisions d'architecture

| Dossier | Rôle |
| --- | --- |
| [`adr/`](adr/) | ADR acceptés. Un ADR n'est jamais modifié : un nouvel ADR le remplace. La section 10 du plan liste les ADR à écrire. Phase 1 (fichiers) : [0043](adr/0043-hack-file-api-and-ambient-context.md) API `Hack*` et contexte ambiant, [0044](adr/0044-settings-as-files.md) paramètres en fichiers, [0045](adr/0045-logs-as-files.md) journaux en fichiers, [0046](adr/0046-virtual-proc-and-dev-filesystems.md) `/proc` et `/dev` virtuels. Phase 2 (modèle d'app) : [0047](adr/0047-app-definition-and-generated-manifests.md) manifeste en classe C# et `BuiltInApplications.json`, [0048](adr/0048-system-apps-and-uninstall.md) apps système et désinstallation, [0049](adr/0049-dangerous-capabilities-and-admin-approval.md) capacités dangereuses et approbation administrateur. |

## Archives

| Dossier | Rôle |
| --- | --- |
| [`old/`](old/) | Documentation antérieure au plan de refonte (état d'implémentation, liste d'intégration, guides de sous-systèmes, pièges courants). Toujours utile comme référence technique ; son index est [`old/README.md`](old/README.md). Les documents encore pertinents seront réécrits ici au fil des phases. |
