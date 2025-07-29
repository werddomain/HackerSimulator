using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Defines a template for user home directory structure creation
    /// </summary>
    public class HomeDirectoryTemplate
    {
        /// <summary>
        /// The name of this template
        /// </summary>
        public string Name { get; set; } = "default";
        
        /// <summary>
        /// Description of this template
        /// </summary>
        public string Description { get; set; } = "Standard user home directory template";
        
        /// <summary>
        /// Directories to create with their permissions (in octal format)
        /// </summary>
        public Dictionary<string, int> Directories { get; set; } = new Dictionary<string, int>();
        
        /// <summary>
        /// Configuration files to create with their paths and permissions
        /// </summary>
        public Dictionary<string, ConfigFileInfo> ConfigFiles { get; set; } = new Dictionary<string, ConfigFileInfo>();
        
        /// <summary>
        /// User type this template is designed for (e.g., "standard", "admin", "developer")
        /// </summary>
        public string UserType { get; set; } = "standard";
        
        /// <summary>
        /// Whether this template is suitable for an admin user
        /// </summary>
        public bool IsForAdmin { get; set; } = false;
    }
    
    /// <summary>
    /// Information about a configuration file
    /// </summary>
    public class ConfigFileInfo
    {
        /// <summary>
        /// Relative path to the file within the home directory
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// Permissions for the file in octal format
        /// </summary>
        public int Permissions { get; set; } = 0644;
        
        /// <summary>
        /// Content generator function name
        /// </summary>
        public string ContentGenerator { get; set; } = "default";
        
        /// <summary>
        /// Whether the file should be hidden
        /// </summary>
        public bool IsHidden { get; set; } = false;
    }
    
    /// <summary>
    /// Manages templates for home directory creation
    /// </summary>
    public class HomeDirectoryTemplateManager
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly Dictionary<string, HomeDirectoryTemplate> _templates = new Dictionary<string, HomeDirectoryTemplate>();
        private readonly Dictionary<string, Func<User, string, Task<string>>> _contentGenerators = new Dictionary<string, Func<User, string, Task<string>>>();
        
        /// <summary>
        /// Initializes a new instance of the HomeDirectoryTemplateManager
        /// </summary>
        /// <param name="fileSystem">The virtual file system instance</param>
        public HomeDirectoryTemplateManager(IVirtualFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            
            // Register default templates
            RegisterDefaultTemplates();
            
            // Register content generators
            RegisterDefaultContentGenerators();
        }
        
        /// <summary>
        /// Gets a template by name
        /// </summary>
        /// <param name="templateName">The name of the template</param>
        /// <returns>The template, or null if not found</returns>
        public HomeDirectoryTemplate GetTemplate(string templateName)
        {
            if (string.IsNullOrEmpty(templateName))
            {
                templateName = "default";
            }
            
            return _templates.TryGetValue(templateName, out var template) ? template : _templates["default"];
        }
        
        /// <summary>
        /// Gets a template suitable for a specific user type
        /// </summary>
        /// <param name="isAdmin">Whether the user is an admin</param>
        /// <param name="userType">The type of user (e.g., "developer", "standard")</param>
        /// <returns>The most appropriate template</returns>
        public HomeDirectoryTemplate GetTemplateForUserType(bool isAdmin, string userType = "standard")
        {
            // Try to find a template specifically for this user type and admin status
            foreach (var template in _templates.Values)
            {
                if (template.IsForAdmin == isAdmin && template.UserType.Equals(userType, StringComparison.OrdinalIgnoreCase))
                {
                    return template;
                }
            }
            
            // Fall back to any template for this user type
            foreach (var template in _templates.Values)
            {
                if (template.UserType.Equals(userType, StringComparison.OrdinalIgnoreCase))
                {
                    return template;
                }
            }
            
            // Fall back to a template for this admin status
            foreach (var template in _templates.Values)
            {
                if (template.IsForAdmin == isAdmin)
                {
                    return template;
                }
            }
            
            // Final fallback to default template
            return _templates["default"];
        }
        
        /// <summary>
        /// Registers a new template
        /// </summary>
        /// <param name="template">The template to register</param>
        public void RegisterTemplate(HomeDirectoryTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }
            
            if (string.IsNullOrEmpty(template.Name))
            {
                throw new ArgumentException("Template name cannot be empty");
            }
            
            _templates[template.Name] = template;
        }
        
        /// <summary>
        /// Registers a content generator for config files
        /// </summary>
        /// <param name="name">The name of the generator</param>
        /// <param name="generator">The generator function</param>
        public void RegisterContentGenerator(string name, Func<User, string, Task<string>> generator)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Generator name cannot be empty");
            }
            
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }
            
            _contentGenerators[name] = generator;
        }
        
        /// <summary>
        /// Generates content for a config file
        /// </summary>
        /// <param name="generatorName">The name of the generator to use</param>
        /// <param name="user">The user</param>
        /// <param name="path">The path to the file</param>
        /// <returns>The generated content</returns>
        public async Task<string> GenerateContentAsync(string generatorName, User user, string path)
        {
            if (string.IsNullOrEmpty(generatorName))
            {
                generatorName = "default";
            }
            
            if (_contentGenerators.TryGetValue(generatorName, out var generator))
            {
                return await generator(user, path);
            }
            
            // Fall back to default content
            return "# Configuration file\n# Created for " + user.Username;
        }
        
        /// <summary>
        /// Registers the default templates
        /// </summary>
        private void RegisterDefaultTemplates()
        {
            // Standard user template
            var standardTemplate = new HomeDirectoryTemplate
            {
                Name = "default",
                Description = "Standard user home directory template",
                UserType = "standard",
                IsForAdmin = false,
                Directories = new Dictionary<string, int>
                {
                    // Public directories (755 - rwxr-xr-x)
                    { "Desktop", 0755 },
                    { "Documents", 0755 },
                    { "Downloads", 0755 },
                    { "Music", 0755 },
                    { "Pictures", 0755 },
                    { "Public", 0755 },
                    { "Videos", 0755 },
                    
                    // Private directories (700 - rwx------)
                    { ".ssh", 0700 },
                    { ".gnupg", 0700 },
                    
                    // Configuration directories (755 - rwxr-xr-x)
                    { ".config", 0755 },
                    { ".local", 0755 },
                    { ".local/share", 0755 },
                    { ".local/bin", 0755 },
                    { ".cache", 0755 }
                },
                ConfigFiles = new Dictionary<string, ConfigFileInfo>
                {
                    { ".bashrc", new ConfigFileInfo
                        {
                            Path = ".bashrc",
                            Permissions = 0644,
                            ContentGenerator = "bashrc",
                            IsHidden = true
                        }
                    },
                    { ".profile", new ConfigFileInfo
                        {
                            Path = ".profile",
                            Permissions = 0644,
                            ContentGenerator = "profile",
                            IsHidden = true
                        }
                    },
                    { ".bash_logout", new ConfigFileInfo
                        {
                            Path = ".bash_logout",
                            Permissions = 0644,
                            ContentGenerator = "bash_logout",
                            IsHidden = true
                        }
                    },
                    { "user-settings.json", new ConfigFileInfo
                        {
                            Path = ".config/hackeros/user-settings.json",
                            Permissions = 0600,
                            ContentGenerator = "user_settings",
                            IsHidden = false
                        }
                    }
                }
            };
            
            // Admin user template
            var adminTemplate = new HomeDirectoryTemplate
            {
                Name = "admin",
                Description = "Administrator home directory template",
                UserType = "admin",
                IsForAdmin = true,
                Directories = new Dictionary<string, int>(standardTemplate.Directories)
                {
                    // Admin-specific directories
                    { ".admin", 0700 },
                    { ".admin/scripts", 0700 },
                    { ".admin/backup", 0700 },
                    { ".admin/logs", 0700 }
                },
                ConfigFiles = new Dictionary<string, ConfigFileInfo>(standardTemplate.ConfigFiles)
                {
                    { "admin-settings.json", new ConfigFileInfo
                        {
                            Path = ".config/hackeros/admin-settings.json",
                            Permissions = 0600,
                            ContentGenerator = "admin_settings",
                            IsHidden = false
                        }
                    }
                }
            };
            
            // Developer user template
            var developerTemplate = new HomeDirectoryTemplate
            {
                Name = "developer",
                Description = "Developer home directory template",
                UserType = "developer",
                IsForAdmin = false,
                Directories = new Dictionary<string, int>(standardTemplate.Directories)
                {
                    // Developer-specific directories
                    { "Projects", 0755 },
                    { "Workspace", 0755 },
                    { ".vscode", 0755 },
                    { ".npm", 0755 },
                    { ".yarn", 0755 },
                    { ".nuget", 0755 },
                    { ".dotnet", 0755 }
                },
                ConfigFiles = new Dictionary<string, ConfigFileInfo>(standardTemplate.ConfigFiles)
                {
                    { ".gitconfig", new ConfigFileInfo
                        {
                            Path = ".gitconfig",
                            Permissions = 0644,
                            ContentGenerator = "gitconfig",
                            IsHidden = true
                        }
                    },
                    { ".vimrc", new ConfigFileInfo
                        {
                            Path = ".vimrc",
                            Permissions = 0644,
                            ContentGenerator = "vimrc",
                            IsHidden = true
                        }
                    }
                }
            };
            
            // Register templates
            RegisterTemplate(standardTemplate);
            RegisterTemplate(adminTemplate);
            RegisterTemplate(developerTemplate);
        }
        
        /// <summary>
        /// Registers the default content generators
        /// </summary>
        private void RegisterDefaultContentGenerators()
        {
            // Register default content generator (fallback)
            RegisterContentGenerator("default", async (user, path) => 
            {
                return $"# Configuration file: {path}\n# Created for {user.Username} on {DateTime.Now}";
            });
            
            // Register .bashrc content generator
            RegisterContentGenerator("bashrc", async (user, path) => 
            {
                return @"# ~/.bashrc: executed by bash for non-login shells

# If not running interactively, don't do anything
[ -z ""$PS1"" ] && return

# Don't put duplicate lines in the history
HISTCONTROL=ignoredups:ignorespace

# Append to the history file, don't overwrite it
shopt -s histappend

# History length
HISTSIZE=1000
HISTFILESIZE=2000

# Check window size after each command
shopt -s checkwinsize

# Make less more friendly for non-text input files
[ -x /usr/bin/lesspipe ] && eval ""$(SHELL=/bin/sh lesspipe)""

# Set prompt
PS1='\[\033[01;32m\]\u@\h\[\033[00m\]:\[\033[01;34m\]\w\[\033[00m\]\$ '

# Enable color support of ls
if [ -x /usr/bin/dircolors ]; then
    test -r ~/.dircolors && eval ""$(dircolors -b ~/.dircolors)"" || eval ""$(dircolors -b)""
    alias ls='ls --color=auto'
    alias grep='grep --color=auto'
    alias fgrep='fgrep --color=auto'
    alias egrep='egrep --color=auto'
fi

# Some useful aliases
alias ll='ls -alF'
alias la='ls -A'
alias l='ls -CF'
alias cls='clear'
alias h='history'
alias j='jobs -l'

# HackerOS specific aliases
alias hackeros-update='sudo apt update && sudo apt upgrade'
alias hackeros-info='neofetch'

# User specific environment and startup programs
if [ -d ""$HOME/.local/bin"" ] ; then
    PATH=""$HOME/.local/bin:$PATH""
fi

# Source global definitions
if [ -f /etc/bashrc ]; then
    . /etc/bashrc
fi
";
            });
            
            // Register .profile content generator
            RegisterContentGenerator("profile", async (user, path) => 
            {
                string homePath = $"/home/{user.Username}";
                return $@"# ~/.profile: executed by the command interpreter for login shells

# Include user's private bin directory in PATH if it exists
if [ -d ""$HOME/.local/bin"" ] ; then
    PATH=""$HOME/.local/bin:$PATH""
fi

# Set environment variables
export EDITOR=nano
export TERM=xterm-256color
export LANG=en_US.UTF-8
export LC_ALL=en_US.UTF-8
export USER={user.Username}
export HOME={homePath}

# Source .bashrc for interactive shells
if [ -n ""$BASH_VERSION"" ]; then
    if [ -f ""$HOME/.bashrc"" ]; then
        . ""$HOME/.bashrc""
    fi
fi

# HackerOS specific environment variables
export HACKEROS_USER_CONFIG=""$HOME/.config/hackeros""

# Display welcome message on login
echo ""Welcome to HackerOS, {user.FullName}!""
echo ""Type 'hackeros-info' for system information.""
";
            });
            
            // Register .bash_logout content generator
            RegisterContentGenerator("bash_logout", async (user, path) => 
            {
                return @"# ~/.bash_logout: executed by bash when login shell exits

# Clear the screen when logging out
clear

# When leaving the console, save the history
if [ ""$SHLVL"" = 1 ]; then
    history -a
fi
";
            });
            
            // Register user-settings.json content generator
            RegisterContentGenerator("user_settings", async (user, path) => 
            {
                // Create a default settings object
                var settings = new
                {
                    ui = new
                    {
                        theme = "default",
                        accentColor = "blue",
                        fontSize = "medium",
                        darkMode = true,
                        animations = true
                    },
                    desktop = new
                    {
                        wallpaper = "/usr/share/hackeros/wallpapers/default.jpg",
                        icons = new
                        {
                            size = "medium",
                            arrangement = "grid"
                        },
                        taskbar = new
                        {
                            position = "bottom",
                            autoHide = false
                        },
                        startMenu = new
                        {
                            showRecentApps = true,
                            showFavorites = true
                        }
                    },
                    applications = new
                    {
                        terminal = new
                        {
                            fontFamily = "Monospace",
                            fontSize = 12,
                            cursorStyle = "block",
                            cursorBlink = true,
                            colorScheme = "hackeros-dark"
                        },
                        browser = new
                        {
                            homepage = "about:newtab",
                            searchEngine = "duckduckgo",
                            privacyMode = "standard"
                        },
                        fileExplorer = new
                        {
                            showHiddenFiles = false,
                            viewMode = "list"
                        }
                    },
                    security = new
                    {
                        lockScreenTimeout = 300,
                        requirePasswordOnWake = true,
                        notificationsOnLockScreen = false
                    },
                    network = new
                    {
                        proxyEnabled = false,
                        proxyAddress = "",
                        proxyPort = 0
                    },
                    preferences = new
                    {
                        language = "en-US",
                        timeFormat = "24h",
                        dateFormat = "yyyy-MM-dd"
                    }
                };
                
                // Serialize to JSON with indentation
                return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            });
            
            // Register admin-settings.json content generator
            RegisterContentGenerator("admin_settings", async (user, path) => 
            {
                var settings = new
                {
                    adminTools = new
                    {
                        showSystemMonitor = true,
                        showUserManager = true,
                        showNetworkManager = true,
                        showPackageManager = true,
                        showLogViewer = true
                    },
                    security = new
                    {
                        requireSudoPassword = true,
                        sudoTimeout = 300,
                        restrictedCommands = new string[] { "rm -rf /", "mkfs", "dd" },
                        auditLogEnabled = true,
                        auditLogPath = "/var/log/admin-audit.log"
                    },
                    systemDefaults = new
                    {
                        newUserTemplate = "standard",
                        umask = "022",
                        defaultShell = "/bin/bash",
                        passwordExpiryDays = 90,
                        minPasswordLength = 8,
                        requirePasswordComplexity = true
                    }
                };
                
                return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            });
            
            // Register .gitconfig content generator for developers
            RegisterContentGenerator("gitconfig", async (user, path) => 
            {
                return $@"[user]
    name = {user.FullName}
    email = {user.Username}@hackeros.local

[core]
    editor = nano
    autocrlf = input
    safecrlf = warn

[color]
    ui = auto

[alias]
    st = status
    ci = commit
    co = checkout
    br = branch
    lg = log --graph --pretty=format:'%Cred%h%Creset -%C(yellow)%d%Creset %s %Cgreen(%cr) %C(bold blue)<%an>%Creset' --abbrev-commit --date=relative

[init]
    defaultBranch = main

[pull]
    rebase = false
";
            });
            
            // Register .vimrc content generator for developers
            RegisterContentGenerator("vimrc", async (user, path) => 
            {
                return @"set nocompatible
syntax enable
set background=dark
set number
set ruler
set tabstop=4
set shiftwidth=4
set expandtab
set smarttab
set autoindent
set smartindent
set backspace=indent,eol,start
set incsearch
set hlsearch
set ignorecase
set smartcase
set showmatch
set wildmenu
set wildmode=list:longest
set mouse=a
set clipboard=unnamed
set history=1000
set undolevels=1000
set title
set visualbell
set noerrorbells
set wrap
set linebreak
set encoding=utf-8
set fileencoding=utf-8
set fileformats=unix,dos,mac
set listchars=tab:>-,trail:.,extends:>,precedes:<,nbsp:+
set updatetime=300
set scrolloff=5
set sidescrolloff=5
highlight Comment cterm=italic
";
            });
        }
    }
}
