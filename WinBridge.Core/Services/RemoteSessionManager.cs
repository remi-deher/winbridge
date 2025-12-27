using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinBridge.SDK;
using WinBridge.SDK.Broadcasting;

namespace WinBridge.Core.Services
{
    public class RemoteSessionManager : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, IRemoteService> _sessions = new();
        private readonly IBroadcastLogger _logger;
        private readonly Timer _keepAliveTimer;

        public RemoteSessionManager(IBroadcastLogger logger)
        {
            _logger = logger;
            _logger.LogInfo("RemoteSessionManager démarré.", "SessionManager");

            // Health Check every 30 seconds
            _keepAliveTimer = new Timer(CheckHealth, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        }

        private void CheckHealth(object? state)
        {
            foreach (var kvp in _sessions)
            {
                var serverId = kvp.Key;
                var service = kvp.Value;

                try 
                {
                    // For SSH, this property reflects the underlying client state.
                    // If KeepAliveInterval is set (which it is), SshClient will update this status if connection drops.
                    if (!service.IsConnected)
                    {
                        _logger.LogError($"Connexion perdue pour le serveur {serverId} (Health Check).", "Monitor", serverId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Erreur monitor {serverId}: {ex.Message}", "Monitor", serverId);
                }
            }
        }

        public IRemoteService? GetSession(Guid serverId)
        {
            if (_sessions.TryGetValue(serverId, out var service))
            {
                if (service.IsConnected)
                {
                    _logger.LogInfo($"Réutilisation session existante pour {serverId}", "SessionManager");
                    return service;
                }
                else
                {
                    // Remove dead session
                    TryRemove(serverId);
                }
            }
            return null;
        }

        public void RegisterSession(Guid serverId, IRemoteService service)
        {
            if (_sessions.TryAdd(serverId, service))
            {
                _logger.LogInfo($"Nouvelle session enregistrée pour {serverId}", "SessionManager");
            }
            else
            {
                if (_sessions.TryRemove(serverId, out var old))
                {
                    old.Disconnect();
                }
                _sessions.TryAdd(serverId, service);
            }
        }
        
        public void InvalidateSession(Guid serverId)
        {
            if (_sessions.TryRemove(serverId, out var service))
            {
                _logger.LogInfo($"Invalidation explicite de la session {serverId}", "SessionManager");
                try { service.Disconnect(); } catch { }
            }
        }

        private void TryRemove(Guid key)
        {
            if (_sessions.TryRemove(key, out var service))
            {
                try { service.Disconnect(); } catch { }
            }
        }

        public void Dispose()
        {
            _keepAliveTimer?.Dispose();
            foreach (var s in _sessions.Values)
            {
                try { s.Disconnect(); } catch { }
            }
            _sessions.Clear();
        }
    }
}
