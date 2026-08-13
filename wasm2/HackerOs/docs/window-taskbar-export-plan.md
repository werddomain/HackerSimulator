# Plan d’extraction du système de fenêtres et de la barre des tâches

**Statut :** proposition d’architecture et plan de reprise  
**Cible immédiate :** réutilisation par plusieurs hôtes de `HackerOs.sln`  
**Cible future :** publication sous forme de packages NuGet versionnés  
**Code concerné :** `wasm2/HackerOs/Platform/HackerOs.Platform.Blazor/`

## 1. Objectif

Extraire le moteur de fenêtres, ses composants Blazor et la barre des tâches de
leur projet actuel afin qu’ils puissent être consommés par une autre application
de la solution sans dépendre du projet hôte `HackerOs.Ecosystem`.

L’extraction doit produire des projets utilisables immédiatement par références
de projets. Leurs API publiques, dépendances et ressources statiques doivent
toutefois être conçues dès maintenant pour permettre une publication NuGet
ultérieure sans réécriture majeure.

Le résultat ne doit pas exporter l’ensemble de HackerOS. Une application
consommatrice doit pouvoir utiliser le système de fenêtres et la barre des tâches
en fournissant ses propres sources d’applications, commandes de cycle de vie,
icônes, horloge, notifications et stratégie de thème.

## 2. État actuel et couplages à traiter

Le code actuel est principalement regroupé dans
`Platform/HackerOs.Platform.Blazor` :

- `Windows/WindowRuntime.cs` possède l’état et les transitions des fenêtres ;
- `Windows/WindowRuntimeState.cs` contient l’identité, la géométrie, le focus,
  l’ordre Z, la modalité et les contraintes ;
- `Windows/DesktopArea.razor`, `WindowHost.razor` et `WindowChrome.razor`
  effectuent le rendu Blazor ;
- `Shell/Taskbar.razor` injecte directement `WindowRuntime` et d’autres services
  HackerOS ;
- `Shell/DesktopShell.razor` compose fenêtres, taskbar, lanceur, notifications et
  session ;
- `WindowCloseCoordinator` dépend du cycle de vie applicatif HackerOS ;
- `WindowAppRenderer` dépend de `WindowAppBase`, `AppDescriptor` et du contexte
  applicatif HackerOS ;
- les identifiants de fenêtres référencent actuellement les identités de processus
  et d’instances de la simulation.

Le moteur headless est déjà bien séparé du DOM, mais son modèle d’identité et les
composants de shell restent liés au domaine HackerOS. La barre des tâches est plus
fortement couplée que le moteur de fenêtres et doit passer par des contrats
d’adaptation.

### 2.1 Changement récent : contenu générique et dialogues (2026-08)

Un couplage supplémentaire, absent de la rédaction initiale de ce document, existe
dans `Platform/HackerOs.Platform.Blazor/Dialogs/` : `FileDialogCoordinator`,
`DialogCoordinator` et `FileDialogWindowAdapter` créent des fenêtres owner-modal via
`WindowRuntime.Apply(new CreateWindowCommand(...))`, exactement comme
`WindowLaunchCoordinator` le fait pour les applications. Ces dialogues (sélection de
fichier, boîtes de message, saisie de texte) restent spécifiques à HackerOS
(système de fichiers virtuel, capacités) et ne font pas partie du périmètre exporté.

Ce couplage a toutefois motivé une extension générique déjà en place :
`WindowRuntimeState` porte désormais `Content` (`RenderFragment?`) et
`OnRequestClose` (`Func<Task>?`). `DesktopShell.razor` ne connaît plus les types de
dialogues concrets ; il rend `window.Content` lorsqu’il est fourni, et route la
fermeture demandée par l’utilisateur vers `window.OnRequestClose` quand il existe. Ce
mécanisme générique — pas les dialogues eux-mêmes — fait partie du moteur exporté (voir
3.3) : n’importe quel hôte peut désormais créer une fenêtre dont le contenu et la
logique de fermeture sont fournis par l’appelant, sans que le moteur ait besoin d’un
cas spécial par type de fenêtre.

