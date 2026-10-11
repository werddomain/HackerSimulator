# Éditeur de code

`Apps/System/HackerOs.Apps.CodeEditor` (`org.hackeros.code-editor`) : éditeur
de code façon VS Code, sur Monaco 0.55 (ADR 0020) et disposé en atelier
(ADR 0062). Il ouvre des fichiers et des dossiers du système de fichiers
virtuel, cherche dans tout un dossier, et embarque des terminaux `hsh`.

L'ancienne documentation (passage de CodeMirror à Monaco, preuves d'août 2026)
est archivée dans [`old/code-editor.md`](old/code-editor.md).

## Fonctions

| Domaine | Ce que l'utilisateur fait |
| --- | --- |
| Dossier | File › Open Folder… (ou le bouton de l'explorateur vide) ; l'arbre du dossier s'affiche dans la barre latérale ; File › Close Folder. |
| Explorateur | Un clic montre un fichier sans quitter l'arbre (flèches, Suppr, F2 restent à l'arbre) ; double-clic ou Entrée l'ouvre et donne le focus à l'éditeur. Boutons « New File… », « New Folder… », « Refresh » ; le nom se tape en place dans le dossier sélectionné. Menu contextuel : Open, Rename, Delete (corbeille), Delete permanently (avec confirmation), New File…, New Folder…, Find in Folder…. Glisser-déposer, couper/copier sont ceux de `FileView`. |
| Onglets | Plusieurs fichiers ouverts, chacun avec son historique d'annulation, sa sélection et son défilement. Point « ● » sur un onglet modifié, croix au survol ; clic du milieu pour fermer ; nom du dossier ajouté quand deux onglets ont le même nom. Ctrl+Tab et Ctrl+Maj+Tab changent d'onglet. |
| Recherche | Ctrl+Maj+F ou l'icône loupe : recherche dans tous les fichiers du dossier, pendant la frappe. Options casse (Aa), mot entier (ab), expression régulière (.*), « files to include » et « files to exclude » (`*.cs`, `src/**`). Résultats groupés par fichier, repliables ; un clic ouvre le fichier et sélectionne l'occurrence. Le texte sélectionné dans l'éditeur sert de recherche initiale. |
| Ouverture rapide | Ctrl+P : liste les fichiers du dossier, filtrés en sous-suite floue (`edwin` trouve `EditorWindow.razor`), flèches et Entrée. Sans dossier ouvert, Ctrl+P ouvre la boîte « Open File ». |
| Édition | Tout Monaco : rechercher/remplacer dans le fichier (Ctrl+F, Ctrl+H), aller à la ligne (Ctrl+G), commenter (Ctrl+/), formater, palette de commandes de Monaco (F1), multicurseur, mini-carte. View › Word Wrap (Alt+Z), Show/Hide Minimap. |
| Enregistrement | Ctrl+S, Ctrl+Maj+S (Save As), File › Save All. Les fichiers du dossier ouvert passent par l'accès accordé au dossier. Le contenu non enregistré est sauvegardé pour récupération et restauré au prochain lancement. |
| Terminal | Ctrl+` ou l'icône du bas de la barre d'activité : panneau redimensionnable, onglets « + », un `hsh` par onglet, démarré dans le dossier ouvert. |
| Disposition | Barre latérale (Ctrl+B) et panneau terminal redimensionnables par glissement ; la zone Monaco prend toujours tout l'espace restant ; barre d'état avec Ln/Col, sélection, encodage, fin de ligne et langage (modifiable). |

Hors périmètre (ADR 0062) : compilation, exécution, « aller à la définition »,
IntelliSense entre fichiers, contrôle de source, débogueur, extensions.

## Architecture

| Élément | Rôle |
| --- | --- |
| `CodeEditorWindow.razor` / `.razor.cs` / `.razor.css` | Atelier : menus, barre d'activité, barre latérale, onglets, fil d'Ariane, zone Monaco, filigrane, panneau terminal, barre d'état. Possède la session, l'espace de travail, la récupération et le garde de fermeture. |
| `CodeEditorWindow.razor.js` | Module collocé : `createEditor` (Monaco + nom accessible), `attachWorkbench` (raccourcis capturés avant le navigateur et Monaco, puis `OnShortcut(nom)` ; poignées de redimensionnement qui posent `--sidebar-width` et `--panel-height` sur la racine). |
| `CodeEditor.monaco.entry.js` → `wwwroot/monaco/monaco.bundle.js` | Thème `hackeros-dark` et `createCodeEditor` : un modèle par onglet (`showDocument`, `closeDocument`), `setLanguage`, `reveal`, `runAction`, `getSelectedText`, `setOptions`, `layout`, `focus`, `destroy` ; renvoie `OnEditorChanged(id, contenu)` et `OnCursorChanged(ligne, colonne, sélection)`. |
| `CodeEditorSession`, `CodeEditorDocument` | Onglets et contenu en C# ; `RelocatePaths` suit un renommage de fichier ou de dossier. |
| `CodeEditorWorkspace` | Dossier ouvert : liste des fichiers (exclusions `.git`, `.svn`, `.hg`, `node_modules`, `bin`, `obj` ; au plus 5 000), création de fichiers et de dossiers, chemins relatifs. |
| `CodeEditorSearch.cs` | `CodeEditorTextSearch` (ligne par ligne, 1 000 occurrences par fichier, aperçu coupé autour de l'occurrence), `CodeEditorGlob`, `CodeEditorWorkspaceSearch` (5 000 occurrences au total, fichiers de plus de 1 Mio ignorés), `CodeEditorQuickOpen` (classement flou). |
| `CodeEditorExplorer`, `CodeEditorSearchView`, `CodeEditorQuickOpenPalette`, `CodeEditorCursorStatus` | Vues de la barre latérale, boîte Ctrl+P et position du curseur, chacune avec son CSS collocé. |

Le contenu de chaque fichier appartient à `CodeEditorDocument` (C#). Monaco
garde un modèle par onglet pour l'historique et la position ; quand un onglet
devient actif, `showDocument` ne remplace le texte du modèle que s'il diffère
de celui du document (rechargement, récupération).

Pour éviter de redessiner tout l'atelier à chaque touche, `OnEditorChanged`
ne redessine la fenêtre que si l'état « modifié » de l'onglet change, et la
position du curseur est affichée par `CodeEditorCursorStatus`, qui se
redessine seul.

Entrée dans la boîte Ctrl+P et dans la zone de création de l'explorateur
agit au relâchement de la touche : l'éditeur reprend le focus quand la boîte
se ferme, et un traitement à l'appui laisserait la fin de la frappe insérer
un saut de ligne dans le fichier.

## Raccourcis

| Raccourci | Action |
| --- | --- |
| Ctrl+N / Ctrl+O | Nouveau fichier texte / ouvrir un fichier |
| Ctrl+S / Ctrl+Maj+S | Enregistrer / enregistrer sous |
| Ctrl+W | Fermer l'onglet |
| Ctrl+P | Aller au fichier (ouverture rapide) |
| Ctrl+Maj+F / Ctrl+Maj+E | Vue Recherche / vue Explorateur |
| Ctrl+B | Afficher ou masquer la barre latérale |
| Ctrl+` | Afficher ou masquer le terminal |
| Ctrl+Tab / Ctrl+Maj+Tab | Onglet suivant / précédent |
| Alt+Z | Retour à la ligne automatique |
| F1, Ctrl+F, Ctrl+H, Ctrl+G, Ctrl+/ | Monaco (palette, rechercher, remplacer, aller à la ligne, commenter) |

