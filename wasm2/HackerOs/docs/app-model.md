# Modèle d'application : `AppDefinition` et manifestes générés

Phase 2 du [plan de refonte](ecosystem-rebuild-plan.md) (sections 6.1 et 6.2),
décisions de l'[ADR 0047](adr/0047-app-definition-and-generated-manifests.md).

## Objectif

Le manifeste d'une app a une seule source : une classe C# marquée
`[AppManifest]`. Le build en tire le `manifest.json` de l'app, puis l'hôte
agrège tous les manifestes dans `BuiltInApplications.json`. Installer une app
dans l'hôte se résume à une `ProjectReference`.

## `AppDefinition` et `[AppManifest]`

Projet `Shared/HackerOs.App.Abstractions`, dossier `Definition/`, namespace
`HackerOs.App.Abstractions`.

| Type | Rôle |
| --- | --- |
| `AppManifestAttribute` | Marque la classe qui décrit une app. Usage unique, non hérité. Propriété nommée `Execution` (ADR 0070) : où l'OS exécute l'app (voir [Hôte d'exécution](#hôte-dexécution-adr-0070)). |
| `AppDefinition` | Classe de base abstraite : 4 membres obligatoires (`Id`, `Name`, `Kind`, `EntryPoint`) et des membres facultatifs avec valeur par défaut. |
| `FileHandler`, `TerminalCommand` | Formes courtes de `FileHandlerManifest` et `TerminalCommandManifest`. |
| `AppDefinitionMapper` | `ToManifest(definition)` convertit une définition en `AppManifest` (schéma version 1). |

Valeurs par défaut des membres facultatifs :

| Membre | Défaut |
| --- | --- |
| `Version` | `1.0.0` |
| `Description` | `Name` |
| `PublisherId` | `pub.local` |
| `Category` | `utilities` |
| `LaunchVisibility` | `Visible` |
| `Icons`, `Capabilities`, `FileHandlers`, `Dependencies`, `Intents`, `DeclaredTopicPermissions`, `Localizations`, `Assets` | liste vide |
| `Terminal`, `MobileEntryPoint`, `Settings` | `null` |
| `Components` | liste vide ; `components` est alors omis du manifeste (ADR 0063, [`embedded-components.md`](embedded-components.md)) |
| `SingleInstancePerUser`, `AutoStart`, `SupportsBack` | `false` |
| `Resources` | `AppResourceProfileManifest.None` |

Exemple :

```csharp
[AppManifest]
public sealed class TextEditorApp : AppDefinition
{
    public override string Id => "org.hackeros.text-editor";
    public override string Name => "Text Editor";
    public override AppKind Kind => AppKind.Window;
    public override Type EntryPoint => typeof(TextEditorWindow);
    public override string PublisherId => "pub.hackeros";
    public override IReadOnlyList<string> Capabilities =>
        [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite];
    public override IReadOnlyList<FileHandler> FileHandlers =>
        [new("text/plain", [".txt", ".md", ".log"], ["open", "edit"])];
}
```

### Règles de conversion

- `EntryPoint` devient `{ "assembly": "<assembly>.dll", "type": "<nom complet>" }`.
- `MobileEntryPoint` non nul produit la forme `platform` (`desktop` et `mobile`).
  Si c'est le même type que `EntryPoint`, une seule entrée couvre les deux
  plateformes ; sinon, une entrée par plateforme.
- `Icons` devient `presentation.iconAssetPaths`.
- `TerminalCommand.Aliases` nul devient une liste vide.
- `sdkCompatibility.minimumVersion` n'est pas une propriété : c'est la version
  `major.minor.0` de `HackerOs.App.Abstractions`. La surcharge
  `ToManifest(definition, sdkMinimumVersion)` permet au générateur d'écrire la
  version contre laquelle l'app a été compilée.
- `osCompatibility` et `update` ne sont pas exposés par `AppDefinition` : ils
  restent absents du manifeste généré.
- `[AppManifest(Execution = …)]` devient `execution`, toujours écrit, résolu
  (jamais `default`) : voir la section suivante.

### Hôte d'exécution (ADR 0070)

L'attribut `[AppManifest]` dit **où** l'OS exécute l'app, par sa propriété
nommée `Execution` (`AppExecutionHost`, `HackerOs.App.Abstractions`) :

| Valeur | Champ `execution` | Où | Défaut de |
| --- | --- | --- | --- |
| `MainThread` | `mainThread` | le thread principal du navigateur, avec le bureau | app à fenêtre (la seule valeur permise : elle a besoin du DOM) |
| `Pool` | `pool` | un worker du pool, un par cœur virtuel ; les étapes d'un même pipeline vont sur le même | commande |
| `Dedicated` | `dedicated` | un worker à lui, pris au worker prêt (ADR 0071) | service |
| `Default` | — | laissé tel quel dans l'attribut : le générateur écrit le défaut du genre | — |

```csharp
// ADR 0070: stays on the main thread: it acts on the calling shell (its working directory),
// like the cd builtin of bash.
[AppManifest(Execution = AppExecutionHost.MainThread)]
public sealed class CdApp : AppDefinition { … }
```

- Le mappeur lit l'attribut de la classe de définition et écrit la valeur
  résolue (`AppExecutionHosts.Resolve`) ; une définition sans attribut (tests)
  reçoit le défaut de son genre.
- `AppManifest.Execution` est `null` pour un manifeste écrit avant l'ADR 0070 :
  `AppExecutionHosts.Of(manifest)` donne alors le défaut du genre.
- Le validateur refuse `pool` ou `dedicated` pour une app à fenêtre
  (`manifest.execution.window`) et la valeur `default`
  (`manifest.execution.invalid`) ; le schéma et la liste blanche de
  `AppManifestJsonSerializer.DeserializeStrict` connaissent `execution`.
- Une commande ou un service qui a besoin de ce qu'un worker n'offre pas déclare
  `MainThread`, **avec la raison en commentaire** au-dessus de l'attribut. Dans
  `HackerOs.Commands` : les built-ins du shell (`cd`, `exit`, `export`,
  `unset`, `jobs`, `read`, `help`, `which`, `kill`, `sudo`, `watch`) et les
  commandes plein écran (`nano`, `top`, `htop`) ; le service d'exemple
  (événements typés). Les 49 autres commandes vont dans le pool.
- Un hôte sans Web Worker (`HackerOs.Server`) ignore `execution` (ADR 0070 §6).

Détails du mécanisme : [`worker-processes.md`](worker-processes.md).

### Écart avec le texte de l'ADR

L'ADR 0047 écrit `SettingsSchema? Settings`. `SettingsSchema` vit dans
`HackerOs.Simulation.Abstractions`, que `HackerOs.App.Abstractions` ne référence
pas. La propriété utilise donc l'enregistrement de manifeste existant
`AppSettingsSchemaManifest?`, conformément à la règle de l'ADR « les cas rares
réutilisent les enregistrements `*Manifest` existants ». Le contenu JSON est le
même.

### Règles imposées à la classe

Vérifiées par le générateur (voir plus bas) : classe `public`, non abstraite,
marquée `[AppManifest]`, constructeur public sans paramètre ; `EntryPoint` et
`MobileEntryPoint` dans la même assembly que la définition ; getters purs (le
générateur compare deux instanciations) ; `AppManifestValidator` et les règles
de point d'entrée de `AppEntryPointDiscovery` passent.

## Génération de `manifest.json` au build

### Outil

`Tools/HackerOs.Tools.ManifestGenerator` (exécutable .NET, framework partagé
`Microsoft.AspNetCore.App`) remplace l'ancien `HackerOs.Tools.ManifestValidator`.

| Commande | Rôle |
| --- | --- |
| `generate --assembly <dll> --project <csproj> --stamp <fichier> [--dependencies <liste>]` | Appelée par MSBuild après la compilation d'une app. |
| `aggregate --project <hôte.csproj> --apps <liste> --output <BuiltInApplications.json>` | Appelée par l'hôte (voir plus bas). |
| `validate <manifest.json>` | Lecture stricte (schéma v1) et `AppManifestValidator` sur un fichier ; remplace l'ancien validateur. |

Fonctionnement de `generate` :

1. charge l'assembly compilée dans un `AssemblyLoadContext` isolé et collectable
   (`AppAssemblyLoadContext`). Les assemblies du framework partagé et les
   contrats HackerOS de l'outil viennent du contexte par défaut (une seule
   identité pour `AppDefinition`) ; les autres viennent de la liste
   `@(ReferenceCopyLocalPaths)` + `@(ReferencePath)` puis du dossier de sortie
   de l'app. Les assemblies de référence (`[ReferenceAssembly]`) sont ignorées ;
2. `AppDefinitionInspector` trouve les classes `[AppManifest]` (par nom
   d'attribut), applique les règles, instancie chaque classe **deux fois** et
   compare le JSON canonique ;
3. si **aucune** erreur, `ManifestWriter` écrit les fichiers (sinon rien n'est
   écrit) ; un fichier n'est réécrit que si ses octets changent ;
4. le fichier témoin `obj/<config>/<tfm>/hackeros-app-manifests.txt` liste les
   manifestes produits.

### Fichiers produits

- une classe `[AppManifest]` → `manifest.json` à la racine du projet ;
- plusieurs classes → `manifests/<appId>.json` ; passer d'une forme à l'autre
  supprime les fichiers de l'ancienne forme.

Le JSON est le JSON canonique de l'ADR 0010 (`AppManifestJsonSerializer.SerializeCanonical`),
schéma version 1, sans champ ajouté. Les fichiers sont **suivis par git** (un
changement de capacité se voit dans la PR) et ne sont **pas** du contenu MSBuild
(`Content Remove`) : ils ne sont jamais copiés ni publiés. Une modification à la
main est écrasée au build suivant.

### Intégration MSBuild

`build/HackerOs.Apps.targets`, importé par `Directory.Build.targets` :

| Élément | Rôle |
| --- | --- |
| `$(HackerOsAppProject)` | `true` par défaut sous `Apps/` ; un projet ailleurs la positionne lui-même. Il n'y a pas d'exception : un projet sous `Apps/` sans classe `[AppManifest]` échoue avec `HOSAPP0001`. |
| `ProjectReference` vers le générateur | Ajoutée automatiquement aux projets d'app pour l'ordre de build (`ReferenceOutputAssembly=false`, `PrivateAssets=all` : l'outil n'entre jamais dans le graphe d'un hôte WebAssembly). |
| Cible `GenerateHackerOsAppManifests` | Après `CopyFilesToOutputDirectory`. Incrémentale : entrées = assembly compilée, outil, liste des dépendances, manifestes existants ; sortie = fichier témoin. |
| Cible `GetHackerOsAppManifests` | Cible de requête pour l'hôte : les manifestes du projet, avec les métadonnées `AssemblyName`, `AssemblyFileName` et `AppProject`. Vide pour un projet qui n'est pas une app. |

### Codes d'erreur

Chaque erreur est une erreur MSBuild au format canonique, attribuée au `.csproj`,
avec le nom complet de la classe en cause : `App.csproj : error HOSAPP0005: [Ns.MyApp] ...`.

| Code | Règle |
| --- | --- |
| `HOSAPP0001` | Aucune classe `[AppManifest]` dans le projet d'app. |
| `HOSAPP0002` | Classe non publique. |
| `HOSAPP0003` | Classe abstraite. |
| `HOSAPP0004` | Ne dérive pas de `AppDefinition`. |
| `HOSAPP0005` | Pas de constructeur public sans paramètre. |
| `HOSAPP0006` | Le constructeur ou un getter lève une exception. |
| `HOSAPP0007` | `EntryPoint`/`MobileEntryPoint` nul ou dans une autre assembly. |
| `HOSAPP0008` | Getters non déterministes (deux instances, deux manifestes). |
| `HOSAPP0009` | `AppManifestValidator` refuse le manifeste (le code du validateur est rappelé). |
| `HOSAPP0010` | Règles de `AppEntryPointDiscovery` : type concret, classe de base selon le genre, `IAppBackHandler` si `SupportsBack` ; vérifiées pour chaque plateforme déclarée. |
| `HOSAPP0011` | Deux classes de la même assembly déclarent le même identifiant. |
| `HOSAPP0020` | Agrégation : fichier manifeste absent ou JSON non strict. |
| `HOSAPP0021` | Agrégation : catalogue invalide (identifiant en double, dépendance manquante ou cyclique). |
| `HOSAPP0030` | Frontière du SDK : `ProjectReference` interdite dans un projet d'app (ADR 0055 §2, numéro fixé par l'ADR 0059). Émis par la cible `ValidateHackerOsSdkBoundary`. Voir [`app-sdk.md`](app-sdk.md). |
| `HOSAPP0031` | Frontière du SDK : service injecté qui n'est ni un type du SDK, ni MudBlazor, ni `IJSRuntime` (ADR 0055 §3, ADR 0059). Voir [`app-sdk.md`](app-sdk.md). |
| `HOSAPP0090` | Arguments de l'outil invalides. |
| `HOSAPP0091` | Assembly de l'app impossible à charger. |
| `HOSAPP0099` | L'outil a échoué sans diagnostic (plantage). |

Les dépendances entre apps ne sont pas vérifiées à la génération (une seule app
est connue) : elles le sont à l'agrégation dans l'hôte.

### Migration des apps

Les 44 projets sous `Apps/` (12 apps, 28 commandes, 4 exemples) ont leur classe
`[AppManifest]` et leur `manifest.json` généré ; il ne reste aucun
`app.manifest.json` écrit à la main ni aucun champ `StaticManifest`.
`Apps/System/HackerOs.Apps.Calculator` (`CalculatorApp`) a servi d'exemple.

Convention de nommage de la classe : nom du point d'entrée sans le suffixe
`Window` ou `Command`, suivi de `App` (`CalculatorWindow` → `CalculatorApp`,
`RmCommand` → `RmApp`). Les exemples, dont le point d'entrée porte déjà ce genre
de nom, utilisent `<Projet>SampleApp` (`TerminalSampleApp`, `WindowSampleApp`,
`ServiceSampleApp`, `PlatformSampleApp`). La classe est dans le namespace de son
point d'entrée.

Les valeurs viennent de l'ancien `app.manifest.json`, qui faisait foi au runtime :
quand un `StaticManifest` C# divergeait (profil de ressources `None`, description
abrégée), c'est le JSON qui a été repris. Le code et les tests qui lisaient
`StaticManifest` utilisent `AppDefinitionMapper.ToManifest(new XxxApp())` ; c'est
aussi le cas des constructeurs sans paramètre de `SampleTerminalApp` et
`SampleTickerService`.

Vérification : un test de migration temporaire
(`LegacyManifestMigrationTests`, commit `feat(app-model): [AppManifest] classes
for the 43 remaining apps`) a comparé, pour les 43 apps, le `manifest.json`
généré et l'ancien fichier après lecture stricte, normalisation des collections
facultatives et sérialisation canonique : 43 égaux. Il a été supprimé avec les
anciens fichiers. Seul écart corrigé avant comparaison : le `app.manifest.json`
de l'exemple TerminalApp utilisait les clés hors schéma `commandName` et
`helpSummary` (refusées en lecture stricte) ; ce sont `name` et `usage`, déjà
utilisées par son `StaticManifest`.

### Commandes intégrées : un seul projet (ADR 0051)

Depuis le chantier `rebuild/3-commands-merge`, les commandes intégrées (28, puis
29 avec `trash` du chantier `rebuild/3-trash`, voir [`trash.md`](trash.md)) sont
dans un seul projet, `Apps/Commands/HackerOs.Commands/HackerOs.Commands.csproj` :

- un dossier par commande (`Cat/CatApp.cs`, `Cat/CatCommand.cs`), chacune garde
  son espace de noms (`HackerOs.Commands.Cat`) et sa classe `[AppManifest]` ;
- le générateur écrit `manifests/<appId>.json` (forme « plusieurs classes ») ;
  seul `entryPoint.assembly` change, il vaut `HackerOs.Commands.dll` ;
- **les identifiants d'app ne changent pas** (`org.hackeros.cmd.cat`,
  `org.hackeros.commands.curl`…) : `apps.config`, `app-permissions.config` et
  les alias existants restent valides ;
