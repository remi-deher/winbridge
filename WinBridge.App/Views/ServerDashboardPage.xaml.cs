using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using WinBridge.Models.Entities;
using WinBridge.Core.Services;
using WinBridge.SDK;

namespace WinBridge.App.Views
{
    public sealed partial class ServerDashboardPage : Page
    {
        private ServerModel? _server;
        private IRemoteService? _remoteService;
        private DispatcherTimer? _refreshTimer;

        public ServerDashboardPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ServerModel server)
            {
                _server = server;
                TxtServerName.Text = _server.Name;
                TxtServerHost.Text = _server.Host;

                if (App.Services != null)
                {
                    try
                    {
                        var factory = App.Services.GetRequiredService<RemoteServiceFactory>();
                        _remoteService = await factory.ConnectAsync(_server);

                        // Set Logs Context
                        MyLogsControl.ServerId = _server.Id;

                        // Setup Terminal if SSH
                        if (_remoteService is SshService ssh)
                        {
                             // Assuming TerminalControl has this method based on previous error logs
                             MyTerminal.SetSshService(ssh);
                             ssh.StartTerminal();
                        }
                        
                        StartMetricsLoop();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Dashboard Connection Error: {ex.Message}");
                        TxtServerName.Text += " (Erreur)";
                    }
                }
            }
        }

        private void StartMetricsLoop()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) => await UpdateMetrics();
            _refreshTimer.Start();
            _ = UpdateMetrics(); 
        }

        private async Task UpdateMetrics()
        {
            if (_remoteService == null || !_remoteService.IsConnected) return;

            try 
            {
                // Basic Linux Commands
                var uptime = await _remoteService.ExecuteCommandAsync("uptime -p");
                var kernel = await _remoteService.ExecuteCommandAsync("uname -r");
                var os = await _remoteService.ExecuteCommandAsync("grep -oP '(?<=PRETTY_NAME=\").*?(?=\")' /etc/os-release");
                var ip = await _remoteService.ExecuteCommandAsync("hostname -I | awk '{print $1}'");
                var cpu = await _remoteService.ExecuteCommandAsync("cat /proc/loadavg | awk '{print $1}'");
                var ram = await _remoteService.ExecuteCommandAsync("free -h | grep Mem | awk '{print $3 \"/\" $2}'");
                var disk = await _remoteService.ExecuteCommandAsync("df -h / | tail -1 | awk '{print $4 \" free\"}'");
                
                lblUptime.Text = uptime;
                lblKernel.Text = kernel;
                lblOs.Text = os;
                lblIp.Text = ip;
                lblCpu.Text = cpu;
                lblRam.Text = ram;
                lblDisk.Text = disk;

                // Save to DB to populate Server List cache
                bool saveNeeded = false;
                if (!string.IsNullOrWhiteSpace(os) && _server.CachedOsInfo != os) { _server.CachedOsInfo = os; saveNeeded = true; }
                if (!string.IsNullOrWhiteSpace(kernel) && _server.CachedKernelVersion != kernel) { _server.CachedKernelVersion = kernel; saveNeeded = true; }
                if (!string.IsNullOrWhiteSpace(ip) && _server.CachedIpAddress != ip) { _server.CachedIpAddress = ip; saveNeeded = true; }
                
                if (saveNeeded)
                {
                    using var db = new WinBridge.Core.Data.AppDbContext();
                    db.Servers.Update(_server);
                    await db.SaveChangesAsync();
                    App.RaiseServerListChanged();
                }
            } 
            catch { }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
             base.OnNavigatingFrom(e);
             _refreshTimer?.Stop();
             // Le SessionManager gère le cycle de vie, on ne déconnecte pas forcément ici pour permettre la réutilisation.
        }

        private void MyTerminal_ToggleFullScreen(object sender, bool isFullScreen)
        {
            if (isFullScreen)
            {
                // Go Full Screen (Maximize Right Column / Terminal)
                ColCenter.Width = new GridLength(0);
                ColRight.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // Restore Normal Layout
                ColCenter.Width = new GridLength(1, GridUnitType.Star);
                ColRight.Width = new GridLength(400);
            }
        }

        private async void BtnReboot_Click(object sender, RoutedEventArgs e)
        {
             if (_remoteService != null) await _remoteService.ExecuteCommandAsync("sudo reboot");
        }

        private async void BtnShutdown_Click(object sender, RoutedEventArgs e)
        {
             if (_remoteService != null) await _remoteService.ExecuteCommandAsync("sudo shutdown now");
        }
    }
}
