# HackerOs.Host

Ce projet ASP.NET Core sert d'hôte pour l'application Blazor WebAssembly HackerOS.

## 🚀 Démarrage rapide

### 1. Installation des prérequis

```bash
# Installer .NET (si nécessaire)
./install-dotnet.sh
```

### 2. Déploiement de l'application HackerOS

```bash
# Compiler et déployer HackerOS dans le Host
./deploy.sh
```

### 3. Lancement du serveur

```bash
# Démarrer le serveur
dotnet run
```

Le serveur sera accessible sur `http://localhost:5000`

## ⚙️ Configuration

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

## 📦 Publication et déploiement

### Publication automatique avec scripts

```bash
# Windows x64
./build.bat win-x64 Release

# Linux x64  
./build.sh linux-x64 Release

# macOS x64
./build.sh osx-x64 Release
```

### Publication manuelle

```bash
# Windows x64
dotnet publish HackerOs.Host -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Linux x64
dotnet publish HackerOs.Host -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# macOS x64
dotnet publish HackerOs.Host -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
```

### Exécution des binaires publiés

```bash
# Windows x64
./HackerOs.Host/bin/Release/net8.0/win-x64/publish/HackerOs.Host.exe

# Linux x64
./HackerOs.Host/bin/Release/net8.0/linux-x64/publish/HackerOs.Host

# macOS x64
./HackerOs.Host/bin/Release/net8.0/osx-x64/publish/HackerOs.Host
```

## 🔧 Fonctionnalités

- ✅ Hébergement de l'application Blazor WebAssembly HackerOS
- ✅ Configuration du port via appsettings.json
- ✅ Publication en tant qu'exécutable autonome
- ✅ Support du débogage Blazor WebAssembly
- ✅ Compression des réponses
- ✅ Gestion optimisée des fichiers WASM
- ✅ Fallback vers index.html pour le routage côté client
- ✅ Scripts de déploiement automatisés
- ✅ Installation automatique de .NET

## 📂 Structure du projet

```
HackerOs.Host/
├── Program.cs                 # Configuration du serveur ASP.NET Core
├── HackerOs.Host.csproj      # Fichier de projet
├── appsettings.json          # Configuration de production
├── appsettings.Development.json # Configuration de développement
├── README.md                 # Documentation
├── install-dotnet.sh         # Script d'installation .NET
├── deploy.sh                 # Script de déploiement
├── build.sh                  # Script de publication (Linux/macOS)
├── build.bat                 # Script de publication (Windows)
├── Properties/
│   └── launchSettings.json   # Profils de lancement
└── wwwroot/                  # Fichiers statiques de l'application
    └── index.html           # Page d'accueil (sera remplacée par HackerOS)
```

## 🔍 Troubleshooting

### Problème: .NET n'est pas installé
```bash
./install-dotnet.sh
```

### Problème: Le projet HackerOS ne compile pas
```bash
cd ../HackerOs
dotnet restore
dotnet build
```

### Problème: Le serveur ne démarre pas
```bash
# Vérifier la configuration du port
cat appsettings.json

# Vérifier que le port n'est pas utilisé
lsof -i :5000
```

### Problème: Les fichiers WASM ne se chargent pas
- Vérifiez que les types MIME sont correctement configurés dans `Program.cs`
- Assurez-vous que les fichiers sont présents dans `wwwroot/`

## 🚦 Flux de travail recommandé

1. **Développement**: Travaillez sur le projet HackerOS
2. **Test**: Utilisez `./deploy.sh` pour tester localement
3. **Publication**: Utilisez `./build.sh` pour créer les binaires de distribution
4. **Déploiement**: Copiez l'exécutable et lancez-le sur le serveur cible

## 📝 Notes importantes

- Le serveur démarre par défaut sur le port 5000
- Les fichiers de l'application doivent être dans le dossier `wwwroot/`
- La compilation croisée est supportée pour Windows, Linux et macOS
- Le serveur prend en charge le débogage Blazor WebAssembly en mode développement
