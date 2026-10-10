# Journaux en fichiers (`/var/log`)

Mise en œuvre de l'[ADR 0045](adr/0045-logs-as-files.md) et de la section 5.3 du
[plan de refonte](ecosystem-rebuild-plan.md).

## Rôle

Les journaux de HackerOS sont de vrais fichiers texte. `cat`, `tail`, `grep` et
Error Log Viewer les lisent avec les droits de fichier ordinaires.

| Fichier | Contenu | Mode |
|---|---|---|
| `/var/log/syslog` | `ILogger<T>` de la plateforme, démarrage (`kernel`), pannes de l'hôte (`ecosystem-host`), lignes perdues (`logging`) | `system:system 0644` |
| `/var/log/auth.log` | `IAuditLog` (`session.login`, `session.elevate`…) | `system:system 0600` |
| `/var/log/apps/<appId>.log` | `IAppExecutionContext.Logging` de l'app | `system:system 0644` |

## Architecture

```text
ILogger<T> ──► HackerOsDiagnosticLoggerProvider ─┐
IAppLoggingGateway ──► AppLoggingGateway ────────┼─► LogFileWriter ─► IFileSystemService ─► /var/log/…
IAuditLog ──► FileAuditLog ──────────────────────┘        │ (canal borné, vidage groupé, rotation)
HostExceptionReporter ───────────────────────────┘        ▼
                                     IDiagnosticSink (tampon mémoire des entrées récentes)
```