- l'hôte n'a qu'une `ProjectReference ... IsSystemApp="true"` pour toutes les
  commandes ; l'assembly est chargée à la demande en un seul bloc
  (`BuildKnownLazyAssemblies.Names` la contient une seule fois) ;
- les projets de tests (`HackerOs.Commands.Tests`, `HackerOs.Commands.Wave5.Tests`,
  `HackerOs.Commands.Nano.Tests`, `HackerOs.Network.Tests`) référencent ce projet.

`cd`, `pwd`, `clear`, `help` et `sudo` sont installées et résolues comme les
autres commandes : le terminal ne connaît aucun nom (chantier
`rebuild/3-shell-commands`, ADR 0051 sections 2 à 5, voir
[`shell-commands.md`](shell-commands.md)). Celles qui agissent sur le shell
déclarent la capacité `shell.control`. Une commande tierce reste un projet à part (modèle :
`Apps/Samples/HackerOs.Samples.TerminalApp`).

### Tests

`Tests/HackerOs.Tools.ManifestGenerator.Tests` :

- `AppDefinitionInspectorTests` : un test par code `HOSAPP0001` à `HOSAPP0011`
  sur des définitions de test ;
- `ManifestWriterTests` : formes un/plusieurs fichiers, pas de réécriture si
  identique, écrasement d'une modification à la main ;
