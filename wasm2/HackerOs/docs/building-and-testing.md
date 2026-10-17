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

### Mesure des ressources (ADR 0066)

Deux propriétés MSBuild choisissent le code de mesure livré par les hôtes
(`build/HackerOs.ResourceAccounting.targets`, importé par les trois hôtes) :

| Propriété | Défaut | Effet à `false` |
|---|---|---|
| `HackerOsCpuAccounting` | `true` | mesure du temps CPU (ADR 0067) coupée ; option d'exécution `HackerOs.ResourceAccounting.Cpu.IsSupported=false` |
| `HackerOsMemoryAccounting` | `true` | mesure de la mémoire (ADR 0068) coupée ; option `HackerOs.ResourceAccounting.Memory.IsSupported=false` |

Les deux à `false` : `HackerOs.Platform.ResourceAccounting` n'est plus
référencé, son code n'est pas dans le binaire.

```bash
dotnet build HackerOs.sln -p:HackerOsCpuAccounting=false -p:HackerOsMemoryAccounting=false
```

En ligne de commande (ou dans un `Directory.Build.props`), la propriété vaut
pour tout le graphe ; dans le seul `.csproj` de `test/test` ou du serveur, elle
ne coupe que l'option d'exécution de cet hôte. Au démarrage, la section
`HackerOs:ResourceAccounting` de `appsettings.json` coupe une mesure sans
recompiler : voir [`resource-accounting.md`](resource-accounting.md).

### Conteneur Linux : contournements connus

| Symptôme | Cause | Contournement |
|---|---|---|
| `NU1902` sur `Microsoft.Build.Tasks.Git` traité en erreur | avis de sécurité NuGet sur une dépendance de build | `export NuGetAudit=false` (ou `-p:NuGetAudit=false`) |
| `NU1301` sur la source `local-nupkg` | `Samples/HackerOs.Windowing.SampleHost/nuget.config` cite un dossier local absent ; sous Linux, son chemin `..\..\artifacts\local-nupkg` est lu littéralement | `mkdir -p artifacts/local-nupkg 'Samples/HackerOs.Windowing.SampleHost/..\..\artifacts\local-nupkg'` (dossiers vides, ignorés par git) |
| Page blanche de `test/test` ou de `HackerOs.Server` lancé avec `dotnet run --no-build --urls …` (journal : « Static Web Assets are not enabled ») | sans profil de lancement, l'hôte démarre en `Production` et ne sert pas les actifs des projets référencés hors d'une publication | `ASPNETCORE_ENVIRONMENT=Development` |
| `IOException … being used by another process` dans `obj/…/compressed/` d'Ecosystem | `test/test` et `HackerOs.Server` compilés en même temps : ils partagent Ecosystem | compiler la solution une fois, puis lancer les hôtes avec `--no-build` |

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

## Vérifier les trois hôtes

Une commande compile, lance et parcourt les trois hôtes
([`execution-isolation-plan.md`](execution-isolation-plan.md), section 5.2) :

```bash
build/verify-hosts.sh                 # tout : build, publications, cinq parcours
build/verify-hosts.sh --skip-build    # réutilise les sorties déjà compilées
build/verify-hosts.sh --only server,test-host
```

Prérequis : SDK .NET 10 (`~/.dotnet` est ajouté au `PATH` s'il existe),
`python3` (serveur statique de la PWA publiée), `curl`, `setsid`, et un
Chromium que Playwright peut lancer (dans un conteneur cloud,
`/opt/pw-browsers/chromium` est pris automatiquement, voir
[Chromium pour Playwright](#chromium-pour-playwright)). Le script applique
lui-même les contournements Linux ci-dessus, compile une seule fois puis lance
les hôtes avec `--no-build`, et pose `ASPNETCORE_ENVIRONMENT=Development` pour
les hôtes lancés par `dotnet run` ([`old/common-pitfalls.md`](old/common-pitfalls.md) §4).

Déroulé :

1. `dotnet build` (Debug) d'`OS/HackerOs.Ecosystem`, `test/test`,
   `Server/HackerOs.Server` et `Tests/HackerOs.HostSmoke.Tests` ;
   `dotnet publish` (Release) d'Ecosystem, deux fois (réglages par défaut,
   puis mesures coupées au build).
2. Pour chaque parcours, l'hôte démarre sur un port libre de `127.0.0.1` dans
   son propre groupe de processus : Ecosystem publié derrière un serveur
   statique Python, les deux autres par `dotnet run --no-build`. La base, les
   blobs et les sauvegardes du serveur vont dans un dossier jetable : le
   `Server/HackerOs.Server/hackeros.db` suivi par git n'est jamais modifié.
3. `Tests/HackerOs.HostSmoke.Tests` ouvre l'hôte dans Chromium (contexte
   neuf) : création du profil, rechargement et écran « Sign in », bureau
   (styles scopés appliqués à la barre des tâches, puis au cadre des fenêtres :
   `display: flex` calculé, sinon le bundle `{Projet}.styles.css` de l'hôte
   manque), Terminal avec `ps` puis `cat /proc/self/status` (valeurs de `CpuSource:` et
   `MemSource:` vérifiées ; chaque sortie est lue une fois l'invite suivante
   affichée, car une commande dans un Web Worker écrit en plusieurs messages), System Monitor (le processus du terminal est
   listé), fermeture de chaque fenêtre. Toute erreur de la console ou de la
   page fait échouer le parcours, comme toute ligne `fail:` ou « Unhandled
   exception » du journal de l'hôte (erreurs d'un circuit `InteractiveServer`,
   invisibles côté navigateur).