## 3. Architecture cible

### 3.1 Projets proposés

Créer les projets suivants sous `wasm2/HackerOs/` :

```text
Shared/
  HackerOs.Windowing.Abstractions/
Platform/
  HackerOs.Windowing.Core/
  HackerOs.Windowing.Blazor/
  HackerOs.Taskbar.Blazor/
  HackerOs.Platform.Blazor/          # composition/adaptateurs HackerOS existants
Tests/
  HackerOs.Windowing.Core.Tests/
  HackerOs.Windowing.Blazor.Tests/
  HackerOs.Taskbar.Blazor.Tests/
Samples/
  HackerOs.Windowing.SampleHost/     # consommateur sans HackerOs.Ecosystem
```

Les noms pourront être ajustés avant création, mais les frontières de dépendances
doivent être conservées.

`HackerOs.sln` est un fichier `.sln` classique (pas au format XML `slnx`) : chaque
nouveau projet doit être ajouté explicitement via `dotnet sln add`, avec une entrée
`GlobalSection(ProjectConfigurationPlatforms)` et, si on le range dans un dossier de
solution existant (`Shared`, `Platform`, `Tests`, `Samples`), une entrée
`GlobalSection(NestedProjects)`. Aucune découverte automatique n’a lieu.

### 3.2 `HackerOs.Windowing.Abstractions`

Bibliothèque .NET sans dépendance Blazor, MudBlazor, navigateur ou hôte HackerOS.

Elle doit contenir :

- `WindowId` et les identifiants d’owner génériques ;
- `WindowBounds`, `WindowConstraints` et `WindowVisualState` ;
- les descriptions immuables de titre, icône et contenu logique — le contenu logique
  est un `RenderFragment` fourni par l’appelant (voir 2.1 et 3.3), pas un contrat
  opaque : la librairie exportée est de toute façon toujours consommée depuis Blazor,
  donc ce type n’ajoute aucun couplage réel à un hôte non-Blazor ;
- les commandes et événements de fenêtres ;
- les contrats de fermeture, confirmation et activation ;
- les contrats de source d’éléments pour la barre des tâches ;
- les contrats de commandes de barre : activer, réduire, restaurer, fermer,
  afficher l’accueil ou ouvrir une surface fournie par l’hôte ;
- des contrats de thème sous forme de noms de tokens, jamais de CSS arbitraire.

Le projet `Shared/HackerOs.AppSdk.Icons` existe déjà et fournit un modèle d’identité
d’icône indépendant du rendu ; les contrats d’icônes de fenêtre et de barre des tâches
doivent le réutiliser plutôt que d’en recréer un.

Les identités propres à HackerOS (`ProcessId`, `AppInstanceId`, `AppId`) ne doivent
pas être obligatoires dans le moteur exportable. Utiliser un identifiant de
propriétaire opaque validé, puis fournir un adaptateur HackerOS qui conserve les
relations processus/application existantes.

### 3.3 `HackerOs.Windowing.Core`

Bibliothèque headless contenant :

- `WindowRuntime` ;
- l’application atomique des commandes ;
- le focus et l’ordre Z ;
- déplacement, redimensionnement et contraintes ;
- minimisation, maximisation et restauration ;
- modalité et relation owner/enfant ;
- adaptation au changement de work area ;
- état sérialisable facultatif de géométrie ;
- événements déterministes observables par l’hôte.

Elle dépend de `HackerOs.Windowing.Abstractions` et peut référencer
`Microsoft.AspNetCore.Components` (uniquement pour le type délégué `RenderFragment`
porté par `WindowRuntimeState.Content`, cf. 2.1 et 3.2). Elle ne doit contenir aucun
composant `.razor`, aucun `RenderFragment` produit par le moteur lui-même, aucun
MudBlazor et aucune interop JavaScript : le moteur reste headless, il consomme
seulement le contenu que l’appelant lui fournit sans jamais le construire.

### 3.4 `HackerOs.Windowing.Blazor`

Razor Class Library contenant :

