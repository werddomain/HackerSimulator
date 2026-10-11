# Interpréteur du shell `hsh`

Phase 3 du plan de reconstruction, chantier `rebuild/3-shell-interpreter`
([ADR 0050](adr/0050-shell-grammar-and-process-environment.md), sections 1 à 4
et 7, précisé par l'[ADR 0054](adr/0054-shell-exit-status-and-detached-execution.md)).

## Objectif

Un seul shell, sans interface, testable sans navigateur, utilisé par le
terminal et par `ExecuteCommandIntent`. Il comprend les tubes, les
redirections, `&&` et l'expansion des variables, lance chaque commande comme
processus enfant du shell et affiche la sortie au fil de l'eau.

```text
alice@hackeros:~$ cat notes.txt | grep todo | wc -l > count.txt && echo "fait : $?"
fait : 0
```

## Architecture

Tout est dans `Platform/HackerOs.Platform.Core/Shell/` :

| Type | Rôle |
|---|---|
| `ShellParser` | Découpe la ligne (lexer) puis vérifie la grammaire ; renvoie un `ShellCommandList` ou un `ShellSyntaxError` (code, message, position, longueur). Ne lève jamais d'exception pour une saisie. |
| `ShellSyntax.cs` | L'arbre : `ShellCommandList` → `ShellPipeline` → `ShellCommand` (mots + redirections) ; un `ShellWord` garde ses morceaux (`ShellLiteralPart`, `ShellVariablePart`, `ShellExitStatusPart`, `ShellTildePart`) pour développer sans jamais relire le résultat comme de la syntaxe. Codes d'erreur dans `ShellSyntaxErrorCodes`. |
| `ShellInterpreter` | Exécute l'arbre : expansion, alias, redirections, tubes, lancement par `AppLifecycleOrchestrator`, codes de sortie. Fournit aussi `GetInstalledCommands()` et l'exécution élevée de `sudo` ([`shell-commands.md`](shell-commands.md)). |
| `ShellSession` | État d'un shell : son processus (le processus `hsh` d'un shell hébergé, voir [`shell-host.md`](shell-host.md) ; celui de l'appelant pour `ExecuteCommandIntent`), l'historique, `LastExitCode`. Le répertoire et l'environnement sont **ceux du processus**, lus par `IAppEnvironmentGateway`. `ChangeDirectoryAsync` fait le `cd` (vérification, `PWD`, `OLDPWD`) ; `Fork()` donne la copie d'un sous-shell. |
| `ShellPipe` | Tube en mémoire de 64 Ki caractères entre deux étapes. |
| `ShellCommandResolver` | Recherche d'une commande (nom, alias de manifeste ou identifiant d'app) dans le catalogue ; déplacé depuis `TerminalCommandResolver`. |
| `ShellExecution.cs` | `ShellExecutionOptions` (dont `CredentialPrompt` pour `sudo` et `StandardInput` pour l'entrée du terminal), `ShellExecutionResult`, `ShellFailure`, `ShellElevation`. |
| `ShellSessionAccess` | `IShellSession` remis aux commandes qui déclarent `shell.control` (ADR 0051, [`shell-commands.md`](shell-commands.md)). |
| `ShellErrorText` | Texte court d'une erreur de fichier (`No such file or directory`, `Permission denied`…). Dans le SDK (`HackerOs.AppSdk`) depuis `4-sdk-network`, pour que `cd` l'utilise sans Platform Core. |

Côté terminal (`Shared/HackerOs.AppSdk.Terminal/`) :

| Type | Rôle |
|---|---|
| `TerminalPanel` | Discipline de ligne et affichage dans un terminal xterm.js ([`terminal-emulator.md`](terminal-emulator.md), ADR 0061) : édition de ligne, historique au clavier, complétion, fenêtre d'identifiants. Démarre son shell par `HackShell.StartAsync` ; chaque ligne passe par `IHackShell.ExecuteAsync` ([`shell-host.md`](shell-host.md)). |
| `TerminalOutputWriter` | `TextWriter` qui pousse chaque morceau écrit vers le terminal : la sortie apparaît pendant que la commande tourne. |
| `TerminalControlSequenceDecoder` | Décodeur des séquences d'effacement (`ESC[2J`), gardé dans l'API publique ; `TerminalPanel` ne s'en sert plus depuis l'ADR 0061 (xterm.js interprète toutes les séquences). |

`ShellParser` et `TerminalSession` de `HackerOs.AppSdk.Terminal` ont disparu.

## Grammaire

```text
liste       := pipeline ( "&&" pipeline )*
pipeline    := commande ( "|" commande )*
commande    := ( mot | redirection )+
redirection := ( ">" | ">>" ) mot
```

- Guillemets et échappements de l'ADR 0014 : `'…'` littéral ; `"…"` garde les
  espaces, développe les variables et comprend `\"`, `\\`, `\$`, `\n`, `\r`,
  `\t` (les autres `\x` restent tels quels) ; hors guillemets, `\` rend le
  caractère suivant littéral. `""` donne un argument vide.
- Les opérateurs n'ont pas besoin d'espaces : `ls|wc`, `echo a>f`.
- Refusés hors guillemets (`shell.syntax.unsupported`) : `;`, `||`, `&`, `<`,
  `2>` (un mot fait de chiffres devant `>`), `&>`, `>&`, `` ` ``, `$(`, `(`, `)`.
- `NOM=valeur` en premier mot : `shell.syntax.assignment` (« use export
  NOM=value »).
- Autres erreurs : `shell.syntax.unexpected-token` (opérateur en tête, en fin
  ou doublé), `shell.syntax.missing-redirect-target`,
  `shell.syntax.unterminated-quote`, `shell.syntax.trailing-escape`,
  `shell.syntax.bad-substitution` (`${` non fermé ou nom invalide).
- Une commande faite seulement d'une redirection est valide : `> f` crée ou
  vide `f`.

## Expansion

Juste avant de lancer un pipeline, pour toutes ses étapes à la fois :

- `$NOM`, `${NOM}` : variable du processus du shell, vide si absente ; `$`
  devant autre chose reste littéral (`$5`, `$ `).
- `$?`, `${?}` : code du dernier pipeline (ADR 0054).
- `~` et `~/…` en début de mot non entre guillemets : `HOME` ; `~bob` reste
  littéral.
- Pas de découpage en mots après expansion : `$X` reste un argument.
- **Alias** : si le premier mot est un mot simple non entre guillemets, le
  shell cherche `nom=valeur` dans `~/.hackeros/aliases` (lu avec l'identité de
  l'hôte). La valeur doit être une commande simple ; elle remplace le premier
  mot. `alias` lui-même n'est jamais remplacé. `unalias` et `rmalias` ne sont
  plus réécrits par le shell : ce sont des alias du manifeste d'`alias`, qui
  lit son `argv[0]` ([`shell-commands.md`](shell-commands.md)).

## Exécution

- **Tubes** : toutes les étapes démarrent en même temps, reliées par des
  `ShellPipe`. Écriture asynchrone : attend de la place ; lecture asynchrone :
  attend des données. Une lecture synchrone sur un tube vide encore ouvert lève
  `InvalidOperationException` ; une écriture synchrone n'attend jamais (ADR
  0054). Quand une étape se termine, son entrée se ferme : l'écrivain suivant
  reçoit `HackIOException("Broken pipe")`, et une commande qui la laisse
  passer sort avec `141`.
- **Flux** : entrée de la première étape vide (`TextReader.Null`) ; sortie de
  la dernière étape vers le terminal ; sortie d'erreur de toutes les étapes
  vers le terminal. `AppLaunchRequest` porte `StandardInput`,
  `StandardOutput`, `StandardError` ; quand ils sont fournis,
  `AppLaunchResult.StandardOutput`/`StandardError` sont vides.
- **Redirections** : ouvertes par le shell avant l'étape, avec le contexte de
  l'app hôte (ses capacités et les droits du fichier). `>` vide le fichier à
  l'ouverture ; `>>` fait un ajout vide pour créer le fichier ou prouver qu'il
  est modifiable. Plusieurs redirections : chacune est ouverte dans l'ordre,
  la dernière reçoit la sortie. Échec : `hsh: <cible>: <raison>`, étape non
  lancée, code `1`. Une étape redirigée au milieu d'un tube ferme tout de suite
  sa sortie vers l'étape suivante, qui lit une entrée vide. Le fichier est
  publié à la fin de l'étape (`HackFileStream`).
- **`&&`** : le pipeline suivant ne tourne que si le précédent vaut `0`. Code
  d'un pipeline : celui de sa dernière étape.
- **Codes** : `127` introuvable (ou désinstallée), `2` syntaxe, `130`
  interruption, `1` faute ou redirection, `141` tube cassé.
- **Interruption** : Ctrl+C annule le jeton : toutes les étapes reçoivent
  l'annulation et la liste s'arrête.
- **Processus** : chaque étape est lancée avec `ParentPid` = processus du shell
  et `WorkingDirectory` = répertoire du shell. `ps` montre donc
  `terminal → grep, wc`.
- **Aucun built-in** : toute commande vient du catalogue (ADR 0051). Celles
  qui agissent sur le shell (`cd`, `export`, `sudo`…) passent par
  `IShellSession` ([`shell-commands.md`](shell-commands.md)). Comme sous Linux,
  `sudo echo x > /etc/f` échoue : la redirection est ouverte par le shell non
  élevé.
- **Approbation ponctuelle** (ADR 0042) : si un pipeline d'une seule commande
  échoue sur un refus de permission, `PermissionApproval` peut fournir un
  jeton ; la commande est relancée une fois avec lui.

## Terminal

Depuis le chantier `4-sdk-shell-host` (ADR 0056 §7), le shell est son propre
processus `hsh`, enfant de la fenêtre Terminal (ou de l'app qui embarque
`TerminalPanel`, comme l'éditeur de code) : chaque panneau a le sien. Avant,
le shell partageait le processus de l'app hôte. `InitialCwd` est le répertoire
de départ du shell ([`shell-host.md`](shell-host.md)). L'invite vient du processus :
`utilisateur@machine:répertoire$` (machine lue dans `/etc/hostname`), `~` pour le dossier personnel,
`#` pour root.

Le terminal ne connaît aucun nom de commande : `cd`, `clear`, `help`, `sudo`
sont des commandes du catalogue, et la complétion propose les commandes
installées ([`shell-commands.md`](shell-commands.md)).

## `ExecuteCommandIntent`

`AppIntentDispatcher` exécute `CommandLine` avec le même interpréteur, en mode
non interactif, sortie en tampon dans `AppIntentDispatchResult`. `apps.launch`
de l'appelant est vérifié une fois. Avec `callerContext`, le shell est le
processus de l'appelant (et `WorkingDirectory` change ce processus) ; sans, il
est détaché (ADR 0054). Statut : `NotFound`, `Disabled`, `ElevationDenied` ou
`EntryPointFault` selon l'échec de la dernière étape, sinon `Dispatched` avec
le code de sortie (une erreur de syntaxe donne `Dispatched`, code `2` et
`ErrorCode` = code de l'erreur).

## Décisions clés

- Le shell n'a pas d'état de répertoire ou d'environnement à lui : celui du
  processus fait foi, ce qui garde `/proc/<pid>/cwd`, `ps` et les commandes
  cohérents.
- Expansion par pipeline, avant le lancement : des étapes concurrentes ne
  voient pas les effets les unes des autres.
- Écart volontaire avec POSIX repris de l'ADR 0014 : pas de découpage en mots
  après expansion.

## Tests

- `Tests/HackerOs.Platform.Core.Tests/Shell/ShellParserTests.cs` : guillemets,
  opérateurs, refus, erreurs, morceaux de mots.
- `Tests/HackerOs.Platform.Core.Tests/Shell/ShellPipeTests.cs` : attente,
  capacité, lectures synchrones, tube cassé.
- `Tests/HackerOs.Platform.Core.Tests/Shell/ShellInterpreterTests.cs` : bout en
  bout avec orchestrateur, processus et fichiers réels (arguments, tubes,
  `yes | head1` → 141, `&&`, `$?`, variables, redirections, parent et
  répertoire, interruption, alias, `ExecuteCommandIntent`, et les commandes
  du shell de [`shell-commands.md`](shell-commands.md)).
- `Tests/HackerOs.Platform.Core.Tests/Shell/ShellCommandResolverTests.cs`.

## Liste des tâches

- [x] Parseur en arbre avec erreurs structurées (`ShellParser`, `ShellSyntax`).
- [x] Expansion `$VAR`, `${VAR}`, `$?`, `~` sans découpage.
- [x] `ShellPipe` 64 Ki, tube cassé → 141.
- [x] Redirections `>` et `>>` ouvertes par le shell avec l'identité de l'hôte.
- [x] `&&`, codes de sortie, interruption.
- [x] `ShellInterpreter` et `ShellSession` dans Platform Core.
- [x] Flux sur `AppLaunchRequest`, sortie au fil de l'eau dans le terminal.
- [x] Commandes enfants du processus du terminal, dans son répertoire (depuis `4-sdk-shell-host` : enfants du processus `hsh`).
- [x] `ExecuteCommandIntent` par l'interpréteur.
- [x] Suppression de `ShellParser` et `TerminalSession` du terminal.
- [x] `cd`, `export`, `sudo`… du catalogue et `IShellSession` (chantier `3-shell-commands`, [`shell-commands.md`](shell-commands.md)).
