<!-- filepath: c:\Users\clefw\source\repos\HackerSimulator\project requirement v2.md -->
Création d'un simulateur de hack en TypeScript/JavaScript avec les caractéristiques suivantes:

# Objectif

* Jeu mixte éducatif et ludique simulant l'expérience d'un hacker.
* Système de missions via emails/contrats et mode libre (piratage de banques, arnaques, etc.).

# Interface utilisateur & Simulation OS

* **Style Visuel**: OS d'inspiration Linux avec une interface graphique complète, adoptant un **style visuel simple, fonctionnel et épuré (potentiellement minimaliste)** pour ne pas détourner l'attention du gameplay principal. Éviter les éléments visuels trop distrayants.
* **Composants UI**: Barre des tâches, bureau avec icônes, système de fenêtrage complet (minimiser, maximiser, restaurer, redimensionner, déplacer).
* **Types d'applications**: Applications fenêtrées classiques ("Windform/dialog") et applications en ligne de commande ("console/commande").
* **Simulation Système**:
    * Simulation visible (ex: via un moniteur système ou des indicateurs dans la barre des tâches) de l'**utilisation CPU et RAM**.
    * Cette utilisation est influencée par les applications lancées et les tâches en cours (ex: cracking).
    * Directement liée aux **mises à niveau matérielles** (plus de RAM permet plus d'apps, meilleur CPU accélère les tâches).
    * Simulation basique de **processus** pour les applications lancées.
* **Interaction Inter-Applications**: Permettre des interactions de base entre les fenêtres/applications, comme le **copier/coller de texte** et potentiellement le **glisser-déposer** (ex: fichier depuis l'explorateur vers l'éditeur).

# Terminal et système de commandes

* Terminal basé sur **xterm.js**.
* Système de commandes extensible:
    * Commande (`commande arg1 arg2 -option valeur`) -> Recherche du module `commande`.
    * Exécution: `module.exec({ args: ['arg1', 'arg2'], option: 'valeur' })`.
    * Arguments positionnels dans `args`, arguments nommés comme propriétés.
* Support des commandes Linux de base (ls, cd, mkdir, rm, cat, pwd, etc.).
* Support de commandes réseau simulées comme `curl`, `ping`, `nmap` (scan de ports sur IPs/réseaux simulés).

# Applications intégrées

* **Éditeur de code** (basé sur **Monaco Editor** pour une expérience riche type VS Code) (Pensser à peut-être utiliser CodeMirror):
    * Support coloration syntaxique JS/HTML/CSS et autres formats pertinents (scripts custom?).
    * Permet au joueur de **créer ses propres outils**, **automatiser des tâches répétitives** (scripts), et **écrire/modifier des exploits** pour des vulnérabilités spécifiques rencontrées.
    * Possibilité d'exécuter/interpréter ces scripts dans l'environnement simulé du jeu.
* **Navigateur web factice**:
    * Gestion des favoris, barre d'URL, historique simple.
    * Accès aux sites web intégrés (banque, emails, boutique, darkweb, forums, cibles...).
    * Inspecteur de code source basique (voir le HTML/CSS/JS simulé des pages).
* **Bloc-notes**: Édition de fichiers texte (.txt, .log, .conf...).
* **Calculatrice**: Fonctions de base et potentiellement scientifiques/programmeur.
* **Explorateur de fichiers**: Navigation graphique dans le système de fichiers simulé, opérations CRUD sur fichiers/dossiers.
* **Moniteur Système**: Visualisation de l'utilisation CPU/RAM simulée, liste des processus actifs.
* **Antivirus/Pare-feu**: Applications de sécurité simulées (configurables? à désactiver/contourner lors de certaines missions?).
* **Terminal**: L'interface principale pour les commandes.

# Système de fichiers (Simulé)

* Structure de dossiers inspirée de Linux (`/`, `/home/user`, `/bin`, `/etc`, `/tmp`, `/var/log`...).
* Utilisation de **IndexedDB** (préférable à localStorage pour la taille et les performances) pour la persistance.
* Interfaces claires (`IFileSystemProvider`) pour l'abstraction de la couche de stockage (préparation pour backend futur).

# Mécanismes de hack (Gameplay)

* **Exploitation de vulnérabilités**: Injection SQL simulée, XSS, failles logiques, buffer overflow simplifié, configuration serveur erronée sur les sites/services simulés.
* **Déchiffrement/Cracking**: Mini-jeux ou processus en ligne de commande pour mots de passe (dictionnaire, force brute simulée – vitesse dépend du CPU/GPU virtuel), fichiers chiffrés.
* **Ingénierie sociale**: Via emails, dialogues PNJ, récupération d'infos sur réseaux sociaux/forums simulés.
* **Attaques par force brute**: Logins, clés SSH/Wifi simulées.
* **Phishing**: Création de fausses pages (via l'éditeur?) et envoi d'emails.
* **Scanning de réseau**: Utilisation d'outils type `nmap` simulé pour découvrir des hôtes, ports ouverts, services vulnérables sur des réseaux virtuels.
* **Élévation de privilèges**: Exploiter des failles locales pour passer de simple utilisateur à root/admin sur les systèmes cibles.
* **Pivotement**: Utiliser une machine compromise pour attaquer d'autres machines sur le même réseau simulé.

# Architecture technique

* Développement en **TypeScript**.
* **Vanilla TS/JS** pour le cœur de l'OS et la gestion des fenêtres (pour contrôle total et potentiellement meilleures perfs). Utilisation possible de micro-librairies pour des tâches spécifiques (ex: gestion du drag-and-drop) si nécessaire, mais pas de framework UI global.
* Structure de dossiers organisée (confirmée): `core`, `commands`, `apps`, `missions`, `websites`, `server` (interfaces persistance).
* Architecture modulaire (modules ES6/TS), interfaces claires, événements custom pour la communication inter-modules.

# Gestion des sites web (Simulés)

* Logique "serveur" en TS/JS interceptant les requêtes du navigateur factice.
* Contrôleurs retournant du HTML/CSS/JS.
* Simulation d'API REST.

# Sites web intégrés (Exemples - confirmés)

* Banque
* Webmail
* Boutique (logiciels/hardware)
* Darkweb/Marché noir
* Forums hackers
* Sites cibles (entreprises, etc.)
* Réseaux sociaux simulés.

# Progression et économie

* Progression: Missions + maîtrise des outils.
* Monnaie virtuelle.
* Dépenses: Logiciels, matériel, exploits.
* Mesure succès: Argent, **Réputation** (positive/négative? Influence les missions/interactions?), missions, matériel/logiciels.
* **Système de mise à niveau du hardware (confirmé)**:
    * Composants: RAM (impacte multitâche/nombre d'apps), CPU (vitesse calculs/cracking), GPU (certains types de cracking/graphismes?), Stockage (taille dispo), Carte réseau (vitesse/capacités?).
    * Impact tangible: Vitesse, capacité multitâche, accès à logiciels/techniques gourmands.
    * Hardware spécialisé (ex: cartes pour cracking Wifi simulé).

# Sécurité et réalisme

* Systèmes de sécurité simulés (Pare-feu, IDS, Antivirus) avec règles/signatures à contourner.
* Exploits Zeroday (puissants mais périssables).
* **Équilibre Réalisme/Jouabilité**: Viser un **réalisme crédible** dans les concepts et les étapes d'un hack, mais **simplifier les aspects trop techniques ou fastidieux**. Le jeu doit rester **amusant et accessible** à un public intéressé par le sujet sans être expert. Les commandes et outils doivent être fonctionnels mais pas nécessairement aussi complexes que leurs équivalents réels.
* Références/clins d'œil aux outils/techniques réels.

# Personnalisation

* Fond d'écran, thèmes de couleur.
* Installation/désinstallation d'applications.
* Potentiellement: Partage de scripts/outils si backend ajouté.
* Pouvoir développer des applications ou scripts/commande pour progresser dans le jeu

# Système de profil et sauvegarde

* Profil local unique au début.
* Authentification: Nom d'utilisateur simple, **mot de passe optionnel** pour la version locale (si implémenté, hashage SHA-256 simple avant stockage).
* Sauvegarde automatique dans IndexedDB.

# Tutoriel et apprentissage

* Mission initiale guidée.
* Missions progressives.
* Système d'aide: commande `help`, `man <commande>` pour des manuels simulés.

# Support de langues

* Internationalisation (i18n) prévue.
* Fichiers de traduction JSON (Anglais, Français).

# Conception Mobile et Responsive

* **Support multi-plateformes**: Adaptation complète pour les appareils mobiles tout en conservant l'expérience desktop.
* **Détection de plateforme**: Système automatique de détection du type d'appareil avec possibilité de forcer manuellement la vue mobile/desktop.
* **Architecture MVVM**: Séparation claire entre la logique métier (Models), l'interface utilisateur (Views) et les contrôleurs de vue (ViewModels) pour faciliter l'adaptation multi-plateformes.

## Interface utilisateur mobile

* **Cibles tactiles**: Taille minimale de 44px × 44px pour tous les éléments interactifs, idéalement 48px × 48px pour les actions principales.
* **Espacement**: Minimum 8px entre les cibles tactiles adjacentes.
* **Zones d'interaction**: Élargissement des zones tactiles via padding ou bordures transparentes quand les éléments visuels sont plus petits que 44px.

```css
/* Exemple de dimensionnement correct pour cibles tactiles */
.touch-button {
  min-width: 44px;
  min-height: 44px;
  padding: 12px;
  margin: 4px;
}

/* Pour les éléments visuels plus petits avec zone d'interaction élargie */
.small-icon-button {
  width: 24px;
  height: 24px;
  padding: 12px; /* Crée une cible de 48px */
}
```

## Gestes et interactions tactiles

* **Gestes standard**:
  * Tap: Action primaire ou sélection
  * Double tap: Zoom ou action spéciale
  * Appui long: Menu contextuel ou actions secondaires
  * Glissement horizontal: Navigation, révéler des actions
  * Glissement vertical: Défilement du contenu
  * Pincement: Zoom avant/arrière
  * Rotation: Rotation du contenu

* **Gestes spécifiques aux applications**:
  * Terminal: Glissement vers la gauche pour accéder à l'historique des commandes
  * Explorateur de fichiers: Glissement vers le bas pour rafraîchir le répertoire
  * Navigateur: Glissement droite/gauche pour naviguer avant/arrière
  * Éditeur de code: Pincement pour zoomer le texte
  * Moniteur système: Glissement gauche/droite pour basculer entre les métriques

## Adaptation des composants système

* **Système de fenêtres**:
  * Fenêtres en plein écran pour l'application active sur mobile
  * Transitions fluides entre applications
  * Gestes de navigation (glissement pour minimiser, etc.)

* **Barre des tâches et navigation**:
  * Barre de navigation en bas de l'écran avec accès rapide aux applications
  * Lanceur d'applications organisé en grille
  * Gestionnaire de tâches compact avec représentation par cartes

* **Clavier virtuel**:
  * Support complet pour la saisie de texte
  * Configurations spécifiques pour le terminal (touches spéciales)
  * Autocomplètion et suggestions adaptées au contexte

## Adaptation des applications

* **Terminal**: 
  * Interface tactile avec support de clavier virtuel spécialisé
  * Accès à l'historique par gestes
  * Suggestions de commandes

* **Explorateur de fichiers**: 
  * Vue en grille/liste pour les fichiers
  * Sélection tactile et opérations via menu contextuel
  * Navigation entre dossiers par gestes

* **Navigateur**: 
  * Contrôles de navigation tactiles
  * Gestion des onglets optimisée pour le toucher
  * Mode plein écran pour le contenu

* **Moniteur système**: 
  * Onglets de performance glissables
  * Visualisations et graphiques adaptés au toucher
  * Interface simplifiée pour la gestion des processus

* **Éditeur de code**: 
  * Navigation dans le code par gestes
  * Clavier virtuel avec symboles de programmation
  * Sélection et manipulation du texte adaptées au toucher

## Implémentation technique

* **Design responsive**: 
  * Utilisation d'unités relatives (rem/em, %) plutôt que des pixels fixes
  * Media queries pour adaptation aux différentes tailles d'écran
  * Points de rupture standards (xs, sm, md, lg, xl)

```css
/* Exemple de layout responsive */
.container {
  display: flex;
  flex-direction: column;
  padding: 1rem;
}

/* Tablette et plus grand */
@media (min-width: 48em) {
  .container {
    flex-direction: row;
    flex-wrap: wrap;
  }
  
  .container > * {
    flex: 1 1 300px;
  }
}
```

* **Architecture des composants**:
  * Approche mobile-first pour le développement CSS
  * Séparation claire entre les vues desktop et mobile
  * Utilisation de la classe `GestureDetector` pour la gestion des interactions tactiles

```typescript
// Exemple d'implémentation de bouton mobile
class MobileButton {
  constructor(element: HTMLElement) {
    // Garantir une taille minimale
    element.style.minHeight = '44px';
    element.style.minWidth = '44px';
    
    // Ajouter le feedback tactile
    element.addEventListener('touchstart', () => {
      element.classList.add('active');
    });
    
    element.addEventListener('touchend', () => {
      element.classList.remove('active');
      // Ajout d'un petit délai pour assurer que l'utilisateur voit le feedback
      setTimeout(() => {
        // Exécuter l'action
      }, 50);
    });
    
    // Ajouter le support pour le feedback haptique si disponible
    if ('vibrate' in navigator) {
      element.addEventListener('click', () => {
        navigator.vibrate(10);
      });
    }
  }
}
```

## Tests et validation

* **Exigences de test mobile**:
  * Test sur au moins 3 tailles d'appareils différentes
  * Test en orientations portrait et paysage
  * Vérification sur appareils à performances variables
  * Test des méthodes d'entrée tactile et clavier
  * Validation sous différentes conditions réseau

* **Métriques de performance**:
  * Temps de chargement initial < 5 secondes sur réseau 3G
  * Poids total de page < 1MB
  * Animation à 60fps sur appareils de milieu de gamme
  * Optimisations pour réduire la consommation de mémoire

Un prompt pour l'utilisateur décrivant l'infrastructure de programmation/commandes pour que l'utilisateur puisse utiliser l'intelligence artificiel pour créer des outils et programmes dans le jeu.