using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace HackerOs.IO.FileSystem
{
    /// <summary>
    /// Provides functionality to create and initialize standard Unix directory structure.
    /// Includes /etc, /home, /var, /bin, and other standard directories with default content.
    /// </summary>
    public static class UnixDirectoryInitializer
    {
        /// <summary>
        /// Creates the standard Unix directory structure with default configuration files.
        /// </summary>
        /// <param name="fileSystem">The virtual file system to initialize</param>
        /// <returns>True if initialization was successful</returns>
        public static async Task<bool> InitializeStandardDirectoryStructureAsync(IVirtualFileSystem fileSystem)
        {
            try
            {
                // Create standard directories
                await CreateStandardDirectoriesAsync(fileSystem);
                
                // Create default configuration files
                await CreateDefaultConfigurationFilesAsync(fileSystem);
                
                // Create default user directories
                await CreateDefaultUserDirectoriesAsync(fileSystem);
                
                // Create default system files
                await CreateDefaultSystemFilesAsync(fileSystem);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize standard directory structure: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates the standard Unix directories.
        /// </summary>
        private static async Task CreateStandardDirectoriesAsync(IVirtualFileSystem fileSystem)
        {
            var standardDirectories = new[]
            {
                "/bin",        // Essential command binaries
                "/boot",       // Boot loader files
                "/dev",        // Device files
                "/etc",        // System configuration files
                "/etc/systemd", // Systemd configuration
                "/home",       // User home directories
                "/lib",        // Essential shared libraries
                "/lib64",      // 64-bit libraries
                "/media",      // Mount point for removable media
                "/mnt",        // Mount point for temporarily mounted filesystems
                "/opt",        // Optional application software packages
                "/proc",       // Virtual filesystem documenting kernel and process status
                "/root",       // Home directory for root user
                "/run",        // Runtime variable data
                "/sbin",       // Essential system binaries
                "/srv",        // Site-specific data served by system
                "/sys",        // Virtual filesystem providing information about the system
                "/tmp",        // Temporary files
                "/usr",        // Secondary hierarchy for read-only user data
                "/usr/bin",    // Non-essential command binaries
                "/usr/include", // Standard include files
                "/usr/lib",    // Libraries for /usr/bin/ and /usr/sbin/
                "/usr/lib64",  // 64-bit libraries for /usr/bin/ and /usr/sbin/
                "/usr/local",  // Tertiary hierarchy for local data
                "/usr/local/bin", // Local binaries
                "/usr/local/lib", // Local libraries
                "/usr/sbin",   // Non-essential system binaries
                "/usr/share",  // Shared data
                "/usr/src",    // Source code
                "/var",        // Variable data files
                "/var/cache",  // Application cache data
                "/var/lib",    // Variable state information
                "/var/lock",   // Lock files
                "/var/log",    // Log files
                "/var/mail",   // User mailbox files
                "/var/run",    // Runtime variable data
                "/var/spool",  // Spool for tasks waiting to be processed
                "/var/tmp",    // Temporary files preserved between reboots
                "/var/www",    // Web server content
                "/var/www/html" // Default web content
            };

            foreach (var dir in standardDirectories)
            {
                await fileSystem.CreateDirectoryAsync(dir);
            }
        }

        /// <summary>
        /// Creates default configuration files in /etc.
        /// </summary>
        private static async Task CreateDefaultConfigurationFilesAsync(IVirtualFileSystem fileSystem)
        {
            // /etc/passwd - User account information
            var passwdContent = @"root:x:0:0:root:/root:/bin/bash
daemon:x:1:1:daemon:/usr/sbin:/usr/sbin/nologin
bin:x:2:2:bin:/bin:/usr/sbin/nologin
sys:x:3:3:sys:/dev:/usr/sbin/nologin
sync:x:4:65534:sync:/bin:/bin/sync
games:x:5:60:games:/usr/games:/usr/sbin/nologin
man:x:6:12:man:/var/cache/man:/usr/sbin/nologin
lp:x:7:7:lp:/var/spool/lpd:/usr/sbin/nologin
mail:x:8:8:mail:/var/mail:/usr/sbin/nologin
news:x:9:9:news:/var/spool/news:/usr/sbin/nologin
nobody:x:65534:65534:nobody:/nonexistent:/usr/sbin/nologin
";
            await fileSystem.WriteFileAsync("/etc/passwd", Encoding.UTF8.GetBytes(passwdContent));

            // /etc/group - Group information
            var groupContent = @"root:x:0:
daemon:x:1:
bin:x:2:
sys:x:3:
adm:x:4:
tty:x:5:
disk:x:6:
lp:x:7:
mail:x:8:
news:x:9:
uucp:x:10:
man:x:12:
proxy:x:13:
kmem:x:15:
dialout:x:20:
fax:x:21:
voice:x:22:
cdrom:x:24:
floppy:x:25:
tape:x:26:
sudo:x:27:
audio:x:29:
dip:x:30:
www-data:x:33:
backup:x:34:
operator:x:37:
list:x:38:
irc:x:39:
src:x:40:
gnats:x:41:
shadow:x:42:
utmp:x:43:
video:x:44:
sasl:x:45:
plugdev:x:46:
staff:x:50:
games:x:60:
users:x:100:
nogroup:x:65534:
";
            await fileSystem.WriteFileAsync("/etc/group", Encoding.UTF8.GetBytes(groupContent));

            // /etc/hostname
            await fileSystem.WriteFileAsync("/etc/hostname", Encoding.UTF8.GetBytes("hackeros\n"));

            // /etc/hosts
            var hostsContent = @"127.0.0.1	localhost
127.0.1.1	hackeros

# The following lines are desirable for IPv6 capable hosts
::1     ip6-localhost ip6-loopback
fe00::0 ip6-localnet
ff00::0 ip6-mcastprefix
ff02::1 ip6-allnodes
ff02::2 ip6-allrouters
";
            await fileSystem.WriteFileAsync("/etc/hosts", Encoding.UTF8.GetBytes(hostsContent));

            // /etc/fstab - File system table
            var fstabContent = @"# <file system> <mount point>   <type>  <options>       <dump>  <pass>
# / was on /dev/sda1 during installation
/dev/root            /               auto    defaults         0       1
tmpfs                /tmp            tmpfs   defaults         0       0
";
            await fileSystem.WriteFileAsync("/etc/fstab", Encoding.UTF8.GetBytes(fstabContent));

            // /etc/profile - System-wide environment
            var profileContent = @"# /etc/profile: system-wide .profile file for the Bourne shell (sh(1))
# and Bourne compatible shells (bash(1), ksh(1), ash(1), ...).

if [ ""${PS1-}"" ]; then
  if [ ""${BASH-}"" ] && [ ""$BASH"" != ""/bin/sh"" ]; then
    # The file bash.bashrc already sets the default PS1.
    # PS1='\h:\w\$ '
    if [ -f /etc/bash.bashrc ]; then
      . /etc/bash.bashrc
    fi
  else
    if [ ""`id -u`"" -eq 0 ]; then
      PS1='# '
    else
      PS1='$ '
    fi
  fi
fi

if [ -d /etc/profile.d ]; then
  for i in /etc/profile.d/*.sh; do
    if [ -r $i ]; then
      . $i
    fi
  done
  unset i
fi
";
            await fileSystem.WriteFileAsync("/etc/profile", Encoding.UTF8.GetBytes(profileContent));

            // /etc/bash.bashrc
            var bashrcContent = @"# System-wide .bashrc file for interactive bash(1) shells.

# If not running interactively, don't do anything
[ -z ""$PS1"" ] && return

# check the window size after each command and, if necessary,
# update the values of LINES and COLUMNS.
shopt -s checkwinsize

# set variable identifying the chroot you work in (used in the prompt below)
if [ -z ""${debian_chroot:-}"" ] && [ -r /etc/debian_chroot ]; then
    debian_chroot=$(cat /etc/debian_chroot)
fi

# set a fancy prompt (non-color, overwrite the one in /etc/profile)
PS1='${debian_chroot:+($debian_chroot)}\u@\h:\w\$ '

# enable bash completion in interactive shells
if ! shopt -oq posix; then
  if [ -f /usr/share/bash-completion/bash_completion ]; then
    . /usr/share/bash-completion/bash_completion
  elif [ -f /etc/bash_completion ]; then
    . /etc/bash_completion
  fi
fi
";
            await fileSystem.WriteFileAsync("/etc/bash.bashrc", Encoding.UTF8.GetBytes(bashrcContent));

            // Create /etc/profile.d directory
            await fileSystem.CreateDirectoryAsync("/etc/profile.d");
        }

        /// <summary>
        /// Creates default user directories and configuration files.
        /// </summary>
        private static async Task CreateDefaultUserDirectoriesAsync(IVirtualFileSystem fileSystem)
        {
            // Create root user home directory with dotfiles
            await CreateUserHomeAsync(fileSystem, "root", "/root");
            
            // Create a default user
            await CreateUserHomeAsync(fileSystem, "user", "/home/user");
        }

        /// <summary>
        /// Creates a user home directory with default configuration files.
        /// </summary>
        private static async Task CreateUserHomeAsync(IVirtualFileSystem fileSystem, string username, string homePath)
        {
            await fileSystem.CreateDirectoryAsync(homePath);
            await fileSystem.CreateDirectoryAsync($"{homePath}/.config");
            await fileSystem.CreateDirectoryAsync($"{homePath}/.local");
            await fileSystem.CreateDirectoryAsync($"{homePath}/.local/bin");
            await fileSystem.CreateDirectoryAsync($"{homePath}/.local/share");
            await fileSystem.CreateDirectoryAsync($"{homePath}/Desktop");
            await fileSystem.CreateDirectoryAsync($"{homePath}/Documents");
            await fileSystem.CreateDirectoryAsync($"{homePath}/Downloads");

            // .bashrc
            var bashrcContent = $@"# ~/.bashrc: executed by bash(1) for non-login shells.

# If not running interactively, don't do anything
case $- in
    *i*) ;;
      *) return;;
esac

# don't put duplicate lines or lines starting with space in the history.
HISTCONTROL=ignoreboth

# append to the history file, don't overwrite it
shopt -s histappend

# for setting history length see HISTSIZE and HISTFILESIZE in bash(1)
HISTSIZE=1000
HISTFILESIZE=2000

# check the window size after each command and, if necessary,
# update the values of LINES and COLUMNS.
shopt -s checkwinsize

# set a fancy prompt
if [ ""${{color_prompt:-}}"" = yes ]; then
    PS1='{username}@hackeros:\w\$ '
else
    PS1='{username}@hackeros:\w\$ '
fi
unset color_prompt force_color_prompt

# enable color support of ls and also add handy aliases
if [ -x /usr/bin/dircolors ]; then
    test -r ~/.dircolors && eval ""$(dircolors -b ~/.dircolors)"" || eval ""$(dircolors -b)""
    alias ls='ls --color=auto'
    alias grep='grep --color=auto'
    alias fgrep='fgrep --color=auto'
    alias egrep='egrep --color=auto'
fi

# some more ls aliases
alias ll='ls -alF'
alias la='ls -A'
alias l='ls -CF'
";
            await fileSystem.WriteFileAsync($"{homePath}/.bashrc", Encoding.UTF8.GetBytes(bashrcContent));

            // .profile
            var profileContent = @"# ~/.profile: executed by the command interpreter for login shells.

# if running bash
if [ -n ""$BASH_VERSION"" ]; then
    # include .bashrc if it exists
    if [ -f ""$HOME/.bashrc"" ]; then
	. ""$HOME/.bashrc""
    fi
fi

# set PATH so it includes user's private bin if it exists
if [ -d ""$HOME/.local/bin"" ] ; then
    PATH=""$HOME/.local/bin:$PATH""
fi
";
            await fileSystem.WriteFileAsync($"{homePath}/.profile", Encoding.UTF8.GetBytes(profileContent));

            // .bash_history (empty initially)
            await fileSystem.WriteFileAsync($"{homePath}/.bash_history", Array.Empty<byte>());
        }

        /// <summary>
        /// Creates default system files and utilities.
        /// </summary>
        private static async Task CreateDefaultSystemFilesAsync(IVirtualFileSystem fileSystem)
        {
            // /proc/version
            var versionContent = "HackerOS version 1.0.0 (hackeros@simulator) (WebAssembly) #1 SMP PREEMPT\n";
            await fileSystem.WriteFileAsync("/proc/version", Encoding.UTF8.GetBytes(versionContent));

            // /proc/cpuinfo
            var cpuinfoContent = @"processor	: 0
vendor_id	: WebAssembly
cpu family	: 1
model		: 1
model name	: WASM Virtual CPU
stepping	: 1
microcode	: 0x1
cpu MHz		: 1000.000
cache size	: 256 KB
physical id	: 0
siblings	: 1
core id		: 0
cpu cores	: 1
apicid		: 0
initial apicid	: 0
fpu		: yes
fpu_exception	: yes
cpuid level	: 1
wp		: yes
flags		: wasm
bogomips	: 2000.00
clflush size	: 64
cache_alignment	: 64
address sizes	: 32 bits physical, 32 bits virtual
power management:
";
            await fileSystem.WriteFileAsync("/proc/cpuinfo", Encoding.UTF8.GetBytes(cpuinfoContent));

            // /proc/meminfo
            var meminfoContent = @"MemTotal:        1048576 kB
MemFree:          524288 kB
MemAvailable:     786432 kB
Buffers:           32768 kB
Cached:           131072 kB
SwapCached:            0 kB
Active:           262144 kB
Inactive:         131072 kB
SwapTotal:             0 kB
SwapFree:              0 kB
";
            await fileSystem.WriteFileAsync("/proc/meminfo", Encoding.UTF8.GetBytes(meminfoContent));

            // Create some basic README files
            var welcomeContent = @"Welcome to HackerOS Simulator!

This is a Linux-like operating system simulation running in your browser.
It provides a realistic Unix-like environment with:

- Virtual file system with persistence
- Linux-style permissions and ownership
- Standard Unix directory structure
- Shell commands and utilities
- File operations and navigation

Explore the system and enjoy your hacking experience!

For help, try:
  man <command>    - Get help for a specific command
  ls               - List directory contents
  cd <directory>   - Change directory
  cat <file>       - Display file contents
  
Happy hacking!
";
            await fileSystem.WriteFileAsync("/home/user/README.txt", Encoding.UTF8.GetBytes(welcomeContent));
            
            var indexContent = @"<!DOCTYPE html>
<html>
<head>
    <title>Welcome to HackerOS</title>
</head>
<body>
    <h1>Welcome to HackerOS Web Server</h1>
    <p>This is the default page for the HackerOS web server.</p>
    <p>You can customize this page by editing /var/www/html/index.html</p>
</body>
</html>";
            await fileSystem.WriteFileAsync("/var/www/html/index.html", Encoding.UTF8.GetBytes(indexContent));
        }
    }
}
