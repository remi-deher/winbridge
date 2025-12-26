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

        // S'abonner à l'événement de changement de menu
        NavView.SelectionChanged += NavView_SelectionChanged;

        // Charger la page par défaut (Dashboard ou Servers)
        ContentFrame.Navigate(typeof(AddServerPage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            return;
        }

        var selectedItem = args.SelectedItemContainer as NavigationViewItem;

        if (selectedItem != null)
        {
            string tag = selectedItem.Tag.ToString();

            // Selon le "Tag" défini dans le XAML, on change de page
            switch (tag)
            {
                case "Servers":
                    ContentFrame.Navigate(typeof(AddServerPage));
                    break;
            }
        }
    }
}