Dans le panneau terminal, seul Ctrl+` (masquer) est intercepté ; les autres
touches vont au shell. Chrome garde Ctrl+N, Ctrl+W et Ctrl+T pour lui tant que
HackerOS n'est pas installé en PWA ; chaque commande a aussi son entrée de
menu.

## Mettre à jour Monaco

Comme avant : modifier `package.json`, puis dans
`Apps/System/HackerOs.Apps.CodeEditor` lancer `npm ci && npm run build`, et
versionner `wwwroot/monaco/`. Le build .NET n'exécute pas npm.

## Tâches

- [x] Reproduire les défauts (zone d'édition réduite à une ligne, pas de
  création dans l'arbre, onglets qui perdent leur état).
- [x] Monaco : un modèle par onglet, curseur, `reveal`, actions, options,
  thème `hackeros-dark`.
- [x] `CodeEditorWorkspace` : liste des fichiers, création, chemins relatifs.
- [x] Recherche dans le dossier (casse, mot entier, regex, globs, contenu non
  enregistré) et ouverture rapide floue.
- [x] Vues Explorateur (création en place, menu contextuel, corbeille) et
  Recherche ; boîte Ctrl+P.
- [x] Atelier : menus, barre d'activité, barre latérale et terminal
  redimensionnables, fil d'Ariane, filigrane, barre d'état Ln/Col.
- [x] Raccourcis de VS Code capturés par le module collocé.
- [x] Onglets qui suivent un renommage ; Save All ; Close All.
- [x] `FileView` : le clic droit sur un élément n'ouvre plus le menu du fond.
- [x] Tests : espace de travail, recherche, globs, ouverture rapide,
  déplacement des onglets.
- [x] Essai dans Chromium : dossier, création, recherche, Ctrl+P, Ctrl+S,
  suppression, menu contextuel, redimensionnement.
