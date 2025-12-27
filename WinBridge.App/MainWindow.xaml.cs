using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.App.Views;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;
using WinBridge.SDK.Broadcasting;
using WinBridge.SDK;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WinBridge.App;

public sealed partial class MainWindow : Window
{
    private readonly IBroadcastLogger _logger;
    private readonly RemoteSessionManager _sessionManager;
    private int _unreadLogs = 0;

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "WinBridge";

        // Resolve services
        _logger = App.Services?.GetRequiredService<IBroadcastLogger>() ?? new BroadcastLogger();
        _sessionManager = App.Services?.GetRequiredService<RemoteSessionManager>() ?? new RemoteSessionManager(_logger);

        _logger.OnLogReceived += Logger_OnLogReceived;

        // Nav logic
        NavView.SelectionChanged += NavView_SelectionChanged; // Ensure verified
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void Logger_OnLogReceived(LogMessage msg)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TxtStatus.Text = $"{msg.Timestamp:HH:mm:ss} - {msg.Message}";
            
            if (LogPanel.Visibility == Visibility.Collapsed)
            {
                _unreadLogs++;
                BadgeLogs.Value = _unreadLogs;
                BadgeLogs.Visibility = Visibility.Visible;
            }
        });
    }

    private void BtnLogs_Click(object sender, RoutedEventArgs e)
    {
        if (LogPanel.Visibility == Visibility.Visible)
        {
            LogPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            LogPanel.Visibility = Visibility.Visible;
            _unreadLogs = 0;
            BadgeLogs.Visibility = Visibility.Collapsed;
        }
    }

    // Called from ServerListPage
    public void OpenServerTab(ServerModel server)
    {
        // Check if already open
        foreach (var item in MainTabView.TabItems)
        {
            if (item is TabViewItem tvi && tvi.Tag is ServerModel s && s.Id == server.Id)
            {
                MainTabView.SelectedItem = item;
                return;
            }
        }

        // Create new tab
        var newTab = new TabViewItem
        {
            Header = server.Name,
            Tag = server,
            IconSource = new SymbolIconSource { Symbol = Symbol.Remote }
        };

        var frame = new Frame();
        newTab.Content = frame;
        
        MainTabView.TabItems.Add(newTab);
        MainTabView.SelectedItem = newTab;

        // Navigate to ServerDashboardPage
        frame.Navigate(typeof(ServerDashboardPage), server);
    }

    private async void MainTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is TabViewItem tvi && tvi.Tag is ServerModel server)
        {
            // Check session
            var session = _sessionManager.GetSession(server.Id);
            if (session != null && session.IsConnected)
            {
                var dialog = new ContentDialog
                {
                    Title = "Fermeture de l'onglet",
                    Content = $"La connexion à {server.Name} est toujours active.\nQue souhaitez-vous faire ?",
                    PrimaryButtonText = "Déconnecter",
                    SecondaryButtonText = "Garder en arrière-plan",
                    CloseButtonText = "Annuler",
                    XamlRoot = this.Content.XamlRoot
                };

                // Use a safe wrapper for XamlRoot if needed, but this.Content should be set.
                if (this.Content == null || this.Content.XamlRoot == null)
                {
                     // Fallback check
                     System.Diagnostics.Debug.WriteLine("XamlRoot null on Close Tab");
                }

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.None) return; // Cancel

                if (result == ContentDialogResult.Primary)
                {
                    // Disconnect
                    _sessionManager.InvalidateSession(server.Id);
                }
                // Else (Secondary) -> Keep session, just remove tab
            }

            sender.TabItems.Remove(args.Item);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        var selectedItem = args.SelectedItemContainer as NavigationViewItem;
        if (selectedItem?.Tag == null) return;

        string tag = selectedItem.Tag.ToString();
        switch (tag)
        {
            case "Dashboard": ContentFrame.Navigate(typeof(DashboardPage)); break;
            case "Servers": ContentFrame.Navigate(typeof(ServerListPage)); break;
            case "Keys": ContentFrame.Navigate(typeof(KeysPage)); break;
            case "Extensions": ContentFrame.Navigate(typeof(ExtensionsPage)); break;
            case "ModulesManagement": ContentFrame.Navigate(typeof(ModulesManagementPage)); break;
            case "DevTools": ContentFrame.Navigate(typeof(DevToolsPage)); break;
        }
    }
    // Command Palette Logic
    private void ToggleCommandPalette(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (CommandPaletteOverlay.Visibility == Visibility.Visible)
        {
            CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            CommandPaletteOverlay.Visibility = Visibility.Visible;
            CmdPaletteSearch.Text = "";
            CmdPaletteSearch.Focus(FocusState.Programmatic);
            LoadModuleActions();
        }
    }

    private void LoadModuleActions()
    {
         var actions = new System.Collections.Generic.List<ModuleAction>();
         
         if (MainTabView.SelectedItem is TabViewItem tvi && tvi.Tag is ServerModel server)
         {
             // TODO: Fetch real modules from ModuleRegistry/Server Context
             // For now, mock basic actions
             actions.Add(new ModuleAction { Title = "Open Terminal", Description = "Open SSH Terminal for " + server.Name, IconGlyph = "\uE756", Action = () => { /* Already open? */ } });
             actions.Add(new ModuleAction { Title = "View Metrics", Description = "Go to dashboard", IconGlyph = "\uE9D2" });
         }

         CmdPaletteList.ItemsSource = actions;
    }

    private void CmdPaletteSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        // TODO: Implement filtering based on text
    }

    private void CmdPaletteSearch_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void CmdPaletteList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModuleAction action)
        {
            action.Execute();
            CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        }
    }
}
