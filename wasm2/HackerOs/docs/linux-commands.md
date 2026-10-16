# Commandes Linux du shell

Commandes ajoutées ou revues par l'[ADR 0064](adr/0064-posix-shell-scripts-jobs-and-app-commands.md)
pour que le terminal se comporte comme un shell Linux. Elles vivent dans
`Apps/Commands/HackerOs.Commands/` (un dossier par commande, ADR 0051), ne
dépendent que du SDK public (ADR 0055) et sont des apps système. Le langage du
shell lui-même (tubes, redirections, scripts, jobs, apps lancées par leur nom)
est décrit dans [`shell-interpreter.md`](shell-interpreter.md) ; les
commandes qui agissent sur le shell (`cd`, `export`, `sudo`…) dans
[`shell-commands.md`](shell-commands.md).

```text
alice@hackeros:~$ cut -d, -f1 users.csv | sort | uniq -c | sort -rn | head -3
alice@hackeros:~$ zip -r backup.zip projets && unzip -l backup.zip
alice@hackeros:~$ chmod +x deploy.sh && ./deploy.sh
alice@hackeros:~$ sleep 30 & jobs; kill %1
alice@hackeros:~$ watch -n 1 'ps | wc -l'
```

## Principes

- **Mimer le métal** : messages, options et codes de sortie de coreutils,
  procps, iproute2, net-tools, Info-ZIP et systemd ; chaque valeur affichée
  vient d'une vraie source de l'OS (`/proc`, `HackDriveInfo`,
  `HackServiceController`, réseau simulé) ou est documentée comme simulée.
- Les fichiers passent par l'API `Hack*` (ou la passerelle de fichiers pour
  les commandes plus anciennes) : chemins relatifs au répertoire de la
  commande, permissions et capacités de la commande.
- Une commande plein écran (`top`, `htop`, `watch`, `nano`) n'a l'écran
  alternatif que si sa sortie est le terminal ; dans un tube ou redirigée,
  elle produit du texte (`top -b`).
- `Common/` : `CommandIO` (fichiers ou entrée standard, tailles `-h`),
  `Wildcard`, `SystemSnapshot` (`top`, `htop`), `NetworkInfo` (`ip`,
  `netstat`).

## Texte

| Commande | Options | Notes |
|---|---|---|
| `sort` | `-r -n -h -V -f -b -u -c`, `-k N[,M]`, `-t SEP`, `-o FICHIER` | Tri stable avec comparaison finale octet par octet, comme GNU sort ; options combinables (`-rn`). |
| `head` | `-n N`, `-N`, `-n -N` (tout sauf les N dernières), `-c N`, `-q`, `-v` | Lit l'entrée standard au fil de l'eau : `yes \| head` se termine. |
| `tail` | `-n N`, `-N`, `-n +N`, `-c N`, `-f`, `-q`, `-v` | `-f` suit le fichier (relu toutes les 500 ms) jusqu'à Ctrl+C ; signale `file truncated`. |
| `cut` | `-f LISTE` avec `-d SEP`, `-s`, `--output-delimiter`, `-c`/`-b LISTE`, `--complement` | LISTE : `N`, `N-M`, `N-`, `-M`, séparés par des virgules. |
| `tee` | `-a` | Copie l'entrée vers la sortie et chaque fichier ; un fichier inaccessible n'arrête pas les autres (code 1). |
| `uniq` | `-c -d -u -i`, `[ENTRÉE [SORTIE]]` | Lignes adjacentes. |
| `printf` | `%s %b %c %d %i %u %o %x %X %e %f %g %%`, drapeaux, largeur, précision, `*` | Format réutilisé tant qu'il reste des arguments ; échappements `\n \t \0NNN \xHH \c`. |
| `seq` | `[PREMIER [PAS]] DERNIER`, `-s SEP`, `-w` | Entiers ou décimaux. |
| `basename`, `dirname` | `basename NOM [SUFFIXE]`, `-a`, `-s` | Texte seulement, comme coreutils. |

## Fichiers

