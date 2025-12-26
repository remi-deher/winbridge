using Renci.SshNet;
using SshNet.Agent; // La bibliothèque que vous avez installée
using System;
using System.IO;
using System.Linq; // INDISPENSABLE pour .ToArray()
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

    public event Action<string>? DataReceived;

    public SshService()
    {
        _vaultService = new VaultService();
    }

    public void Connect(ServerModel server)
    {
        ConnectionInfo connInfo;

        // CAS 1 : Agent SSH (1Password / OpenSSH)
        if (server.UseSshAgent)
        {
            // CORRECTION : On utilise SshAgent pour récupérer les clés
            try
            {
                var agent = new SshAgent(); // Se connecte au pipe \\.\pipe\openssh-ssh-agent
                var identities = agent.RequestIdentities(); // Récupère les clés disponibles

                // On transforme ces identités en une méthode d'authentification par clé
                // Note : Les identités de l'agent se comportent comme des PrivateKeyFile
                var authMethod = new PrivateKeyAuthenticationMethod(server.Username, identities.ToArray());

                connInfo = new ConnectionInfo(server.Host, server.Port, server.Username, authMethod);
            }
            catch (Exception ex)
            {
                throw new Exception($"Impossible de contacter l'Agent SSH. Vérifiez que 1Password/OpenSSH est lancé.\nDétail : {ex.Message}");
            }
        }
        // CAS 2 : Clé Privée stockée dans WinBridge (Vault)
        else if (server.UsePrivateKey && server.SshKeyId.HasValue)
        {
            // ... (Le reste du code reste identique) ...
            var (keyContent, passphrase) = _vaultService.GetKeyContent(server.SshKeyId.Value.ToString());

            if (string.IsNullOrWhiteSpace(keyContent))
                throw new Exception("La clé privée est vide ou introuvable.");

            keyContent = keyContent.Trim();

            // Vérification format pour éviter l'erreur "Invalid private key"
            if (!keyContent.Contains("\n") && !keyContent.Contains("\r"))
            {
                throw new Exception("Format de clé invalide (tout sur une ligne).");
            }

            using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(keyContent));
            PrivateKeyFile keyFile;

            try
            {
                keyFile = string.IsNullOrEmpty(passphrase)
                    ? new PrivateKeyFile(keyStream)
                    : new PrivateKeyFile(keyStream, passphrase);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lecture de la clé échouée : {ex.Message}");
            }

            connInfo = new ConnectionInfo(server.Host, server.Port, server.Username,
                new PrivateKeyAuthenticationMethod(server.Username, keyFile));
        }
        // CAS 3 : Mot de passe
        else
        {
            string password = server.Password ?? string.Empty;
            connInfo = new ConnectionInfo(server.Host, server.Port, server.Username,
                new PasswordAuthenticationMethod(server.Username, password));
        }

        // --- CONNEXION ---
        connInfo.Timeout = TimeSpan.FromSeconds(10);

        _client = new SshClient(connInfo);
        _client.Connect();

        _stream = _client.CreateShellStream("xterm", 80, 24, 800, 600, 1024);

        Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (_client is { IsConnected: true } && !_isDisposed && _stream != null)
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
                    else
                    {
                        await Task.Delay(50);
                    }
                }
                catch
                {
                    break;
                }
            }
        });
    }

    public void SendCommand(string command)
    {
        if (_stream != null && _stream.CanWrite)
        {
            _stream.WriteLine(command);
            _stream.Flush();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _stream?.Dispose();
            if (_client != null && _client.IsConnected)
            {
                _client.Disconnect();
            }
            _client?.Dispose();
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}