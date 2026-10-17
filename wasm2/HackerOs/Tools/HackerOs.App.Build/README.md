# HackerOs.App.Build

Intégration de build des applications HackerOS pour un projet hors du dépôt HackerOS (ADR 0098 §2) :
génération du `manifest.json`, règles de frontière du SDK (HOSAPP0030), générateur de routes ASP.Hack
et agrégation des paquets d'applications par un hôte.

```xml
<PackageReference Include="HackerOs.App.Build" Version="0.9.0-preview.1" PrivateAssets="all" />
```

Un hôte ajoute `<HackerOsAppHost>true</HackerOsAppHost>`. Voir `docs/sdk-packages.md` dans le dépôt.
