using System;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using WinBridge.App.Models.Store;
using WinBridge.App.Services;

namespace WinBridge.App.Views;

public sealed partial class ModuleDetailPage : Page
{
    private MarketplaceModule? _module;
    private readonly StoreService _storeService;

    public ModuleDetailPage()
    {
        this.InitializeComponent();

        var httpClient = new HttpClient();
        var settingsService = new SettingsService();
        _storeService = new StoreService(httpClient, settingsService, App.ModuleManagerService);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is MarketplaceModule module)
        {
            _module = module;
            PopulateUI(module);
        }
    }

    private void PopulateUI(MarketplaceModule module)
    {
        TitleText.Text = module.Name;
        AuthorText.Text = module.Author;
        VersionText.Text = module.Version;
        DescriptionText.Text = module.FullDescription;
        DateText.Text = module.ReleaseDate.ToString("d");

        if (!string.IsNullOrEmpty(module.IconUrl))
        {
            IconImage.Source = new BitmapImage(new Uri(module.IconUrl));
        }

        ScreenshotsList.ItemsSource = module.Screenshots;

        PermissionsList.ItemsSource = module.RequiredPermissions;

        WebsiteLink.Visibility = Visibility.Collapsed; 
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_module == null) return;

        InstallButton.IsEnabled = false;
        InstallProgress.Visibility = Visibility.Visible;
        StatusText.Visibility = Visibility.Visible;
        StatusText.Text = "Downloading...";

        try
        {
            await _storeService.InstallModuleAsync(_module);

            StatusText.Text = "Installed successfully!";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)); 

            var dialog = new ContentDialog
            {
                Title = "Success",
                Content = $"Module '{_module.Name}' has been installed.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Installation failed";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)); 

            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Failed to install module: {ex.Message}",
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            InstallButton.IsEnabled = true;
            InstallProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }
}
