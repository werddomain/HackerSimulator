# Administration du système dans le SDK

## Objet

Donne aux apps système les opérations d'administration qui passaient par des
services internes (ADR 0056 §8 à §11, ajusté par l'ADR 0060) :

- lire et écrire les documents de paramètres de l'OS ;
- administrer les capacités dangereuses (politique, décisions machine) ;
- se connecter au serveur optionnel et synchroniser ;
- redémarrer ou réinitialiser la machine ;
- lire l'instantané brut du stockage.

Paramètres et Diagnostic utilisent ces API et ne référencent plus Platform
Core ni Infrastructure.Browser.

## Architecture

| Couche | Fichiers |
|---|---|
| SDK (contrats, façades) | `Shared/HackerOs.AppSdk/Administration/` (`HackPermissionAdministration`, `HackServerConnection`, `HackSystem`, `HackStorageDiagnostics` et leurs contrats), `Shared/HackerOs.AppSdk/Settings/IAppSystemSettingsGateway.cs`, `SettingsDocumentStores.cs` |
| Platform Core (passerelles) | `Platform/HackerOs.Platform.Core/Administration/` : `AppSystemSettingsGateway`, `AppPermissionAdministrationGateway`, `AppServerConnectionGateway`, `AppSystemPowerGateway`, `AppStorageDiagnosticsGateway`, `AdministratorGate`, `SystemAdministrationGatewayServices` |
| Platform Core (services) | `Power/SystemPowerService.cs` (séquence d'arrêt), `Settings/OsSettingsDocumentRegistry.cs`, `Policy/AppPermissionAdministration.cs` (`ApplyMachineDecisionAsync`, `ApplyPolicyAsync`) |
| Hôte Ecosystem | `Administration/BrowserSystemPowerDevice.cs` (`location.reload`, suppression de la base IndexedDB), `Administration/IndexedDbStorageInspector.cs` (adaptateur de `IndexedDbInspectorService`) |
| SDK Blazor | `Interop/BrowserClipboard.cs` et `wwwroot/clipboard.js` (presse-papiers de l'hôte), `[Obsolete]` depuis l'ADR 0065 : Diagnostic copie par `HackClipboard`, que l'OS recopie vers l'hôte ([`clipboard.md`](clipboard.md)) |

`AppExecutionContextFactory` reçoit un `SystemAdministrationGatewayServices`
(résolutions paresseuses) et crée, pour chaque instance, les cinq passerelles
avec les capacités et le principal de l'app. `AdministratorGate` appelle
l'agent d'authentification de l'OS
([`admin-authentication-agent.md`](admin-authentication-agent.md)) : l'app ne
voit jamais le mot de passe, root par `sudo` passe sans demande, et l'OS agit
ensuite lui-même.

## API

### Documents de l'OS

```csharp
await using HackSettingsDocument appearance = await HackSettings.OpenSystemAsync(AppContext, "appearance");
await appearance.SetAsync("accent", "cyan");                               // couche utilisateur
await appearance.SetManyAsync(values, SettingLayer.System);                // /etc : une seule approbation
```

Voir [`settings-files.md`](settings-files.md#documents-de-los-hacksettingsopensystemasync-adr-0056-8).

### Permissions (`permissions.manage`)

| Méthode | Effet | Administrateur |
|---|---|---|
| `GetPolicyAsync`, `GetOverviewAsync` | politique et état de chaque capacité dangereuse de chaque app | non |
| `SetPolicyAsync(Prompt / Block)` | écrit `/etc/hackeros/security.config`, journal `capability.policy` | oui (`permissions.policy`) |
| `SetMachineDecisionAsync(app, capacité, Allow / Deny / Ask)` | écrit `/etc/hackeros/app-permissions.config`, journal `capability.approve` / `capability.revoke` | oui (`permissions.machine-decision`) |
| `RemoveOwnDenialAsync(app, capacité)` | retire le refus de l'utilisateur, la question revient au lancement | non |

Une app système ou une capacité non déclarée par l'app est refusée **avant**
toute demande d'identifiants.

### Serveur (`server.connection.manage`)

`GetStateAsync`, `ConnectAsync`, `CreateAccountAsync`, `DisconnectAsync`,
`SyncAsync(domaine, sens)`, `GetConflictsAsync`. Domaines : `FileSystem`
(push, pull), `Grants` (pull seulement : `ArgumentException` sinon),
`FileAssociations` (push, pull). Sans serveur configuré : `NotConnected`, et
la synchronisation ne fait rien. Les erreurs réseau deviennent
`HackServerConnectionException`.

`GetRelayPolicyAsync` rend la politique du relais réseau du serveur connecté
(`HackServerRelayPolicy` : ports, plages privées, quotas, flux ouverts, apps
accordées, avertissements), ou `null` sans serveur ; Paramètres l'affiche
dans sa section du serveur ([`server-network-relay.md`](server-network-relay.md)).

ADR 0101 (chantier `v3/9-sync`) : `GetConflictsAsync` lit les conflits en
attente enregistrés dans `~/.local/state/hackeros/sync-conflicts.json`
(`HackSyncConflict` porte `DetectedAt`) ; `ShowConflictsAsync` ouvre le
dialogue de conflit de l'OS ; `GetSyncStatusAsync` rend un `HackSyncStatus`
(synchro périodique active, suspendue, en ligne, dernier passage, envoyés,
reçus, dernière erreur, intervalle) ; `SetSyncPausedAsync` suspend ou reprend
la synchro de l'utilisateur (`~/.config/hackeros/sync.config`) ;
`SyncNowAsync` lance un passage complet. Les suppressions, les conflits et la
synchro périodique sont décrits dans
[`synchronization.md`](synchronization.md).

### Alimentation (`system.power`)

`HackSystem.RebootAsync` et `HackSystem.FactoryResetAsync` demandent toujours
un administrateur (`system.reboot`, `system.factory-reset`). Séquence de
`SystemPowerService`, comme un `shutdown` :

1. entrée d'audit ;
2. vidage des journaux ;
3. fermeture de la session (`ISessionService.ShutdownAsync`) ;
4. pour la réinitialisation, effacement du stockage ;
5. redémarrage de l'hôte.

### Stockage (`system.storage.inspect`)

`HackStorageDiagnostics.GetSnapshotAsync` rend un `HackStorageSnapshot`
(magasins, nombre d'enregistrements, JSON, chemins résolus, liens) et
`ToExportJson()`. L'instantané contourne les droits des fichiers : c'est
pourquoi Diagnostic, app non système, demande un administrateur à son premier
lancement.

`GetWebSiteDatabasesAsync` liste les bases IndexedDB des sites (`hackweb.<id>` :
version, magasins, nombre d'enregistrements, taille estimée) et
`DeleteWebSiteDatabaseAsync(name)` en supprime une, avec la même capacité
(ADR 0088 §4, [`asp-hack-site-data.md`](asp-hack-site-data.md)). Diagnostic les
montre sous *Website databases*.

## Décisions

- Voir l'ADR 0060 : propriété `SystemPower` (pas `System`),
  `HackPermissionDecision.Ask`, couche système écrite avec les droits de
  l'appelant, `SetManyAsync`, presse-papiers de l'hôte pour Diagnostic
  (ce dernier point est remplacé par l'ADR 0065 : `HackClipboard` et
  `clipboard.write`).
- `IndexedDbInspectorService` reste dans Infrastructure.Browser ;
  l'adaptateur vers le SDK vit dans l'hôte Ecosystem.
- L'ancien chemin par mot de passe de `AppPermissionAdministration` est gardé
  pour ses tests ; les apps passent par l'agent.

## Tests

`Tests/HackerOs.Platform.Core.Tests/Administration/SystemAdministrationGatewayTests.cs` :
document de l'OS lu et écrit sans capacité, couche système refusée à un
utilisateur simple puis écrite sous root en une édition, document inconnu ;
capacités exigées ; refus de l'agent sans écriture ; décision machine et
politique journalisées ; app système refusée avant toute demande ; serveur
absent ; `Grants` en pull seulement ; redémarrage refusé ; réinitialisation
dans l'ordre fermeture, effacement, redémarrage ; inspection du stockage.

## Liste des tâches

Voir [`app-sdk.md`](app-sdk.md#liste-des-tâches-du-chantier-rebuild4-sdk-system-admin).