4. L'hôte est arrêté (SIGTERM au groupe, SIGKILL après 15 s), puis le
   parcours suivant commence. Le script sort avec un code non nul au moindre
   échec et affiche une ligne par étape :

```text
== HackerOS host verification (9d26a77) ==
build HackerOs.Ecosystem         PASS
build test                       PASS
build HackerOs.Server            PASS
build HackerOs.HostSmoke.Tests   PASS
publish ecosystem                PASS
publish ecosystem-build-off      PASS
ecosystem                        PASS  23s
ecosystem-build-off              PASS  21s
ecosystem-settings-off           PASS  21s
test-host                        PASS  38s
server                           PASS  34s
RESULT: PASS
```

| Parcours | Hôte | Mesures (ADR 0066) | `CpuSource:` / `MemSource:` attendus |
|---|---|---|---|
| `ecosystem` | PWA publiée | build et réglage par défaut | `measured` |
| `ecosystem-build-off` | PWA publiée avec `-p:HackerOsCpuAccounting=false -p:HackerOsMemoryAccounting=false` | coupées au build ; aucun `HackerOs.Platform.ResourceAccounting*` ne doit être publié | `simulated` |
| `ecosystem-settings-off` | même publication que `ecosystem`, `wwwroot/appsettings.json` réécrit avec `Enabled: false` pour `Cpu` et `Memory` | coupées au démarrage | `simulated` |
| `test-host` | `test/test` | réglage de l'hôte (activées) | `measured` |
| `server` | `HackerOs.Server` (`InteractiveServer`) | réglage de l'hôte (coupées) | `simulated` |

