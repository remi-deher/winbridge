using Microsoft.UI.Xaml.Controls;
using WinBridge.App.Views;
using System;
using System.Linq;

namespace WinBridge.App;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "WinBridge";

        // Gestion de la navigation
        NavView.SelectionChanged += NavView_SelectionChanged;

        // Au démarrage, on sélectionne le premier item (Dashboard) et on navigue
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // Gérer le clic sur le bouton Paramètres (en bas)
        if (args.IsSettingsSelected)
        {
            // ContentFrame.Navigate(typeof(SettingsPage)); // À créer plus tard si besoin
            return;
        }

        // Vérifier qu'un item est bien sélectionné
        var selectedItem = args.SelectedItemContainer as NavigationViewItem;
        if (selectedItem?.Tag == null) return;

        string tag = selectedItem.Tag.ToString();

        // Navigation principale
        switch (tag)
        {
            case "Dashboard":
                ContentFrame.Navigate(typeof(DashboardPage));
                break;

            case "Servers":
                ContentFrame.Navigate(typeof(ServerListPage));
                break;

            case "Keys":
                ContentFrame.Navigate(typeof(KeysPage));
                break;
        }
    }
}