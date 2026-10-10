# Documentation HackerOS

Point d'entrée de `wasm2/HackerOs/docs/`. Tout ajout, renommage ou retrait de
document dans ce dossier met à jour cet index dans le même changement (voir
[`AGENTS.md`](../../../AGENTS.md)).

## Direction actuelle

| Document | Rôle |
| --- | --- |
| [`ecosystem-rebuild-plan.md`](ecosystem-rebuild-plan.md) | Plan de refonte de l'écosystème validé le 2026-10-10 : API fichier `Hack*`, manifeste en classe C#, paramètres et journaux en fichiers, `/proc` et `/dev`, shell complet, corbeille, permissions dangereuses, apps de base. **À lire en premier.** |

## Décisions d'architecture

| Dossier | Rôle |
| --- | --- |
| [`adr/`](adr/) | ADR acceptés. Un ADR n'est jamais modifié : un nouvel ADR le remplace. La section 10 du plan liste les ADR à écrire. |

## Archives

| Dossier | Rôle |
| --- | --- |
| [`old/`](old/) | Documentation antérieure au plan de refonte (état d'implémentation, liste d'intégration, guides de sous-systèmes, pièges courants). Toujours utile comme référence technique ; son index est [`old/README.md`](old/README.md). Les documents encore pertinents seront réécrits ici au fil des phases. |
