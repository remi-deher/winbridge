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

namespace WinBridge.Core.Services
{
    public class WinRmService : IRemoteService, IDisposable
    {
        public RemoteType Protocol => RemoteType.WinRM;
        public event Action<string>? DataReceived;

        private Runspace? _runspace;

        public void Connect(ServerModel server)
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
                // Fallback logic or trusting server config?
                // For now, assuming standard HTTP with AllowUnencrypted for dev, or HTTPS if port matches
                bool useHttps = server.Port == 5986;
                string scheme = useHttps ? "https" : "http";
                string uri = $"{scheme}://{server.Host}:{server.Port}/wsman";

                var connectionInfo = new WSManConnectionInfo(new Uri(uri), scheme, creds);
                
                // Common pitfalls: TrustedHosts. 
                // In production, we should probably check certificate options.
                connectionInfo.AuthenticationMechanism = AuthenticationMechanism.Default; // Negotiate (Kerberos/NTLM)

                _runspace = RunspaceFactory.CreateRunspace(connectionInfo);
                _runspace.Open();

                DataReceived?.Invoke($"Connecté à {server.Host} via WinRM.\n");
            }
            catch (Exception ex)
            {
                string msg = $"Erreur WinRM: {ex.Message}";
                if (ex.Message.Contains("Authorization")) msg += "\n(Vérifiez le mot de passe)";
                if (ex.Message.Contains("Connecting")) msg += "\n(Vérifiez TrustedHosts ou le service WinRM sur la cible: 'winrm quickconfig')";
                
                DataReceived?.Invoke(msg + "\n");
                throw new Exception(msg);
            }
        }

        public async Task<string> ExecuteCommandAsync(string command)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                return "Non connecté via WinRM.";
            }

            return await Task.Run(() => 
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddScript(command);
                        
                        // Capture output
                        var results = ps.Invoke();
                        
                        var sb = new StringBuilder();
                        
                        // Check for errors
                        if (ps.Streams.Error.Count > 0)
                        {
                            foreach (var err in ps.Streams.Error)
                            {
                                sb.AppendLine($"[ERREUR] {err.ToString()}");
                            }
                        }

                        foreach (var obj in results)
                        {
                            if (obj != null)
                            {
                                sb.AppendLine(obj.ToString());
                            }
                        }
                        
                        return sb.ToString().Trim();
                    }
                }
                catch (Exception ex)
                {
                    return $"Erreur d'exécution: {ex.Message}";
                }
            });
        }

        public void SendData(string data)
        {
            // WinRM PSSession is usually command-response oriented, ensuring full shell interactivity 
            // via this simplified interface is complex.
            // For now, we treat this as a no-op or log it.
            // Future: Implement nested interactive shell logic if needed.
        }

        public void Dispose()
        {
            _runspace?.Dispose();
            _runspace = null;
        }
    }
}
