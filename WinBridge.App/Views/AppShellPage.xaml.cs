using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace WinBridge.App.Views;

public sealed partial class AppShellPage : Page
{
    public static AppShellPage Current { get; private set; } = null!;
    public TabView PublicTabView => MainTabView;

    public AppShellPage()
    {
        this.InitializeComponent();
        Current = this;

        this.Loaded += (s, e) =>
        {
            try
            {
                UpdateDeveloperVisibility(WinBridge.App.App.IsDeveloperMode);
                WinBridge.App.App.DeveloperModeChanged += UpdateDeveloperVisibility;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing developer mode: {ex.Message}");
            }
        };
    }

    private void UpdateDeveloperVisibility(bool isDeveloper)
    {
        DeveloperNavItem?.Visibility = isDeveloper ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AppShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];
        NavigateToSystemView("DashboardPage");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateToSystemView("SettingsPage");
        }
        else if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateToSystemView(tag);
        }
    }

    public void NavigateToSystemView(string viewName)
    {
        var typeName = $"WinBridge.App.Views.{viewName}";
        var pageType = typeof(AppShellPage).Assembly.GetType(typeName);

        if (pageType != null)
        {
            
            SystemFrame.Visibility = Visibility.Visible;
            MainTabView.Visibility = Visibility.Collapsed;

            SystemFrame.Navigate(pageType);
        }
    }

    public void OpenTab(string title, Type pageType, object? parameter = null, string iconGlyph = "\uE7BE")
    {
        var frame = new Frame();
        frame.Navigate(pageType, parameter);

        var tabItem = new TabViewItem
        {
            Header = title,
            IconSource = new FontIconSource { Glyph = iconGlyph },
            Content = frame,
            Tag = parameter
        };

        MainTabView.TabItems.Add(tabItem);
        MainTabView.SelectedItem = tabItem;

        MainTabView.Visibility = Visibility.Visible;
        SystemFrame.Visibility = Visibility.Collapsed;

        NavView.SelectedItem = null;
    }

    private void MainTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        
        if (args.Tab.Content is Frame frame && frame.Content is IDisposable disposablePage)
        {
            disposablePage.Dispose();
        }

        sender.TabItems.Remove(args.Tab);

        if (sender.TabItems.Count == 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            NavigateToSystemView("DashboardPage");
        }
    }
}
