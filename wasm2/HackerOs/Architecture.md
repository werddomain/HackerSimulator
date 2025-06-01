# Architecture du projet HackerSimulator (C# / Blazor / WASM v2)

## Vue d’ensemble

Ce projet simule un OS modulaire en C# avec Blazor (WebAssembly).
L’architecture est inspirée d’un vrai système d’exploitation, avec une séparation stricte des responsabilités et des scopes d’isolation pour la sécurité et la maintenabilité.

## Structure des modules

- **Blazor UI** : Interface graphique, communication avec le Shell et les Applications via des services.
- **Shell** : Interpréteur de commandes, point d’entrée utilisateur, communique avec le Kernel via des interfaces.
- **Applications** : Gestionnaire d’applications, API pour apps tierces, isolation par scope.
- **System** : Services système, initialisation, gestion globale.
- **Kernel** : Gestion des processus, mémoire, interruptions, abstraction matérielle.
- **IO** : Système de fichiers, gestion des périphériques.
- **Network** : Pile réseau, gestion des connexions.
- **Driver** : Pilotes matériels, abstraction pour extensions.
- **Security** : Authentification, permissions, sandboxing.
- **User** : Gestion des profils, sessions, préférences.
- **Settings** : Paramètres globaux et utilisateurs.
- **Theme** : Gestion des thèmes et personnalisation UI.

## Dépendances et isolation

- **Blazor UI** dépend uniquement de `Shell`, `Applications`, `Settings`, `Theme`.
- **Shell** dépend de `Kernel`, `Applications`, `User`.
- **Applications** accèdent à `System`, `IO`, `Network` via des interfaces publiques, jamais directement au `Kernel`.
- **Kernel** est isolé : seuls `Shell` et `System` peuvent y accéder via des interfaces.
- **IO**, **Network**, **Driver**, **Security** sont accessibles uniquement via le `Kernel` ou des services système.
- **User**, **Settings**, **Theme** sont accessibles par le front-end, mais n’ont pas accès direct au `Kernel`.

## Scopes d’isolation

- **Kernel** : strictement isolé, aucune dépendance vers l’UI ou les modules utilisateurs.
- **Applications** : exécutées dans un scope sandboxé, accès limité via API.
- **Security** : module transversal, contrôle les accès entre modules.
- **Settings/Theme** : accessibles en lecture/écriture par l’UI et les apps, mais jamais par le `Kernel`.

## Exemple de structure de dossiers

```
wasm2\HackerOs\HackerOs\OS
  /Kernel
  /System
  /IO
  /Network
  /Driver
  /Security
  /User
  /Shell
  /Applications
  /Settings
  /Theme
  /UI (Blazor)
```

## Bonnes pratiques

- Utiliser l’injection de dépendances pour tous les services.
- Respecter les interfaces publiques pour la communication inter-modules.
- Tester chaque module indépendamment.
- Documenter les API exposées par chaque module.

---

Pour toute contribution, merci de respecter cette architecture et de proposer toute modification majeure via une discussion préalable.
