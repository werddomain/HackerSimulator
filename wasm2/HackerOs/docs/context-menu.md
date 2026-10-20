# Menu contextuel de l'OS (`hackeros.context-menu`)

## Objet

Un seul menu contextuel pour tout l'OS, multi-niveaux, dessiné par la
plateforme au thème de l'OS, qu'une app attache à n'importe quel élément de sa
fenêtre sans écrire de JavaScript. Clic droit, appui long au doigt, Touche Menu
et Maj+F10 l'ouvrent ; le clavier et les lecteurs d'écran s'y déplacent comme
dans le menu de Windows ou de GNOME. C'est la base des menus du contrôle de
fichiers (chantier 3) et des entrées inscrites par les apps (chantier 4) du plan
[`desktop-and-context-menu-plan.md`](desktop-and-context-menu-plan.md).

Décisions : [ADR 0150](adr/0150-os-context-menu.md) ; ajustements de la mise en
œuvre : [ADR 0154](adr/0154-context-menu-implementation-adjustments.md).

Depuis le chantier 3 (ADR 0155), les menus du contrôle de fichiers
(`HackFileView`) sont des menus de l'OS : le fournisseur de l'explorateur
définit un menu par contrôle, le remplit à chaque ouverture (plan §7) et le
passe à `ContextMenuOpening` de l'hôte ; le `MudMenu` de `FileView` n'est plus
dessiné quand la vue est liée au menu de l'OS. Voir
[`embedded-components.md`](embedded-components.md#hackfileview-et-ihackfileview).

## Utiliser un menu dans une app

### Dans le markup

```razor
@using HackerOs.AppSdk.Blazor.ContextMenus
@using HackerOs.AppSdk.Icons

<HackContextMenu Name="rows" Opening="OnRowsMenuOpening">
    <HackMenuItem Id="open" Title="@L["Menu.Open"]" Icon="OpenIcon" OnClick="@(item => OpenAsync((Row)item!))" />
    <HackMenuGroup>
        <HackMenuItem Id="export" Title="@L["Menu.Export"]">
            <HackMenuItem Id="csv" Title="CSV" OnClick="@(item => ExportAsync((Row)item!, "csv"))" />
            <HackMenuItem Id="json" Title="JSON" OnClick="@(item => ExportAsync((Row)item!, "json"))" />
        </HackMenuItem>
    </HackMenuGroup>
    <HackMenuGroup>
        <HackMenuItem Id="delete" Title="@L["Menu.Delete"]" Shortcut="Del" OnClick="@(item => DeleteAsync((Row)item!))" />
    </HackMenuGroup>
</HackContextMenu>

@foreach (Row row in _rows)
{
    <tr tabindex="0" @attributes="@(Menus.Attach("rows", row))">…</tr>
}

@code {
    private static readonly HackIcon OpenIcon = HackIcons.Vector(IconLibrary.Lucide, "folder-open");

    // Avant chaque ouverture, pour l'objet attaché : masquer, désactiver, ajouter, annuler.
    private void OnRowsMenuOpening(HackMenuOpeningEventArgs args)
    {
        if (args.Item is Row { IsReadOnly: true })
        {
            args.Menu.Disable("delete");
        }
    }
}
```

- `HackContextMenu` ne dessine **rien** là où il est écrit : il décrit le menu
  à l'OS. Le nom (`Name`) est unique dans la fenêtre.
- `Menus` existe sur `WindowAppBase` et sur `HackComponentBase`.
  `Menus.Attach(nom, objet)` renvoie deux attributs (`data-hack-menu`,
  `data-hack-menu-token`) ; l'objet reste en C#, sous un jeton opaque. Le même
  objet attaché à nouveau garde son jeton (par référence pour un objet, par
  valeur pour une chaîne ou un type valeur).
- L'élément le plus proche qui porte un jeton gagne : une ligne dans une liste
  attachée à son tour ouvre le menu de la ligne, le vide de la liste celui de la
  liste.
- Un attribut dont la valeur contient des guillemets s'écrit `@(…)` ou entre
  apostrophes : `@attributes="@(Menus.Attach("rows", row))"`.
- L'action (`OnClick`) reçoit l'objet attaché et s'exécute dans le contexte de
  l'app, comme un `@onclick` du composant qui l'a déclarée (le composant est
  redessiné après).
- Pour qu'une ligne s'ouvre au clavier (Touche Menu, Maj+F10), elle doit
  pouvoir prendre le focus (`tabindex="0"`).
- Préférer `IsVisible="false"` à un `@if` autour d'une entrée : une entrée qui
  apparaît après le premier rendu va à la fin de son niveau.

### En C#

```csharp
protected override void OnAppInitialized()
{
    Menus.Define("rows-area", menu => menu
        .Item("new", L["Menu.New"], _ => CreateAsync(), icon: HackIcons.Vector(IconLibrary.Lucide, "file-plus"))
        .Group(group => group
            .Submenu("sort", L["Menu.SortBy"], sort => sort
                .Item("sort-name", L["Menu.Name"], _ => SortAsync("name"))
                .Item("sort-date", L["Menu.Date"], _ => SortAsync("date"), isDisabled: true))));
}
```

- Même modèle que le markup ; le constructeur est rappelé à chaque ouverture,
  donc les titres suivent la langue et l'état courants.
- `Define` renvoie un `IDisposable` ; la fermeture de la fenêtre retire aussi le
  menu. Un menu C# et un menu markup du même nom dans une fenêtre sont une
  erreur (`InvalidOperationException`).

### Depuis un bouton

```razor
<button @onclick="@(args => Menus.OpenAsync("rows-area", null, args.ClientX, args.ClientY))">⋯</button>
```

`HackContextMenu.Controller` (`IHackContextMenu`) donne aussi `OpenAsync(objet, x, y)`
et `Close()`.

L'exemple complet est dans l'app d'exemple `HackerOs.Samples.WindowApp`
(section « Context Menu »).

## Modèle

| Type | Rôle |
|---|---|
| `HackMenuDefinition` | Un menu : `Name`, `Label` (nom accessible), `Groups` ; `Find`, `Hide`, `Disable`, `AddGroup`, `Clone`, `Validate` ; `MaxDepth = 3` |
| `HackMenuGroupDefinition` | Un groupe d'entrées : un séparateur n'est dessiné qu'entre deux groupes visibles |
| `HackMenuItemDefinition` | Une entrée : `Id`, `Title` (déjà traduit), `Icon`, `IsDisabled`, `IsVisible`, `Shortcut` (texte seulement), `OnClick`, `Children` (sous-menu, groupes) |
| `HackMenuBuilder` | Construction en C# : `Item`, `Submenu`, `Group` |
| `HackMenuOpeningEventArgs` | `Menu` (copie de cette ouverture), `Item`, `Cancel` |
| `HackIcons.Vector(bibliothèque, nom)`, `HackIcons.FromImage(chemin)` | Icône décrite (`HackIcon`), dessinée par l'OS (`HackerOs.AppSdk.Icons`) |
| `HackMenus` | `Menus` d'un composant : `Attach`, `Define`, `OpenAsync`, `Close`, `IsAvailable` |
| `IHackMenuHost` | Côté OS des menus d'une fenêtre (mis en œuvre par la plateforme, donné en cascade) |
| `IHackContextMenu` | Contrôleur du contrat : `Name`, `OpenAsync`, `Close` |

Règles vérifiées à chaque ouverture (`Validate`) : identifiants présents et
uniques dans tout le menu, trois niveaux au plus, pas d'action sur une entrée
qui ouvre un sous-menu. Le constructeur C# et `HackMenuItem` refusent un
quatrième niveau dès la construction.

### Séparateurs

Après les filtres (`IsVisible`, ajustements de `Opening`) :

1. un groupe sans entrée visible disparaît ;
2. un sous-menu sans entrée visible masque son entrée parente ;
3. un séparateur n'est dessiné qu'entre deux groupes visibles : jamais en tête,
   en fin, ni deux à la suite. La règle vaut à chaque niveau.

## Architecture

```text
App (SDK)                                         Plateforme (Platform.Blazor)
<HackContextMenu Name="rows">  ── contrat ──►     PlatformContextMenu (HackPlatformComponent)
   HackMenuItem / HackMenuGroup                       │ inscrit le menu dans la fenêtre
<tr @attributes="Menus.Attach("rows", row)">          ▼
        │ data-hack-menu-token              ContextMenuScope (une par fenêtre, IHackMenuHost)
        ▼                                             │
ContextMenuOverlay.razor.js ── jeton, x, y ──► ContextMenuService (un menu ouvert dans tout l'OS)
  (clic droit, appui long, Touche Menu,               │ Build → copie → Opening → Validate → Arrange
   Maj+F10, fermeture, jetons libérés)                ▼
                                             ContextMenuOverlay (bureau : panneaux ; mobile : feuille)
```

- **Contrat.** `HackContextMenu` est un composant typé
  (`HackContractComponent<IHackContextMenu>`, ADR 0153) ; la plateforme fournit
  `hackeros.context-menu` par `HackPlatformComponent` (`PlatformContextMenu`),
  sans manifeste ni chargement d'app. `PlatformContextMenu` ne dessine rien : il
  inscrit le menu dans la fenêtre et rend le contrôleur.
- **Fenêtre.** `WindowAppRenderer` crée une `ContextMenuScope` par fenêtre
  (`ContextMenuService.CreateScope(AppContext)`) et la donne en cascade
  (`IHackMenuHost`) ; elle est détruite avec la fenêtre (menus, objets attachés,
  menu ouvert). Une zone de l'OS (bureau, barre des tâches) crée la sienne avec
  `CreateScope(null)`.
- **Service.** `ContextMenuService` (scoped, comme `WindowRuntime`) garde le
  seul menu ouvert (`Current`, `Changed`). À l'ouverture : copie fraîche du menu,
  `Opening` dans le contexte de l'app, vérification, mise en page
  (`ContextMenuLayout.Arrange`). Un menu qui échoue n'ouvre rien et l'erreur va
  dans `/var/log/syslog` (`ILogger`) ; l'OS continue. Une action qui lève est
  écrite au journal de la même façon.
- **Rendu.** `ContextMenuOverlay` est dans `DesktopShell` (panneaux flottants)
  et `MobileShell` (feuille du bas, `Presentation="Sheet"`). Composant natif avec
  CSS isolé, pas `MudMenu`. Le clavier passe par `ContextMenuNavigator`, testable
  sans navigateur.
- **Script.** `ContextMenuOverlay.razor.js`, module du composant importé par
  le C# (aucun hôte ne le référence) : écoute `contextmenu`, l'appui long
  (500 ms, 10 px), la Touche Menu et Maj+F10 sur l'élément qui a le focus ;
  annule le menu du navigateur sur un élément attaché ; place les panneaux
  dans la fenêtre du navigateur ; ferme le menu sur un appui dehors, un
  défilement, un redimensionnement ou la perte du focus ; rend le focus à
  l'élément d'origine ; signale les jetons des éléments retirés de la page.

### Ce que fait la souris, le doigt et le clavier

| Geste | Effet |
|---|---|
| Clic droit sur un élément attaché | Le menu s'ouvre au pointeur ; rien n'est en surbrillance |
| Appui long au doigt ou au stylet (500 ms sans bouger de plus de 10 px) | Le menu s'ouvre (feuille du bas sur mobile) ; le clic qui suit le lever du doigt est absorbé |
| Touche Menu, Maj+F10 | Le menu s'ouvre au coin de l'élément qui a le focus, première entrée en surbrillance |
| Haut, Bas | Entrée précédente ou suivante, en boucle, sans séparateur ni entrée désactivée |
| Début, Fin | Première ou dernière entrée |
| Droite, Entrée sur une entrée parente | Ouvre le sous-menu sur sa première entrée |
| Gauche | Ferme le sous-menu |
| Entrée, Espace | Choisit l'entrée |
| Échap | Ferme un niveau ; au premier niveau, ferme le menu |
| Tab | Ferme le menu, comme Windows |
| Une lettre | Va à l'entrée suivante qui commence par elle |
| Survol d'une entrée parente | Ouvre son sous-menu (sur bureau) |
| Clic ou appui hors du menu, défilement, redimensionnement, perte du focus | Ferme le menu ; le clic continue vers sa cible |

Un champ de texte dans un élément attaché garde le menu du navigateur
(copier, coller), sauf s'il est lui-même attaché. Un élément sans jeton dans une
zone `data-hack-menu-suppress` (une zone de l'OS qui a son propre menu) n'ouvre
pas non plus le menu du navigateur. Un `contextmenu` déjà annulé par l'app
(`@oncontextmenu:preventDefault`) est laissé à l'app.

### Accessibilité

`role="menu"` sur chaque panneau (nom accessible : `Label`, sinon « Menu
contextuel » ; le titre de l'entrée parente pour un sous-menu),
`role="menuitem"`, `role="separator"`, `aria-haspopup="menu"`,
`aria-expanded` et `aria-disabled` écrits en `"true"`/`"false"`
([`old/common-pitfalls.md`](old/common-pitfalls.md) §8), surbrillance annoncée
par `aria-activedescendant`. Le focus revient à l'élément d'origine à la
fermeture, avant l'action choisie (une boîte ou un renommage en place qu'elle
ouvre garde donc son focus). Mouvement réduit : pas d'animation.

### Limites connues

- Une fenêtre détachée dans une autre fenêtre du navigateur (ADR 0078) garde le
  menu du navigateur : le menu de l'OS n'est dessiné que dans le document du
  bureau.
- L'ordre des entrées du markup est celui de leur premier rendu (voir
  `IsVisible` plus haut).

## Tests

- `Tests/HackerOs.AppSdk.Blazor.Tests/HackContextMenuTests.cs` : groupes
  implicites, quatrième niveau refusé, validation, copie, `Menus` hors fenêtre,
  `Define`, description du menu du markup au fournisseur, action dans le
  contexte de l'app, rien dessiné sans fournisseur.
- `Tests/HackerOs.Platform.Blazor.Tests/ContextMenus/` : repli des séparateurs
  (exemple « Ouvrir avec » du plan), sous-menus vides, clavier et survol,
  jetons (même objet, valeurs, libération), menus en double, `Opening` sur une
  copie, menus annulés ou cassés, un seul menu ouvert dans l'OS, action dans le
  contexte de l'app après la fermeture, fournisseur de la plateforme de bout en
  bout.
- `Tests/HackerOs.Ecosystem.Tests` : le conteneur de services résout le contrat
  vers `PlatformContextMenu` sans charger d'app.

## Tâches (chantier 2)

- [x] Modèle du SDK (`HackMenuDefinition`, groupes, entrées, trois niveaux, validation, copie).
- [x] Construction en C# (`HackMenuBuilder`, `Menus.Define`).
- [x] Markup (`HackContextMenu`, `HackMenuItem`, `HackMenuGroup`) sur le contrat `hackeros.context-menu`.
- [x] `Menus.Attach` et jetons ; `Menus` sur `WindowAppBase` et `HackComponentBase`.
- [x] `HackIcons` (vectorielle ou image).
- [x] Fournisseur de la plateforme (`PlatformContextMenu`), une portée par fenêtre (`WindowAppRenderer`).
- [x] Service : un menu ouvert dans l'OS, `Opening`, repli des séparateurs, erreurs au journal.
- [x] Rendu natif multi-niveaux (bureau) et feuille du bas (mobile), thème de l'OS, ARIA.
- [x] Script : clic droit, appui long, Touche Menu, Maj+F10, placement à l'écran, fermeture, focus rendu, jetons libérés.
- [x] Clavier (`ContextMenuNavigator`).
- [x] Exemple dans `HackerOs.Samples.WindowApp`.
- [x] Tests, ADR 0154, index des documents.
