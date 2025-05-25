using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:key")]
    public partial class AuthSettingsApp : Windows.WindowBase
    {
        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Auth Settings";
        }
    }
}
