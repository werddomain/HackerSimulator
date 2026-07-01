using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.Ecosystem.Modules;

/// <summary>
/// A gothic/hacker themed resource monitor. It mimics a real system monitor by
/// polling a collocated JS sampler once per second and rendering CPU, memory and
/// network gauges plus a simulated process table.
/// </summary>
public partial class SystemMonitorApp
{
    private readonly List<ProcessRow> _processes = new();
    private IJSObjectReference? _module;
    private System.Threading.Timer? _timer;
    private int _cpu;
    private int _mem;
    private int _net;
    private int _ticks;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed record ProcessRow(int Pid, string Name, int Cpu, int Mem);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SeedProcesses();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./Modules/SystemMonitorApp.razor.js");

        await SampleAsync();

        // Mimic a real monitor with a periodic refresh.
        _timer = new System.Threading.Timer(
            _ => _ = InvokeAsync(SampleAsync), null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private async Task SampleAsync()
    {
        if (_module is null)
        {
            return;
        }

        var reading = await _module.InvokeAsync<Reading>("sample");
        _cpu = reading.Cpu;
        _mem = reading.Mem;
        _net = reading.Net;
        _ticks++;

        // Nudge the process table so it feels alive.
        for (var i = 0; i < _processes.Count; i++)
        {
            var jitter = reading.Jitter[i % reading.Jitter.Length];
            _processes[i] = _processes[i] with
            {
                Cpu = Math.Clamp(_processes[i].Cpu + jitter, 0, 99)
            };
        }

        StateHasChanged();
    }

    private void SeedProcesses()
    {
        _processes.Clear();
        _processes.Add(new ProcessRow(1337, "kerneld", 3, 128));
        _processes.Add(new ProcessRow(2048, "netscan", 12, 64));
        _processes.Add(new ProcessRow(3141, "cryptominer", 41, 512));
        _processes.Add(new ProcessRow(4096, "hackershell", 2, 32));
        _processes.Add(new ProcessRow(8192, "watchdog", 1, 16));
    }

    private sealed record Reading(int Cpu, int Mem, int Net, int[] Jitter);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Dispose();
            _timer = null;
            if (_module is not null)
            {
                // Fire and forget; the JS runtime may already be disconnected as
                // the application shuts down. Swallow that expected failure so the
                // task does not raise an unobserved exception.
                _ = DisposeModuleAsync(_module);
                _module = null;
            }
        }

        base.Dispose(disposing);
    }

    private static async Task DisposeModuleAsync(IJSObjectReference module)
    {
        try
        {
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // JS runtime already gone; nothing to clean up.
        }
    }
}
