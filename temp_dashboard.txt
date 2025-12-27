using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using WinBridge.Core.Data;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class ServerDashboardPage : Page
{
    private SshService _sshService;
    private DispatcherTimer _refreshTimer;
    private ServerModel? _server;
    private bool _isWindows = false; // Flag pour savoir quel jeu de commandes utiliser

    public ServerDashboardPage()
    {
        this.InitializeComponent();
        _sshService = new SshService();

        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(5);
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ServerModel server)
        {
            _server = server;
            TxtServerName.Text = server.Name;
            TxtServerHost.Text = $"{server.Username}@{server.Host}";

            try
            {
                await Task.Run(() => _sshService.Connect(server));
                MyTerminal.SetSshService(_sshService);

                // Détection initiale de l'OS
                await DetectOsType();

                await LoadServerStats();
                _refreshTimer.Start();
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

    private async Task DetectOsType()
    {
        // On essaie une commande typiquement Linux
        var check = await _sshService.ExecuteCommandAsync("uname");

        // Si ça répond "Linux" ou "Darwin" (Mac), c'est du *nix.
        // Si c'est Windows, "uname" n'existe pas (erreur) ou renvoie autre chose.
        if (check.Contains("Linux") || check.Contains("Darwin"))
        {
            _isWindows = false;
        }
        else
        {
            // On vérifie si c'est Windows
            var ver = await _sshService.ExecuteCommandAsync("cmd /c ver");
            if (ver.Contains("Microsoft Windows"))
            {
                _isWindows = true;
            }
        }
    }

    private async void RefreshTimer_Tick(object sender, object e)
    {
        await LoadServerStats();
    }

    private async Task LoadServerStats()
    {
        try
        {
            if (_isWindows)
                await LoadWindowsStats();
            else
                await LoadLinuxStats();
        }
        catch (Exception)
        {
            lblOs.Text = "Données indisponibles";
        }
    }

    private async Task LoadLinuxStats()
    {
        bool hasChanges = false;

        // 1. OS & Noyau (Cache)
        if (string.IsNullOrEmpty(_server?.CachedOsInfo))
        {
            // OS Name
            var os = await _sshService.ExecuteCommandAsync("grep -E '^(PRETTY_NAME)=' /etc/os-release | cut -d '\"' -f 2");
            var cleanOs = string.IsNullOrWhiteSpace(os) ? "Linux" : os.Replace("\n", "").Trim();

            // Kernel Version
            var kernel = await _sshService.ExecuteCommandAsync("uname -r");

            if (_server != null)
            {
                _server.CachedOsInfo = cleanOs;
                _server.CachedKernelVersion = kernel.Trim();
                hasChanges = true;
            }
            lblOs.Text = cleanOs;
            lblKernel.Text = kernel.Trim();
        }
        else
        {
            lblOs.Text = _server.CachedOsInfo;
            lblKernel.Text = _server.CachedKernelVersion ?? "Unknown";
        }

        // 2. IP
        if (string.IsNullOrEmpty(_server?.CachedIpAddress))
        {
            var ip = await _sshService.ExecuteCommandAsync("hostname -I | cut -d' ' -f1");
            var cleanIp = string.IsNullOrWhiteSpace(ip) ? "Inconnue" : ip.Trim();
            if (_server != null) { _server.CachedIpAddress = cleanIp; hasChanges = true; }
            lblIp.Text = cleanIp;
        }
        else
        {
            lblIp.Text = _server.CachedIpAddress;
        }

        // 3. Stats Dynamiques
        var uptime = await _sshService.ExecuteCommandAsync("uptime -p");
        lblUptime.Text = uptime.Replace("up ", "").Trim();

        var cpu = await _sshService.ExecuteCommandAsync("cat /proc/loadavg | awk '{print $1}'");
        lblCpu.Text = cpu; // Load Average

        var ram = await _sshService.ExecuteCommandAsync("free -m | awk '/Mem:/ { printf(\"%d%%\", $3/$2*100) }'");
        lblRam.Text = ram;

        var disk = await _sshService.ExecuteCommandAsync("df -h / --output=pcent | tail -1");
        lblDisk.Text = $"{disk.Trim()} utilisé";

        UpdateServerCache(hasChanges);
    }

    private async Task LoadWindowsStats()
    {
        // Commandes PowerShell pour Windows
        bool hasChanges = false;

        // 1. OS & Noyau
        if (string.IsNullOrEmpty(_server?.CachedOsInfo))
        {
            // OS Name (via cmd pour aller vite)
            var os = await _sshService.ExecuteCommandAsync("powershell -command \"(Get-CimInstance Win32_OperatingSystem).Caption\"");

            // Kernel (Version Build)
            var kernel = await _sshService.ExecuteCommandAsync("powershell -command \"(Get-CimInstance Win32_OperatingSystem).Version\"");

            if (_server != null)
            {
                _server.CachedOsInfo = os.Trim();
                _server.CachedKernelVersion = kernel.Trim();
                hasChanges = true;
            }
            lblOs.Text = os.Trim();
            lblKernel.Text = kernel.Trim();
        }
        else
        {
            lblOs.Text = _server.CachedOsInfo;
            lblKernel.Text = _server.CachedKernelVersion ?? "...";
        }

        // 2. IP
        if (string.IsNullOrEmpty(_server?.CachedIpAddress))
        {
            // Récupère la première IPv4 non-loopback
            var ip = await _sshService.ExecuteCommandAsync("powershell -command \"(Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -notlike '*Loopback*'}).IPAddress | Select-Object -First 1\"");
            var cleanIp = ip.Trim();
            if (_server != null) { _server.CachedIpAddress = cleanIp; hasChanges = true; }
            lblIp.Text = cleanIp;
        }
        else
        {
            lblIp.Text = _server.CachedIpAddress;
        }

        // 3. Stats Dynamiques (PowerShell)

        // Uptime (Calculé)
        var uptimeCmd = "powershell -command \"New-TimeSpan -Start (Get-CimInstance Win32_OperatingSystem).LastBootUpTime -End (Get-Date) | Select-Object -Property Days,Hours,Minutes | ForEach-Object { '{0}j {1}h {2}m' -f $_.Days, $_.Hours, $_.Minutes }\"";
        var uptime = await _sshService.ExecuteCommandAsync(uptimeCmd);
        lblUptime.Text = uptime.Trim();

        // CPU (Load Percentage)
        var cpu = await _sshService.ExecuteCommandAsync("powershell -command \"(Get-WmiObject Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average\"");
        lblCpu.Text = $"{cpu.Trim()}%";

        // RAM (%)
        var ramCmd = "powershell -command \"$m = Get-CimInstance Win32_OperatingSystem; '{0:N0}%' -f ((($m.TotalVisibleMemorySize - $m.FreePhysicalMemory) / $m.TotalVisibleMemorySize) * 100)\"";
        var ram = await _sshService.ExecuteCommandAsync(ramCmd);
        lblRam.Text = ram.Trim();

        // Disk (C:)
        var diskCmd = "powershell -command \"$d = Get-PSDrive C; '{0:N0}%' -f (($d.Used / ($d.Used + $d.Free)) * 100)\"";
        var disk = await _sshService.ExecuteCommandAsync(diskCmd);
        lblDisk.Text = $"{disk.Trim()} utilisé (C:)";

        UpdateServerCache(hasChanges);
    }

    private void UpdateServerCache(bool hasChanges)
    {
        if (_server != null)
        {
            _server.LastSeen = DateTime.Now;
            if (hasChanges)
            {
                _ = Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    db.Servers.Update(_server);
                    db.SaveChanges();
                });
            }
        }
    }

    // --- ACTIONS ---

    private async void BtnReboot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Confirmation",
            Content = "Voulez-vous vraiment redémarrer le serveur ?",
            PrimaryButtonText = "Redémarrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            // Commande adaptée à l'OS
            if (_isWindows)
                _sshService.SendData("shutdown /r /t 0\n");
            else
                _sshService.SendData("sudo reboot\n");
        }
    }

    private async void BtnShutdown_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Arrêt du serveur",
            Content = "Voulez-vous vraiment éteindre le serveur ?",
            PrimaryButtonText = "Éteindre",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (_isWindows)
                _sshService.SendData("shutdown /s /t 0\n");
            else
                _sshService.SendData("sudo poweroff\n");
        }
    }

    private void MyTerminal_ToggleFullScreen(object sender, bool isFullScreen)
    {
        if (isFullScreen)
        {
            ColCenter.Width = new GridLength(0);
            ColRight.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRowSpan(MyTerminal, 2);
        }
        else
        {
            ColCenter.Width = new GridLength(1, GridUnitType.Star);
            ColRight.Width = new GridLength(400);
            Grid.SetRowSpan(MyTerminal, 1);
        }
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (_refreshTimer.IsEnabled) _refreshTimer.Stop();
        MyTerminal.Dispose();
        _sshService.Dispose();
        base.OnNavigatingFrom(e);
    }
}