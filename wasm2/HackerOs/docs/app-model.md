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
| `AppManifestAttribute` | Marque la classe qui décrit une app. Usage unique, non hérité. |
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
| `$(HackerOsAppProject)` | `true` par défaut sous `Apps/` ; un projet ailleurs la positionne lui-même. **Transition :** un projet qui a encore un `app.manifest.json` écrit à la main n'est pas un projet d'app pour le générateur ; cette exception disparaît avec `rebuild/2-app-migration`. |
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
| `HOSAPP0090` | Arguments de l'outil invalides. |
| `HOSAPP0091` | Assembly de l'app impossible à charger. |
| `HOSAPP0099` | L'outil a échoué sans diagnostic (plantage). |

Les dépendances entre apps ne sont pas vérifiées à la génération (une seule app
est connue) : elles le sont à l'agrégation dans l'hôte.

### App migrée en exemple

`Apps/System/HackerOs.Apps.Calculator` a sa classe `CalculatorApp` et son
`manifest.json` généré ; son `app.manifest.json` est supprimé. Le manifeste
généré est sémantiquement identique à l'ancien. Les autres apps gardent leur
`app.manifest.json` jusqu'au chantier `rebuild/2-app-migration`.

### Tests

`Tests/HackerOs.Tools.ManifestGenerator.Tests` :

- `AppDefinitionInspectorTests` : un test par code `HOSAPP0001` à `HOSAPP0011`
  sur des définitions de test ;
- `ManifestWriterTests` : formes un/plusieurs fichiers, pas de réécriture si
  identique, écrasement d'une modification à la main ;
- `MsBuildIntegrationTests` : build réel des projets hors solution
  `Tests/ManifestGenerator.Fixtures.SingleApp`, `.MultiApp` et `.NoDefinition`
  (`HOSAPP0001`).

## Tâches

- [x] `rebuild/2-app-definition` : `AppDefinition`, `[AppManifest]`,
  `FileHandler`, `TerminalCommand`, `AppDefinitionMapper`, tests du mappeur
  (`Tests/HackerOs.App.Abstractions.Tests/Definition`).
- [x] `rebuild/2-manifest-generator` : `Tools/HackerOs.Tools.ManifestGenerator` (`generate`, `validate`), `build/HackerOs.Apps.targets`, cibles `GenerateHackerOsAppManifests` et `GetHackerOsAppManifests`, erreurs `HOSAPP*`, projets de test, Calculator migrée en exemple.
- [ ] `rebuild/2-builtin-applications` : `BuiltInApplications.json` et `.targets` partagé.
