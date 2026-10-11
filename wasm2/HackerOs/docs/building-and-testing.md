# Compiler, lancer et tester HackerOS

## Objet

Comment compiler la solution `wasm2/HackerOs`, lancer l'OS dans le navigateur
et exécuter les tests, y compris dans un conteneur Linux sans outils
préinstallés. Pour la structure du code, voir
[`project-overview.md`](project-overview.md).

## Prérequis

- **SDK .NET 10** (`global.json` : `10.0.100`, `rollForward: latestFeature`).
  Dans un conteneur sans .NET :

  ```bash
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet
  export PATH="$HOME/.dotnet:$PATH"
  ```

- **Node.js** seulement pour régénérer le bundle xterm.js
  ([`terminal-emulator.md`](terminal-emulator.md)) ou importer des icônes
  (`Tools/icon-import`). Le build .NET n'en a pas besoin.
- **Playwright et Chromium** seulement pour les suites E2E.

## Compiler

Toutes les commandes se lancent depuis `wasm2/HackerOs`.

```bash
dotnet build HackerOs.sln
```

Le build applique les règles du projet (avertissements en erreurs, actifs en
ligne interdits, manifestes régénérés, frontière du SDK) : voir
[`project-overview.md`](project-overview.md#règles-vérifiées-au-build). Un
`manifest.json` d'app modifié par le build est un changement à committer.

### Conteneur Linux : contournements connus

| Symptôme | Cause | Contournement |
|---|---|---|
| `NU1902` sur `Microsoft.Build.Tasks.Git` traité en erreur | avis de sécurité NuGet sur une dépendance de build | `export NuGetAudit=false` (ou `-p:NuGetAudit=false`) |
| `NU1301` sur la source `local-nupkg` | `Samples/HackerOs.Windowing.SampleHost/nuget.config` cite un dossier local absent ; sous Linux, son chemin `..\..\artifacts\local-nupkg` est lu littéralement | `mkdir -p artifacts/local-nupkg 'Samples/HackerOs.Windowing.SampleHost/..\..\artifacts\local-nupkg'` (dossiers vides, ignorés par git) |

## Lancer l'OS

| Hôte | Commande | Usage |
|---|---|---|
| PWA autonome | `dotnet run --project OS/HackerOs.Ecosystem` | L'hôte livré, sans serveur. |
| Hôte de débogage | `dotnet run --project test/test` | Débogueur WebAssembly du navigateur ou de l'IDE ([`old/webassembly-debugging.md`](old/webassembly-debugging.md)). `dotnet run --project test/test -- --test-demo` affiche le bouton qui crée les fichiers de test E2E ([`old/e2e-test-demo-mode.md`](old/e2e-test-demo-mode.md)). |
| Serveur optionnel | `dotnet run --project Server/HackerOs.Server` | Synchronisation, comptes, relais réseau, et interface servie en `InteractiveServer`. |

Publier la PWA :

```bash
dotnet publish OS/HackerOs.Ecosystem -c Release -o artifacts/ecosystem
python3 -m http.server -d artifacts/ecosystem/wwwroot 8080   # n'importe quel serveur statique
```

Au premier démarrage, l'OS demande de créer le profil administrateur local,
puis affiche le bureau. Les données vivent dans l'IndexedDB du navigateur :
effacer les données du site remet la machine à neuf.

## Tester

```bash
dotnet test HackerOs.sln
```

| Suites | Contenu |
|---|---|
| `Tests/*.Tests` (hors E2E) | Tests unitaires et de composants (xUnit, bUnit), un projet par projet de code. Rapides, sans navigateur. |
| `Tests/HackerOs.Tools.ManifestGenerator.Tests` | Builds MSBuild réels des projets `Tests/ManifestGenerator.Fixtures.*` : génération, agrégation, frontière du SDK. Plus lents. |
| `Tests/HackerOs.E2E.Tests`, `HackerOs.UI.E2E.Tests`, `HackerOs.Pwa.E2E.Tests` | Playwright contre l'OS lancé dans Chromium. Pièges connus : [`old/common-pitfalls.md`](old/common-pitfalls.md) §4 à §6. |

Tester un seul projet ou un seul test :

```bash
dotnet test Tests/HackerOs.AppSdk.Tests
dotnet test Tests/HackerOs.Platform.Core.Tests --filter "FullyQualifiedName~ShellInterpreterTests"
```

Pour tester une app sans OS, `Tests/HackerOs.Tests.Support` fournit
`MinimalAppExecutionContext` et des passerelles factices (fichiers, horloge,
journaux, processus, intentions, environnement, shell). Exemple : voir le
[guide du SDK](sdk-developer-guide.md#8-tester-une-app).

### État connu

- **GitHub Actions** ne tourne plus (les jobs échouent sans exécuteur) : un
  build et des tests locaux verts font foi avant de fusionner.
- Dans un conteneur, les suites E2E échouent aussi sur la branche de base
  (version de Chromium attendue par Playwright absente) : comparer avec la
  base plutôt que conclure à une régression.
- Un test de `AppInstallationService` est instable (environ une fois sur
  douze), sur la base aussi.

## Tâches de ce document

- [x] Prérequis et installation de .NET dans un conteneur.
- [x] Build de la solution et contournements Linux connus.
- [x] Lancement des trois hôtes et publication de la PWA.
- [x] Suites de tests, filtrage, outils de test des apps, état connu.
