using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
using WinBridge.Core.Data;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class ServerListPage : Page
{
    public ServerListPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        using var db = new AppDbContext();
        // On récupère les serveurs et on les affiche
        ServerGrid.ItemsSource = db.Servers.ToList();
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        // Récupère l'objet ServerModel attaché à la carte cliquée
        if (sender is Button btn && btn.DataContext is ServerModel server)
        {
            // Navigue vers le terminal en transmettant les infos du serveur
            this.Frame.Navigate(typeof(TerminalPage), server);
        }
    }
}