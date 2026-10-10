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

## Tâches

- [x] `rebuild/2-app-definition` : `AppDefinition`, `[AppManifest]`,
  `FileHandler`, `TerminalCommand`, `AppDefinitionMapper`, tests du mappeur
  (`Tests/HackerOs.App.Abstractions.Tests/Definition`).
- [ ] `rebuild/2-manifest-generator` : générateur `manifest.json` et erreurs `HOSAPP*`.
- [ ] `rebuild/2-builtin-applications` : `BuiltInApplications.json` et `.targets` partagé.
