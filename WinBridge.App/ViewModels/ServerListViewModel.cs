using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WinBridge.Core.Models;

namespace WinBridge.App.ViewModels;

public partial class ServerListViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<Server> Servers { get; set; } = new();

    public ServerListViewModel()
    {
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        var dataService = global::WinBridge.App.App.DataService;
        if (dataService != null)
        {
            var servers = await dataService.GetServersAsync();
            Servers = new ObservableCollection<Server>(servers);
        }
    }

    [RelayCommand]
    private async Task AddServerAsync()
    {
        var newServer = new Server
        {
            Name = "Nouveau Serveur",
            Host = "192.168.1.100",
            Port = 22,
            Protocol = ServerProtocol.SSH
        };

        await global::WinBridge.App.App.DataService.AddServerAsync(newServer);
        Servers.Add(newServer);
    }

    [RelayCommand]
    private async Task DeleteServerAsync(Server server)
    {
        if (server == null) return;

        await global::WinBridge.App.App.DataService.DeleteServerAsync(server);
        Servers.Remove(server);
    }
}

