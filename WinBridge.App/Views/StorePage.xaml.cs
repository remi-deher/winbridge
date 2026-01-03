using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using WinBridge.App.Models.Store;
using WinBridge.App.Services;

namespace WinBridge.App.Views;

public sealed partial class StorePage : Page
{
    private readonly StoreService _storeService;
    private readonly SettingsService _settingsService;
    private List<MarketplaceModule> _allModules = new();
    private readonly List<string> _selectedCategories = new();

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<string> StoreSources { get; } = new();

    public StorePage()
    {
        this.InitializeComponent();

        var httpClient = new HttpClient();
        _settingsService = new SettingsService();
        _storeService = new StoreService(httpClient, _settingsService, App.ModuleManagerService);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshSources();
        await LoadCatalogAsync();
    }

    private void RefreshSources()
    {
        StoreSources.Clear();
        foreach (var url in _settingsService.StoreSourceUrls)
        {
            StoreSources.Add(url);
        }
    }

    private void AddSource_Click(object sender, RoutedEventArgs e)
    {
        var url = NewSourceUrlBox.Text;
        if (!string.IsNullOrWhiteSpace(url))
        {
            _settingsService.AddStoreSource(url);
            NewSourceUrlBox.Text = string.Empty;
            RefreshSources();
            
            SourcesFlyout.Hide();

             _ = LoadCatalogAsync();
        }
    }

    private void DeleteSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url)
        {
            _settingsService.RemoveStoreSource(url);
            RefreshSources();
             _ = LoadCatalogAsync();
        }
    }

    private async System.Threading.Tasks.Task LoadCatalogAsync()
    {
        LoadingRing.IsActive = true;
        ModulesGrid.Visibility = Visibility.Collapsed;
        ErrorText.Visibility = Visibility.Collapsed;
        NoModulesInfoBar.IsOpen = false;

        try
        {
            var roots = await _storeService.GetCatalogsAsync();
            _allModules = roots.SelectMany(r => r.Modules).ToList();

            if (_allModules.Count == 0)
            {
                NoModulesInfoBar.Message = "Aucun module trouvé. Vérifiez les URLs de vos dépôts.";
                NoModulesInfoBar.IsOpen = true;
            }
            else
            {
                ModulesGrid.Visibility = Visibility.Visible;
                
                var cats = _allModules.SelectMany(m => m.Categories).Distinct().OrderBy(c => c).ToList();
                Categories.Clear();
                foreach (var c in cats) Categories.Add(c);

                RefreshGrid();
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Failed to load catalog: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            NoModulesInfoBar.Message = "Error loading catalogs.";
            NoModulesInfoBar.IsOpen = true; 
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCatalogAsync();
    }

    private void RefreshGrid()
    {
        var query = SearchBox.Text.Trim();
        var fp = _selectedCategories;

        var filtered = _allModules.Where(m =>
        {
            
            bool matchesSearch = string.IsNullOrWhiteSpace(query) ||
                                 m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 m.ShortDescription.Contains(query, StringComparison.OrdinalIgnoreCase);

            bool matchesCategory = fp.Count == 0 || fp.Any(c => m.Categories.Contains(c));

            return matchesSearch && matchesCategory;
        }).ToList();

        ModulesGrid.ItemsSource = filtered;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        RefreshGrid();
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.Content is string category)
        {
            if (btn.IsChecked == true)
            {
                if (!_selectedCategories.Contains(category)) _selectedCategories.Add(category);
            }
            else
            {
                _selectedCategories.Remove(category);
            }
            RefreshGrid();
        }
    }

    private void ModulesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MarketplaceModule module)
        {
            Frame.Navigate(typeof(ModuleDetailPage), module);
        }
    }
}
