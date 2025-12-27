using Renci.SshNet;
using SshNet.Agent;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Models.Entities;
using WinBridge.Models.Enums;
using WinBridge.SDK;
using WinBridge.SDK.Broadcasting;

namespace WinBridge.Core.Services;

public class SshService : ISshService, IRemoteService, IDisposable
{
    public RemoteType Protocol => RemoteType.SSH;
    public bool IsConnected => _client != null && _client.IsConnected;
    public DateTime LastActivity { get; private set; } = DateTime.Now;

    private SshClient? _client;
    private ShellStream? _stream;
    private bool _isDisposed;
    private readonly VaultService _vaultService;
    private readonly IBroadcastLogger _logger;

    // Événement pour envoyer le texte reçu vers l'UI
    public event Action<string>? DataReceived;

    private SshAgent? _clientAgent;
    private readonly SshAgentService _agentService;

    public SshService(IBroadcastLogger logger, VaultService vaultService, SshAgentService agentService)
    {
        _logger = logger;
        _vaultService = vaultService;
        _agentService = agentService;
    }

    private void Touch() => LastActivity = DateTime.Now;

    private Guid? _serverId;

    public async Task ConnectAsync(ServerModel server)
    {
        _serverId = server.Id;
        _logger.LogInfo($"Tentative de connexion SSH vers {server.Host}...", "SSH", _serverId);
        Touch();
        
        ConnectionInfo connInfo;

        // Clean previous agent
        _clientAgent = null;

        if (server.UseSshAgent)
        {
            try
            {
                // Logic to prefer default constructor for standard pipe to ensure auto-detection works
                string? pipePath = server.SshAgentPipePath;
                bool isDefaultPipe = string.IsNullOrEmpty(pipePath) || 
                                     pipePath.Equals(@"\\.\pipe\openssh-ssh-agent", StringComparison.OrdinalIgnoreCase) ||
                                     pipePath.Equals("openssh-ssh-agent", StringComparison.OrdinalIgnoreCase);

                _clientAgent = !isDefaultPipe 
                             ? new SshAgent(pipePath) 
                             : new SshAgent();

                var identities = _clientAgent.RequestIdentities();
                var authMethod = new PrivateKeyAuthenticationMethod(server.Username, identities.ToArray());
                connInfo = new ConnectionInfo(server.Host, server.Port, server.Username, authMethod);

                if (server.AllowAgentForwarding)
                {
                    _logger.LogInfo("Activation du transfert d'agent SSH demandée.", "SSH", _serverId);
                    // Hook for agent forwarding would go here
                    // e.g. connInfo.ForwardAgent = true; (if supported)
                    // or client.AddForwardedAgent(_clientAgent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur Agent SSH : {ex.Message}", "SSH", _serverId);
                throw new Exception($"Erreur Agent SSH : {ex.Message}");
            }
        }
        else if (server.UsePrivateKey && server.SshKeyId.HasValue)
        {
            // Use Pipe for secret retrieval
            var pipeName = await _vaultService.GetSecretViaPipeAsync(server.SshKeyId.Value.ToString());
            
            if (string.IsNullOrEmpty(pipeName)) 
            {
                // Fallback or error
                 throw new Exception("Clé introuvable ou erreur de pipe.");
            }

            using var pipeClient = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.In);
            await pipeClient.ConnectAsync(1000);

            using var ms = new MemoryStream();
            await pipeClient.CopyToAsync(ms);
            ms.Position = 0;

            // Retrieve passphrase separately (not via pipe yet for simplicity, or we assume key has no passphrase for now if pipe used solely for content)
            // Ideally we'd need another call for passphrase if needed.
            // For now, let's keep GetKeyContent just for passphrase? 
            // The user requirement said: "GetSecretViaPipeAsync... retourner le nom du pipe".
            // It didn't specify passphrase. 
            // I'll grab passphrase via standard call for now since it's usually short string.
            var (_, passphrase) = _vaultService.GetKeyContent(server.SshKeyId.Value.ToString());

            PrivateKeyFile keyFile = string.IsNullOrEmpty(passphrase)
                ? new PrivateKeyFile(ms)
                : new PrivateKeyFile(ms, passphrase);

            connInfo = new ConnectionInfo(server.Host, server.Port, server.Username,
                new PrivateKeyAuthenticationMethod(server.Username, keyFile));
        }
        else
        {
            connInfo = new ConnectionInfo(server.Host, server.Port, server.Username,
                new PasswordAuthenticationMethod(server.Username, server.Password ?? ""));
        }

        try
        {
            connInfo.Timeout = TimeSpan.FromSeconds(30);
            
            // SshClient creation and connection must be synchronous usually, but we are inside async method.
            // SshClient.Connect() is blocking. We should wrap it in Task.Run if we want to be truly async UI.
            
            await Task.Run(() => 
            {
                _client = new SshClient(connInfo);
                _client.KeepAliveInterval = TimeSpan.FromSeconds(30);
                _client.Connect();
            });

            _logger.LogSuccess($"Connecté à {server.Host} via SSH.", "SSH", _serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Échec connexion SSH : {ex.Message}", "SSH", _serverId);
            throw;
        }
    }

    public void StartTerminal()
    {
        if (_client == null || !_client.IsConnected) return;

        Touch();
        // On crée le Shell (Taille par défaut 80x24, sera ajustée dynamiquement)
        _stream = _client.CreateShellStream("xterm-256color", 80, 24, 800, 600, 1024);

        Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (_client.IsConnected && !_isDisposed && _stream != null)
            {
                try
                {
                    if (_stream.DataAvailable)
                    {
                        Touch();
                        int count = await _stream.ReadAsync(buffer, 0, buffer.Length);
                        if (count > 0)
                        {
                            var text = Encoding.UTF8.GetString(buffer, 0, count);
                            DataReceived?.Invoke(text);
                        }
                    }
                    else await Task.Delay(20);
                }
                catch { break; }
            }
        });
    }

    public void ResizeTerminal(int cols, int rows)
    {
        Touch();
        if (_stream == null) return;

        try
        {
            // ... (Reflection Logic Same) ...
            var channelField = _stream.GetType().GetField("_channel", BindingFlags.NonPublic | BindingFlags.Instance);
            if (channelField != null)
            {
                var channel = channelField.GetValue(_stream);
                if (channel != null)
                {
                    var method = channel.GetType().GetMethod("SendWindowChangeRequest", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        method.Invoke(channel, new object[] { (uint)cols, (uint)rows, (uint)(cols * 8), (uint)(rows * 16) });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erreur Resize SSH : {ex.Message}", "SSH", _serverId);
        }
    }

    public void SendData(string data)
    {
        Touch();
        if (_stream != null && _stream.CanWrite)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }
    }

    public async Task<string> ExecuteCommandAsync(string command)
    {
        if (_client == null || !_client.IsConnected) return "Non connecté";
        
        Touch();
        _logger.LogInfo($"Exécution commande: {command}", "SSH", _serverId);

        return await Task.Run(() =>
        {
            try
            {
                var cmd = _client.CreateCommand(command);
                var result = cmd.Execute();
                _logger.LogSuccess("Commande terminée", "SSH", _serverId);
                return result.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur commande: {ex.Message}", "SSH", _serverId);
                return $"Erreur: {ex.Message}";
            }
        });
    }

    public void Disconnect()
    {
        _logger.LogInfo("Déconnexion demandée...", "SSH", _serverId);
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        try { 
            _stream?.Dispose(); 
            if (_client != null && _client.IsConnected) _client.Disconnect();
            _client?.Dispose(); 
            _logger.LogInfo("Session SSH fermée.", "SSH", _serverId);
        } catch { }
    }
}