using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using WinBridge.Core.Data;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;

namespace WinBridge.App.Views
{
    public sealed partial class ServerListPage : Page
    {
        public ObservableCollection<ServerModel> Servers { get; } = new ObservableCollection<ServerModel>();
        private System.Collections.Generic.List<ServerModel> _allServers = new();

        public ServerListPage()
        {
            this.InitializeComponent();
            App.ServerListChanged += (s, e) => DispatcherQueue.TryEnqueue(LoadServers);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadServers();
        }

        private void LoadServers()
        {
            using var db = new AppDbContext();
            _allServers = db.Servers.ToList();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var query = _allServers.AsEnumerable();

            // Text Search
            if (TxtSearch != null && !string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term) || s.Host.ToLower().Contains(term));
            }

            // OS Filter
            if (CmbOsFilter != null && CmbOsFilter.SelectedItem is ComboBoxItem item && item.Tag != null && item.Tag.ToString() != "All")
            {
                var tag = item.Tag.ToString();
                if (tag == "Linux")
                {
                    query = query.Where(s => s.OperatingSystem == ServerOsType.Linux);
                }
                else if (tag == "Windows")
                {
                    query = query.Where(s => s.OperatingSystem == ServerOsType.Windows);
                }
                // Refinements (Debian, etc.) could be added if ServerModel supported distro details
            }

            Servers.Clear();
            foreach (var s in query)
            {
                Servers.Add(s);
            }
            if (ServerGrid != null) ServerGrid.ItemsSource = Servers;
            
            if (TxtEmpty != null) TxtEmpty.Visibility = Servers.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnFilterChanged(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private async void BtnAddServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ServerEditDialog();
            dialog.XamlRoot = this.XamlRoot;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                using var db = new AppDbContext();
                db.Servers.Add(dialog.Result);
                await db.SaveChangesAsync();
                LoadServers();
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ServerModel server)
            {
                var dialog = new ServerEditDialog(server);
                dialog.XamlRoot = this.XamlRoot;
                
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    using var db = new AppDbContext();
                    db.Servers.Update(dialog.Result);
                    await db.SaveChangesAsync();
                    LoadServers();
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ServerModel server)
            {
                var dialog = new ContentDialog
                {
                    Title = "Confirmer la suppression",
                    Content = $"Voulez-vous vraiment supprimer {server.Name} ({server.Host}) ?",
                    PrimaryButtonText = "Supprimer",
                    CloseButtonText = "Annuler",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    using var db = new AppDbContext();
                    db.Servers.Remove(server);
                    await db.SaveChangesAsync();
                    LoadServers();
                }
            }
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ServerModel server)
            {
                // Call MainWindow to open tab
                if ((Application.Current as App)?.Window is MainWindow mainWindow)
                {
                    mainWindow.OpenServerTab(server);
                }
                else
                {
                    // Fallback to legacy navigation just in case
                    Frame.Navigate(typeof(ServerDashboardPage), server);
                }
            }
        }
    }

    public class OsToIconConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ServerOsType os)
            {
                // E714 = Windows Logo or E770 (Desktop)
                // E713 = Server (Generic/Linux)
                return os == ServerOsType.Windows ? "\uE770" : "\uE713"; 
            }
            return "\uE7F8";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) 
            => throw new NotImplementedException();
    }
}