| Commande | Options | Notes |
|---|---|---|
| `chmod` | `MODE` octal ou symbolique (`+x`, `u+x`, `go-w`, `a=r`, `u=rwx,g=rx,o=`, `g+u`, `X`), `-R`, `-v`, `-c`, `-f` | Sans lettre de classe, le masque `022` s'applique (`+w` = propriétaire seul) ; les bits spéciaux (4ᵉ chiffre) sont ignorés. |
| `ln` | `-s`, `-f`, `-n`, `-v`, `CIBLE [LIEN]`, `CIBLE… DOSSIER` | Liens symboliques par `HackFile.CreateSymbolicLinkAsync` ; le texte de la cible est gardé tel quel. Sans `-s` : `Operation not supported` (pas de liens physiques dans le système de fichiers HackerOS) et le conseil d'utiliser `-s`. |
| `locate` | `-i -b -c -e`, `-l N` | Pas de base `updatedb` : parcours en direct avec les droits de l'utilisateur, `/proc`, `/dev`, `/sys`, `/run` exclus ; un motif sans joker est cherché partout dans le chemin. Code 1 sans résultat. |
| `du` | `-a -s -h -c -b`, `-d N` | Blocs de 4 Kio comme ext4 (un par dossier) ; affichage en Kio. |
| `grep` | `-i -n -v`, `-r`/`-R` (récursif, dossier courant sans fichier), options combinables (`-rn`) | Préfixe le nom du fichier quand il y en a plusieurs ou avec `-r` ; les liens symboliques rencontrés ne sont pas suivis. |
| `df` | `-h -T -a` | Disques de `HackDriveInfo` (`/dev/hda` sur `/`) ; `-a` ajoute les pseudo-systèmes de `/proc/mounts`. |
| `zip` | `-r -q -j -d -0…-9` | Vraie archive deflate lisible par tout outil zip ; met à jour une archive existante ; les modes Unix sont stockés dans les attributs externes. |
| `unzip` | `-l -t -p -o -n -q -j`, `-d DOSSIER`, `[MOTIFS]` | Restaure les modes (`+x`) ; refuse une entrée qui sortirait du dossier (`../`, « zip slip ») ; ne remplace pas un fichier sans `-o`. |

## Processus et services

