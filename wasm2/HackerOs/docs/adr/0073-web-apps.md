# ADR 0073 : apps web (genre `Web`), sites externes et sites hébergés

## Statut

Proposé le 2026-10-16.

- **Phase du plan :** phase web ([`asp-hack-plan.md`](../asp-hack-plan.md)).
- **Complète :** ADR 0047 (manifeste en classe C#), ADR 0049 (capacités),
  ADR 0070 (hôte d'exécution).

## Contexte

Benoit veut des sites web internes : des projets C# façon ASP.NET MVC, des sites
Blazor interactifs et des sites statiques. Aujourd'hui un site simulé est un
contrôleur enregistré au démarrage de l'hôte, sans processus ni manifeste
(`ISimulatedWebsiteController`). Il faut dire ce qu'est un site du point de vue
du modèle d'app, d'où viennent ses fichiers et à quoi il a droit.

## Décision

### 1. Nouveau genre d'app `Web`

`AppKind` gagne `Web` (`"web"` dans `manifest.json`). La définition dérive de
`WebAppDefinition` (bibliothèque `HackerOs.AppSdk.Web`) :

```csharp
[AppManifest]
public sealed class IntranetApp : WebAppDefinition
{
    public override string Id => "com.example.intranet";
    public override string Name => "Intranet";
    public override IReadOnlyList<string> Urls => ["http://intranet.hack/"];
    public override void Configure(HackWebApplicationBuilder builder) { ... } // MVC (ADR 0076)
    public override Type? BlazorRoot => null;                                 // ou typeof(App) (§4)
}
```

- `Urls` est obligatoire et généré dans le manifeste (champ `web.urls`) ; le
  résolveur de l'OS le lit (ADR 0074). Un nom pris par deux apps est une erreur
  de catalogue (`catalog.web.url-conflict`).
- Un site peut être MVC seul, Blazor seul, ou les deux : les routes MVC passent
  d'abord, le reste va au composant Blazor.
- Hôte d'exécution par défaut (ADR 0070) : `Dedicated` pour un site sans
  Blazor (le rendu HTML n'a pas besoin du DOM), `MainThread` pour un site avec
  Blazor.

### 2. Deux sortes de sites, selon d'où viennent les fichiers

| Sorte | Origine | Adresse | Fichiers |
|---|---|---|---|
| **Externe** | un projet C# (app `Web` installée) | les `Urls` de son manifeste (`intranet.hack`) | `wwwroot` du projet, embarqué dans l'app, **lecture seule** ; l'utilisateur le consulte comme un site distant |
| **Hébergé** | des fichiers de l'OS, déclarés par l'utilisateur (ADR 0077) | `http://localhost:<port>/`, plus un nom de `/etc/hosts` s'il le veut | dossier du système de fichiers, modifiable |

Les sites enregistrés statiquement (HackMail, HackerSearch...) restent pris en
charge tels quels, comme sites externes sans processus.

### 3. Capacités d'un site

- Un site n'obtient jamais `filesystem.user-home.*`, `filesystem.user-selected.*`
  ni `filesystem.system.*` : le générateur refuse une `WebAppDefinition` qui en
  demande (`HOSAPP0050`). Il voit ses ressources embarquées et peut demander
  `filesystem.private.*`, son dossier privé d'app (comme le compte de service
  d'un vrai serveur web) pour ses données.
- `network.listen` lui est accordé d'office sur ses propres `Urls` ;
  `network.simulated.read`/`write` sont normales ; `network.real.access` reste
  dangereuse.

### 4. Site Blazor interactif

Un site Blazor ressemble à une app à fenêtre, mais son composant racine est
dessiné dans la zone de page du navigateur HackerOS au lieu d'une fenêtre. Il
navigue par l'URL de l'onglet (`NavigationManager` propre au site, relié à la
barre d'adresse). Il ne reçoit pas les services de fenêtre (`WindowAppBase`) ni
aucune capacité `filesystem.*`. Le composant est isolé par le CSS scopé ; le navigateur ne
lui transmet aucun service de l'OS hors du SDK permis.

### 5. Ouverture vers la compilation dans l'OS

Le catalogue ne suppose pas qu'une app `Web` vient de la solution : un futur
compilateur dans l'OS produira la même `WebAppDefinition` et le même manifeste,
installés par `IAppInstallationService`.

## Conséquences

- Le générateur de manifestes, le schéma et `AppCatalog` connaissent `Web` et
  `web.urls`.
- Le navigateur devient l'hôte d'affichage des sites Blazor.
- Un site Blazor n'a pas de `filesystem.private.*` non plus : son code tourne
  dans le navigateur de l'utilisateur, il garde ses données par les API MVC de
  son propre site.
- Les apps existantes ne changent pas.
