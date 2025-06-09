using ProxyServer.GUI.ViewModels;

namespace ProxyServer.GUI.Pages
{
    public partial class ProxySettingsPage : ContentPage
    {
        private readonly ProxySettingsViewModel _viewModel;

        public ProxySettingsPage(ProxySettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }
    }
}
