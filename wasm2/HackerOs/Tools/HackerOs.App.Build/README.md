# HackerOs.App.Build

Intégration de build des applications HackerOS pour un projet hors du dépôt HackerOS (ADR 0098 §2) :
génération du `manifest.json`, règles de frontière du SDK (HOSAPP0030), générateur de routes ASP.Hack
et agrégation des paquets d'applications par un hôte.

```xml
<PackageReference Include="HackerOs.App.Build" Version="0.9.0-preview.1" PrivateAssets="all" />
```

Un hôte ajoute `<HackerOsAppHost>true</HackerOsAppHost>`. Publié avec `PublishTrimmed=true`, il garde
entières les assemblies `HackerOs.*` et celles de chaque app installée (racines générées depuis les
manifestes, ADR 0106 et 0113) ; un projet d'app reçoit `IsTrimmable` et l'analyse de trimming s'il ne les
règle pas. Voir `docs/sdk-packages.md` et `docs/trimmed-publish.md` dans le dépôt.
