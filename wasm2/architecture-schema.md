# Schéma visuel de l’architecture HackerSimulator (WASM v2)

```
+-------------------+
|    Blazor UI      |
+---------+---------+
          |
          v
+---------+---------+
|      Shell        | <---+
+---------+---------+     |
          |               |
          v               |
+---------+---------+     |
|     Applications  |     |
+---------+---------+     |
          |               |
          v               |
+---------+---------+     |
|      System       |     |
+---------+---------+     |
|  |   |   |   |   |      |
|  |   |   |   |   |      |
v  v   v   v   v   v      |
Kernel  IO  Network  Driver  Security  User
  |      |     |      |        |        |
  +------+------+------+--------+--------+
          |
          v
+---------+---------+
|      Settings     |
+-------------------+
|      Theme        |
+-------------------+
```

Légende :
- Les flèches indiquent les dépendances principales.
- Les modules “Kernel”, “IO”, “Network”, “Driver”, “Security”, “User” sont au cœur du système et isolés du front-end.
- “Shell” et “Applications” interagissent avec le système via des interfaces publiques.
- “Settings” et “Theme” sont accessibles par l’UI et les modules utilisateurs, mais n’ont pas accès direct au kernel.
