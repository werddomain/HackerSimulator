using ProxyServer.GUI.Models;
using ProxyServer.GUI.PageModels;

namespace ProxyServer.GUI.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}