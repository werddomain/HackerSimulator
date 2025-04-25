Alwais create code in **TypeScript**.

When you create an app (with a GUI), follow these rules:
- Each app should be created in the apps directory (src/apps).
- if it generate html: 
    - Never include style inside the html. Create an file with the same name as the module.ts 
    - Inport the style inside 'src\styles\apps.less'
- Register it in the app manager (src\core\app-manager.ts). Take exemple of the "terminal" app.
  ```
    this.registerApp({
      id: 'your-app-id',
      name: 'Your App Name',
      description: 'Description of your app',
      icon: 'Lucide:icon-identifier', // avaliable prefix: 'ionicons:', 'data:' (data encoded image), 'lucide:', 'fa:', 'fa-solid:', 'fa-regular:', 'fa-brands:'
      launchable: true,
      singleton: false // Set to true if only one instance should run
  });
  ```
- if the app need more than 2 files, create a folder with the name of the app in the apps directory (src/apps).
- all apps extends GuiApplication

if you create a command,(or a Terminal Based App) follow these rules:
- Each command should be created in the commands directory (src/commands).
- Register it in the command registry (src\commands\command-registry.ts)
- if the command need more than 2 files, create a folder with the name of the command in the commands directory (src/commands).
- if it's a command that need to be run in the terminal:
  - it extend TerminalAppBase 
  - it's located in (src/commands/app).
- if it's a regular command:
  - it extend CommandBase 
  - it's located in (src/commands/linux).

  

