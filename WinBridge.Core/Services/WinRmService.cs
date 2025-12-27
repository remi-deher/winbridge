using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;
using WinBridge.SDK;
using WinBridge.SDK.Broadcasting;

namespace WinBridge.Core.Services
{
    public class WinRmService : IRemoteService, IDisposable
    {
        public RemoteType Protocol => RemoteType.WinRM;
        public bool IsConnected => _runspace != null && _runspace.RunspaceStateInfo.State == RunspaceState.Opened;
        public DateTime LastActivity { get; private set; } = DateTime.Now;

        public event Action<string>? DataReceived;

        private Runspace? _runspace;
        private readonly IBroadcastLogger _logger;

        private Guid? _serverId;

        public WinRmService(IBroadcastLogger logger)
        {
            _logger = logger;
        }

        private void Touch() => LastActivity = DateTime.Now;

        public async Task ConnectAsync(ServerModel server)
        {
            _serverId = server.Id;
            _logger.LogInfo($"Tentative de connexion WinRM vers {server.Host}...", "WinRM", _serverId);
            Touch();

            await Task.Run(() =>
            {
                try 
                {
                    // Convert password to SecureString
                    var securePass = new SecureString();
                    if (!string.IsNullOrEmpty(server.Password))
                    {
                        foreach (char c in server.Password) securePass.AppendChar(c);
                    }
                    securePass.MakeReadOnly();

                    var creds = new PSCredential(server.Username, securePass);
                    
                    // WinRM HTTP default port 5985, HTTPS 5986
                    int port = server.WinRmPort > 0 ? server.WinRmPort : 5985;
                    bool useHttps = port == 5986;
                    string scheme = useHttps ? "https" : "http";
                    string uri = $"{scheme}://{server.Host}:{port}/wsman";

                    var connectionInfo = new WSManConnectionInfo(new Uri(uri), scheme, creds);
                    
                    connectionInfo.AuthenticationMechanism = AuthenticationMechanism.Default; 

                    _runspace = RunspaceFactory.CreateRunspace(connectionInfo);
                    _runspace.Open();

                    _logger.LogSuccess($"Connecté à {server.Host} via WinRM.", "WinRM", _serverId);
                    DataReceived?.Invoke($"Connecté à {server.Host} via WinRM.\n");
                }
                catch (Exception ex)
                {
                    string msg = $"Erreur WinRM: {ex.Message}";
                    if (ex.Message.Contains("Authorization")) msg += " (Vérifiez le mot de passe)";
                    if (ex.Message.Contains("Connecting")) msg += " (Vérifiez TrustedHosts ou le service WinRM)";
                    
                    _logger.LogError(msg, "WinRM", _serverId);
                    throw new Exception(msg);
                }
            });
        }

        public async Task<string> ExecuteCommandAsync(string command)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                return "Non connecté via WinRM.";
            }

            Touch();
            _logger.LogInfo($"Exécution commande: {command}", "WinRM", _serverId);

            return await Task.Run(() => 
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddScript(command);
                        
                        var results = ps.Invoke();
                        
                        var sb = new StringBuilder();
                        
                        if (ps.Streams.Error.Count > 0)
                        {
                            foreach (var err in ps.Streams.Error)
                            {
                                sb.AppendLine($"[ERREUR] {err.ToString()}");
                                _logger.LogError($"Erreur flux: {err}", "WinRM", _serverId);
                            }
                        }

                        foreach (var obj in results)
                        {
                            if (obj != null)
                            {
                                sb.AppendLine(obj.ToString());
                            }
                        }
                        
                        _logger.LogSuccess("Commande terminée", "WinRM", _serverId);
                        return sb.ToString().Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erreur d'exécution: {ex.Message}", "WinRM", _serverId);
                    return $"Erreur d'exécution: {ex.Message}";
                }
            });
        }
        
        public void SendData(string data)
        {
            // WinRM PSSession is usually command-response oriented
            Touch();
        }

        public void Disconnect()
        {
            _logger.LogInfo("Déconnexion demandée...", "WinRM", _serverId);
            Dispose();
        }

        public void Dispose()
        {
            if (_runspace != null)
            {
                _runspace.Dispose();
                _runspace = null;
                _logger.LogInfo("Session WinRM fermée.", "WinRM", _serverId);
            }
        }
    }
}
