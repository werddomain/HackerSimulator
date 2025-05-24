using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:cog")]
    public partial class SettingsApp : Windows.WindowBase
    {
        [Inject] private ApplicationService AppService { get; set; } = default!;
        [Inject] private SettingsService Settings { get; set; } = default!;
        [Inject] private AuthService Auth { get; set; } = default!;

        private IReadOnlyList<ApplicationService.AppInfo> _apps = new List<ApplicationService.AppInfo>();
        private string? _selectedApp;
        private int _tab;

        private readonly List<SettingEditor.SettingItem> _userSettings = new();
        private readonly List<SettingEditor.SettingItem> _machineSettings = new();
        private bool _dirtyUser;
        private bool _dirtyMachine;
        private bool _isAdmin;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Settings";
            _apps = AppService.GetApps();
            _isAdmin = Auth.GetUserGroup() == 1;
        }

        private async Task OnAppChanged(string? value)
        {
            _selectedApp = value;
            await LoadSettings();
        }

        private async Task LoadSettings()
        {
            if (string.IsNullOrWhiteSpace(_selectedApp)) return;
            _userSettings.Clear();
            _machineSettings.Clear();
            var usr = await Settings.LoadUser(_selectedApp);
            foreach (var kv in usr)
                _userSettings.Add(new SettingEditor.SettingItem { Key = kv.Key, Value = kv.Value });
            var mach = await Settings.LoadMachine(_selectedApp);
            foreach (var kv in mach)
                _machineSettings.Add(new SettingEditor.SettingItem { Key = kv.Key, Value = kv.Value });
            _dirtyUser = false;
            _dirtyMachine = false;
            StateHasChanged();
        }

        private static Dictionary<string,string> ToDictionary(IEnumerable<SettingEditor.SettingItem> items)
            => items.Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

        private async Task SaveUser()
        {
            if (_selectedApp == null) return;
            await Settings.SaveUser(_selectedApp, ToDictionary(_userSettings));
            _dirtyUser = false;
        }

        private async Task SaveMachine()
        {
            if (_selectedApp == null || !_isAdmin) return;
            await Settings.SaveMachine(_selectedApp, ToDictionary(_machineSettings));
            _dirtyMachine = false;
        }

    }
}
