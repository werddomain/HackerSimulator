# Error Report Generator

Ce script génère un rapport des erreurs de compilation d'un projet .NET en format Markdown.

## Utilisation

### Exécution simple
```powershell
.\Generate-ErrorReport.ps1
```
Cette commande va:
- Compiler le projet dans le répertoire courant
- Générer un rapport des erreurs dans le fichier `erreurs-compilation.md` dans le répertoire courant
- Marquer toutes les erreurs comme résolues par défaut

### Options avancées
```powershell
.\Generate-ErrorReport.ps1 -OutputFile "chemin/vers/rapport.md" -ProjectPath "chemin/vers/projet" -MarkAsResolved $false
```

### Paramètres

- **OutputFile**: Chemin du fichier de sortie (défaut: "erreurs-compilation.md" dans le répertoire courant)
- **ProjectPath**: Chemin du projet à compiler (défaut: "." pour le répertoire courant)
- **MarkAsResolved**: Marquer les erreurs comme résolues (défaut: $true)

### Exécution avec chemin complet
Si vous exécutez le script depuis un autre répertoire, utilisez le chemin complet:

```powershell
& "c:\chemin\complet\vers\Generate-ErrorReport.ps1" -OutputFile "c:\chemin\sortie\rapport.md" -ProjectPath "c:\chemin\projet"
```

## Format du rapport

Le rapport généré inclut:
- Un en-tête avec la date, le chemin du projet et le nombre total d'erreurs
- Une liste des erreurs groupées par fichier
- Une section de statistiques avec le nombre d'occurrences de chaque type d'erreur

## Notes

- Le script utilise l'encodage Windows-1252 pour préserver les caractères accentués.
- Les erreurs sont représentées sous forme de liste à cocher dans le format Markdown:
  - `[ ]` pour les erreurs non résolues
  - `[x]` pour les erreurs résolues
