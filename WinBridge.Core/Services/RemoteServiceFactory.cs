using System;
using System.Threading.Tasks;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;
using WinBridge.SDK;
using Microsoft.Extensions.DependencyInjection;

namespace WinBridge.Core.Services
{
    public class RemoteServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public RemoteServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<IRemoteService> ConnectAsync(ServerModel server)
        {
            // Determine sequence
            RemoteProtocol firstProto = RemoteProtocol.SSH;
            RemoteProtocol? secondProto = null;

            if (server.OSFamily == OSCategory.Windows)
            {
                firstProto = server.PrimaryProtocol;
                if (server.IsFallbackEnabled)
                {
                    secondProto = (firstProto == RemoteProtocol.SSH) ? RemoteProtocol.WinRM : RemoteProtocol.SSH;
                }
            }
            else
            {
                // Linux -> Only SSH
                firstProto = RemoteProtocol.SSH;
            }

            try
            {
                return await TryConnectAsync(server, firstProto);
            }
            catch (Exception ex)
            {
                if (secondProto.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"First protocol {firstProto} failed ({ex.Message}). Auto-Fallback to {secondProto}.");
                    return await TryConnectAsync(server, secondProto.Value);
                }
                else
                {
                    throw; // Rethrow if no fallback
                }
            }
        }

        private async Task<IRemoteService> TryConnectAsync(ServerModel server, RemoteProtocol protocol)
        {
            IRemoteService service;
            
            if (protocol == RemoteProtocol.WinRM)
            {
                var winRm = _serviceProvider.GetRequiredService<WinRmService>();
                await Task.Run(() => winRm.Connect(server));
                service = winRm;
            }
            else
            {
                var ssh = _serviceProvider.GetRequiredService<SshService>();
                await Task.Run(() => ssh.Connect(server));
                service = ssh;
            }

            return service;
        }
    }
}