- `DesktopArea.razor` ;
- `WindowHost.razor` ;
- `WindowChrome.razor` ;
- les fichiers `.razor.css` associés ;
- les modules `.razor.js` strictement nécessaires aux Pointer Events, à la
  projection de géométrie et à la gestion du focus ;
- un composant racine paramétrable, par exemple `WindowSurface`, qui reçoit un
  runtime et un fragment de rendu de contenu ;
- des abstractions d’icônes et de chrome permettant au consommateur de choisir son
  rendu sans imposer MudBlazor.

Le package doit embarquer ses ressources via les mécanismes standards de Razor
Class Library (`_content/{PackageId}/...`). Aucun chemin ne doit dépendre du nom du
projet hôte.

### 3.5 `HackerOs.Taskbar.Blazor`

Razor Class Library séparée afin qu’un consommateur puisse utiliser le moteur de
fenêtres sans adopter la barre des tâches HackerOS.

La taskbar doit recevoir des interfaces plutôt que d’injecter directement :

- `WindowRuntime` concret ;
- `AppCatalog` ;
- `ISimulationClock` ;
- `INotificationQueue` ;
- `ISessionService` ;
- `AppIntentDispatcher`.

Contrats proposés :

- `ITaskbarWindowSource` : instantané ordonné des fenêtres affichables ;
- `ITaskbarCommandDispatcher` : activation, réduction, restauration et fermeture ;
- `ITaskbarLauncher` : ouverture du lanceur fourni par l’hôte ;
- `ITaskbarStatusSource` : heure, réseau, batterie simulée ou états personnalisés ;
- `ITaskbarNotificationSource` : compteur et ouverture du centre de notifications ;
- `ITaskbarSessionCommands` : verrouillage, déconnexion ou arrêt facultatifs ;
- `TaskbarOptions` : visibilité des zones, ordre, densité et labels.

Chaque fonctionnalité optionnelle doit disparaître proprement lorsque son contrat
n’est pas fourni. La taskbar ne doit pas créer un faux service global pour
remplacer une intégration absente.

### 3.6 `HackerOs.Platform.Blazor`

Le projet existant devient la couche de composition HackerOS :

- implémentation des adaptateurs entre les contrats exportables et
  `AppCatalog`/processus/sessions/notifications ;
- `WindowAppRenderer` et validation de `WindowAppBase` ;
- `WindowCloseCoordinator` relié à `AppLifecycleOrchestrator` ;
- shell Desktop et Mobile propres à HackerOS ;
- lanceur d’applications, dialogues, centre de notifications et UX de session.

Ainsi, les packages exportables ne connaissent pas le modèle complet de l’OS.

Vérifié dans le code actuel : `WindowCloseCoordinator` dépend de
`AppLifecycleOrchestrator` (`HackerOs.Platform.Core.Lifecycle`) et
`WindowAppRenderer` dépend de `AppDescriptor`
(`HackerOs.Platform.Core.Discovery`) ainsi que de `WindowAppBase`/
`IWindowCloseGuard` (`HackerOs.AppSdk.Blazor`). Les deux confirment donc bien la
frontière ci-dessus et restent dans `HackerOs.Platform.Blazor` sans modification
de signature publique lors de l’extraction. Le système de dialogues (2.1) suit la
même règle : `FileDialogWindowAdapter` reste dans `HackerOs.Platform.Blazor` et
n’utilise que le mécanisme générique `Content`/`OnRequestClose` du moteur exporté.

## 4. API de consommation attendue

Le consommateur interne doit pouvoir enregistrer les services sans connaître
l’implémentation :

```csharp
builder.Services.AddHackerOsWindowing(options =>
{
    options.InitialWorkArea = new WindowBounds(0, 0, 1280, 720);
});

builder.Services.AddHackerOsTaskbar();
```

La composition Razor doit rester simple :

```razor
<WindowSurface Runtime="WindowRuntime"
               WindowContent="RenderWindowContent" />

<HackerTaskbar WindowSource="TaskbarWindowSource"
               Commands="TaskbarCommands"
               Options="TaskbarOptions" />
```

Ces exemples représentent l’ergonomie recherchée. Les noms définitifs doivent
être choisis après la revue de l’API publique.

