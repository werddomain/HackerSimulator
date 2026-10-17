# Paquets NuGet, version du SDK et modèles

## Objet

Permettre à un autre dépôt (le jeu, ADR 0092) de construire des apps HackerOS
**et** un hôte d'OS complet avec ses propres apps, sans copier ce dépôt
([ADR 0098](adr/0098-sdk-versions-packages-and-manifest-checks.md) §1, §2, §6).
Tout ce qui est publié porte une seule version, sort en paquets NuGet dans un
flux **local** (`artifacts/local-nupkg`, jamais nuget.org), et cinq modèles
`dotnet new` créent les projets.

Les vérifications faites au chargement (version du SDK, commandes en double,
empreintes des ressources, ADR 0098 §3 à §5) sont décrites dans
[`app-model.md`](app-model.md#version-du-sdk-commandes-intégrité-adr-0098-3-5).

## Version unique

- `build/Versions.props` définit `HackerOsVersion` (aujourd'hui
  `0.9.0-preview.1`), importé par `Directory.Build.props` : chaque projet de la
  solution porte ce `Version`.
- Jusqu'au gel du SDK 1.0 (chantier `v3/15`) : `0.9.0-preview.<n>`, `n` augmenté
  de un par chaque chantier qui change une API publiée. Ensuite SemVer
  (ADR 0019).
- `HackerOsSdk.Version` (`HackerOs.App.Abstractions`) est cette version à
  l'exécution (version informative de l'assembly, sans métadonnées `+sha`).
  Le générateur l'écrit comme `sdkCompatibility.minimumVersion` de chaque app ;
  `AppCatalog` refuse une app dont la plage n'accepte pas le SDK de l'hôte (même
  majeure que le minimum, minimum ≤ SDK ≤ maximum).

## Paquets

Un projet est publié quand il importe `Platform/Packaging.props` ;
`Directory.Build.props` rend tous les autres non empaquetables. `Packaging.props`
apporte les métadonnées communes (auteur, licence, dépôt, SourceLink, symboles
`.snupkg`, documentation XML) et l'**analyseur d'API publique** : chaque
bibliothèque liste sa surface dans `PublicAPI.Shipped.txt` /
`PublicAPI.Unshipped.txt` (tout est encore « Unshipped » jusqu'au gel,
ADR 0098 §8). Les avertissements de documentation XML sont des erreurs
(`TreatWarningsAsErrors`), donc chaque membre public est documenté.

| Paquets | Contenu |
|---|---|
| `HackerOs.App.Abstractions`, `HackerOs.Simulation.Abstractions`, `HackerOs.Theming.Abstractions`, `HackerOs.AppSdk`, `.Blazor`, `.Terminal`, `.Web`, `.Web.Blazor`, `.FileView`, `.Icons`, `.VirtualKeyboard` | le SDK public, ce qu'une app référence |
| `HackerOs.App.Build` | l'intégration de build (voir plus bas) |
| `HackerOs.Platform.Core`, `.Blazor`, `.ResourceAccounting`, `.WorkerHost`, `HackerOs.Infrastructure.Browser`, `HackerOs.Windowing.Abstractions`, `.Core`, `.Blazor`, `HackerOs.Taskbar.Blazor`, `HackerOs.Theming.Blazor`, `HackerOs.MobileShell.Blazor`, `HackerOs.Server.Contracts` | la plateforme, ce qu'un hôte compose (`Server.Contracts` est une dépendance de `Platform.Core`) |
| `HackerOs.Ecosystem.Host` | l'OS lui-même : bureau, démarrage, services, adaptateurs du navigateur, jetons de design |
| `HackerOs.Apps.*` (13 apps), `HackerOs.Commands` | les apps de base, chacune avec ses manifestes |
| `HackerOs.Templates` | les modèles `dotnet new` |

### Produire et consommer le flux local

```bash
build/pack-local.sh                 # ou build/pack-local.ps1 ; --output <dossier> pour un autre flux
```

