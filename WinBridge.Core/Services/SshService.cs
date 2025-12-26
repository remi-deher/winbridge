using Renci.SshNet;
using System.Threading.Tasks;
using WinBridge.Models.Entities;

namespace WinBridge.Core.Services;

public class SshService
{
    public async Task<string> ExecuteQuickCommandAsync(ServerModel server, string command)
    {
        return await Task.Run(() =>
        {
            using var client = new SshClient(server.Host, server.Port, server.Username, server.Password);
            client.Connect();
            var result = client.RunCommand(command);
            client.Disconnect();
            return result.Result;
        });
    }
}