using Renci.SshNet;
using SshNet.Agent;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Models.Entities;

namespace WinBridge.Core.Services;

public class SshService : IDisposable
{
    private SshClient? _client;
    private ShellStream? _stream;
    private bool _isDisposed;
    private readonly VaultService _vaultService;

    // Événement déclenché quand le terminal reçoit du texte
    public event Action<string>? DataReceived;

    public SshService()
    {
        _vaultService = new VaultService();
    }

    public void Connect(ServerModel server)
    {
        ConnectionInfo connInfo;

        if (server.UseSshAgent)
        {
            try
            {
                var agent = new SshAgent();
                var identities = agent.RequestIdentities();
                var authMethod = new PrivateKeyAuthenticationMethod(server.Username, identities.ToArray());
                connInfo = new ConnectionInfo(server.Host, server.Port, server.Username, authMethod);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur Agent SSH : {ex.Message}");
            }
        }
        else if (server.UsePrivateKey && server.SshKeyId.HasValue)
        {
            var (keyContent, passphrase) = _vaultService.GetKeyContent(server.SshKeyId.Value.ToString());
            if (string.IsNullOrWhiteSpace(keyContent)) throw new Exception("Clé introuvable.");

            keyContent = keyContent.Trim();
            using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(keyContent));

            PrivateKeyFile keyFile = string.IsNullOrEmpty(passphrase)
                ? new PrivateKeyFile(keyStream)
                : new PrivateKeyFile(keyStream, passphrase);

            connInfo = new ConnectionInfo(server.Host, server.Port, server.Username,
                new PrivateKeyAuthenticationMethod(server.Username, keyFile));
        }
        else
        {
            connInfo = new ConnectionInfo(server.Host, server.Port, server.Username,
                new PasswordAuthenticationMethod(server.Username, server.Password ?? ""));
        }

        connInfo.Timeout = TimeSpan.FromSeconds(10);
        _client = new SshClient(connInfo);
        _client.Connect();
    }

    // --- CORRECTION ECRAN NOIR : Cette méthode lance le flux interactif ---
    public void StartTerminal()
    {
        if (_client == null || !_client.IsConnected) return;

        // Création du Shell "xterm-256color" pour avoir les couleurs
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

    public void SendData(string data)
    {
        if (_stream != null && _stream.CanWrite)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }
    }

    // --- POUR LES MODULES DU DASHBOARD (Info Système, Docker...) ---
    public async Task<string> ExecuteCommandAsync(string command)
    {
        if (_client == null || !_client.IsConnected) return "Non connecté";

        return await Task.Run(() =>
        {
            try
            {
                var cmd = _client.CreateCommand(command);
                var result = cmd.Execute();
                return result.Trim();
            }
            catch (Exception ex)
            {
                return $"Erreur: {ex.Message}";
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        try
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
        catch { }
    }
}