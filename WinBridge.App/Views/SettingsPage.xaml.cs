using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinBridge.App.Services;

namespace WinBridge.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService;
    public ObservableCollection<string> RepositoryUrls { get; } = new();

    public SettingsPage()
    {
        this.InitializeComponent();
        _settingsService = new SettingsService();
        DeveloperModeToggle.IsOn = WinBridge.App.App.IsDeveloperMode;
        InitializeRepositories();
    }

    private void InitializeRepositories()
    {
        RepositoryUrls.Clear();
        foreach (var url in _settingsService.StoreSourceUrls)
        {
            RepositoryUrls.Add(url);
        }
        RepositoriesList.ItemsSource = RepositoryUrls;
    }

    private void DeveloperSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        WinBridge.App.App.IsDeveloperMode = DeveloperModeToggle.IsOn;
    }

    private void AddRepo_Click(object sender, RoutedEventArgs e)
    {
        var url = RepoUrlBox.Text;
        if (!string.IsNullOrWhiteSpace(url))
        {
            _settingsService.AddStoreSource(url);
            RepoUrlBox.Text = string.Empty;
            InitializeRepositories();
        }
    }

    private void DeleteRepo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url)
        {
            _settingsService.RemoveStoreSource(url);
            InitializeRepositories();
        }
    }
}
