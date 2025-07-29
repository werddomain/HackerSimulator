using ProxyServer.GUI.ViewModels;

namespace ProxyServer.GUI.Pages
{
    public partial class ProxyStatusPage : ContentPage
    {
        private readonly ProxyServerViewModel _viewModel;

        public ProxyStatusPage(ProxyServerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }
    }
}
