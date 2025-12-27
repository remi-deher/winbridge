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
        private readonly RemoteSessionManager _sessionManager;

        public RemoteServiceFactory(IServiceProvider serviceProvider, RemoteSessionManager sessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
        }

        public async Task<IRemoteService> ConnectAsync(ServerModel server)
        {
            // 1. Check existing session
            var existing = _sessionManager.GetSession(server.Id);
            if (existing != null) return existing;

            // 2. Determine sequence
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

            IRemoteService? connectedService = null;

            try
            {
                connectedService = await TryConnectAsync(server, firstProto);
            }
            catch (Exception ex)
            {
                if (secondProto.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"First protocol {firstProto} failed ({ex.Message}). Auto-Fallback to {secondProto}.");
                    
                    // Specific try for fallback
                    connectedService = await TryConnectAsync(server, secondProto.Value);
                }
                else
                {
                    throw; // Rethrow if no fallback
                }
            }
            
            // 3. Register new session if successful
            if (connectedService != null)
            {
                _sessionManager.RegisterSession(server.Id, connectedService);
            }

            return connectedService!;
        }

        private async Task<IRemoteService> TryConnectAsync(ServerModel server, RemoteProtocol protocol)
        {
            IRemoteService service;
            
            if (protocol == RemoteProtocol.WinRM)
            {
                service = _serviceProvider.GetRequiredService<WinRmService>();
            }
            else
            {
                service = _serviceProvider.GetRequiredService<SshService>();
            }

            await service.ConnectAsync(server);

            return service;
        }
    }
}