- `MsBuildIntegrationTests` : build réel des projets hors solution
  `Tests/ManifestGenerator.Fixtures.SingleApp`, `.MultiApp` et `.NoDefinition`
  (`HOSAPP0001`).

## Agrégation dans l'hôte : `BuiltInApplications.json`

### Déclarer un hôte

Un projet hôte déclare `<HackerOsAppHost>true</HackerOsAppHost>`. Seul
`OS/HackerOs.Ecosystem` le fait. `test/test` réutilise le catalogue compilé de
l'Ecosystem (`BuildKnownLazyApps.Catalog`) et ne s'agrège pas lui-même.

### Cible `CollectHackerOsBuiltInApplications`

Dans `build/HackerOs.Apps.targets`, après `ResolveProjectReferences` (donc avant
la préparation des ressources et avant les cibles Blazor) :

1. pour chaque `ProjectReference` directe (hors références à
   `ReferenceOutputAssembly=false`), appelle `GetHackerOsAppManifests` avec les
   mêmes propriétés globales que `ResolveProjectReferences` ; un projet qui
   n'est pas une app ne renvoie rien et est ignoré ;
2. associe à chaque manifeste la métadonnée `IsSystemApp` de sa `ProjectReference`
   (vide = `false`, sens fixé par l'ADR 0048) ;
3. appelle `ManifestGenerator aggregate`, qui relit chaque manifeste en mode strict,
   le valide, construit le catalogue avec `AppCatalog.Build` (identifiant en
   double, dépendance manquante ou cyclique = `HOSAPP0021`) et écrit
   `obj/<config>/<tfm>/BuiltInApplications.json` seulement si ses octets changent
   (pas de recompilation inutile) ; elle y mesure aussi l'**image** de chaque
   app (ADR 0068) à partir du chemin de son assembly et de sa liste de
   dépendances, que `GetHackerOsAppManifests` renvoie (`AssemblyPath`,
   `DependenciesFile`) ;
4. ajoute ce fichier en `EmbeddedResource` (`HackerOs.BuiltInApplications.json`) ;
5. ajoute un `BlazorWebAssemblyLazyLoad` pour l'assembly de chaque app (en
   retirant d'abord un éventuel doublon écrit à la main).

L'ancrage après `ResolveProjectReferences` est vérifié par un
`dotnet publish -c Release` de l'Ecosystem : `HackerOs.Apps.Calculator` figure
dans la section `lazyAssembly` de la configuration de démarrage publiée, et le
générateur n'apparaît pas dans `_framework`.

### Format

```json
{"schemaVersion":1,"applications":[{"isSystemApp":false,"manifest":{"schemaVersion":1,"id":"org.hackeros.calculator", "...": "..."},"image":{"assemblies":[{"name":"HackerOs.Apps.Calculator.dll","bytes":17408,"shared":false},{"name":"HackerOs.AppSdk.dll","bytes":400896,"shared":true}]}}]}
```

`image` (facultatif, ADR 0068) liste les assemblies que l'app mappe, triées par
nom, avec leur taille : la sienne (`shared: false`, `VmExe`) et les
bibliothèques qu'elle référence, directement ou non (`shared: true`), hors
runtime et framework .NET (`System.*`, `Microsoft.*`). Le calcul est fait par
`AppImageInspector` (métadonnées seulement, aucun code chargé) ; voir
[`memory-accounting.md`](memory-accounting.md). Le `manifest.json` versionné
ne change pas : les tailles ne vivent que dans ce fichier produit à chaque
build.

`BuiltInApplicationsJsonSerializer` (`HackerOs.App.Abstractions`, à côté
d'`AppManifestJsonSerializer`) écrit la forme canonique (applications triées par
identifiant, manifestes canoniques) et relit en mode strict (propriété inconnue
ou en double, `schemaVersion` autre que 1, `isSystemApp` non booléen = erreur ;
chaque manifeste passe par `AppManifestJsonSerializer.DeserializeStrict`). Le
type `BuiltInApplication(IsSystemApp, Manifest, Image)` porte une entrée
(`Image` vaut `null` quand le champ est absent). Le fichier
n'est jamais écrit à la main ni versionné.

### Lecture au démarrage

`BuildKnownLazyApps` lit la ressource `HackerOs.BuiltInApplications.json` et
construit `Catalog` avec `AppCatalog.BuildFromBuiltInApplications` : le catalogue
porte les drapeaux système (voir « Apps système » ci-dessous), et expose
`BuildKnownLazyApps.Images` (image de chaque app, lue par la mesure de la
mémoire). Seul le
JSON est lu : les DLL d'apps restent chargées au premier lancement et les
classes `AppDefinition` ne sont jamais instanciées au runtime.

C'est la seule source : `HackerOs.Ecosystem.csproj` n'embarque plus de manifeste
à la main (`HackerOs.Ecosystem.Manifests.*`) et n'a plus de liste
`BlazorWebAssemblyLazyLoad`, et la cible de transition
`_HackerOsMarkLegacySystemManifests` a disparu avec elles : `IsSystemApp` vient
seulement de `BuiltInApplications.json`. Une ressource absente est une erreur au démarrage.

### Installer une app

Une fois l'app dotée de sa classe `[AppManifest]` : une `ProjectReference` dans
`HackerOs.Ecosystem.csproj`, avec `IsSystemApp="true"` si l'hôte la déclare
système. Rien d'autre.

### Tests

- `BuiltInApplicationsJsonSerializerTests` (`HackerOs.App.Abstractions.Tests`) :
  tri et aller-retour canonique, document vide, normalisation, rejets stricts.
- `BuiltInApplicationsAggregatorTests` : `IsSystemApp` reporté, identifiant en
  double refusé (`HOSAPP0021`), manifeste absent ou invalide (`HOSAPP0020`),
  analyse de la liste.
- `MsBuildIntegrationTests.Host_embeds_BuiltInApplications_...` : build réel de
  `Tests/ManifestGenerator.Fixtures.Host` (une app système, un projet à deux apps,
  un projet qui n'est pas une app, ignoré).
- `BuildKnownLazyAppsTests` (`HackerOs.Ecosystem.Tests`) : le catalogue contient
  toutes les apps de l'hôte (75 aujourd'hui : 12 fenêtrées et 63 commandes, `stress` depuis l'ADR 0070) ; toutes viennent de la ressource agrégée (aucune ressource
  `HackerOs.Ecosystem.Manifests.*`), et la liste de chargement différé est
  exactement celle de leurs assemblies.

## Apps système (ADR 0048 §1)

### Source de confiance

Le statut système se déclare **uniquement** sur la `ProjectReference` de l'hôte
(`IsSystemApp="true"`), recopié par l'agrégation dans `isSystemApp`. Ni
`AppDefinition`, ni `[AppManifest]`, ni le schéma de `manifest.json` n'ont de champ
pour cela : un `manifest.json` qui contient `isSystemApp` est rejeté par le
désérialiseur strict. Une app installée au runtime par le futur magasin sera donc
toujours non système.

### API

`AppCatalog` (`Platform/HackerOs.Platform.Core/AppCatalog.cs`) :

| Membre | Rôle |
|---|---|
| `SystemAppIds` | ensemble (ordinal, figé) des identifiants système du catalogue |
| `IsSystemApp(appId)` | `true` si l'hôte a déclaré l'app système |
| `Build(manifests)` | catalogue sans app système (tests, catalogue vide) |
| `Build(manifests, systemAppIds)` | catalogue avec la liste système de l'hôte |
| `BuildFromBuiltInApplications(applications)` | raccourci depuis les entrées de `BuiltInApplications.json` |

Un identifiant système absent des manifestes fait échouer la construction
(`catalog.system-app.unknown`) : une faute de frappe ne peut pas laisser une app
sans protection. `BuildKnownLazyApps.Catalog` est le catalogue injecté par les
hôtes (`Program.cs`) ; les services de l'OS lisent donc
`AppCatalog.IsSystemApp` depuis l'injection de dépendances.

### Apps système de l'hôte Ecosystem

| Système | Non système (désinstallables) |
|---|---|
| Terminal, File Explorer, Settings, Text Editor, Code Editor (depuis `rebuild/4-sdk-shell-host` : son terminal héberge un shell, ADR 0056 §7), System Monitor, Error Log Viewer, et les 62 commandes (projet `HackerOs.Commands`) | Browser, Calculator, Diagnostic, HackPaint, Icon Viewer |

Effets du statut : une app système n'est pas désinstallable (voir
[`app-installation.md`](app-installation.md), ADR 0048 §2-6) ; ses capacités
dangereuses accordées d'office arrivent avec l'ADR 0049.

