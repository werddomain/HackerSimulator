# ADR 0101 : suppressions synchronisées, conflits résolus par l'utilisateur, synchro périodique et domaines de synchro restants

## Statut

**Proposé** le 2026-10-17 (chantier `v3/1-adr`, pour le chantier `v3/9-sync`).
Tout est accepté sauf le §2 (forme de la résolution des conflits), qui applique
le choix recommandé en attendant la réponse de Benoit à la question posée dans
le fil du chantier `v3/1-adr`.

- **Complète :** [ADR 0025](0025-record-synchronization-envelope-and-conflict-model.md)
  (enveloppe, pierres tombales, table des conflits) et
  [ADR 0030](0030-filesystem-sync.md) (synchro de `/home/{user}`).
- **Remplace :** la partie AppCatalog de l'[ADR 0033](0033-appcatalog-and-fileassociations-sync.md)
  (§3 ci-dessous). L'ADR 0029 (synchro des réglages) est déjà remplacé par
  l'ADR 0044.
- **Plan :** [`wasm-v3-completion-plan.md`](../wasm-v3-completion-plan.md),
  ligne `v3/9-sync` ; manque R18 ; décision différée n° 7.

## Contexte

- Le serveur gère les pierres tombales (`SyncService.cs:141`) et la résolution
  des conflits (`SyncService.cs:162`). Côté client, `FileSystemSyncService` et
  `FileAssociationsSyncService` envoient toujours `IsTombstone: false`
  (`IFileSystemSyncService.cs:119`, `IFileAssociationsSyncService.cs:73`) : une
  suppression locale ne se propage jamais.
- `ISyncClient.ResolveConflictAsync` existe mais rien ne l'appelle. Les conflits
  sont seulement gardés en mémoire (`UnresolvedConflicts`) et perdus au
  rechargement.
- La synchro se lance seulement à la main (`AppServerConnectionGateway.SyncAsync`).
  Aucune minuterie.
- Il n'y a pas de client du domaine `app_catalog` ; il n'y a plus de client
  `settings` depuis l'ADR 0044.

## Décision

### 1. Suppressions

- Le client garde, pour chaque entrée déjà synchronisée, son état d'enregistrement
  (magasin IndexedDB des états, version 4 du schéma). Quand une telle entrée est
  supprimée, il pousse une enveloppe `IsTombstone: true` avec la révision
  suivante. Une entrée jamais synchronisée ne laisse pas de pierre tombale.
- Mettre un fichier à la corbeille est un déplacement dans
  `~/.local/share/Trash` (ADR 0052), donc un renommage synchronisé, pas une
  suppression. Vider la corbeille produit les pierres tombales.
- Une pierre tombale **reçue** ne supprime pas définitivement : le fichier local
  est mis à la corbeille de l'utilisateur, avec l'origine « supprimé sur un
  autre appareil » dans son `.trashinfo`. L'utilisateur peut donc le récupérer.
- Le serveur garde une pierre tombale **90 jours**. Un appareil resté hors ligne
  plus longtemps refait une synchro complète (comparaison des hash) au lieu
  d'une synchro par curseur ; c'est la profondeur d'historique hors ligne de la
  décision différée n° 7.
- `FileAssociationsSyncService` pousse aussi une pierre tombale si
  `/etc/hackeros/file-associations.json` est supprimé.

### 2. Conflits : garder les deux copies (choix recommandé, question ouverte)

- Un conflit sur un fichier ne bloque rien et ne perd rien, comme Syncthing : la
  version du serveur prend le chemin d'origine et la version locale est
  renommée `<nom>.sync-conflict-<AAAAMMJJ-HHMMSS>-<appareil>.<ext>` dans le même
  dossier.
- Une notification signale le conflit. **Paramètres → Synchronisation** liste
  les conflits ouverts avec trois choix : « Garder la mienne », « Garder celle de
  l'autre appareil », « Garder les deux » ; le choix appelle
  `ResolveConflictAsync` (`ClientWins`, `ServerWins`, ou rien côté serveur pour
  les deux) et supprime ou garde la copie renommée.
- Les conflits ouverts sont enregistrés dans
  `~/.local/state/hackeros/sync-conflicts.json` (état local, jamais synchronisé,
  ADR 0044), donc gardés après un rechargement.
- Les domaines où le serveur fait autorité (accords, ADR 0025) se résolvent
  seuls en `ServerWins`, sans rien demander.

### 3. Domaines restants

- **`filesystem`** : `/home/{user}` sauf `~/.local/state` (ADR 0030, 0044).
  Les réglages de l'utilisateur (`~/.config`) et son choix d'apps désinstallées
  (`~/.config/hackeros/apps.config`, ADR 0048) voyagent par ce domaine, comme
  n'importe quel fichier.
- **`file_associations`** et **`grants`** : inchangés, avec les pierres
  tombales.
- **`app_catalog`** : **pas de client**. Le choix de l'utilisateur est déjà dans
  `~/.config` (voir plus haut), et l'état machine (`/etc/hackeros/apps.config`)
  reste propre à chaque appareil, comme les paquets installés sur deux machines
  Linux. Le serveur garde le domaine (aucune rupture de contrat) mais l'OS ne
  l'utilise pas. Cela remplace la partie AppCatalog de l'ADR 0033 et le « client
  du domaine AppCatalog » du plan.
- **`settings`** : plus de client depuis l'ADR 0044 ; le serveur garde le
  domaine pour la même raison.

### 4. Synchro périodique

- Quand l'OS est connecté au serveur, un service de la plateforme synchronise
  toutes les **5 minutes**, à chaque reconnexion, et 10 secondes après la dernière
  modification d'un fichier synchronisé (regroupement des écritures).
- L'intervalle se règle dans `/etc/hackeros/sync.config` (administrateur) ;
  l'utilisateur peut suspendre sa synchro dans `~/.config/hackeros/sync.config`.
- Hors ligne ou sans serveur, le service ne fait rien et l'OS marche comme
  avant (l'Ecosystem ne dépend jamais du serveur).
- Chaque passage est écrit dans `/var/log/syslog` (domaines, envoyés, reçus,
  conflits).

## Conséquences

- `v3/9` modifie les deux services de synchro du client, ajoute le service
  périodique, la page de Paramètres, l'enregistrement des conflits et des tests
  client et serveur (suppression propagée, pierre tombale reçue mise à la
  corbeille, conflit des deux côtés, reprise après rechargement, appareil
  hors ligne plus de 90 jours).
- Le serveur purge les pierres tombales de plus de 90 jours.
- La doc de connexion au serveur et de synchro (`system-administration.md`,
  `trash.md`, `settings-files.md`) est mise à jour.
