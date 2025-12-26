using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;
using WinBridge.SDK;

namespace WinBridge.Module.Base
{
    public sealed partial class BaseModuleView : UserControl
    {
        private readonly IRemoteService _remoteService;
        private readonly ServerModel _server;

        public BaseModuleView(IRemoteService remoteService, ServerModel server)
        {
            this.InitializeComponent();
            _remoteService = remoteService;
            _server = server;
            
            Loaded += BaseModuleView_Loaded;
        }

        private void BaseModuleView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private async void RefreshData()
        {
            if (_server.OperatingSystem == ServerOsType.Linux)
            {
                // Linux Commands
                var uptime = await _remoteService.ExecuteCommandAsync("uptime");
                ServiceStatusText.Text = uptime;

                var disks = await _remoteService.ExecuteCommandAsync("df -h | head -n 5");
                DiskUsageText.Text = disks;
            }
            else if (_server.OperatingSystem == ServerOsType.Windows)
            {
                // Windows Commands
                var services = await _remoteService.ExecuteCommandAsync("Get-Service | Where-Object {$_.Status -eq 'Running'} | Select-Object -First 5 | Format-Table -AutoSize | Out-String");
                ServiceStatusText.Text = "Services (Top 5 Active):\n" + services;

                var volumes = await _remoteService.ExecuteCommandAsync("Get-Volume | Format-Table -AutoSize | Out-String");
                DiskUsageText.Text = volumes;
            }
            else
            {
                ServiceStatusText.Text = "OS Inconnu.";
                DiskUsageText.Text = "OS Inconnu.";
            }
        }

        private async void CleanLogsButton_Click(object sender, RoutedEventArgs e)
        {
            CleanLogsButton.IsEnabled = false;
            OutputLog.Text += "Nettoyage en cours...\n";

            string result = "";
            if (_server.OperatingSystem == ServerOsType.Linux)
            {
                 result = await _remoteService.ExecuteCommandAsync("journalctl --vacuum-time=2d");
            }
            else if (_server.OperatingSystem == ServerOsType.Windows)
            {
                 // Clear Event Log example (requires Admin usually)
                 result = await _remoteService.ExecuteCommandAsync("wevtutil cl Application");
                 result += "\n(Note: Nécessite des privilèges Admin)";
            }

            OutputLog.Text += result + "\n";
            CleanLogsButton.IsEnabled = true;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }
    }
}