### Tests

- `AppCatalogTests` (`HackerOs.Platform.Core.Tests`) : pas d'app système par
  défaut, ensemble exposé et comparaison ordinale, identifiant inconnu refusé,
  drapeau pris de chaque entrée `BuiltInApplication`.
- `SystemAppTrustTests` (`HackerOs.App.Abstractions.Tests`) : aucun membre
  « SystemApp » sur `AppDefinition`, `AppManifest`, `[AppManifest]` ; un
  `manifest.json` qui revendique `isSystemApp` est rejeté.
- `BuildKnownLazyAppsTests.System_apps_follow_the_host_ProjectReference_metadata`
  (`HackerOs.Ecosystem.Tests`) : l'ensemble système du catalogue de l'hôte est
  exactement la table ci-dessus.

## Tâches

- [x] `rebuild/2-app-definition` : `AppDefinition`, `[AppManifest]`,
  `FileHandler`, `TerminalCommand`, `AppDefinitionMapper`, tests du mappeur
  (`Tests/HackerOs.App.Abstractions.Tests/Definition`).
- [x] `rebuild/2-manifest-generator` : `Tools/HackerOs.Tools.ManifestGenerator` (`generate`, `validate`), `build/HackerOs.Apps.targets`, cibles `GenerateHackerOsAppManifests` et `GetHackerOsAppManifests`, erreurs `HOSAPP*`, projets de test, Calculator migrée en exemple.
- [x] `rebuild/2-builtin-applications` : cible `CollectHackerOsBuiltInApplications`, `BuiltInApplications.json` embarqué, lazy load automatique, `BuildKnownLazyApps` lit le nouveau fichier, `IsSystemApp` transporté.
- [x] `rebuild/2-system-apps` (ADR 0048) : `AppCatalog.SystemAppIds` et `IsSystemApp`, `AppCatalog.Build(manifests, systemAppIds)` et `BuildFromBuiltInApplications`, `IsSystemApp="true"` posé sur les `ProjectReference` de l'Ecosystem, pont de transition `_HackerOsMarkLegacySystemManifests`, tests.
- [x] `rebuild/2-app-uninstall` (ADR 0048 §2-6) : désinstallation par `apps.config`, voir [`app-installation.md`](app-installation.md).
- [x] `rebuild/5-worker-processes` (ADR 0070) : `AppManifestAttribute.Execution`, `AppExecutionHost`, champ `execution` (schéma, liste blanche, validateur, fixture canonique), défauts par genre dans le mappeur, chaque commande et service déclare son hôte ou reçoit le défaut.
- [x] `rebuild/2-app-migration` : classes `[AppManifest]` pour les 43 autres apps, test d'égalité avec les anciens JSON, suppression des `app.manifest.json`, `StaticManifest`, des listes manuelles de l'hôte et de la cible `_HackerOsMarkLegacySystemManifests`, de la règle `Content Update` de `Directory.Build.targets` et des exceptions « transition ».
