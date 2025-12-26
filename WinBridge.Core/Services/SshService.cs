using Renci.SshNet;
using SshNet.Agent; // Nécessite le package NuGet "SshNet.Agent"
using System;
using System.IO;
using System.Linq; // INDISPENSABLE pour .ToArray() avec l'agent
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

    // Événement pour renvoyer le texte du terminal à l'interface (WebView ou TextBlock)
    public event Action<string>? DataReceived;

    public SshService()
    {
        _vaultService = new VaultService();
    }

    public void Connect(ServerModel server)
    {
        ConnectionInfo connInfo;

        // --- CAS 1 : AGENT SSH (1Password, OpenSSH, Pageant) ---
        if (server.UseSshAgent)
        {
            try
            {
                // On se connecte au pipe local de l'agent (ex: \\.\pipe\openssh-ssh-agent)
                var agent = new SshAgent();

                // On récupère les identités (clés) disponibles dans l'agent
                var identities = agent.RequestIdentities();

                // On transforme ces identités en une méthode d'authentification par clé standard
                // "identities.ToArray()" convertit la liste pour SSH.NET
                var authMethod = new PrivateKeyAuthenticationMethod(server.Username, identities.ToArray());

                connInfo = new ConnectionInfo(server.Host, server.Port, server.Username, authMethod);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur Agent SSH : Impossible de récupérer les identités.\nVérifiez que 1Password/OpenSSH est lancé.\nDétail : {ex.Message}");
            }
        }
        // --- CAS 2 : CLÉ PRIVÉE STOCKÉE (Vault) ---
        else if (server.UsePrivateKey && server.SshKeyId.HasValue)
        {
            var (keyContent, passphrase) = _vaultService.GetKeyContent(server.SshKeyId.Value.ToString());

            if (string.IsNullOrWhiteSpace(keyContent))
                throw new Exception("La clé privée est vide ou introuvable dans le coffre-fort.");

            // Nettoyage préventif
            keyContent = keyContent.Trim();

            // Vérification basique du format PEM/OpenSSH
            if (!keyContent.Contains("\n") && !keyContent.Contains("\r"))
            {
                throw new Exception("Format de clé invalide (tout sur une ligne). Assurez-vous d'avoir les sauts de ligne.");
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
        // --- CAS 3 : MOT DE PASSE ---
        else
        {
            string password = server.Password ?? string.Empty;
            connInfo = new ConnectionInfo(server.Host, server.Port, server.Username,
                new PasswordAuthenticationMethod(server.Username, password));
        }

        // --- CONNEXION ---
        connInfo.Timeout = TimeSpan.FromSeconds(10);

        _client = new SshClient(connInfo);
        _client.Connect(); // Bloquant (à exécuter dans un Task.Run côté UI)

        // Création du stream terminal (xterm)
        // 80 colonnes, 24 lignes (sera redimensionné par xterm.js si géré, sinon valeur par défaut)
        _stream = _client.CreateShellStream("xterm-256color", 80, 24, 800, 600, 1024);

        // Boucle de lecture asynchrone
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
                            // Notifie l'UI (qui transmettra au JS via base64)
                            DataReceived?.Invoke(text);
                        }
                    }
                    else
                    {
                        await Task.Delay(20); // Latence faible pour fluidité
                    }
                }
                catch
                {
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Envoie des données brutes au serveur (pour les flèches, CTRL+C, etc.)
    /// Sans ajouter de saut de ligne automatique.
    /// </summary>
    public void SendData(string data)
    {
        if (_stream != null && _stream.CanWrite)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }
    }

    /// <summary>
    /// Envoie une ligne de commande complète (avec \n à la fin).
    /// Utile pour les scripts ou boutons macro.
    /// </summary>
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
        catch
        {
            // Ignorer les erreurs de fermeture
        }

        GC.SuppressFinalize(this);
    }
}