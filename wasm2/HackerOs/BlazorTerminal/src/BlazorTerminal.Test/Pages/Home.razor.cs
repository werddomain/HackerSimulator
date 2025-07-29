using Microsoft.AspNetCore.Components;
using BlazorTerminal.Components;
using BlazorTerminal.Models;

namespace BlazorTerminal.Test.Pages;

public partial class Home : ComponentBase
{
    private Terminal? mainTerminal;    private TerminalTheme selectedTheme = TerminalTheme.Dark();    private string selectedThemeName = "Dark";
    public string SelectedThemeName 
    {
        get => selectedThemeName; 
        set
        {
            selectedThemeName = value;
            _ = OnThemeChanged(new ChangeEventArgs { Value = value });
        }
    }
    private string selectedFontFamily = "'Consolas', 'Courier New', monospace";
    private int selectedFontSize = 14;
    private int terminalRows = 24;
    private int terminalColumns = 80;
    private string customBackgroundColor = "#000000";
    private string customForegroundColor = "#ffffff";
    
    private readonly List<CommandHistoryItem> commandHistory = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && mainTerminal != null)
        {
            await ShowWelcomeMessage();
        }
    }    private async Task HandleTerminalInput(string input)
    {
        if (mainTerminal == null) return;

        // Record command in history
        commandHistory.Add(new CommandHistoryItem
        {
            Command = input.Trim(),
            Timestamp = DateTime.Now
        });

        // Process the command
        await ProcessCommand(input.Trim());
        
        StateHasChanged();
    }

    private async Task ProcessCommand(string command)
    {
        if (mainTerminal == null) return;

        var lowerCommand = command.ToLower();

        switch (lowerCommand)
        {
            case "help":
                await ShowHelpText();
                break;
            case "clear":
                ClearTerminal();
                break;
            case "colors":
                ShowAnsiColors();
                break;
            case "format":
                ShowFormattingDemo();
                break;
            case "progress":
                await ShowProgressDemo();
                break;
            case "system":
                ShowSystemInfo();
                break;
            case "lorem":
                ShowLoremIpsum();
                break;
            case "welcome":
                await ShowWelcomeMessage();
                break;
            case "themes":
                ShowThemeDemo();
                break;
            default:
                mainTerminal.WriteLine($"Command not found: {command}");
                mainTerminal.WriteLine("Type 'help' for available commands.");
                break;
        }

        mainTerminal.WriteLine(""); // Empty line for spacing
        mainTerminal.Write("demo> ");
    }

    private async Task ShowWelcomeMessage()
    {
        if (mainTerminal == null) return;

        mainTerminal.Clear();
        
        // Welcome banner with ANSI colors
        mainTerminal.WriteLine("\x1b[1;36m╔══════════════════════════════════════════════════════════════════════════════╗\x1b[0m");
        mainTerminal.WriteLine("\x1b[1;36m║\x1b[0m                          \x1b[1;33mBlazorTerminal Demo\x1b[0m                           \x1b[1;36m║\x1b[0m");
        mainTerminal.WriteLine("\x1b[1;36m╚══════════════════════════════════════════════════════════════════════════════╝\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[1;32mWelcome to the BlazorTerminal interactive demo!\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("This terminal component supports:");
        mainTerminal.WriteLine("  • \x1b[1;31m■\x1b[1;32m■\x1b[1;33m■\x1b[1;34m■\x1b[1;35m■\x1b[1;36m■\x1b[0m Full ANSI color support");
        mainTerminal.WriteLine("  • \x1b[1mBold\x1b[0m, \x1b[3mitalic\x1b[0m, \x1b[4munderline\x1b[0m text formatting");
        mainTerminal.WriteLine("  • Text selection and clipboard operations");
        mainTerminal.WriteLine("  • Configurable themes and fonts");
        mainTerminal.WriteLine("  • High-performance rendering with virtualization");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[1;33mAvailable commands:\x1b[0m");
        mainTerminal.WriteLine("  help      - Show this help message");
        mainTerminal.WriteLine("  colors    - Demonstrate ANSI color support");
        mainTerminal.WriteLine("  format    - Show text formatting options");
        mainTerminal.WriteLine("  progress  - Display progress bar animation");
        mainTerminal.WriteLine("  system    - Show system information");
        mainTerminal.WriteLine("  lorem     - Generate sample text");
        mainTerminal.WriteLine("  themes    - Show theme examples");
        mainTerminal.WriteLine("  clear     - Clear the terminal");
        mainTerminal.WriteLine("");
        mainTerminal.Write("demo> ");

        await Task.CompletedTask;
    }

    private async Task ShowHelpText()
    {
        if (mainTerminal == null) return;

        mainTerminal.WriteLine("\x1b[1;33mBlazorTerminal Commands:\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[1;32mhelp\x1b[0m      - Display this help message");
        mainTerminal.WriteLine("\x1b[1;32mclear\x1b[0m     - Clear the terminal screen");
        mainTerminal.WriteLine("\x1b[1;32mcolors\x1b[0m    - Demonstrate ANSI color capabilities");
        mainTerminal.WriteLine("\x1b[1;32mformat\x1b[0m    - Show text formatting examples");
        mainTerminal.WriteLine("\x1b[1;32mprogress\x1b[0m  - Display animated progress bar");
        mainTerminal.WriteLine("\x1b[1;32msystem\x1b[0m    - Show system and browser information");
        mainTerminal.WriteLine("\x1b[1;32mlorem\x1b[0m     - Generate Lorem Ipsum text");
        mainTerminal.WriteLine("\x1b[1;32mthemes\x1b[0m    - Display theme color examples");
        mainTerminal.WriteLine("\x1b[1;32mwelcome\x1b[0m   - Show the welcome screen");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[3mTip: Use the controls on the right to customize the terminal appearance.\x1b[0m");

        await Task.CompletedTask;
    }

    private void ShowAnsiColors()
    {
        if (mainTerminal == null) return;

        mainTerminal.WriteLine("\x1b[1;33mANSI Color Demonstration:\x1b[0m");
        mainTerminal.WriteLine("");

        // Standard colors
        mainTerminal.WriteLine("\x1b[1mStandard Colors:\x1b[0m");
        mainTerminal.WriteLine("  \x1b[30m■\x1b[31m■\x1b[32m■\x1b[33m■\x1b[34m■\x1b[35m■\x1b[36m■\x1b[37m■\x1b[0m Normal");
        mainTerminal.WriteLine("  \x1b[1;30m■\x1b[1;31m■\x1b[1;32m■\x1b[1;33m■\x1b[1;34m■\x1b[1;35m■\x1b[1;36m■\x1b[1;37m■\x1b[0m Bright");
        mainTerminal.WriteLine("");

        // Background colors
        mainTerminal.WriteLine("\x1b[1mBackground Colors:\x1b[0m");
        mainTerminal.WriteLine("  \x1b[40m \x1b[41m \x1b[42m \x1b[43m \x1b[44m \x1b[45m \x1b[46m \x1b[47m \x1b[0m");
        mainTerminal.WriteLine("");

        // 256-color palette (sample)
        mainTerminal.WriteLine("\x1b[1m256-Color Palette (sample):\x1b[0m");
        var colors256 = "";
        for (int i = 16; i < 52; i++)
        {
            colors256 += $"\x1b[48;5;{i}m \x1b[0m";
        }
        mainTerminal.WriteLine("  " + colors256);
    }

    private void ShowFormattingDemo()
    {
        if (mainTerminal == null) return;

        mainTerminal.WriteLine("\x1b[1;33mText Formatting Examples:\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("  \x1b[1mBold text\x1b[0m");
        mainTerminal.WriteLine("  \x1b[2mDim text\x1b[0m");
        mainTerminal.WriteLine("  \x1b[3mItalic text\x1b[0m");
        mainTerminal.WriteLine("  \x1b[4mUnderlined text\x1b[0m");
        mainTerminal.WriteLine("  \x1b[7mInverted text\x1b[0m");
        mainTerminal.WriteLine("  \x1b[9mStrikethrough text\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("  \x1b[1;4;31mCombined: Bold + Underline + Red\x1b[0m");
        mainTerminal.WriteLine("  \x1b[3;42;37mCombined: Italic + Green Background + White Text\x1b[0m");
    }

    private async Task ShowProgressDemo()
    {
        if (mainTerminal == null) return;

        mainTerminal.WriteLine("\x1b[1;33mProgress Bar Animation:\x1b[0m");
        mainTerminal.WriteLine("");

        const int totalSteps = 20;
        for (int i = 0; i <= totalSteps; i++)
        {
            var percentage = (i * 100) / totalSteps;
            var filled = new string('█', i);
            var empty = new string('░', totalSteps - i);
            
            // Clear the current line and redraw progress
            mainTerminal.Write($"\r  [\x1b[32m{filled}\x1b[37m{empty}\x1b[0m] {percentage,3}%");
            
            await Task.Delay(100); // Simulate work
            StateHasChanged();
        }
        
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("  \x1b[1;32m✓ Progress completed!\x1b[0m");
    }

    private void ShowSystemInfo()
    {
        if (mainTerminal == null) return;

        mainTerminal.WriteLine("\x1b[1;33mSystem Information:\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine($"  \x1b[1mDate/Time:\x1b[0m     {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        mainTerminal.WriteLine($"  \x1b[1mBlazor Mode:\x1b[0m   WebAssembly");
        mainTerminal.WriteLine($"  \x1b[1m.NET Version:\x1b[0m  {Environment.Version}");
        mainTerminal.WriteLine($"  \x1b[1mUser Agent:\x1b[0m    (detected via JavaScript)");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[1mTerminal Configuration:\x1b[0m");
        mainTerminal.WriteLine($"  \x1b[1mRows:\x1b[0m         {terminalRows}");
        mainTerminal.WriteLine($"  \x1b[1mColumns:\x1b[0m      {terminalColumns}");
        mainTerminal.WriteLine($"  \x1b[1mFont:\x1b[0m         {selectedFontFamily}");
        mainTerminal.WriteLine($"  \x1b[1mFont Size:\x1b[0m    {selectedFontSize}");
        mainTerminal.WriteLine($"  \x1b[1mTheme:\x1b[0m        {selectedThemeName}");
    }

    private void ShowLoremIpsum()
    {
        if (mainTerminal == null) return;

        mainTerminal.WriteLine("\x1b[1;33mLorem Ipsum Text:\x1b[0m");
        mainTerminal.WriteLine("");
        
        var lorem = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod 
tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim 
veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea 
commodo consequat. Duis aute irure dolor in reprehenderit in voluptate 
velit esse cillum dolore eu fugiat nulla pariatur.

Excepteur sint occaecat cupidatat non proident, sunt in culpa qui 
officia deserunt mollit anim id est laborum. Sed ut perspiciatis unde 
omnis iste natus error sit voluptatem accusantium doloremque laudantium.";

        var lines = lorem.Split('\n');
        foreach (var line in lines)
        {
            mainTerminal.WriteLine("  " + line.Trim());
        }
    }

    private void ShowThemeDemo()
    {
        if (mainTerminal == null) return;

        mainTerminal.WriteLine("\x1b[1;33mTheme Color Examples:\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[1mDark Theme:\x1b[0m");
        mainTerminal.WriteLine("  Background: #1E1E1E, Foreground: #D4D4D4");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[1mLight Theme:\x1b[0m");
        mainTerminal.WriteLine("  Background: #FFFFFF, Foreground: #333333");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("\x1b[1mRetro Theme:\x1b[0m");
        mainTerminal.WriteLine("  \x1b[32mBackground: #000000, Foreground: #00FF00\x1b[0m");
        mainTerminal.WriteLine("");
        mainTerminal.WriteLine("Use the theme selector on the right to switch themes!");
    }

    private void ClearTerminal()
    {
        if (mainTerminal != null)
        {
            mainTerminal.Clear();
            mainTerminal.Write("demo> ");
        }
    }

    private async Task OnThemeChanged(ChangeEventArgs e)
    {
        selectedThemeName = e.Value?.ToString() ?? "Dark";
        
        selectedTheme = selectedThemeName switch
        {
            "Dark" => TerminalTheme.Dark(),
            "Light" => TerminalTheme.Light(),            "HighContrast" => new TerminalTheme
            {
                Background = "#000000",
                Foreground = "#FFFFFF",
                CursorColor = "#FFFF00",
                SelectionBackground = "#0066CC"
            },
            "Matrix" => new TerminalTheme
            {
                Background = "#000000",
                Foreground = "#00FF00",
                CursorColor = "#00FF00",
                SelectionBackground = "#003300"
            },
            "Solarized" => new TerminalTheme
            {
                Background = "#002B36",
                Foreground = "#839496",
                CursorColor = "#93A1A1",
                SelectionBackground = "#073642"
            },
            _ => TerminalTheme.Dark()
        };
        
        await Task.CompletedTask;
    }

    private async Task UpdateCustomTheme()
    {        selectedTheme = new TerminalTheme
        {
            Background = customBackgroundColor,
            Foreground = customForegroundColor,
            CursorColor = customForegroundColor,
            SelectionBackground = BlendColors(customBackgroundColor, customForegroundColor, 0.3)
        };
        
        selectedThemeName = "Custom";
        await Task.CompletedTask;
    }

    private string BlendColors(string color1, string color2, double ratio)
    {
        // Simple color blending for selection background
        try
        {
            var c1 = System.Drawing.ColorTranslator.FromHtml(color1);
            var c2 = System.Drawing.ColorTranslator.FromHtml(color2);
            
            var r = (int)(c1.R * (1 - ratio) + c2.R * ratio);
            var g = (int)(c1.G * (1 - ratio) + c2.G * ratio);
            var b = (int)(c1.B * (1 - ratio) + c2.B * ratio);
            
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch
        {
            return "#333333"; // Fallback
        }
    }

    private class CommandHistoryItem
    {
        public string Command { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