| Commande | Options | Notes |
|---|---|---|
| `top` | `-b`, `-n N`, `-d SECONDES`, `-u USER`, `-o CHAMP` | Plein écran rafraîchi toutes les 3 s ; touches `q`, `M`, `P`, `N`, `T`. CPU depuis deux lectures de `/proc/stat`, mémoire depuis `/proc/meminfo`, processus par `HackProcess`. |
| `htop` | `-d DIXIÈMES`, `-u USER`, `-t`, `-C` | Barres par cœur, mémoire et swap en couleur, liste défilante avec sélection, arbre (`t`), tri (`P`/`M`/`T`/`N`), recherche (`/`), utilisateur (`u`), `k` tue le processus choisi (`process.manage`), `q` quitte. |
| `kill` | `-s SIG`, `-SIG`, `-N`, `-l`, `PID` ou `%job` | `KILL` (9) tue tout de suite, sans prévenir ; les autres signaux demandent l'arrêt (`StopRequested`, raison `Terminated`, ADR 0069) puis forcent après 5 s ; `kill -0 PID` teste l'existence ; `%n` passe par le shell (`shell.control`). |
| `jobs` | `-l -p -r -s` | Jobs du shell appelant ; les jobs terminés sont montrés une fois. |
| `systemctl` (`service`) | `list-units`, `list-unit-files`, `status`, `start`, `stop`, `restart`, `enable`, `disable`, `mask`, `unmask`, `is-active`, `is-enabled` | Les apps `Service` du catalogue vues comme des unités `nom.service` ; automatique = `enabled`, manuel = `disabled`, désactivé = `masked`. Agir sur le service d'une autre app demande `services.manage`. `service NOM ACTION` est accepté. |
| `watch` | `-n SECONDES`, `-t`, `-g`, `-e` | Relance la ligne par `IShellSession.RunAsync` (sous-shell : tubes et variables marchent) et affiche sa dernière sortie en plein écran ; `q` ou Ctrl+C quitte. |
| `sleep` | `N[smhd]…`, décimaux | Ctrl+C l'interrompt (130). |
| `stress` | `-c N`, `-t SECONDES`, `--yield` | Comme `stress(1)` : N boucles de calcul sans rendre la main (`--yield` : avec points d'arrêt). Tourne dans un Web Worker : le bureau reste fluide, le cœur passe « not-responding », `kill -9` termine le worker ([`worker-processes.md`](worker-processes.md)). |
| `whoami` | — | `root` sous `sudo`. |
| `which` | `NOM…` | `/usr/bin/NOM` pour une commande installée, `/bin/sh` pour le shell ; code 1 sinon. |

## Réseau

| Commande | Options | Notes |
|---|---|---|
| `curl` | `-I -i -v -L -s -f`, `-o FICHIER`, `-O`, `-H "Nom: valeur"`, `-A`, `-X`, `-d`/`--data-raw`/`--data-binary` (`@fichier`), `--data-urlencode`, `--json` | `-o`, `-O`, `-H`, `-A`, `-s` ajoutés par l'ADR 0064 ; corps bruts, `-i`, `--json`, `--data-*` et `-f` facultatif depuis `web/2-http-bodies` (ADR 0074 §1) : sans `-f`, une erreur HTTP affiche le corps et sort avec 0, comme le vrai `curl` ([`http-bodies.md`](http-bodies.md)). |
| `wget` | `-O FICHIER` (`-O -` = sortie), `-P DOSSIER`, `-q`, `-S`, `--spider` | Ne remplace jamais : `fichier.1`, `fichier.2`… ; progression sur la sortie d'erreur. |
| `ip` (`ifconfig`) | `addr`, `link`, `route`, `neigh`, `-br`, `-s`, `dev NOM` | `lo` 127.0.0.1/8 et `eth0` 192.168.1.100/24 derrière le routeur 192.168.1.1 du réseau simulé, MAC stable dérivée du nom de la machine ; compteurs réels de `/proc/net/dev`. |
| `netstat` (`ss`) | `-t -u -l -n -a -p`, `-i`, `-r`, `-s` | Les ports à l'écoute sont ceux que le réseau simulé ouvre sur `127.0.0.1` (sondés par `HackTcpClient`, ce que `nmap localhost` voit) ; `-i` et `-s` lisent `/proc/net/dev`. |

## Scripts

| Commande | Notes |
|---|---|
| `test`, `[` | `-e -f -d -s -r -w -x -L -h`, `-n -z`, `= == != < >`, `-eq -ne -lt -le -gt -ge`, `-nt -ot -ef`, `! -a -o ( )` ; `[` exige `]` (le shell lie `[` à `test`). Code 2 sur erreur d'usage. |
| `true`, `false` | Codes 0 et 1. |
| `read` | `-r -p -s -n -t -d`, `NOM…` (sinon `REPLY`) ; découpe sur `IFS`, le dernier nom prend le reste ; affecte des variables du shell appelant (`shell.control`), donc perdues dans un tube comme dans bash ; code 1 en fin d'entrée, 142 au délai dépassé. |

## Tests

- `Tests/HackerOs.Platform.Core.Tests/Shell/ShellInterpreterTests.Commands.cs` :
  `test`/`[`, `read` avec `while read`, `printf`, `seq`, `cut | sort | uniq -c`,
  `sort -k -t -n -r`, `tail -n +N`, `head -c`, `tee`, `chmod +x` puis
  `./script`, modes symboliques, `ln -s` et refus des liens physiques,
  `zip`/`unzip` avec les modes, `whoami`, `basename`, `dirname`, `which`,
  `du`, `locate`, `jobs` et `kill %1`, `kill PID` et `kill -9 PID` sur un vrai
  processus, `grep -r`, `ip`, `top -b`.
- `Tests/HackerOs.Commands.Wave5.Tests` : `chmod`, `kill`, `sort`, `head`,
  `tail` avec les passerelles factices.
- `Tests/HackerOs.Ecosystem.Tests` : 75 apps intégrées dont 63 commandes (`stress` ajoutée par `rebuild/5-worker-processes`).

## Liste des tâches

- [x] `sort`, `head`, `tail` (`-f`), `kill` (`%job`, signaux), `curl` (`-o`, `-O`, `-H`) revus.
- [x] `chmod` symbolique et récursif (`chmod +x`).
- [x] `cut`, `tee`, `uniq`, `printf`, `seq`, `basename`, `dirname`.
- [x] `ln -s` et `HackFile.CreateSymbolicLinkAsync`.
- [x] `locate`, `du`, `df`, `zip`, `unzip`.
- [x] `top`, `htop` (couleurs dans les cadres plein écran), `watch`, `jobs`, `sleep`, `whoami`, `which`.
- [x] `systemctl`/`service`.
- [x] `wget`, `ip`/`ifconfig`, `netstat`/`ss`.
- [x] `test`/`[`, `true`, `false`, `read`.
- [x] Tests et documentation.
- [x] ADR 0069 (`rebuild/5-graceful-stop`) : `find`, `grep` (et `grep -r`), `du`, `zip`, `unzip`, `locate` appellent `HackProcess.CheckpointAsync` dans leurs boucles ; `kill` envoie `Terminated`.
