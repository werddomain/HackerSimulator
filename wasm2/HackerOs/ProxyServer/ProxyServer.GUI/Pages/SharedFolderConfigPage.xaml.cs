using ProxyServer.GUI.ViewModels;

namespace ProxyServer.GUI.Pages
{
    public partial class SharedFolderConfigPage : ContentPage
    {
        private readonly SharedFolderViewModel _viewModel;

        public SharedFolderConfigPage(SharedFolderViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }
    }
}
