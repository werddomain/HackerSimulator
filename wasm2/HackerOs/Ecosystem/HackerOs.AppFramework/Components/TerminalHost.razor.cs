using BlazorTerminal.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.AppFramework.Components;

/// <summary>
/// A reusable, dependency-free interactive console surface built on top of the
/// <see cref="Terminal"/> component. It handles character echo, line editing and
/// command submission so <see cref="TerminalAppBase"/> applications only need to
/// implement command handling.
/// </summary>
public partial class TerminalHost : ComponentBase, IAsyncDisposable
{
    private Terminal? _terminal;
    private ElementReference _hostElement;
    private IJSObjectReference? _module;
    private string _line = string.Empty;
    private bool _started;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Number of character columns.</summary>
    [Parameter] public int Columns { get; set; } = 80;

    /// <summary>Number of character rows.</summary>
    [Parameter] public int Rows { get; set; } = 24;

    /// <summary>Font size in pixels.</summary>
    [Parameter] public int FontSize { get; set; } = 15;

    /// <summary>The prompt written before each input line.</summary>
    [Parameter] public string Prompt { get; set; } = "$ ";

    /// <summary>Optional banner written once when the terminal starts.</summary>
    [Parameter] public string? Banner { get; set; }

    /// <summary>Invoked when the user submits a command by pressing Enter.</summary>
    [Parameter] public EventCallback<string> OnCommand { get; set; }

    /// <summary>Invoked once when the terminal is ready for interaction.</summary>
    [Parameter] public EventCallback OnReady { get; set; }

    /// <summary>Writes raw text to the terminal at the cursor position.</summary>
    public void Write(string text) => _terminal?.Write(text);

    /// <summary>Writes a line of text followed by a CR/LF sequence.</summary>
    public void WriteLine(string text = "") => _terminal?.Write(text + "\r\n");

    /// <summary>Clears the terminal screen and resets the cursor.</summary>
    public void ClearScreen()
    {
        _terminal?.Clear();
        StateHasChanged();
    }

    /// <summary>Writes the prompt for a fresh input line.</summary>
    public void WritePrompt() => Write(Prompt);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _started)
        {
            return;
        }

        _started = true;

        // Load the collocated JS module used for focus management.
        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/HackerOs.AppFramework/Components/TerminalHost.razor.js");

        if (!string.IsNullOrEmpty(Banner))
        {
            foreach (var bannerLine in Banner.Replace("\r\n", "\n").Split('\n'))
            {
                WriteLine(bannerLine);
            }
        }

        WritePrompt();
        await FocusAsync();

        if (OnReady.HasDelegate)
        {
            await OnReady.InvokeAsync();
        }
    }

    /// <summary>Gives keyboard focus to the underlying terminal element.</summary>
    public async Task FocusAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("focusTerminal", _hostElement);
        }
    }

    private async Task HandleInputAsync(string input)
    {
        switch (input)
        {
            case "\r": // Enter -> submit the current line.
                WriteLine();
                var command = _line;
                _line = string.Empty;
                if (OnCommand.HasDelegate)
                {
                    await OnCommand.InvokeAsync(command);
                }
                WritePrompt();
                break;

            case "\b": // Backspace -> destructive erase of the last character.
                if (_line.Length > 0)
                {
                    _line = _line[..^1];
                    Write("\b \b");
                }
                break;

            default:
                // Echo printable single characters; ignore control/escape sequences.
                if (input.Length == 1 && !char.IsControl(input[0]))
                {
                    _line += input;
                    Write(input);
                }
                break;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already gone; nothing to clean up.
            }
        }
    }
}