Sur un arbre sans les interrupteurs de l'ADR 0066
(`build/HackerOs.ResourceAccounting.targets`, section
`HackerOs:ResourceAccounting` d'`appsettings.json`), les deux parcours `-off`
sont marqués `SKIP` et les sources ne sont pas vérifiées.

Journaux, publications et captures d'écran des échecs :
`artifacts/verify-hosts/` (ignoré par git). Sans `HACKEROS_SMOKE_URL`, que
pose le script, le test de démarrage est marqué « skipped » : un
`dotnet test HackerOs.sln` n'en dépend pas. Pour le lancer contre un hôte
démarré à la main :

```bash
HACKEROS_SMOKE_URL=http://127.0.0.1:5000 dotnet test Tests/HackerOs.HostSmoke.Tests \
  --logger "console;verbosity=detailed"
```

Variables facultatives : `HACKEROS_SMOKE_HOST` (nom affiché),
`HACKEROS_SMOKE_EXPECT_CPU_SOURCE` / `HACKEROS_SMOKE_EXPECT_MEM_SOURCE`
(valeur attendue dans `/proc/self/status`), `HACKEROS_SMOKE_ARTIFACTS`
(dossier des captures).

Chaque PR `rebuild/5-*` colle la sortie de `build/verify-hosts.sh`.

## Tester

```bash
dotnet test HackerOs.sln
```

| Suites | Contenu |
|---|---|
| `Tests/*.Tests` (hors E2E) | Tests unitaires et de composants (xUnit, bUnit), un projet par projet de code. Rapides, sans navigateur. |
| `Tests/HackerOs.AppSdk.Web.Tests` | ASP.Hack : routeur, pipeline d'un site compilé avec le générateur de routes, générateur piloté sur des extraits (`HOSWEB*`), page hôte et `blazor.boot.json` d'un site Blazor. |
| `Tests/HackerOs.AppSdk.Web.Blazor.Tests` | Sites Blazor : routeur (`HackSiteRouter`), gestionnaire de navigation de l'onglet, `HackWebSite`. |
| `Tests/HackerOs.Samples.Intranet.Tests` | Exemple Intranet en mémoire (`HackWebHost.CreateApplication`) : vues `.cshtml` et `.razor`, `wwwroot`, session, anti-falsification, CRUD par formulaires, API REST, page hôte du tableau Blazor. Le parcours dans Chromium est `BrowserIntranetTests` (UI E2E). |
| `Tests/HackerOs.Tools.ManifestGenerator.Tests` | Builds MSBuild réels des projets `Tests/ManifestGenerator.Fixtures.*` : génération, agrégation, frontière du SDK. Plus lents. |
| `Tests/HackerOs.E2E.Tests`, `HackerOs.UI.E2E.Tests`, `HackerOs.Pwa.E2E.Tests` | Playwright contre l'OS lancé dans Chromium. Pièges connus : [`old/common-pitfalls.md`](old/common-pitfalls.md) §4 à §6. |
| `Tests/HackerOs.HostSmoke.Tests` | Démarrage d'un hôte déjà lancé, piloté par `build/verify-hosts.sh` (voir plus haut) ; « skipped » sans `HACKEROS_SMOKE_URL`. |

Tester un seul projet ou un seul test :

```bash
dotnet test Tests/HackerOs.AppSdk.Tests
dotnet test Tests/HackerOs.Platform.Core.Tests --filter "FullyQualifiedName~ShellInterpreterTests"
```

### Chromium pour Playwright

Chaque version de `Microsoft.Playwright` attend une révision précise de
Chromium (ou Google Chrome pour `Channel = "chrome"`). Toutes les suites
lancent le navigateur par `ChromiumLauncher` (`Tests/HackerOs.E2E.Support`) :

1. `HACKEROS_CHROMIUM_EXECUTABLE`, s'il est posé, est utilisé tel quel ;
2. sinon, le lancement prévu par le test ;
3. s'il échoue, nouvel essai avec `$PLAYWRIGHT_BROWSERS_PATH/chromium` puis
   `/opt/pw-browsers/chromium` (le lien des conteneurs cloud), sans canal.

Dans un conteneur cloud, **ne jamais lancer `playwright install`** : la
révision fournie sous `/opt/pw-browsers` sert de repli.

Pour tester une app sans OS, `Tests/HackerOs.Tests.Support` fournit
`MinimalAppExecutionContext` et des passerelles factices (fichiers, horloge,
journaux, processus, intentions, environnement, shell). Exemple : voir le
[guide du SDK](sdk-developer-guide.md#8-tester-une-app).

### État connu

- **GitHub Actions** ne tourne plus (les jobs échouent sans exécuteur) : un
  build et des tests locaux verts font foi avant de fusionner.
- Dans un conteneur cloud, les suites Playwright tournent sur le Chromium de
  `/opt/pw-browsers` ([repli](#chromium-pour-playwright)). État au
  2026-10-16 (après `dotnet build HackerOs.sln -c Release`, une suite à la
  fois) :
  - `HackerOs.E2E.Tests` : 16/16 ;
  - `HackerOs.UI.E2E.Tests` : 10/10 depuis `rebuild/5-ui-a11y-mkdir`, 15/15 sur
    `web/base` depuis son portage (`web/13`, 2026-10-17)
    (2026-10-16 : contraste de `--hos-text-muted` du thème HackerOS, états
    ARIA écrits en `"true"`/`"false"`, voir
    [`old/common-pitfalls.md`](old/common-pitfalls.md) §8 ; le test du choix
    Mobile attend la bascule avant de recharger, car l'écriture du réglage
    dans `~/.config/hackeros` n'est pas instantanée : environ 2 s avant
    `web/15`, 235 ms depuis, voir
    [`filesystem-path-lookup.md`](filesystem-path-lookup.md)) ;
  - `HackerOs.Pwa.E2E.Tests` : 0/5, le conteneur n'a pas d'IPv6 et la suite
    se lie volontairement à `[::1]` (le service worker refuse
    `127.0.0.1` et `localhost`) : `SocketException : Address family not
    supported by protocol`.
- Phase 5 : chaque PR `rebuild/5-*` colle la sortie de
  `build/verify-hosts.sh` ([Vérifier les trois hôtes](#vérifier-les-trois-hôtes)).
- Un test de `AppInstallationService` est instable (environ une fois sur
  douze), sur la base aussi.

## Tâches de ce document

- [x] Prérequis et installation de .NET dans un conteneur.
- [x] Propriétés `HackerOsCpuAccounting` et `HackerOsMemoryAccounting` (ADR 0066).
- [x] Build de la solution et contournements Linux connus.
- [x] Lancement des trois hôtes et publication de la PWA.
- [x] Suites de tests, filtrage, outils de test des apps, état connu.
- [x] `build/verify-hosts.sh` : les trois hôtes, les combinaisons des mesures, le test de démarrage (`rebuild/5-host-smoke`).
- [x] Repli des suites Playwright sur le Chromium du conteneur et leur état dans un conteneur.
