namespace HackerSimulator.Wasm.Apps
{
    public partial class ThemePreviewEnhancerApp : Windows.WindowBase
    {
        private string _color = "#0080ff";

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Theme Preview";
        }
    }
}
