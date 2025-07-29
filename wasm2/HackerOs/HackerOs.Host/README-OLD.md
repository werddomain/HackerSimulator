# HackerOs.Host

Ce projet ASP.NET Core sert d'hôte pour l'application Blazor WebAssembly HackerOS.

## Configuration

Le serveur peut être configuré via le fichier `appsettings.json` :

```json
{
  "HackerOs": {
    "Host": {
      "Port": 5000,
      "EnableHttps": false,
      "HttpsPort": 5001
    }
  }
}
```

## Utilisation en développement

```bash
dotnet run --project HackerOs.Host
```

## Publication et déploiement

### 1. Publier le projet en tant qu'exécutable autonome

```bash
dotnet publish HackerOs.Host -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### 2. Exécuter le serveur publié

```bash
# Windows x64
./HackerOs.Host/bin/Release/net9.0/win-x64/publish/HackerOs.Host.exe

# Linux
./HackerOs.Host/bin/Release/net9.0/linux-x64/publish/HackerOs.Host
```

### 3. Publication pour différentes plateformes

```bash
# Windows x64
dotnet publish HackerOs.Host -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Linux x64
dotnet publish HackerOs.Host -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# macOS x64
dotnet publish HackerOs.Host -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
```

## Fonctionnalités

- ✅ Hébergement de l'application Blazor WebAssembly HackerOS
- ✅ Configuration du port via appsettings.json
- ✅ Publication en tant qu'exécutable autonome
- ✅ Support du débogage Blazor WebAssembly
- ✅ Compression des réponses
- ✅ Gestion des fichiers statiques
- ✅ Fallback vers index.html pour le routage côté client

## Structure du projet

```
HackerOs.Host/
├── Program.cs              # Configuration du serveur ASP.NET Core
├── HackerOs.Host.csproj   # Fichier de projet
├── appsettings.json       # Configuration de production
├── appsettings.Development.json # Configuration de développement
└── Properties/
    └── launchSettings.json # Profils de lancement
```

## Notes

- Le serveur démarre par défaut sur le port 5000
- La configuration du port peut être modifiée dans `appsettings.json`
- L'application Blazor WebAssembly est référencée via une ProjectReference
- Le serveur prend en charge le débogage Blazor WebAssembly en mode développement
