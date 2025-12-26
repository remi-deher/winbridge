using Renci.SshNet;
using SshNet.Agent;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

    // Événement pour envoyer le texte reçu vers l'UI
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

    public void StartTerminal()
    {
        if (_client == null || !_client.IsConnected) return;

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

    // --- CORRECTION DU REDIMENSIONNEMENT ---
    public void ResizeTerminal(int cols, int rows)
    {
        if (_stream == null) return;

        try
        {
            // 1. On récupère le champ privé "_channel" à l'intérieur du ShellStream
            var channelField = _stream.GetType().GetField("_channel", BindingFlags.NonPublic | BindingFlags.Instance);

            if (channelField != null)
            {
                var channel = channelField.GetValue(_stream);

                if (channel != null)
                {
                    // 2. On appelle SendWindowChangeRequest sur ce canal (et non sur le stream)
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
            System.Diagnostics.Debug.WriteLine($"Erreur lors du redimensionnement SSH : {ex.Message}");
        }
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
        try { _stream?.Dispose(); _client?.Dispose(); } catch { }
    }
}