- `LogLineFormat` (`Shared/HackerOs.Simulation.Abstractions/Diagnostics/`) :
  `Format`, `FormatAudit` et `TryParse`. Une entrée tient sur une ligne :
  `2026-10-10T03:11:42.123Z WARN  org.hackeros.text-editor[42]: message {clé=valeur}`.
  Le message échappe `\`, `\n`, `\r`, `\t` et aussi `{` (en `\{`), pour qu'un
  message ne soit jamais relu comme un bloc de propriétés. Dans ce bloc, les
  clés et valeurs échappent en plus `,`, `=` et `}`. Les espaces d'une source
  deviennent `_`.
- `LogFileTarget` : `Syslog`, `AuthLog`, `ForApp(appId)`, avec chemin et mode.
- `LogRotationPolicy` : `MaxFileBytes` = 1 Mio, `MaxArchives` = 5. Rotation à
  la `logrotate` (`.5` supprimé, `.4`→`.5` … `<f>`→`.1`, nouveau `<f>` vide).
  Un fichier vide ne tourne jamais : un lot plus gros que 1 Mio est écrit entier.
- `LogFileWriter` (singleton, `Platform.Core/Diagnostics/`) :
  - `Enqueue(target, line)` et `Write(target, severity, source, message, pid)`,
    synchrones, jamais bloquants. Canal de 10 000 lignes ; au-delà, les plus
    anciennes sont perdues et une ligne `WARN logging: <n> entries dropped` est
    écrite dans `syslog` au vidage suivant.
  - Vidage toutes les 1 s de temps simulé, dès 100 lignes en attente, à la
    déconnexion (`SessionLoggedOutEvent`) et à `DisposeAsync`. Un seul vidage à
    la fois ; une seule écriture par fichier et par vidage.
  - `AttachAsync(IFileSystemService)` est appelé par `EcosystemBootCoordinator`
    juste après `EnsureRootAsync`. Avant, les lignes attendent dans le canal.
    Le rédacteur crée lui-même `/var/log` et `/var/log/apps` s'ils manquent.
  - Un échec d'écriture n'est jamais journalisé dans un fichier : il est noté une
    fois par fichier (catégorie `logging`) dans le tampon mémoire.
- `FileAuditLog` remplace `BoundedAuditLog` dans la composition ; `Entries` reste
  servi par un tampon de 512 entrées.
- `SimulationClockDriver` (`Platform.Core/Time/`) : l'adaptateur de production
  prévu par l'ADR 0012, qui n'existait pas. Il fait avancer `ManualSimulationClock`
  depuis un minuteur de la plateforme (au plus 50 tics par battement, le reste
  est abandonné quand l'onglet est ralenti). Sans lui, le vidage périodique, l'horloge
  de la barre des tâches et les délais d'arrêt des processus ne s'exécutaient
  jamais. Il est démarré par `EcosystemBootCoordinator`. `ManualSimulationClock`
  protège désormais sa file de rappels par un verrou.

## Retraits

- `IAppDiagnosticsGateway`, `AppDiagnosticsGateway` et `IAppExecutionContext.Diagnostics`
  sont supprimés. Lire les journaux, c'est lire des fichiers.
- `IPersistentDiagnosticRepository` et `IndexedDbDiagnosticRepository` ne sont
  plus enregistrés ; l'audit IndexedDB n'était déjà pas branché. Les classes et
  leurs magasins restent, et la version du schéma IndexedDB ne change pas.
- `diagnostics.read` et `diagnostics.clear` restent dans le catalogue mais ne
  sont plus évaluées (ADR de permissions de la phase 2).
- L'hôte `OS/HackerOs.Ecosystem` ne retire plus son `ILoggerProvider` : toutes ses
  dépendances sont des singletons. Le serveur garde sa composition (le pont
  `ILogger` y reste retiré, aucun système de fichiers n'y est attaché).

## Error Log Viewer

- Manifeste : `filesystem.system.read` et `filesystem.system.write` (au lieu de
  `diagnostics.*`).
- `LogFileReader` liste `syslog` puis `apps/*.log` (et les archives `.1` à `.5`
  si la case « Archives » est cochée), lit et analyse avec `LogLineFormat.TryParse`,
  et compte les lignes étrangères. Il ne lit jamais `auth.log` (`sudo cat` pour ça).
- Suivi en direct par `AppContext.Watch` sur `/var/log` et `/var/log/apps`.
- « Truncate » vide le journal sélectionné (jamais une archive). Pour un
  utilisateur ordinaire c'est refusé, la fenêtre propose l'approbation
  administrateur (ADR 0042), puis un second clic réussit.
- **Écart temporaire avec l'ADR 0045 §6 :** le lecteur passe par
  `IAppFileSystemGateway` et non par `HackFile`/`HackFileSystemWatcher`, qui
  arrivent avec la branche `rebuild/1-file-api` (ADR 0043). Le passage à `Hack*`
  se fera une fois cette API mergée ; le comportement ne change pas.

## Tests de contrat

- `Tests/HackerOs.Platform.Core.Tests/Diagnostics/LogLineFormatTests.cs` : format,
  exemple exact de l'ADR, échappements, propriétés, audit, rejet des lignes étrangères.
- `.../LogFileWriterTests.cs` : création et modes, vidage périodique et par seuil,
  une écriture par fichier, rotation et archives, lot géant, perte de lignes,
  échecs non récursifs, déconnexion, arrêt, droits de lecture.
- `.../LogProducersTests.cs` : `ILogger`, `AppLoggingGateway`, `FileAuditLog`.
- `Tests/HackerOs.Platform.Core.Tests/Time/SimulationClockDriverTests.cs`.
- `Tests/HackerOs.Apps.ErrorLogViewer.Tests/` : manifeste et `LogFileReader` sur un
  vrai `AppFileSystemGateway`.

## Liste des tâches

- [x] `LogLineFormat` (`Format`, `FormatAudit`, `TryParse`).
- [x] `LogFileTarget`, `LogRotationPolicy` (1 Mio × 5).
- [x] `LogFileWriter` : canal borné, vidage groupé, rotation, pas de récursion.
- [x] `ILogger` → `syslog`, `IAppLoggingGateway` → `apps/<appId>.log`, `IAuditLog` → `auth.log`.
- [x] `HostExceptionReporter` sur `syslog`.
- [x] Attache au démarrage et ligne `kernel: boot` ; `/var/log/apps` créé par `FileSystemSeeder`.
- [x] `SimulationClockDriver` pour que le temps simulé avance.
- [x] Retrait de `IAppDiagnosticsGateway` et des dépôts IndexedDB de la composition.
- [x] Error Log Viewer lit les fichiers.
- [x] Tests de contrat.
- [ ] Passer Error Log Viewer sur `HackFile` après le merge de l'ADR 0043.
