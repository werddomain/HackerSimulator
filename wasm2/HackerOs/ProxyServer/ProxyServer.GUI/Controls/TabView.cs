using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace ProxyServer.GUI.Controls
{
    public class TabView : Grid
    {
        private readonly StackLayout _tabHeaderLayout;
        private readonly ContentView _tabContentView;
        private readonly List<TabItem> _tabItems = new List<TabItem>();

        public TabView()
        {            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            };
            
            _tabHeaderLayout = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Spacing = 0
            };
            
            _tabContentView = new ContentView();
            
            Children.Add(_tabHeaderLayout);
            SetRow((BindableObject)_tabHeaderLayout, 0);

            Children.Add(_tabContentView);
            SetRow((BindableObject)_tabContentView, 1);
        }

        protected override void OnChildAdded(Element child)
        {
            if (child is TabItem tabItem)
            {
                _tabItems.Add(tabItem);
                UpdateTabs();
                return;
            }
            base.OnChildAdded(child);
        }

        private void UpdateTabs()
        {
            _tabHeaderLayout.Children.Clear();

            foreach (var tabItem in _tabItems)
            {
                var tabButton = new Button
                {
                    Text = tabItem.Header,
                    BackgroundColor = Colors.LightGray,
                    TextColor = Colors.Black,
                    BorderWidth = 0,
                    CornerRadius = 5,
                    Margin = new Thickness(2),
                    Padding = new Thickness(10, 5)
                };

                tabButton.Clicked += (sender, e) => SelectTab(tabItem);
                _tabHeaderLayout.Children.Add(tabButton);
            }

            // Select first tab by default if any exist
            if (_tabItems.Any())
            {
                SelectTab(_tabItems.First());
            }
        }

        private void SelectTab(TabItem tabItem)
        {
            _tabContentView.Content = tabItem;

            // Update button styles to show selection
            foreach (Button button in _tabHeaderLayout.Children)
            {
                if (button.Text == tabItem.Header)
                {
                    button.BackgroundColor = Colors.Blue;
                    button.TextColor = Colors.White;
                }
                else
                {
                    button.BackgroundColor = Colors.LightGray;
                    button.TextColor = Colors.Black;
                }
            }
        }
    }

    public class TabItem : ContentView
    {
        public static readonly BindableProperty HeaderProperty =
            BindableProperty.Create(nameof(Header), typeof(string), typeof(TabItem), string.Empty);

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }
    }
}