## 5. Stratégie de migration sans rupture

1. Copier d’abord les contrats génériques dans les nouveaux projets sans supprimer
   les types existants.
2. Écrire les tests de contrat du moteur extrait.
3. Faire utiliser le nouveau moteur par des adaptateurs conservant les signatures
   HackerOS actuelles.
4. Déplacer les composants Razor et leurs assets collocatés.
5. Remplacer les chemins d’import JS par les chemins `_content` du nouveau projet.
6. Extraire la taskbar après stabilisation du moteur et introduire ses sources de
   données abstraites.
7. Migrer `DesktopShell` vers les nouveaux composants.
8. Ajouter un hôte exemple qui ne référence ni `HackerOs.Ecosystem` ni les projets
   d’applications HackerOS.
9. Supprimer les anciens types seulement après réussite de tous les tests et
   consommateurs.
10. Conserver, si nécessaire, une façade obsolète pendant une version afin de
    faciliter les migrations internes.

## 6. Préparation à NuGet

Chaque projet destiné à être publié doit définir :

- `PackageId`, `Version`, `Authors`, `Description`, `PackageTags` et licence ;
- génération du package et des symboles ;
- documentation XML publique sans avertissement ;
- `README` inclus dans le package ;
- dépendances minimales avec versions compatibles explicites ;
- déterminisme de build et Source Link ;
- ressources statiques Razor correctement empaquetées ;
- compatibilité de trimming documentée et testée ;
- politique SemVer et baseline de compatibilité API.

Ne pas publier immédiatement. Produire d’abord les `.nupkg` localement et les
consommer depuis une source NuGet temporaire dans le sample host. Cela détectera
les dépendances transitives ou assets qui fonctionnent par référence de projet
mais manquent dans un package.

## 7. Thème et personnalisation

- Les composants exportables fournissent des valeurs de repli accessibles.
- Les couleurs, espacements, tailles, rayons et animations utilisent un ensemble
  documenté de variables CSS préfixées.
- Le consommateur surcharge les tokens dans sa feuille globale ; il ne fournit pas
  de texte CSS exécutable au composant.
- Le chrome et la taskbar exposent des fragments ou contrats d’icônes bornés.
- Les fonctionnalités de base restent utilisables sans MudBlazor.
- Si une variante MudBlazor est conservée, elle appartient à un package adaptateur
  facultatif, pas au moteur ou aux abstractions.

## 8. Tests requis

### Tests headless

- création et suppression ;
- focus unique et ordre Z ;
- déplacement et huit directions de redimensionnement ;
- contraintes min/max et changement de viewport ;
- minimisation, maximisation et restauration ;
- owner/modal, fermeture et restauration du focus ;
- ordre déterministe des événements ;
- séparation entre identité générique et métadonnées HackerOS.

### Tests de composants

- rendu du contenu fourni par un consommateur ;
- navigation clavier complète ;
- labels accessibles et réduction des animations ;
- taskbar avec toutes les sources facultatives présentes ou absentes ;
- adaptation du thème par tokens ;
- absence de CSS/JS inline dans Razor.

### Tests navigateur et packaging

- drag/resize mouse, touch et pen ;
- focus et ordre Z sans course de rendu ;
- import des modules depuis `_content` ;
- console et réseau sans erreur ;
- application exemple consommant les projets ;
- application exemple consommant les `.nupkg` locaux ;
- publication Release et analyse de trimming.

## 9. Risques à contrôler

- **Fuite du domaine HackerOS :** éviter que les types de processus, catalogue ou
  session deviennent des dépendances obligatoires du package.
- **API trop générique :** conserver des contrats orientés fenêtres et taskbar,
  sans construire un framework UI universel.
- **Assets manquants dans NuGet :** tester les packages produits, pas seulement les
  références de projets.
- **Interop fragile :** rendre l’initialisation framework incontournable et la
  destruction déterministe.
- **Double état :** `WindowRuntime` reste l’unique source de vérité.
- **MudBlazor transitif :** isoler ou supprimer cette dépendance dans les packages
  génériques.
