using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;
using Microsoft.AspNetCore.Components.Web;
using HackerSimulator.Wasm.Commands;
using HackerSimulator.Wasm.Windows;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:terminal")] 
    public partial class TerminalApp : WindowBase
    {
        private readonly List<string> _lines = new();
        private string _input = string.Empty;
        private int _historyIndex = -1;
        private readonly List<string> _history = new();
        private ElementReference _inputRef;
        private string _cwd = "~";

        private string _display => string.Join("\n", _lines);
        private string Prompt => $"user@hacker-machine:{_cwd}$ ";

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Terminal";
            _lines.Add("Welcome to HackerOS Terminal");
            _lines.Add("Type \"help\" for a list of commands");
            _lines.Add(string.Empty);
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                await FocusInput();
                AppendPrompt();
            }
        }

        private async Task FocusInput()
        {
            await _inputRef.FocusAsync();
        }

        private void AppendPrompt()
        {
            _lines.Add(Prompt);
            StateHasChanged();
        }

        private async Task HandleInputKey(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await ExecuteCommand();
            }
            else if (e.Key == "ArrowUp")
            {
                NavigateHistory(1);
            }
            else if (e.Key == "ArrowDown")
            {
                NavigateHistory(-1);
            }
        }

        private void NavigateHistory(int direction)
        {
            if (_history.Count == 0)
                return;

            if (direction > 0)
            {
                if (_historyIndex < _history.Count - 1)
                    _historyIndex++;
            }
            else
            {
                if (_historyIndex >= 0)
                    _historyIndex--;
            }

            if (_historyIndex >= 0 && _historyIndex < _history.Count)
            {
                _input = _history[_history.Count - 1 - _historyIndex];
            }
            else
            {
                _input = string.Empty;
            }
        }

        private async Task ExecuteCommand()
        {
            var command = _input.Trim();
            _lines[^1] = Prompt + _input; // replace prompt line with command
            if (!string.IsNullOrWhiteSpace(command))
            {
                _history.Add(command);
            }
            _input = string.Empty;
            _historyIndex = -1;

            if (!string.IsNullOrWhiteSpace(command))
            {
                var output = await RunShellCommand(command);
                if (!string.IsNullOrEmpty(output))
                {
                    foreach (var line in output.Split('\n'))
                    {
                        _lines.Add(line.Replace("\r", ""));
                    }
                }
            }

            AppendPrompt();
        }

        private async Task<string> RunShellCommand(string command)
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var ctx = new CommandContext
            {
                Stdin = new StringReader(string.Empty),
                Stdout = stdout,
                Stderr = stderr,
                Env = new Dictionary<string, string>
                {
                    {"PWD", _cwd},
                    {"USER", "user"}
                }
            };

            await Shell.ExecuteCommand(command, ctx);
            if (ctx.Env.TryGetValue("PWD", out var newCwd))
            {
                _cwd = newCwd;
            }

            var output = stdout.ToString();
            var error = stderr.ToString();
            return string.IsNullOrEmpty(error) ? output : output + error;
        }
    }
}
