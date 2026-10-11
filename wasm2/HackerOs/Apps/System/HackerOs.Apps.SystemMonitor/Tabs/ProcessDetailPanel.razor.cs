using HackerOs.AppSdk.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.SystemMonitor.Tabs;

/// <summary>
/// Detail of one process (ADR 0058, section 2): the whole <c>status</c>, the command line, the
/// working directory, the environment when readable, the <c>io</c> counters, the parent and the
/// children. Everything comes from <see cref="HackProcess"/>, that is from <c>/proc/&lt;pid&gt;</c>.
/// </summary>
public partial class ProcessDetailPanel
{
    private IReadOnlyDictionary<string, string>? _environment;
    private long? _environmentPid;
    private bool _loadingEnvironment;

    /// <summary>Gets or sets the process shown (refreshed every second by the window).</summary>
    [Parameter, EditorRequired]
    public HackProcess Process { get; set; } = default!;

    /// <summary>Gets or sets every visible process, to find the parent and the children.</summary>
    [Parameter, EditorRequired]
    public IEnumerable<HackProcess> AllProcesses { get; set; } = [];

    /// <summary>Gets or sets the handler that closes the panel.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Gets or sets the handler that shows another process (parent or child).</summary>
    [Parameter]
    public EventCallback<long> OnSelectProcess { get; set; }

    private HackProcess? Parent => Process.ParentId is { } parentId
        ? AllProcesses.FirstOrDefault(process => process.Id == parentId)
        : null;

    private IReadOnlyList<HackProcess> Children => Process.GetChildren(AllProcesses);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // The environment rarely changes: read it when another process is shown, or on "reload".
        if (_environmentPid != Process.Id)
        {
            await LoadEnvironmentAsync();
        }
    }

    private async Task LoadEnvironmentAsync()
    {
        _environmentPid = Process.Id;
        _loadingEnvironment = true;
        try
        {
            _environment = await Process.GetEnvironmentVariablesAsync();
        }
        finally
        {
            _loadingEnvironment = false;
        }
    }
}
