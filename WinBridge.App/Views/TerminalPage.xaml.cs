using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks; // Nécessaire pour Task.Run
using WinBridge.Core.Services;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class TerminalPage : Page
{
    private SshService _sshService = new SshService();
    private ServerModel? _currentServer;

    public TerminalPage()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ServerModel server)
        {
            _currentServer = server;

            // Abonnement aux données reçues
            _sshService.DataReceived += (data) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    TxtTerminalOutput.Text += data;
                    TerminalScroll.ChangeView(0, TerminalScroll.ScrollableHeight, 1);
                });
            };

            // Connexion sécurisée
            try
            {
                TxtTerminalOutput.Text = $"Connexion à {server.Host}...\n";

                // On lance la connexion sur un thread secondaire pour ne pas bloquer l'UI
                await Task.Run(() => _sshService.Connect(server));

                TxtTerminalOutput.Text += "Connecté !\n";
            }
            catch (Exception ex)
            {
                // En cas d'erreur, on affiche une modale et on revient en arrière
                await ShowErrorAndGoBack(ex.Message);
            }
        }
    }

    private async Task ShowErrorAndGoBack(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Erreur de connexion",
            Content = $"Impossible de se connecter au serveur :\n{message}",
            CloseButtonText = "Retour",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();

        // Retour à la liste des serveurs
        if (Frame.CanGoBack) Frame.GoBack();
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        // Nettoyage propre quand on quitte la page
        _sshService.Dispose();
        base.OnNavigatingFrom(e);
    }

    private void TxtInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _sshService.SendCommand(TxtInput.Text);
            TxtInput.Text = "";
        }
    }
}