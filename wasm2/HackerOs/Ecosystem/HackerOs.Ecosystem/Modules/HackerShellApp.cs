using HackerOs.AppFramework.Abstractions;
using HackerOs.AppFramework.Components;
using HackerOs.AppFramework.Registry;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Ecosystem.Modules;

/// <summary>
/// An interactive terminal application built purely in C# by deriving from
/// <see cref="TerminalAppBase"/>. It exposes a tiny shell that can inspect and
/// launch other registered applications, proving the framework works end to end
/// from the console side as well as the windowed side.
/// </summary>
[App("Hacker Shell", Id = "hackeros.hackershell", Icon = "\U0001F5A5", Category = "Development",
    Description = "A scriptable console for exploring the ecosystem", SortOrder = 10)]
public sealed class HackerShellApp : TerminalAppBase
{
    [Inject] private AppRegistry Registry { get; set; } = default!;

    /// <inheritdoc />
    protected override string Prompt => "root@hackeros:~# ";

    /// <inheritdoc />
    protected override string? Banner =>
        "HackerOS Shell v1.0  \u2014  type 'help' for a list of commands.";

    /// <inheritdoc />
    protected override Task OnCommandAsync(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return Task.CompletedTask;
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].ToLowerInvariant();
        var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (verb)
        {
            case "help":
                WriteLine("available commands:");
                WriteLine("  help            show this help");
                WriteLine("  apps            list registered applications");
                WriteLine("  launch <id>     launch an application by id or name");
                WriteLine("  echo <text>     print text back");
                WriteLine("  whoami          print the current user");
                WriteLine("  date            print the current date and time");
                WriteLine("  clear           clear the screen");
                break;

            case "apps":
                foreach (var app in Registry.Apps)
                {
                    WriteLine($"  {app.Icon} {app.Id,-26} {app.Name} [{app.Kind}]");
                }
                break;

            case "launch":
                LaunchApp(argument);
                break;

            case "echo":
                WriteLine(argument);
                break;

            case "whoami":
                WriteLine("root");
                break;

            case "date":
                WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                break;

            case "clear":
                ClearScreen();
                break;

            default:
                WriteLine($"unknown command: {verb} (try 'help')");
                break;
        }

        return Task.CompletedTask;
    }

    private void LaunchApp(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            WriteLine("usage: launch <id>");
            return;
        }

        var descriptor = Registry.Find(idOrName)
            ?? Registry.Apps.FirstOrDefault(a =>
                string.Equals(a.Name, idOrName, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            WriteLine($"no such application: {idOrName}");
            return;
        }

        Registry.Launch(descriptor);
        WriteLine($"launched {descriptor.Name}");
    }
}
