# Construire sur HackerOS depuis un autre dépôt

## Objet

Le guide d'un dépôt qui **utilise** HackerOS sans en copier le code : le dépôt
du jeu (ADR 0092), ou tout projet qui veut ses propres apps et son propre hôte
d'OS. Il part du SDK 1.0, gelé par le chantier `v3/15`
([ADR 0114](adr/0114-sdk-1-0-freeze.md)) ; la version courante est la **1.1.0**
(série v3.1, [ADR 0117](adr/0117-sdk-window-title-and-sdk-1-1.md) §5, voir « Ce que la 1.1 ajoute »).

Détails des paquets et du mode paquet de `HackerOs.App.Build` :
[`sdk-packages.md`](sdk-packages.md). Écrire une app (API, exemples) :
[`sdk-developer-guide.md`](sdk-developer-guide.md) et
[`sdk-reference.md`](sdk-reference.md).

## 1. Ce que la version 1.0 promet

| Votre projet dépend de… | Garantie |
|---|---|
| le SDK : `HackerOs.App.Abstractions`, `HackerOs.Simulation.Abstractions`, `HackerOs.Theming.Abstractions`, `HackerOs.AppSdk*`, `HackerOs.Theming.Blazor`, le contrat de build de `HackerOs.App.Build`, le format du manifeste | une app compilée contre `1.x` tourne sur tout OS `1.y` (y ≥ x) ; une rupture n'arrive qu'avec une 2.0 |
| la plateforme : `HackerOs.Ecosystem.Host`, `HackerOs.Platform.*`, `HackerOs.Windowing.*`, `HackerOs.Infrastructure.Browser`, les types publics des apps de base | versionnée avec le SDK ; une mineure peut casser un **hôte**, jamais une app. Chaque rupture est listée dans [`sdk-packages.md`](sdk-packages.md#ruptures-hors-sdk) |

L'OS refuse une app compilée contre un SDK plus récent que le sien, ou d'une
autre majeure (état « incompatible » dans Paramètres, ADR 0098 §3). Une app
construite en `0.9.0-preview.*` doit être reconstruite contre la 1.0.

## 2. Récupérer les paquets

Les paquets HackerOS ne sont publiés que dans un flux **local** (jamais sur
nuget.org). Dans un clone de HackerOS, sur la version voulue :

```bash
git -C ../HackerSimulator checkout <tag ou branche>
../HackerSimulator/wasm2/HackerOs/build/pack-local.sh      # ou pack-local.ps1
# -> ../HackerSimulator/wasm2/HackerOs/artifacts/local-nupkg/*.1.1.0.nupkg
```

`--output <dossier>` écrit ailleurs (par exemple un dossier partagé de votre
dépôt). Le script purge cette version du cache NuGet global : un paquet
ré-empaqueté n'est jamais servi périmé.

## 3. Disposition du dépôt

```text
mon-depot/
├─ nuget.config                 # copié de build/nuget.config.sample
├─ Directory.Build.props        # HackerOsVersion épinglée une fois
├─ MonDepot.sln
├─ src/
│  ├─ Contoso.Os/               # l'hôte (dotnet new hackeros-host)
│  ├─ Contoso.Notes/            # une app fenêtrée (hackeros-window)
│  └─ Contoso.Hello/            # une commande (hackeros-command)
└─ tests/
   └─ Contoso.Hello.Tests/
```

`nuget.config` : la source `hackeros-local` pointe vers le flux, et le
`packageSourceMapping` ne prend les `HackerOs.*` que de là :

```xml
<configuration>
  <packageSources>
    <add key="hackeros-local" value="../HackerSimulator/wasm2/HackerOs/artifacts/local-nupkg" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="hackeros-local"><package pattern="HackerOs.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
```

`Directory.Build.props` : une seule version pour tous les paquets HackerOS
(ils sont publiés ensemble, ADR 0098 §1). Les modèles écrivent
`<HackerOsVersion>` dans chaque projet ; retirez-la des projets pour ne garder
que celle-ci :

```xml
<Project>
  <PropertyGroup>
    <HackerOsVersion>1.1.0</HackerOsVersion>
  </PropertyGroup>
</Project>
```

## 4. Créer les projets

```bash
dotnet new install ../HackerSimulator/wasm2/HackerOs/artifacts/local-nupkg/HackerOs.Templates.1.1.0.nupkg
dotnet new hackeros-window  -n Contoso.Notes -o src/Contoso.Notes --appId com.contoso.notes --publisherId pub.contoso
dotnet new hackeros-command -n Contoso.Hello -o src/Contoso.Hello --appId com.contoso.hello --publisherId pub.contoso
dotnet new hackeros-service -n Contoso.Sync  -o src/Contoso.Sync  --appId com.contoso.sync  --publisherId pub.contoso
dotnet new hackeros-site    -n Contoso.Site  -o src/Contoso.Site  --appId com.contoso.site  --publisherId pub.contoso
dotnet new hackeros-host    -n Contoso.Os    -o src/Contoso.Os
dotnet sln add src/*/*.csproj
```

Chaque projet d'app référence `HackerOs.AppSdk*` et `HackerOs.App.Build`
(`PrivateAssets="all"`). Au build, `HackerOs.App.Build` écrit `manifest.json`
depuis la classe `[AppManifest]`, avec la version du SDK comme minimum, et
refuse une référence hors du SDK (`HOSAPP0030`) ou une injection d'un service
de la plateforme (`HOSAPP0031`).

## 5. Composer l'hôte

L'hôte `hackeros-host` est une PWA Blazor WebAssembly complète : tout l'OS vient
de `HackerOs.Ecosystem.Host`, les apps sont ses références.

```xml
<!-- src/Contoso.Os/Contoso.Os.csproj -->
<ItemGroup>
  <!-- Vos apps, en projets… -->
  <ProjectReference Include="../Contoso.Notes/Contoso.Notes.csproj" />
  <ProjectReference Include="../Contoso.Hello/Contoso.Hello.csproj" IsSystemApp="true" />
  <!-- …ou en paquets d'apps. -->
  <PackageReference Include="Contoso.Tools" Version="2.3.0" />
  <!-- Les apps de base que vous gardez (retirez les autres). -->
  <PackageReference Include="HackerOs.Apps.Terminal" Version="$(HackerOsVersion)" IsSystemApp="true" />
</ItemGroup>
```

- `IsSystemApp="true"` : app système (non désinstallable, capacités
  dangereuses accordées, ADR 0048, ADR 0049).
- Le build agrège les manifestes de toutes les apps dans
  `BuiltInApplications.json`, embarqué dans l'assembly de l'hôte ;
  `HostAppCatalog.Load` le relit et les assemblies des apps sont chargées
  paresseusement.
- `dotnet run` sert l'hôte ; le premier démarrage demande de créer
  l'administrateur.

## 6. Distribuer une app en paquet

`dotnet pack` d'un projet d'app produit un paquet qui embarque ses manifestes
(`hackeros/manifests/`), leurs traductions et un `buildTransitive` qui les
déclare à l'hôte : un hôte n'a jamais besoin de son code source. Donnez au
paquet un `PackageId` hors de l'espace `HackerOs.*`, réservé au flux HackerOS
par le `packageSourceMapping`.

## 7. Tester

- Une commande se teste sans navigateur : créer la commande avec son manifeste
  (`AppDefinitionMapper.ToManifest(new MonApp())`), une
  `TerminalExecutionContext` sur des passerelles factices, et
  `HackExecutionContext.Enter(app)` autour de `ExecuteAsync`
  ([`sdk-developer-guide.md`](sdk-developer-guide.md), test d'une commande).
- Un hôte se teste comme `HackerOs.Templates.E2E.Tests` : `dotnet run` de
  l'hôte, puis Playwright (création de l'administrateur, menu Applications,
  Terminal).
- Pour vérifier qu'une montée de version ne casse pas vos apps déjà publiées,
  gardez comme HackerOS un paquet d'app compilé contre l'ancienne version et
  chargez-le dans l'hôte courant (`Tests/fixtures/sdk-compat`, ADR 0114 §4).

## 8. Publier l'hôte

```bash
dotnet publish src/Contoso.Os -c Release
```

Le modèle d'hôte active `PublishTrimmed` en mode `partial` : le framework est
élagué, les assemblies HackerOS et toutes les apps restent entières (racines
ajoutées par `HackerOs.App.Build`, ADR 0113). La sortie `wwwroot` est un site
statique ; le service worker ne s'enregistre que hors de `localhost`. Voir
[`trimmed-publish.md`](trimmed-publish.md).

## 9. Monter de version

1. Produire le flux de la nouvelle version (§2).
2. Changer `HackerOsVersion` dans `Directory.Build.props`.
3. Mineure : reconstruire ; lire « Ruptures hors SDK » de
   [`sdk-packages.md`](sdk-packages.md) si vous avez un hôte. Majeure :
   reconstruire **toutes** les apps, l'OS refuse celles de l'ancienne majeure.
4. Les manifestes régénérés portent le nouveau minimum : une app reconstruite
   ne se charge plus sur un OS plus ancien.

### Ce que la 1.1 ajoute

Une mineure : une app compilée contre 1.0.0 tourne sans changement sur l'OS
1.1 (vérifié par l'app figée de `Tests/fixtures/sdk-compat`). Pour utiliser les
ajouts, passer `HackerOsVersion` à `1.1.0` et reconstruire ; l'app exige alors
un OS 1.1 ou plus récent.

- **Titre de fenêtre** : `WindowAppBase.SetWindowTitleAsync`, `WindowTitle`,
  `FormatWindowTitle("rapport.txt", modified: true)` → `*rapport.txt — Mon app`
  (ADR 0117, 0123, [`window-model.md`](window-model.md) §4).
- **Taille des fenêtres retenue** : `WindowOptions.RememberGeometry = false`
  pour qu'une fenêtre ne reprenne pas sa dernière taille (ADR 0116, 0122,
  [`window-geometry.md`](window-geometry.md)).
- **Comptes et mots de passe** : `HackUserAccounts` (`ListAsync`,
  `ChangePasswordAsync`, `ResetPasswordAsync` avec la capacité `users.manage`),
  `TerminalExecutionContext.ReadSecretLineAsync` pour une saisie sans écho
  (ADR 0120, [`sensitive-settings.md`](sensitive-settings.md)).
- **Applications par défaut de l'utilisateur** : `HackApps.SetDefaultHandlerAsync`,
  `ResetDefaultHandlerAsync`, `GetDefaultHandlersAsync` (ADR 0121,
  [`user-file-associations.md`](user-file-associations.md)).

La liste exacte et les ruptures hors SDK d'un hôte (trois changements de
`HackerOs.Platform.Blazor` autour de la géométrie des fenêtres) sont dans
[`sdk-packages.md`](sdk-packages.md#sdk-11).

## Décisions

- **Un flux local plutôt que nuget.org** (ADR 0098 §2) : GitHub Packages
  viendra quand la CI du dépôt remarchera.
- **Version épinglée une fois** dans le dépôt tiers : les paquets HackerOS
  vont ensemble, mélanger deux versions n'est pas pris en charge.

## Tâches

- [x] Promesse de compatibilité et refus des autres majeures.
- [x] Flux local, `nuget.config`, version épinglée.
- [x] Modèles, hôte, apps en projets et en paquets.
- [x] Tests, publication trimmée, montée de version.
- [x] SDK 1.1 : version courante, ajouts et lien vers les ruptures hors SDK (`v3.1/4-sdk-1-1`).