- **Rupture mobile :** ne pas figer dans le moteur des hypothèses propres aux
  fenêtres flottantes Desktop. Le plan mobile est défini dans
  `docs/mobile-interface-platform-plan.md`.

## 10. Plan d’implémentation

- [~] `EXT-WIN-001` Créer `HackerOs.Windowing.Abstractions` et documenter son API
  publique. *Reporté* : les contrats génériques vivent pour l'instant dans
  `HackerOs.Windowing.Core` (voir 003/004) plutôt que dans un projet séparé,
  choix pris pour d'abord déplacer le moteur sans rupture ; le split
  Abstractions/Core proprement dit reste à faire avant la Phase B.
- [x] `EXT-WIN-002` Découpler les identités génériques des identités de processus
  HackerOS. `WindowRuntimeState` porte désormais `WindowOwnerId` (opaque,
  dérivé de l'instance d'app) au lieu de `ProcessId`/`AppInstanceId`.
- [x] `EXT-WIN-003` Déplacer le moteur headless dans `HackerOs.Windowing.Core`.
  Le projet ne référence plus que `Microsoft.AspNetCore.Components`, aucune
  dépendance HackerOS.
- [x] `EXT-WIN-004` Porter les tests du runtime et ajouter une suite de contrats
  indépendante de HackerOS. `HackerOs.Windowing.Core.Tests` (11 tests) ne
  référence que `HackerOs.Windowing.Core`.
- [x] `EXT-WIN-005` Créer la Razor Class Library `HackerOs.Windowing.Blazor` avec
  assets collocatés. `DesktopArea.razor`, `WindowHost.razor` et
  `WindowChrome.razor` (+ `.razor.css`/`.razor.js`) déplacés depuis
  `Platform.Blazor`, servis depuis `_content/HackerOs.Windowing.Blazor/...`.
  `WindowSurface` reste à faire (voir 3.4) : reporté à la Phase D une fois
  l'ergonomie de consommation réelle connue via la migration de `DesktopShell`.
- [x] `EXT-WIN-006` Éliminer les dépendances MudBlazor obligatoires du chrome
  exportable. `WindowChrome.razor` n'utilise plus `MudIconButton` ; boutons
  natifs + icônes SVG scoped-CSS. Vérifié au navigateur : minimiser,
  maximiser/restaurer, fermer et le geste de déplacement pointeur
  fonctionnent identiquement.
- [ ] `EXT-WIN-007` Définir les contrats de source et de commandes de taskbar.
- [ ] `EXT-WIN-008` Créer `HackerOs.Taskbar.Blazor` et ses tests de composants.
- [ ] `EXT-WIN-009` Implémenter les adaptateurs HackerOS dans
  `HackerOs.Platform.Blazor`.
- [ ] `EXT-WIN-010` Migrer `DesktopShell` sans modifier son comportement public.
- [ ] `EXT-WIN-011` Créer un sample host interne sans dépendance à
  `HackerOs.Ecosystem`.
- [ ] `EXT-WIN-012` Ajouter les métadonnées de packaging et produire des packages
  NuGet locaux.
- [ ] `EXT-WIN-013` Tester un sample consommant uniquement les packages locaux.
- [ ] `EXT-WIN-014` Ajouter baseline API, tests Release/trimming et documentation
  de versionnement.
- [ ] `EXT-WIN-015` Mettre à jour la solution, les documents d’architecture et la
  liste d’intégration.

## 11. Définition de complétion

L’extraction est terminée lorsque :

- une application de la solution utilise fenêtres et taskbar sans référencer
  `HackerOs.Ecosystem` ;
- le moteur headless ne dépend ni de Blazor ni des services HackerOS ;
- la taskbar consomme uniquement ses contrats publics ;
- HackerOS conserve son comportement via des adaptateurs explicites ;
- les tests headless, composants et navigateur passent ;
- les ressources statiques fonctionnent depuis une référence de projet et depuis
  un package NuGet local ;
- la publication Release ne produit aucun diagnostic inexpliqué ;
- la documentation publique permet à un autre développeur d’intégrer les packages
  sans lire leur code source.