Le script construit un filtre de solution (`artifacts/packages.slnf`) des
projets qui importent `Packaging.props`, lance `dotnet pack -c Release` vers
`artifacts/local-nupkg`, puis retire cette version de chaque paquet `HackerOs.*`
du cache NuGet global : sans cela, un nouveau paquet de même version serait
masqué par l'ancien.

Dans l'autre dépôt, `build/nuget.config.sample` sert de modèle : une source
`hackeros-local` vers ce dossier, nuget.org pour le reste, et un
`packageSourceMapping` qui ne prend les `HackerOs.*` que du flux local.

### `HackerOs.App.Build`

Paquet sans assembly (`Tools/HackerOs.App.Build`), à référencer avec
`PrivateAssets="all"` pour qu'il ne se propage pas aux consommateurs du projet :

| Chemin dans le paquet | Rôle |
|---|---|
| `buildTransitive/HackerOs.App.Build.targets` | pose `HackerOsBuildFromPackage=true` et importe le fichier suivant |
| `hackeros-build/HackerOs.Apps.targets` | **le même fichier** que `build/HackerOs.Apps.targets` du dépôt (hors de `build/`, où NuGet n'attend que `HackerOs.App.Build.targets`) |
| `hackeros-build/asp-hack/` | `_ViewImports.cshtml` et types de compatibilité des vues `.cshtml` ASP.Hack (ADR 0086) |
| `tools/generator/` | le générateur de manifestes, lancé par `dotnet <dll>` (sans l'exécutable natif) |
| `analyzers/dotnet/cs/` | le générateur de routes ASP.Hack (ADR 0082) |

En **mode paquet**, un projet qui référence `HackerOs.App.Build` est une app,
sauf s'il déclare `HackerOsAppHost=true` (un hôte). Différences avec le dépôt :

- le générateur vient du paquet au lieu d'une `ProjectReference` ;
- la règle de frontière sur les `ProjectReference` (écrite en chemins du dépôt)
  ne s'applique pas ; celle sur les paquets, si
  (`_ValidateHackerOsPackageBoundary`, `HOSAPP0030`, voir
  [`app-sdk.md`](app-sdk.md#vérification)) : toute référence de compilation
  issue d'un paquet `HackerOs.*`, directe ou transitive, doit être permise ;
- l'inspection de l'injection (`HOSAPP0031`), les codes `HOSAPP*` et les
  empreintes des ressources sont identiques.

### Paquets d'apps

Quand un projet d'app est empaqueté, la cible `_HackerOsPackageAppManifests`
(branchée sur `TargetsForTfmSpecificContentInPackage`) ajoute :

- ses manifestes générés sous `hackeros/manifests/` ;
- `buildTransitive/<PackageId>.targets`, qui les déclare en éléments
  `HackerOsPackagedApp` :

```xml
<HackerOsPackagedApp Include="$(MSBuildThisFileDirectory)../hackeros/manifests/manifest.json"
                     PackageId="HackerOs.Apps.Calculator"
                     AssemblyFileName="HackerOs.Apps.Calculator.dll"
                     AssemblyPath="$(MSBuildThisFileDirectory)../lib/net10.0/HackerOs.Apps.Calculator.dll" />
```

Les fichiers de `wwwroot/` d'une app restent des ressources web statiques du
paquet (`_content/<PackageId>/…`), là où `StaticWebAssetAppAssetSource` les lit.

Un hôte agrège ces éléments avec ses `ProjectReference`
(`_HackerOsCollectPackagedAppManifests`) : `IsSystemApp` vient de sa
`PackageReference` sur le paquet ; un paquet d'app arrivé par dépendance est
installé aussi (comme un gestionnaire de paquets installe une dépendance), mais
jamais en app système. Les assemblies sont chargées paresseusement comme les
autres.

## `HackerOs.Ecosystem.Host`

L'exécutable `OS/HackerOs.Ecosystem` contenait tout l'OS. Il est coupé en deux :

| Projet | Contenu |
|---|---|
| `OS/HackerOs.Ecosystem.Host` (bibliothèque Razor, publiée) | `App.razor`, `LoginProgressScreen`, `EcosystemBootCoordinator`, `EcosystemHostState`, `AddHackerOsEcosystem`, `AddHackerOsResourceAccounting`, `AddHackerOsBrowserHost`, `HostAppCatalog`, adaptateurs WebAssembly (transport paresseux, environnement), `Administration/`, `WebData/`, `wwwroot/css/app.css` |
| `OS/HackerOs.Ecosystem` (PWA de ce dépôt) | `wwwroot/` (page, service worker, manifeste web, icônes), les références d'apps, `BuildKnownLazyApps` et `BuildKnownLazyAssemblies` (façades sur son `HostAppCatalog`), un `Program.cs` de trois lignes |

L'espace de noms reste `HackerOs.Ecosystem` : `HackerOs.Ecosystem.App` vit
désormais dans l'assembly `HackerOs.Ecosystem.Host`. `test/test` et le serveur
référencent toujours l'exe (pour son catalogue agrégé) et reçoivent la
bibliothèque par transitivité. Les jetons de design sont servis à
`_content/HackerOs.Ecosystem.Host/css/app.css` par les trois hôtes.

```csharp
// Program.cs d'un hôte WebAssembly
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.AddHackerOsBrowserHost(HostAppCatalog.Load(typeof(Program).Assembly));
await builder.Build().RunAsync();
```

`AddHackerOsBrowserHost(apps, options)` enregistre le composant racine (seulement
si l'élément `#app`, `RootSelector`, existe : un îlot WebAssembly d'une Blazor
Web App n'en a pas), le chargeur paresseux limité aux assemblies du catalogue, la
source des ressources d'apps (`StaticWebAssetAppAssetSource` sur
`_content/`), la mesure des ressources avec les images du catalogue, tous les
services de l'OS et le pool de Web Workers (`WorkerPreloadAssemblies`, par défaut
`HackerOs.Commands.dll` quand l'hôte l'a).

Le paquet est produit avec les deux mesures de ressources compilées
(ADR 0066) ; un hôte les coupe à l'exécution par `appsettings.json`.

## Modèles `dotnet new`

`Templates/HackerOs.Templates` (paquet `HackerOs.Templates`) :

| Modèle | Crée | Paquets |
|---|---|---|
| `hackeros-window` | app fenêtrée (`WindowAppBase`, CSS isolé, `_Imports.razor`) | `HackerOs.AppSdk`, `.Blazor`, `HackerOs.App.Build` |
| `hackeros-command` | commande du shell `hello` (`TerminalAppBase`) | `HackerOs.AppSdk`, `HackerOs.App.Build` |
| `hackeros-service` | service démarré à la connexion (`ServiceAppBase`, horloge simulée) | idem |
| `hackeros-site` | site ASP.Hack (`WebAppDefinition`, `wwwroot/` embarqué) | `HackerOs.AppSdk.Web`, `HackerOs.App.Build` |
| `hackeros-host` | hôte Blazor WebAssembly : `Program.cs`, `index.html`, `appsettings.json`, les apps de base en paquets (système comme dans l'Ecosystem) | `HackerOs.Ecosystem.Host`, `HackerOs.App.Build`, `HackerOs.Apps.*`, `HackerOs.Commands` |

Paramètres des modèles d'app : `--appId` (défaut `com.example.my-app`) et
`--publisherId` (défaut `pub.example`) ; le nom du projet (`-n`) devient
l'espace de noms et le nom affiché.

Chaque projet généré déclare `<HackerOsVersion>` une fois et l'utilise pour
toutes ses références `HackerOs.*`. Dans les sources du modèle, la valeur est
`0.0.0-dev` ; l'empaquetage copie `content/` dans `obj/` et y écrit la version du
dépôt (`XmlPoke`), si bien qu'un projet généré référence exactement les paquets
empaquetés avec lui.

Les points d'entrée des modèles de commande et de service ont le constructeur
`(AppManifest manifest)` par lequel l'OS démarre une app, plus un constructeur
sans paramètre pour les tests unitaires.

## Tests

| Où | Quoi |
|---|---|
| `Tests/HackerOs.Templates.E2E.Tests` | empaquette tout (`pack-local`, ou le flux de `HACKEROS_LOCAL_FEED`), installe les modèles dans un `DOTNET_CLI_HOME` isolé, génère et compile chaque modèle d'app hors du dépôt avec un cache NuGet isolé (manifeste, genre, version minimale du SDK vérifiés) ; puis génère un hôte, y ajoute une app fenêtrée et une commande générées, le compile (catalogue agrégé vérifié), le lance, crée le profil dans Chromium, ouvre l'app (`Clicked 0 times` puis `1`) et exécute `hello Morpheus` dans le terminal (`Hello, Morpheus!`). |
| `Tests/HackerOs.App.Abstractions.Tests` (`HackerOsSdkTests`) | lecture de la version, compatibilité des plages |
| `Tests/HackerOs.Tools.ManifestGenerator.Tests` | version du SDK lue dans la référence, empreintes et `HOSAPP0023`, `HOSAPP0022`, app plus récente que le SDK de l'hôte (`HOSAPP0021`) |
| `Tests/HackerOs.Platform.Core.Tests` | `AppCatalog` (refus, cascade des dépendances, propriétaire des commandes), `AppAssetStore`, `ShellCommandResolver` |
| `build/verify-hosts.sh` | les trois hôtes démarrent après l'extraction de `HackerOs.Ecosystem.Host` |

Vérifié à la main aussi : une app qui ajoute la référence
`HackerOs.Platform.Core` échoue avec `HOSAPP0030 Forbidden package`.

## Décisions

- **Une seule version** pour tout, plutôt qu'une version par paquet : le SDK, la
  plateforme et les apps évoluent ensemble jusqu'au gel.
- **Le même `HackerOs.Apps.targets`** dans le dépôt et dans le paquet, avec un
  interrupteur `HackerOsBuildFromPackage`, pour qu'aucune règle ne diverge.
- **Frontière des paquets vérifiée sur les références résolues** (directes et
  transitives), pas seulement sur les `PackageReference` du projet : une
  bibliothèque intermédiaire ne peut pas faire entrer la plateforme dans une app.
- **Les apps en paquet portent leurs manifestes** : un hôte n'a jamais besoin du
  code source d'une app pour la cataloguer, comme `dpkg` lit le contrôle d'un
  `.deb`.
- **Cache NuGet purgé par `pack-local`** et cache isolé dans le test : une
  version `preview` ré-empaquetée n'est jamais servie périmée.
- **`HostAppCatalog` dans la bibliothèque**, le catalogue agrégé dans l'assembly
  de l'hôte : la bibliothèque ne connaît pas les apps d'un hôte.

## Tâches

- [x] `build/Versions.props`, version portée par chaque projet, `Packaging.props` sans version fixe.
- [x] 37 bibliothèques publiées avec métadonnées, analyseur d'API publique et listes `PublicAPI.*.txt` générées.
- [x] Documentation XML de chaque membre public (≈ 300 avertissements corrigés, aucune suppression).
- [x] `HackerOsSdk`, minimum écrit par le générateur, refus au chargement, état « incompatible » dans Paramètres.
- [x] `HOSAPP0022` et propriétaire unique d'une commande au chargement.
- [x] Empreintes des ressources (`HOSAPP0023`, `AppAssetStore`, état « endommagée »).
- [x] Retrait de `BuildProfileManifest`, de son validateur et de son sérialiseur.
- [x] Paquet `HackerOs.App.Build` et mode paquet de `HackerOs.Apps.targets` (frontière des paquets, agrégation des apps en paquet, manifestes dans les paquets d'apps).
- [x] `HackerOs.Ecosystem.Host` extrait, `AddHackerOsBrowserHost`, `HostAppCatalog`, jetons de design déplacés, trois hôtes mis à jour.
- [x] `build/pack-local.sh` / `.ps1`, `build/nuget.config.sample`.
- [x] Cinq modèles et leur test de bout en bout.
- [x] Documentation (ce fichier, `app-model`, `app-sdk`, `sdk-reference`, `sdk-developer-guide`, `building-and-testing`, `project-overview`, mesure des ressources, notes des documents archivés) et index.
