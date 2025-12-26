using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class ServerDashboardPage : Page
{
    private SshService _sshService;

    public ServerDashboardPage()
    {
        this.InitializeComponent();
        _sshService = new SshService();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ServerModel server)
        {
            TxtServerName.Text = server.Name;
            TxtServerHost.Text = $"{server.Username}@{server.Host}";

            try
            {
                // 1. Connexion SSH (Unique pour toute la page)
                await Task.Run(() => _sshService.Connect(server));

                // 2. On passe le service au composant terminal pour qu'il s'affiche
                MyTerminal.SetSshService(_sshService);

                // 3. On récupère les infos système en arrière-plan
                LoadServerStats();
            }
            catch (Exception ex)
            {
                TxtServerHost.Text = "Erreur de connexion";
                var dialog = new ContentDialog
                {
                    Title = "Erreur",
                    Content = ex.Message,
                    CloseButtonText = "Fermer",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }

    private async void LoadServerStats()
    {
        // Récupération de l'OS
        var os = await _sshService.ExecuteCommandAsync("grep -E '^(PRETTY_NAME)=' /etc/os-release | cut -d '\"' -f 2");
        lblOs.Text = string.IsNullOrWhiteSpace(os) ? "Linux (Inconnu)" : os;

        // Récupération de l'Uptime
        var uptime = await _sshService.ExecuteCommandAsync("uptime -p");
        lblUptime.Text = uptime.Replace("up ", "");

        // Récupération Espace Disque (Racine)
        var disk = await _sshService.ExecuteCommandAsync("df -h / --output=pcent | tail -1");
        lblDisk.Text = $"{disk.Trim()} utilisé";
    }

    // Gère l'agrandissement / réduction du terminal
    private void MyTerminal_ToggleFullScreen(object sender, bool isFullScreen)
    {
        if (isFullScreen)
        {
            // Mode Plein Écran : On cache la colonne de gauche (Modules)
            ColCenter.Width = new GridLength(0);

            // La colonne de droite prend toute la place
            ColRight.Width = new GridLength(1, GridUnitType.Star);

            // Le terminal s'étend sur les 2 lignes (cache les infos du bas)
            Grid.SetRowSpan(MyTerminal, 2);
        }
        else
        {
            // Mode Normal (Cockpit)
            ColCenter.Width = new GridLength(1, GridUnitType.Star);
            ColRight.Width = new GridLength(400);
            Grid.SetRowSpan(MyTerminal, 1);
        }
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        // Nettoyage propre
        MyTerminal.Dispose();
        _sshService.Dispose();
        base.OnNavigatingFrom(e);
    }
}